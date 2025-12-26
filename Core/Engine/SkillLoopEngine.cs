using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Services;
using ShineProCS.Core.Strategies;
using ShineProCS.Models;
using ShineProCS.Utils;
using OpenCvSharp;

namespace ShineProCS.Core.Engine;

/// <summary>
/// 技能循环引擎
/// 负责协调截屏、状态检测、技能选择和释放的核心组件
/// </summary>
public class SkillLoopEngine
{
    #region 依赖组件
    
    private readonly IKeyboardInterface _keyboard;
    private readonly IImageInterface _image;
    private readonly ConfigManager _config;
    private readonly StateDetector _stateDetector;
    
    #endregion
    
    #region 高级功能组件
    
    private readonly PerformanceMonitor _perfMonitor = new();
    private readonly MemoryMonitor _memMonitor = new();
    private readonly AdaptiveDelay _adaptiveDelay;
    private readonly ConfigWatcher _configWatcher;
    private readonly StrategyLoader _strategyLoader;
    private readonly SkillCooldownTracker _cooldownTracker = new();
    private List<ISkillStrategy> _strategies;
    
    #endregion
    
    #region 运行状态
    
    private List<SkillRuntimeState> _skillStates = [];
    private volatile bool _isRunning, _isPaused;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private Task? _captureTask;
    
    #endregion
    
    #region 图像处理
    
    /// <summary>
    /// 生产者-消费者模型的图像队列
    /// </summary>
    private BlockingCollection<Mat> _imageQueue = null!;
    private byte[]? _lastFrameHash;
    private int _unchangedFrameCount;
    
    #endregion
    
    #region 气劲状态
    
    /// <summary>
    /// 状态锁，保护共享状态的线程安全
    /// </summary>
    private readonly object _stateLock = new();
    private bool _isQianZhiActive;
    private bool _qiQingInLoop;
    
    #endregion

    #region 事件
    
    /// <summary>
    /// 引擎状态变化事件
    /// </summary>
    public event Action<EngineStatus>? StatusChanged;
    
    /// <summary>
    /// 日志消息事件
    /// </summary>
    public event Action<string, int>? LogMessage;
    
    #endregion

    #region 气劲配置属性
    
    private string QianZhiSkillName => _config.AppSettings.QianZhiSkillName;
    private string QianZhiBuffName => _config.AppSettings.QianZhiBuffName;
    private int QianZhiKeyCode => _config.AppSettings.QianZhiKeyCode;
    private string ChiShaoSkillName => _config.AppSettings.ChiShaoSkillName;
    private string QiQingSkillName => _config.AppSettings.QiQingSkillName;
    private string QiQingBuffName => _config.AppSettings.QiQingBuffName;
    
    #endregion

    /// <summary>
    /// 创建技能循环引擎实例
    /// </summary>
    /// <param name="keyboard">键盘接口</param>
    /// <param name="image">图像接口</param>
    /// <param name="config">配置管理器</param>
    public SkillLoopEngine(IKeyboardInterface keyboard, IImageInterface image, ConfigManager config)
    {
        _keyboard = keyboard;
        _image = image;
        _config = config;
        _stateDetector = new StateDetector(image, config);
        
        // 使用策略加载器加载所有策略（内置 + 插件）
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

    /// <summary>
    /// 初始化图像队列（支持动态容量配置）
    /// </summary>
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
                
                // Toast 通知
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
            _skillStates = _config.Skills.Select(s => new SkillRuntimeState(s)).ToList();
            Log($"已加载 {_skillStates.Count} 个技能", 1);
        }
        catch (Exception ex)
        {
            Log($"加载技能配置失败: {ex.Message}", 2);
        }
    }

    public void Start()
    {
        if (_isRunning) return;
        _cts = new CancellationTokenSource();
        _isRunning = true;
        _isPaused = false;
        _perfMonitor.Reset();
        _unchangedFrameCount = 0;
        _captureTask = Task.Run(() => CaptureLoop(_cts.Token));
        _loopTask = Task.Run(() => MainLoop(_cts.Token));
        Log("引擎已启动（高级模式：异步截屏 + 自适应延迟）", 1);
        NotifyStatus();
    }

    public void Stop()
    {
        if (!_isRunning) return;
        _cts?.Cancel();
        try { Task.WaitAll([_loopTask!, _captureTask!], TimeSpan.FromSeconds(3)); }
        catch (AggregateException ex) { Log($"引擎停止时发生异常: {ex.InnerExceptions.FirstOrDefault()?.Message}", 2); }
        while (_imageQueue.TryTake(out var mat)) _image.ReturnMat(mat);
        _cts?.Dispose();
        _isRunning = _isPaused = false;
        Log("引擎已停止", 1);
        NotifyStatus();
    }

    public void TogglePause()
    {
        if (!_isRunning) return;
        _isPaused = !_isPaused;
        Log(_isPaused ? "引擎已暂停" : "引擎已恢复", 1);
        NotifyStatus();
    }

    // 新增：记录下一个技能和游戏状态
    private string? _nextSkillName;
    private double _lastHpPercent = 100;
    private double _lastMpPercent = 100;

    public EngineStatus GetStatus()
    {
        var m = _perfMonitor.GetMetrics();
        bool isQianZhiActive, qiQingInLoop;
        lock (_stateLock)
        {
            isQianZhiActive = _isQianZhiActive;
            qiQingInLoop = _qiQingInLoop;
        }
        return new EngineStatus
        {
            IsRunning = _isRunning, IsPaused = _isPaused,
            Mode = _isRunning ? (_isPaused ? "已暂停" : "运行中") : "已停止",
            ExecutionCount = m.TotalExecutions, AvgResponseTime = m.AverageResponseTime, SuccessRate = m.SuccessRate,
            IsQianZhiActive = isQianZhiActive, IsQiQingInLoop = qiQingInLoop,
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
            if (_isPaused) { Thread.Sleep(100); continue; }
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
                // 确保异常时也释放资源
                if (mat != null) _image.ReturnMat(mat);
                Log($"截屏异常: {ex.Message}", 2);
                Thread.Sleep(100);
            }
            Thread.Sleep(10);
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
                if (_isPaused) { Thread.Sleep(100); continue; }
                if (!_imageQueue.TryTake(out currentFrame, 200, token)) continue;
                
                if (IsFrameUnchanged(currentFrame))
                {
                    _unchangedFrameCount++;
                    if (_unchangedFrameCount > 10) Thread.Sleep(_adaptiveDelay.CurrentDelay * 2);
                    continue;
                }
                _unchangedFrameCount = 0;
                _perfMonitor.StartOperation();
                var gameState = _stateDetector.DetectGameState();
                if (gameState.IsCasting) { Log("检测到读条中，等待...", 0); Thread.Sleep(50); continue; }
                foreach (var s in _skillStates) _stateDetector.UpdateSkillState(s, currentFrame);
                var success = ExecuteSkillCycle(gameState);
                _perfMonitor.EndOperation(success);
                loopCount++;
                if (loopCount % 10 == 0) _adaptiveDelay.IsCombatMode = _stateDetector.DetectCombatState();
                if (loopCount % 50 == 0 && _memMonitor.AutoCleanupIfNeeded(150)) Log("内存清理完成", 0);
                var metrics = _perfMonitor.GetMetrics();
                _adaptiveDelay.Adjust(metrics.AverageResponseTime);
                NotifyStatus();
                
                Thread.Sleep(_adaptiveDelay.CurrentDelay);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Log($"循环异常: {ex.Message} | {ex.StackTrace?.Split('\n').FirstOrDefault()}", 3);
                Thread.Sleep(1000);
            }
            finally
            {
                // 确保在所有情况下都释放 Mat 资源
                if (currentFrame != null) _image.ReturnMat(currentFrame);
            }
        }
    }

    private bool IsFrameUnchanged(Mat frame)
    {
        try
        {
            // 使用感知哈希（pHash）的简化版本：缩小 + 灰度 + 二值化
            using var small = new Mat();
            using var gray = new Mat();
            
            // 缩小到 8x8
            Cv2.Resize(frame, small, new OpenCvSharp.Size(8, 8), interpolation: InterpolationFlags.Area);
            
            // 转灰度
            Cv2.CvtColor(small, gray, ColorConversionCodes.BGR2GRAY);
            
            // 计算平均值并生成哈希
            var mean = Cv2.Mean(gray).Val0;
            var hash = new byte[8];
            
            unsafe
            {
                var ptr = (byte*)gray.DataPointer;
                for (int i = 0; i < 64; i++)
                {
                    if (ptr[i] > mean)
                        hash[i / 8] |= (byte)(1 << (i % 8));
                }
            }
            
            // 比较哈希（汉明距离）
            if (_lastFrameHash != null)
            {
                int diff = 0;
                for (int i = 0; i < 8; i++)
                {
                    var xor = (byte)(hash[i] ^ _lastFrameHash[i]);
                    // 计算位差异数
                    while (xor != 0)
                    {
                        diff += xor & 1;
                        xor >>= 1;
                    }
                }
                
                // 如果差异小于阈值（5位），认为帧未变化
                if (diff < 5)
                    return true;
            }
            
            _lastFrameHash = hash;
            return false;
        }
        catch (Exception ex)
        {
            Log($"帧对比异常: {ex.Message}", 0);
            return false;
        }
    }

    private bool ExecuteSkillCycle(GameState gameState)
    {
        UpdateQianZhiState(gameState);
        
        bool isQianZhiActive, qiQingInLoop;
        lock (_stateLock)
        {
            isQianZhiActive = _isQianZhiActive;
            qiQingInLoop = _qiQingInLoop;
        }
        
        if (isQianZhiActive) return HandleQianZhiActiveState(gameState);
        if (qiQingInLoop) { var r = TryExecuteQiQingCombo(gameState); if (r.HasValue) return r.Value; }
        return ExecuteNormalSkillCycle(gameState);
    }

    private void UpdateQianZhiState(GameState gameState)
    {
        var qianZhiBuff = FindBuffByName(QianZhiBuffName);
        if (qianZhiBuff != null)
        {
            var isActive = _stateDetector.CheckBuffExists(qianZhiBuff);
            lock (_stateLock)
            {
                var wasActive = _isQianZhiActive;
                _isQianZhiActive = isActive;
                if (_isQianZhiActive != wasActive) Log(_isQianZhiActive ? "千枝气劲已开启" : "千枝气劲已关闭", 0);
            }
        }
    }

    private bool HandleQianZhiActiveState(GameState gameState)
    {
        bool qiQingInLoop;
        lock (_stateLock) { qiQingInLoop = _qiQingInLoop; }
        
        if (qiQingInLoop)
        {
            var qiQingSkill = FindSkillByName(QiQingSkillName);
            if (qiQingSkill != null && !IsQiQingBuffActive(gameState))
            {
                if (qiQingSkill.IsAvailable && qiQingSkill.IsVisuallyReady) return ExecuteSkill(qiQingSkill);
                lock (_stateLock) { _qiQingInLoop = false; }
                Log($"{QiQingSkillName}CD中，已退出循环", 0);
            }
            else if (IsQiQingBuffActive(gameState))
            {
                lock (_stateLock) { _qiQingInLoop = false; }
                Log("七情被动已激活，已退出循环", 0);
            }
        }
        var chiShaoSkill = FindSkillByName(ChiShaoSkillName);
        if (chiShaoSkill != null && chiShaoSkill.IsAvailable && chiShaoSkill.IsVisuallyReady) return ExecuteSkill(chiShaoSkill);
        Log($"{ChiShaoSkillName}CD中，关闭千枝气劲", 0);
        if (_keyboard.PressAndRelease(QianZhiKeyCode))
        {
            lock (_stateLock) { _isQianZhiActive = false; }
            return true;
        }
        return false;
    }

    private bool? TryExecuteQiQingCombo(GameState gameState)
    {
        var qiQingSkill = FindSkillByName(QiQingSkillName);
        if (qiQingSkill == null) return null;
        if (IsQiQingBuffActive(gameState))
        {
            lock (_stateLock) { _qiQingInLoop = false; }
            Log("七情被动已激活，已退出循环", 0);
            return null;
        }
        if (qiQingSkill.IsAvailable && qiQingSkill.IsVisuallyReady)
        {
            if (gameState.MpPercentage >= 0.3)
            {
                Log("蓝量充足，先开千枝再放七情", 0);
                if (_keyboard.PressAndRelease(QianZhiKeyCode))
                {
                    Thread.Sleep(100);
                    lock (_stateLock) { _isQianZhiActive = true; }
                }
            }
            return ExecuteSkill(qiQingSkill);
        }
        lock (_stateLock) { _qiQingInLoop = false; }
        Log($"{QiQingSkillName}CD中，已退出循环", 0);
        return null;
    }

    private bool ExecuteNormalSkillCycle(GameState gameState)
    {
        // 更新HP/MP状态
        _lastHpPercent = gameState.HpPercentage * 100;
        _lastMpPercent = gameState.MpPercentage * 100;
        
        var context = new StrategyContext { SkillStates = _skillStates, GameState = gameState, LoopMode = _config.AppSettings.EnableSmartMode ? "Smart" : "Default" };
        SkillRuntimeState? skill = null;
        foreach (var s in _strategies) if (s.CanExecute(context) && (skill = s.SelectSkill(context)) != null) break;
        
        // 更新下一个技能名称
        _nextSkillName = skill?.Config.Name;
        
        if (skill == null) return true;

        var buffSatisfied = _stateDetector.CheckBuffRequirements(skill.Config, gameState);
        if (!buffSatisfied && skill.Config.PreCastKeyCode > 0)
        {
            Log($"联动触发: {skill.Config.Name} 缺少Buff [{skill.Config.PreCastConditionBuff}]，释放前置技能", 0);
            if (!_keyboard.PressAndRelease(skill.Config.PreCastKeyCode)) { Log("前置技能释放失败", 2); return false; }
            Thread.Sleep(skill.Config.ComboDelay);
            if (skill.Config.PreCastKeyCode == QianZhiKeyCode)
            {
                lock (_stateLock) { _isQianZhiActive = true; }
            }
            var newState = _stateDetector.DetectGameState();
            if (newState.IsCasting) { Log("前置技能读条中，等待完成...", 0); return true; }
            buffSatisfied = _stateDetector.CheckBuffRequirements(skill.Config, newState);
            if (!buffSatisfied) { Log($"Buff [{skill.Config.PreCastConditionBuff}] 未获得，等待下次循环", 1); return true; }
            Log($"Buff [{skill.Config.PreCastConditionBuff}] 已获得", 0);
        }
        else if (!buffSatisfied && skill.Config.PreCastKeyCode <= 0) { Log($"技能 {skill.Config.Name} Buff条件不满足，跳过", 0); return true; }
        return ExecuteSkill(skill);
    }

    private bool ExecuteSkill(SkillRuntimeState skill)
    {
        if (skill.ConsecutiveFailures >= 5)
        {
            Log($"技能 {skill.Config.Name} 连续失败5次，触发ESC重置", 2);
            _keyboard.PressAndRelease(27);
            skill.ConsecutiveFailures = 0;
            return false;
        }
        
        if (_keyboard.PressAndRelease(skill.Config.KeyCode))
        {
            skill.MarkAsUsed();
            skill.ConsecutiveFailures = 0;
            
            // 记录技能使用，用于CD追踪
            _cooldownTracker.RecordSkillUse(skill.Config.Name, skill.Config.Cooldown);
            
            Log($"释放: {skill.Config.Name}", 0);
            return true;
        }
        
        skill.ConsecutiveFailures++;
        return false;
    }

    /// <summary>
    /// 获取技能CD追踪器
    /// </summary>
    public SkillCooldownTracker CooldownTracker => _cooldownTracker;

    /// <summary>
    /// 获取技能统计信息
    /// </summary>
    public SkillStatistics GetSkillStatistics(string skillName) => _cooldownTracker.GetStatistics(skillName);

    private bool IsQiQingBuffActive(GameState gameState) { var b = FindBuffByName(QiQingBuffName); return b != null && _stateDetector.CheckBuffExists(b); }
    private SkillRuntimeState? FindSkillByName(string name) => _skillStates.FirstOrDefault(s => s.Config.Name == name);
    private BuffRequirement? FindBuffByName(string name) { foreach (var s in _skillStates) { var b = s.Config.BuffRequirements.FirstOrDefault(x => x.Name == name); if (b != null) return b; } return null; }
    private void Log(string msg, int level) { if (level >= _config.AppSettings.LogLevel) LogMessage?.Invoke($"[{DateTime.Now:HH:mm:ss}] {msg}", level); }
    private void NotifyStatus() => StatusChanged?.Invoke(GetStatus());
    public void ReloadConfig() => LoadSkills();
    public void EnableQiQingLoop() { lock (_stateLock) { _qiQingInLoop = true; } Log($"{QiQingSkillName}已加入循环", 1); }
    public void DisableQiQingLoop() { lock (_stateLock) { _qiQingInLoop = false; } Log($"{QiQingSkillName}已退出循环", 1); }
    public bool IsQiQingInLoop { get { lock (_stateLock) { return _qiQingInLoop; } } }
    public bool IsQianZhiActive { get { lock (_stateLock) { return _isQianZhiActive; } } }
}
