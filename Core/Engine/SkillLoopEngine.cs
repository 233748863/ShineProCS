using System.Collections.Concurrent;
using System.IO;
using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Services;
using ShineProCS.Core.Strategies;
using ShineProCS.Models;
using ShineProCS.Utils;
using OpenCvSharp;

using EngineConst = ShineProCS.Core.Constants.Engine;
using DetectionConst = ShineProCS.Core.Constants.Detection;
using VK = ShineProCS.Core.Constants.VirtualKeys;

namespace ShineProCS.Core.Engine;

/// <summary>
/// 技能循环引擎
/// 负责协调截屏、状态检测、技能选择和释放的核心组件
/// </summary>
public class SkillLoopEngine
{
    #region 依赖组件
    
    private IKeyboardInterface _keyboard;
    private readonly IImageInterface _image;
    private readonly ConfigManager _config;
    private readonly StateDetector _stateDetector;
    private readonly TemplatePreloader _templatePreloader;
    private readonly object _keyboardLock = new();
    
    #endregion
    
    #region 高级功能组件
    
    private readonly PerformanceMonitor _perfMonitor = new();
    private readonly MemoryMonitor _memMonitor = new();
    private readonly AdaptiveDelay _adaptiveDelay;
    private readonly ConfigWatcher _configWatcher;
    private readonly StrategyLoader _strategyLoader;
    private readonly SkillCooldownTracker _cooldownTracker = new();
    private readonly StateTracker _stateTracker = new();
    private List<ISkillStrategy> _strategies;
    
    #endregion
    
    #region 运行状态
    
    private List<SkillRuntimeState> _skillStates = [];
    private readonly object _stateLock = new();
    private readonly ReaderWriterLockSlim _skillStatesLock = new();
    private bool _isRunning, _isPaused;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private Task? _captureTask;
    
    // 设备断开状态
    private bool _deviceDisconnected;
    private DateTime _disconnectTime;
    private const int AutoPauseTimeoutMs = 5000; // 断开超过5秒自动暂停
    
    #endregion
    
    #region 图像处理
    
    private BlockingCollection<Mat> _imageQueue = null!;
    private int _unchangedFrameCount;
    private int _lastSampleSum;
    
    #endregion

    #region 事件
    
    public event Action<EngineStatus>? StatusChanged;
    public event Action<string, int>? LogMessage;
    
    #endregion

    private string? _nextSkillName;
    private double _lastHpPercent = 100;
    private double _lastMpPercent = 100;

    public SkillLoopEngine(IKeyboardInterface keyboard, IImageInterface image, ConfigManager config)
    {
        _keyboard = keyboard;
        _image = image;
        _config = config;
        
        _templatePreloader = new TemplatePreloader();
        var preloadCount = _templatePreloader.PreloadFromConfig(config);
        if (preloadCount > 0)
            Log($"已预加载 {preloadCount} 个模板到内存", 1);
        
        _stateDetector = new StateDetector(image, config, _templatePreloader);
        
        _strategyLoader = new StrategyLoader();
        _strategies = _strategyLoader.LoadAllStrategies();
        Log($"已加载 {_strategies.Count} 个策略: {string.Join(", ", _strategies.Select(s => s.Name))}", 1);
        
        _adaptiveDelay = new AdaptiveDelay(config.AppSettings.LoopInterval);
        InitializeImageQueue();
        
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config");
        _configWatcher = new ConfigWatcher(configPath);
        _configWatcher.ConfigChanged += OnConfigChanged;
        _config.ConfigChanged += _ => LoadSkills();
        LoadSkills();
    }

    private void InitializeImageQueue()
    {
        var capacity = Math.Max(2, Math.Min(10, _config.AppSettings.ImageQueueCapacity));
        _imageQueue = new BlockingCollection<Mat>(new ConcurrentQueue<Mat>(), capacity);
        Log($"图像队列已初始化，容量: {capacity}", 0);
    }

    private void OnConfigChanged(string filePath)
    {
        try
        {
            if (filePath.Contains("skills.json") || filePath.Contains("appsettings.json"))
            {
                Log("检测到配置更新，正在热重载...", 1);
                LoadSkills();
                _adaptiveDelay.Reset(_config.AppSettings.LoopInterval);
                
                var fileName = filePath.Contains("skills.json") ? "技能配置" : "应用设置";
                Views.ToastManager.Success($"{fileName}已自动重载", "配置更新");
            }
        }
        catch (Exception ex)
        {
            Log($"配置热重载失败: {ex.Message}", 2);
            Views.ToastManager.Error($"热重载失败: {ex.Message}", "配置错误");
        }
    }

    private void LoadSkills()
    {
        try
        {
            _config.LoadConfigs();
            var newStates = _config.Skills.Select(s => new SkillRuntimeState(s)).ToList();
            
            _skillStatesLock.EnterWriteLock();
            try
            {
                _skillStates = newStates;
            }
            finally
            {
                _skillStatesLock.ExitWriteLock();
            }
            
            // 配置变更，标记边界框缓存失效
            _stateDetector.InvalidateBoundingBoxCache();
            
            Log($"已加载 {newStates.Count} 个技能", 1);
        }
        catch (Exception ex)
        {
            // 配置加载失败时保留旧配置，不修改 _skillStates
            Log($"加载技能配置失败，保留原有配置: {ex.Message}", 2);
        }
    }

    public void Start()
    {
        lock (_stateLock)
        {
            if (_isRunning) return;
            _cts = new CancellationTokenSource();
            _isRunning = true;
            _isPaused = false;
            _perfMonitor.Reset();
            _unchangedFrameCount = 0;
            _captureTask = Task.Run(() => CaptureLoop(_cts.Token));
            _loopTask = Task.Run(() => MainLoop(_cts.Token));
            Log("引擎已启动", 1);
        }
        NotifyStatus();
    }

    public void Stop()
    {
        CancellationTokenSource? ctsToCancel;
        Task? loopToWait, captureToWait;
        
        lock (_stateLock)
        {
            if (!_isRunning) return;
            ctsToCancel = _cts;
            loopToWait = _loopTask;
            captureToWait = _captureTask;
        }
        
        ctsToCancel?.Cancel();
        try 
        { 
            var tasks = new List<Task>();
            if (loopToWait != null) tasks.Add(loopToWait);
            if (captureToWait != null) tasks.Add(captureToWait);
            if (tasks.Count > 0) Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(3)); 
        }
        catch (AggregateException ex) { Log($"引擎停止时发生异常: {ex.InnerExceptions.FirstOrDefault()?.Message}", 2); }
        
        while (_imageQueue.TryTake(out var mat)) _image.ReturnMat(mat);
        
        lock (_stateLock)
        {
            _cts?.Dispose();
            _cts = null;
            _loopTask = null;
            _captureTask = null;
            _isRunning = false;
            _isPaused = false;
        }
        Log("引擎已停止", 1);
        NotifyStatus();
    }

    public void TogglePause()
    {
        lock (_stateLock)
        {
            if (!_isRunning) return;
            _isPaused = !_isPaused;
            Log(_isPaused ? "引擎已暂停" : "引擎已恢复", 1);
        }
        NotifyStatus();
    }

    /// <summary>
    /// 更新键盘接口（用于运行时切换输入驱动）
    /// </summary>
    /// <param name="newKeyboard">新的键盘接口实例</param>
    public void UpdateKeyboardInterface(IKeyboardInterface newKeyboard)
    {
        if (newKeyboard == null) throw new ArgumentNullException(nameof(newKeyboard));
        
        lock (_keyboardLock)
        {
            _keyboard = newKeyboard;
        }
        Log("键盘接口已更新", 1);
    }
    
    /// <summary>
    /// 处理设备断开事件
    /// </summary>
    public void OnDeviceDisconnected()
    {
        lock (_stateLock)
        {
            if (_deviceDisconnected) return;
            
            _deviceDisconnected = true;
            _disconnectTime = DateTime.Now;
            Log("GhostBox 设备已断开，等待重连...", 2);
        }
    }
    
    /// <summary>
    /// 处理设备重连事件
    /// </summary>
    public void OnDeviceReconnected()
    {
        lock (_stateLock)
        {
            if (!_deviceDisconnected) return;
            
            _deviceDisconnected = false;
            
            // 如果之前因断开而暂停，恢复运行
            if (_isPaused && _isRunning)
            {
                Log("GhostBox 设备已重连，可继续运行", 1);
            }
            else
            {
                Log("GhostBox 设备已重连", 1);
            }
        }
    }

    public EngineStatus GetStatus()
    {
        bool isRunning, isPaused;
        lock (_stateLock)
        {
            isRunning = _isRunning;
            isPaused = _isPaused;
        }
        
        var m = _perfMonitor.GetMetrics();
        return new EngineStatus
        {
            IsRunning = isRunning, 
            IsPaused = isPaused,
            Mode = isRunning ? (isPaused ? "已暂停" : "运行中") : "已停止",
            ExecutionCount = m.TotalExecutions, 
            AvgResponseTime = m.AverageResponseTime, 
            SuccessRate = m.SuccessRate,
            NextSkillName = _nextSkillName,
            HpPercent = _lastHpPercent,
            MpPercent = _lastMpPercent
        };
    }

    private void CaptureLoop(CancellationToken token)
    {
        var region = _config.AppSettings.DetectionRegion;
        while (!token.IsCancellationRequested)
        {
            bool isPaused;
            lock (_stateLock) { isPaused = _isPaused; }
            
            if (isPaused) { Thread.Sleep(EngineConst.PauseCheckIntervalMs); continue; }
            Mat? mat = null;
            try
            {
                mat = _image.GetScreenRegion(region[0], region[1], region[2], region[3]);
                if (mat != null && !_imageQueue.TryAdd(mat, 50, token))
                {
                    _image.ReturnMat(mat);
                    mat = null;
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                if (mat != null) _image.ReturnMat(mat);
                Log($"截屏异常: {ex.Message}", 2);
                Thread.Sleep(EngineConst.PauseCheckIntervalMs);
            }
            Thread.Sleep(EngineConst.CaptureIntervalMs);
        }
    }

    private void MainLoop(CancellationToken token)
    {
        int loopCount = 0;
        while (!token.IsCancellationRequested)
        {
            Mat? currentFrame = null;
            try
            {
                bool isPaused;
                bool deviceDisconnected;
                DateTime disconnectTime;
                
                lock (_stateLock) 
                { 
                    isPaused = _isPaused;
                    deviceDisconnected = _deviceDisconnected;
                    disconnectTime = _disconnectTime;
                }
                
                // 检查设备断开状态
                if (deviceDisconnected)
                {
                    // 检查是否超过自动暂停超时时间
                    if (!isPaused && (DateTime.Now - disconnectTime).TotalMilliseconds > AutoPauseTimeoutMs)
                    {
                        lock (_stateLock)
                        {
                            if (!_isPaused && _deviceDisconnected)
                            {
                                _isPaused = true;
                                Log("设备断开超过5秒，引擎已自动暂停", 2);
                                NotifyStatus();
                            }
                        }
                    }
                    Thread.Sleep(EngineConst.PauseCheckIntervalMs);
                    continue;
                }
                
                if (isPaused) { Thread.Sleep(EngineConst.PauseCheckIntervalMs); continue; }
                if (!_imageQueue.TryTake(out currentFrame, EngineConst.ImageQueueTimeoutMs, token)) continue;
                
                if (IsFrameUnchanged(currentFrame))
                {
                    _unchangedFrameCount++;
                    if (_unchangedFrameCount > EngineConst.UnchangedFrameThreshold) Thread.Sleep(_adaptiveDelay.CurrentDelay * 2);
                    continue;
                }
                _unchangedFrameCount = 0;
                _perfMonitor.StartOperation();
                
                // 优化：计算包含所有检测区域的边界框，一次截取大区域
                _skillStatesLock.EnterReadLock();
                (int x, int y, int w, int h)? boundingBox;
                try
                {
                    boundingBox = _stateDetector.CalculateDetectionBoundingBox(_skillStates);
                }
                finally
                {
                    _skillStatesLock.ExitReadLock();
                }
                
                // 如果有有效的边界框，截取大区域并设置为缓存帧
                if (boundingBox.HasValue)
                {
                    var (bx, by, bw, bh) = boundingBox.Value;
                    var bigFrame = _image.GetScreenRegion(bx, by, bw, bh);
                    if (bigFrame != null)
                    {
                        _stateDetector.SetCachedFrame(bigFrame, bx, by);
                    }
                }
                
                var gameState = _stateDetector.DetectGameState();
                if (gameState.IsCasting) { Log("检测到读条中，等待...", 0); Thread.Sleep(DetectionConst.CastDetectionIntervalMs); continue; }
                
                // 使用读锁保护技能状态访问
                _skillStatesLock.EnterReadLock();
                try
                {
                    _stateDetector.UpdateSkillStatesParallel(_skillStates);
                    
                    // Requirements 5.1: 检测技能视觉状态变化，更新 CooldownTracker
                    UpdateSkillReadyStates();
                    
                    var success = ExecuteSkillCycle(gameState);
                    _perfMonitor.EndOperation(success);
                }
                finally
                {
                    _skillStatesLock.ExitReadLock();
                }
                
                loopCount++;
                if (loopCount % EngineConst.CombatDetectionInterval == 0) _adaptiveDelay.IsCombatMode = _stateDetector.DetectCombatState();
                if (loopCount % EngineConst.MemoryCleanupInterval == 0 && _memMonitor.AutoCleanupIfNeeded(DetectionConst.MemoryCleanupThresholdMb)) Log("内存清理完成", 0);
                
                if (loopCount % EngineConst.WindowPositionUpdateInterval == 0 && _image is Infrastructure.OpenCvImageInterface ocv)
                    ocv.UpdateWindowPosition();
                
                var metrics = _perfMonitor.GetMetrics();
                _adaptiveDelay.Adjust(metrics.AverageResponseTime);
                NotifyStatus();
                
                Thread.Sleep(_adaptiveDelay.CurrentDelay);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Log($"循环异常: {ex.Message} | {ex.StackTrace?.Split('\n').FirstOrDefault()}", 3);
                Thread.Sleep(EngineConst.ErrorRecoveryDelayMs);
            }
            finally
            {
                if (currentFrame != null) _image.ReturnMat(currentFrame);
            }
        }
    }

    /// <summary>
    /// 检测帧是否未发生变化
    /// 通过采样像素值的总和变化来判断帧是否有变化
    /// 需求 1.1: 使用可配置的采样步长减少计算量
    /// 需求 1.5: 维护帧差分缓存以避免重复处理未变化的帧
    /// </summary>
    /// <param name="frame">当前帧</param>
    /// <returns>true 表示帧未变化，false 表示帧已变化或检测被禁用</returns>
    private bool IsFrameUnchanged(Mat frame)
    {
        try
        {
            // 需求 1.1: 从配置读取阈值，阈值为0时禁用检测
            var configThreshold = _config.AppSettings.FrameChangeThreshold;
            if (configThreshold <= 0)
            {
                // 阈值为0或负数时禁用帧变化检测，始终返回false表示帧已变化
                return false;
            }
            
            // 需求 1.1: 从配置读取采样步长，使用可配置值减少计算量
            var sampleStride = _config.AppSettings.FrameSampleStride;
            if (sampleStride <= 0) sampleStride = EngineConst.FrameSampleStride; // 使用默认值
            
            int sum = 0;
            int width = frame.Width;
            int height = frame.Height;
            int channels = frame.Channels();
            
            // 优化：使用 unsafe 指针访问提高性能
            unsafe
            {
                var ptr = (byte*)frame.DataPointer;
                int stride = (int)frame.Step();
                
                // 使用配置的采样步长进行稀疏采样
                for (int y = 0; y < height; y += sampleStride)
                {
                    var row = ptr + y * stride;
                    for (int x = 0; x < width; x += sampleStride)
                    {
                        int offset = x * channels;
                        // 累加 RGB 三通道值
                        sum += row[offset] + row[offset + 1] + row[offset + 2];
                    }
                }
            }
            
            // 计算与上一帧的差异
            int diff = Math.Abs(sum - _lastSampleSum);
            _lastSampleSum = sum;
            
            // 计算采样点数量，用于归一化阈值
            int sampleCount = ((width + sampleStride - 1) / sampleStride) * ((height + sampleStride - 1) / sampleStride);
            // 使用配置的阈值乘以采样点数量作为最终阈值
            int threshold = sampleCount * configThreshold;
            
            return diff < threshold;
        }
        catch
        {
            // 发生异常时返回 false，表示帧已变化，确保不会跳过处理
            return false;
        }
    }

    private bool ExecuteSkillCycle(GameState gameState)
    {
        _lastHpPercent = gameState.HpPercentage * 100;
        _lastMpPercent = gameState.MpPercentage * 100;
        
        var context = new StrategyContext 
        { 
            SkillStates = _skillStates, 
            GameState = gameState, 
            LoopMode = _config.AppSettings.EnableSmartMode ? "Smart" : "Default",
            Settings = _config.AppSettings
        };
        
        SkillRuntimeState? skill = null;
        foreach (var s in _strategies) 
            if (s.CanExecute(context) && (skill = s.SelectSkill(context)) != null) 
                break;
        
        _nextSkillName = skill?.Config.Name;
        
        if (skill == null) return true;

        // Requirements 5.1, 5.2, 5.3: 前置技能链逻辑（通过技能名称引用）
        if (!string.IsNullOrEmpty(skill.Config.PreCastSkillName))
        {
            var preCastResult = ExecutePreCastSkillChain(skill);
            if (!preCastResult)
            {
                // Requirements 5.3: 前置技能释放失败，本周期跳过主技能
                return true;
            }
        }

        // 检查Buff条件（旧的PreCastKeyCode逻辑，保持向后兼容）
        var buffSatisfied = CheckBuffCondition(skill.Config);
        
        if (!buffSatisfied && skill.Config.PreCastKeyCode > 0)
        {
            Log($"联动触发: {skill.Config.Name} 缺少Buff [{skill.Config.PreCastConditionBuff}]，释放前置技能", 0);
            if (!_keyboard.PressAndRelease(skill.Config.PreCastKeyCode)) 
            { 
                Log("前置技能释放失败", 2); 
                return false; 
            }
            
            // 等待前置技能施法时间
            Thread.Sleep(skill.Config.ComboDelay);
            
            var newState = _stateDetector.DetectGameState();
            if (newState.IsCasting) 
            { 
                Log("前置技能读条中，等待完成...", 0); 
                return true; 
            }
            
            // 使用配置的重试参数检查Buff
            // Requirements 4.1, 4.3: 前置技能释放后重试检查Buff
            var buffCheckDelay = skill.Config.BuffCheckDelay;
            var buffCheckRetries = skill.Config.BuffCheckRetries;
            
            for (int retry = 0; retry < buffCheckRetries; retry++)
            {
                // 等待Buff生效
                Thread.Sleep(buffCheckDelay);
                
                buffSatisfied = CheckBuffCondition(skill.Config);
                if (buffSatisfied)
                {
                    Log($"Buff [{skill.Config.PreCastConditionBuff}] 已获得 (重试 {retry + 1}/{buffCheckRetries})", 0);
                    break;
                }
                
                if (retry < buffCheckRetries - 1)
                {
                    Log($"Buff [{skill.Config.PreCastConditionBuff}] 检查失败，重试 {retry + 2}/{buffCheckRetries}", 0);
                }
            }
            
            if (!buffSatisfied) 
            { 
                Log($"Buff [{skill.Config.PreCastConditionBuff}] 未获得 (已重试 {buffCheckRetries} 次)，等待下次循环", 1); 
                return true; 
            }
        }
        else if (!buffSatisfied && skill.Config.PreCastKeyCode <= 0) 
        { 
            Log($"技能 {skill.Config.Name} Buff条件不满足，跳过", 0); 
            return true; 
        }
        
        return ExecuteSkill(skill);
    }
    
    /// <summary>
    /// 执行前置技能链
    /// Requirements 5.1: 当技能配置了PreCastSkillName时，首先尝试释放前置技能
    /// Requirements 5.2: 前置技能成功释放后，等待ComboDelay后再释放主技能
    /// Requirements 5.3: 前置技能释放失败时，本周期跳过主技能
    /// </summary>
    /// <param name="mainSkill">主技能</param>
    /// <returns>true表示前置技能链执行成功，可以继续释放主技能；false表示失败，应跳过主技能</returns>
    private bool ExecutePreCastSkillChain(SkillRuntimeState mainSkill)
    {
        var preCastSkillName = mainSkill.Config.PreCastSkillName;
        
        // 通过技能名称查找前置技能
        var preCastSkill = FindSkillByName(preCastSkillName);
        if (preCastSkill == null)
        {
            // 错误处理：无效技能引用，跳过前置技能直接释放主技能
            Log($"前置技能 [{preCastSkillName}] 未找到，跳过前置技能", 1);
            return true;
        }
        
        // 检测循环引用
        if (HasCircularReference(mainSkill.Config.Name, preCastSkillName, new HashSet<string>()))
        {
            Log($"检测到前置技能链循环引用: {mainSkill.Config.Name} -> {preCastSkillName}，跳过前置技能", 2);
            return true;
        }
        
        Log($"前置技能链: {mainSkill.Config.Name} 需要先释放 {preCastSkillName}", 0);
        
        // Requirements 5.1: 尝试释放前置技能
        var preCastSuccess = ExecuteSkill(preCastSkill);
        
        if (!preCastSuccess)
        {
            // Requirements 5.3: 前置技能释放失败，本周期跳过主技能
            Log($"前置技能 [{preCastSkillName}] 释放失败，跳过主技能 [{mainSkill.Config.Name}]", 1);
            return false;
        }
        
        // Requirements 5.2: 等待ComboDelay后再释放主技能
        var comboDelay = mainSkill.Config.ComboDelay;
        if (comboDelay > 0)
        {
            Log($"前置技能 [{preCastSkillName}] 释放成功，等待 {comboDelay}ms 后释放主技能", 0);
            Thread.Sleep(comboDelay);
        }
        
        // 检查是否在读条中
        var newState = _stateDetector.DetectGameState();
        if (newState.IsCasting)
        {
            Log("前置技能读条中，等待完成...", 0);
            return false; // 本周期跳过主技能，等待下次循环
        }
        
        return true;
    }
    
    /// <summary>
    /// 通过技能名称查找技能
    /// </summary>
    /// <param name="skillName">技能名称</param>
    /// <returns>找到的技能运行时状态，未找到返回null</returns>
    private SkillRuntimeState? FindSkillByName(string skillName)
    {
        if (string.IsNullOrEmpty(skillName))
            return null;
        
        return _skillStates.FirstOrDefault(s => 
            s.Config.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// 检测前置技能链是否存在循环引用
    /// </summary>
    /// <param name="currentSkillName">当前技能名称</param>
    /// <param name="preCastSkillName">前置技能名称</param>
    /// <param name="visited">已访问的技能名称集合</param>
    /// <returns>true表示存在循环引用</returns>
    private bool HasCircularReference(string currentSkillName, string preCastSkillName, HashSet<string> visited)
    {
        if (string.IsNullOrEmpty(preCastSkillName))
            return false;
        
        // 如果前置技能指向当前技能，存在循环
        if (preCastSkillName.Equals(currentSkillName, StringComparison.OrdinalIgnoreCase))
            return true;
        
        // 如果前置技能已经访问过，存在循环
        if (visited.Contains(preCastSkillName))
            return true;
        
        visited.Add(preCastSkillName);
        
        // 递归检查前置技能的前置技能
        var preCastSkill = FindSkillByName(preCastSkillName);
        if (preCastSkill != null && !string.IsNullOrEmpty(preCastSkill.Config.PreCastSkillName))
        {
            return HasCircularReference(currentSkillName, preCastSkill.Config.PreCastSkillName, visited);
        }
        
        return false;
    }

    /// <summary>
    /// 检查技能的Buff条件是否满足（从Buff库检查）
    /// </summary>
    private bool CheckBuffCondition(SkillConfig skill)
    {
        if (string.IsNullOrEmpty(skill.PreCastConditionBuff))
            return true;
        
        return _stateDetector.CheckBuffExists(skill.PreCastConditionBuff);
    }

    private bool ExecuteSkill(SkillRuntimeState skill)
    {
        if (skill.ConsecutiveFailures >= EngineConst.MaxConsecutiveFailures)
        {
            Log($"技能 {skill.Config.Name} 连续失败{EngineConst.MaxConsecutiveFailures}次，触发ESC重置", 2);
            _keyboard.PressAndRelease(VK.Escape);
            skill.ConsecutiveFailures = 0;
            return false;
        }
        
        var config = skill.Config;
        var castType = config.CastType;
        
        switch (castType)
        {
            case SkillCastType.Instant:
                // 瞬发技能：直接按下释放
                return ExecuteInstantSkill(skill);
                
            case SkillCastType.CastTime:
                // 正读条技能：按下后等待读条完成
                return ExecuteCastTimeSkill(skill);
                
            case SkillCastType.Channeled:
                // 引导技能：按下后引导指定时间，可提前打断
                return ExecuteChanneledSkill(skill);
                
            default:
                return ExecuteInstantSkill(skill);
        }
    }
    
    /// <summary>
    /// 执行瞬发技能
    /// </summary>
    private bool ExecuteInstantSkill(SkillRuntimeState skill)
    {
        try
        {
            if (_keyboard.PressAndRelease(skill.Config.KeyCode))
            {
                skill.MarkAsUsed();
                skill.ConsecutiveFailures = 0;
                _cooldownTracker.RecordSkillUse(skill.Config.Name, skill.Config.Cooldown);
                // Requirements 6.1, 6.2, 6.3: 技能成功释放后更新状态追踪器
                UpdateStateOnSkillCast(skill.Config);
                Log($"释放: {skill.Config.Name} [瞬发]", 0);
                return true;
            }
            
            skill.ConsecutiveFailures++;
            return false;
        }
        catch (Exception ex)
        {
            // 设备断开时捕获异常，记录日志但不崩溃
            Log($"技能 {skill.Config.Name} 释放失败（设备可能已断开）: {ex.Message}", 2);
            skill.ConsecutiveFailures++;
            return false;
        }
    }
    
    /// <summary>
    /// 执行正读条技能
    /// </summary>
    private bool ExecuteCastTimeSkill(SkillRuntimeState skill)
    {
        var config = skill.Config;
        
        try
        {
            if (_keyboard.PressAndRelease(config.KeyCode))
            {
                skill.MarkAsUsed();
                skill.ConsecutiveFailures = 0;
                _cooldownTracker.RecordSkillUse(config.Name, config.Cooldown);
                // Requirements 6.1, 6.2, 6.3: 技能成功释放后更新状态追踪器
                UpdateStateOnSkillCast(config);
                
                var maxCastTime = config.CastDuration;
                
                if (config.UseCastEndDetection)
                {
                    // 使用视觉检测判断读条结束
                    Log($"释放: {config.Name} [读条, 视觉检测结束]", 0);
                    WaitForCastEnd(config, maxCastTime);
                }
                else if (maxCastTime > 0)
                {
                    // 固定时间等待
                    Log($"释放: {config.Name} [读条 {maxCastTime}ms]", 0);
                    Thread.Sleep(maxCastTime);
                }
                else
                {
                    Log($"释放: {config.Name} [读条]", 0);
                }
                
                return true;
            }
            
            skill.ConsecutiveFailures++;
            return false;
        }
        catch (Exception ex)
        {
            // 设备断开时捕获异常，记录日志但不崩溃
            Log($"技能 {config.Name} 释放失败（设备可能已断开）: {ex.Message}", 2);
            skill.ConsecutiveFailures++;
            return false;
        }
    }
    
    /// <summary>
    /// 等待读条结束（视觉检测）
    /// </summary>
    private void WaitForCastEnd(SkillConfig config, int maxWaitTime)
    {
        var checkInterval = DetectionConst.CastDetectionIntervalMs;
        var elapsed = 0;
        var maxTime = maxWaitTime > 0 ? maxWaitTime : DetectionConst.MaxChannelDurationMs;
        
        while (elapsed < maxTime)
        {
            Thread.Sleep(checkInterval);
            elapsed += checkInterval;
            
            if (config.CastEndDetectionMode == 0)
            {
                // 点色检测
                var point = config.CastEndDetectionPoint;
                if (point.Any(v => v > 0) && CheckColorMatch(point[0], point[1], config.CastEndColor, config.CastEndColorTolerance))
                {
                    Log($"检测到读条结束 (已等待 {elapsed}ms)", 0);
                    return;
                }
            }
            else
            {
                // 模板匹配 - 检测技能图标是否恢复可用
                if (CheckSkillAvailable(config))
                {
                    Log($"检测到技能可用 (已等待 {elapsed}ms)", 0);
                    return;
                }
            }
        }
        
        Log($"读条等待超时 ({maxTime}ms)", 1);
    }
    
    /// <summary>
    /// 检测技能是否可用（模板匹配）
    /// </summary>
    private bool CheckSkillAvailable(SkillConfig config)
    {
        if (string.IsNullOrEmpty(config.TemplatePath)) return false;
        
        var region = config.IconRegion;
        if (region.All(v => v == 0)) return false;
        
        var template = _templatePreloader.GetTemplate(config.TemplatePath);
        if (template == null || template.Empty()) return false;
        
        var frame = _image.GetScreenRegion(region[0], region[1], region[2], region[3]);
        if (frame == null) return false;
        
        try
        {
            using var result = new Mat();
            Cv2.MatchTemplate(frame, template, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out _);
            
            return maxVal >= config.SimilarityThreshold;
        }
        finally
        {
            _image.ReturnMat(frame);
        }
    }
    
    /// <summary>
    /// 执行引导技能
    /// 使用 try-finally 确保按键始终被释放，即使发生异常或中断
    /// </summary>
    private bool ExecuteChanneledSkill(SkillRuntimeState skill)
    {
        var config = skill.Config;
        
        // 引导技能需要按住，这里用 PressKey + Sleep + ReleaseKey 模拟
        if (!_keyboard.PressKey(config.KeyCode))
        {
            skill.ConsecutiveFailures++;
            return false;
        }
        
        try
        {
            skill.MarkAsUsed();
            skill.ConsecutiveFailures = 0;
            _cooldownTracker.RecordSkillUse(config.Name, config.Cooldown);
            // Requirements 6.1, 6.2, 6.3: 技能成功释放后更新状态追踪器
            UpdateStateOnSkillCast(config);
            
            // 执行引导逻辑
            ExecuteChannelLogic(config);
            
            return true;
        }
        catch (Exception ex)
        {
            Log($"引导技能 {config.Name} 执行异常: {ex.Message}", 2);
            return false;
        }
        finally
        {
            // 确保按键始终被释放，无论正常完成、异常还是中断
            if (!_keyboard.ReleaseKey(config.KeyCode))
            {
                Log($"引导技能 {config.Name} 按键释放失败，尝试恢复", 2);
                // 尝试再次释放
                Thread.Sleep(10);
                _keyboard.ReleaseKey(config.KeyCode);
            }
        }
    }
    
    /// <summary>
    /// 执行引导逻辑（从 ExecuteChanneledSkill 提取）
    /// 根据配置选择不同的引导模式
    /// </summary>
    private void ExecuteChannelLogic(SkillConfig config)
    {
        // 根据打断模式执行不同逻辑
        if (config.ChannelInterruptMode == 1 && config.ChannelInterruptPoint.Any(v => v > 0))
        {
            // 点色检测打断模式
            ExecuteColorDetectChannel(config);
        }
        else if (config.ChannelInterruptTime > 0)
        {
            // 固定时间打断模式
            ExecuteFixedTimeChannel(config);
        }
        else if (config.UseCastEndDetection)
        {
            // 使用视觉检测判断引导结束
            Log($"释放: {config.Name} [引导, 视觉检测结束]", 0);
            WaitForCastEnd(config, config.CastDuration);
        }
        else
        {
            // 完整引导
            ExecuteFixedTimeChannel(config);
        }
    }
    
    /// <summary>
    /// 固定时间引导
    /// </summary>
    private void ExecuteFixedTimeChannel(SkillConfig config)
    {
        var channelTime = config.ChannelInterruptTime > 0 
            ? config.ChannelInterruptTime 
            : config.CastDuration;
        
        if (channelTime > 0)
        {
            var interruptInfo = config.ChannelInterruptTime > 0 
                ? $"打断于 {config.ChannelInterruptTime}ms" 
                : "完整引导";
            Log($"释放: {config.Name} [引导 {channelTime}ms, {interruptInfo}]", 0);
            
            Thread.Sleep(channelTime);
        }
        else
        {
            Log($"释放: {config.Name} [引导]", 0);
        }
    }
    
    /// <summary>
    /// 点色检测引导打断
    /// </summary>
    private void ExecuteColorDetectChannel(SkillConfig config)
    {
        var maxTime = config.CastDuration > 0 ? config.CastDuration : DetectionConst.MaxChannelDurationMs;
        var checkInterval = DetectionConst.CastDetectionIntervalMs;
        var elapsed = 0;
        
        var targetColor = config.ChannelInterruptColor;
        var tolerance = config.ChannelColorTolerance;
        var point = config.ChannelInterruptPoint;
        
        Log($"释放: {config.Name} [引导, 点色检测打断 ({point[0]},{point[1]})]", 0);
        
        while (elapsed < maxTime)
        {
            Thread.Sleep(checkInterval);
            elapsed += checkInterval;
            
            // 检测点色
            if (CheckColorMatch(point[0], point[1], targetColor, tolerance))
            {
                Log($"检测到目标颜色，打断引导 (已引导 {elapsed}ms)", 0);
                break;
            }
        }
        
        if (elapsed >= maxTime)
        {
            Log($"引导完成 (达到最大时间 {maxTime}ms)", 0);
        }
    }
    
    /// <summary>
    /// 检测指定点的颜色是否匹配目标颜色
    /// </summary>
    private bool CheckColorMatch(int x, int y, int[] targetColor, int tolerance)
    {
        if (targetColor.Length < 3) return false;
        
        try
        {
            // 获取单个像素
            var pixel = _image.GetScreenRegion(x, y, 1, 1);
            if (pixel == null) return false;
            
            try
            {
                // 获取像素颜色 (BGR格式)
                var indexer = pixel.GetGenericIndexer<OpenCvSharp.Vec3b>();
                var color = indexer[0, 0];
                
                var b = color.Item0;
                var g = color.Item1;
                var r = color.Item2;
                
                // 计算颜色差异
                var diffR = Math.Abs(r - targetColor[0]);
                var diffG = Math.Abs(g - targetColor[1]);
                var diffB = Math.Abs(b - targetColor[2]);
                
                return diffR <= tolerance && diffG <= tolerance && diffB <= tolerance;
            }
            finally
            {
                _image.ReturnMat(pixel);
            }
        }
        catch
        {
            return false;
        }
    }

    public SkillCooldownTracker CooldownTracker => _cooldownTracker;
    
    /// <summary>
    /// 状态追踪器，用于跨周期追踪命名的布尔状态
    /// Requirements 6.1: 技能引擎维护StateTracker字典用于存储命名的布尔状态
    /// </summary>
    public StateTracker StateTracker => _stateTracker;
    
    public SkillStatistics GetSkillStatistics(string skillName) => _cooldownTracker.GetStatistics(skillName);
    
    /// <summary>
    /// 技能成功释放后更新状态追踪器
    /// Requirements 6.2: 当技能配置了SetStateOnCast时，将指定状态设为true
    /// Requirements 6.3: 当技能配置了ClearStateOnCast时，将指定状态设为false
    /// </summary>
    /// <param name="config">技能配置</param>
    private void UpdateStateOnSkillCast(SkillConfig config)
    {
        // Requirements 6.2: SetStateOnCast - 将指定状态设为true
        if (!string.IsNullOrEmpty(config.SetStateOnCast))
        {
            _stateTracker.SetState(config.SetStateOnCast, true);
            Log($"状态追踪: 设置 [{config.SetStateOnCast}] = true", 0);
        }
        
        // Requirements 6.3: ClearStateOnCast - 将指定状态设为false
        if (!string.IsNullOrEmpty(config.ClearStateOnCast))
        {
            _stateTracker.SetState(config.ClearStateOnCast, false);
            Log($"状态追踪: 设置 [{config.ClearStateOnCast}] = false", 0);
        }
    }
    
    /// <summary>
    /// 更新技能就绪状态，检测视觉状态变化
    /// Requirements 5.1: 当技能从 IsVisuallyReady=false 变为 true 时，调用 RecordSkillReady
    /// </summary>
    private void UpdateSkillReadyStates()
    {
        foreach (var skill in _skillStates)
        {
            // 检测 IsVisuallyReady 从 false 变为 true
            if (skill.IsVisuallyReady && !skill.WasVisuallyReady)
            {
                _cooldownTracker.RecordSkillReady(skill.Config.Name);
                Log($"技能 {skill.Config.Name} 视觉就绪，更新冷却追踪", 0);
            }
            // 更新上一次的视觉状态
            skill.WasVisuallyReady = skill.IsVisuallyReady;
        }
    }
    
    private void Log(string msg, int level) 
    { 
        if (level >= _config.AppSettings.LogLevel) 
            LogMessage?.Invoke($"[{DateTime.Now:HH:mm:ss}] {msg}", level); 
    }
    
    private void NotifyStatus() => StatusChanged?.Invoke(GetStatus());
    public void ReloadConfig() => LoadSkills();
}
