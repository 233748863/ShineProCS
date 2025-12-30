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
    private readonly TemplatePreloader _templatePreloader;
    
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
    
    private BlockingCollection<Mat> _imageQueue = null!;
    private int _unchangedFrameCount;
    private int _lastSampleSum;
    private const int SampleStride = 16;
    
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
        Log("引擎已启动", 1);
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

    public EngineStatus GetStatus()
    {
        var m = _perfMonitor.GetMetrics();
        return new EngineStatus
        {
            IsRunning = _isRunning, 
            IsPaused = _isPaused,
            Mode = _isRunning ? (_isPaused ? "已暂停" : "运行中") : "已停止",
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
                
                _stateDetector.UpdateSkillStatesParallel(_skillStates);
                
                var success = ExecuteSkillCycle(gameState);
                _perfMonitor.EndOperation(success);
                loopCount++;
                if (loopCount % 10 == 0) _adaptiveDelay.IsCombatMode = _stateDetector.DetectCombatState();
                if (loopCount % 50 == 0 && _memMonitor.AutoCleanupIfNeeded(150)) Log("内存清理完成", 0);
                
                if (loopCount % 100 == 0 && _image is Infrastructure.OpenCvImageInterface ocv)
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
                Thread.Sleep(1000);
            }
            finally
            {
                if (currentFrame != null) _image.ReturnMat(currentFrame);
            }
        }
    }

    private bool IsFrameUnchanged(Mat frame)
    {
        try
        {
            int sum = 0;
            int width = frame.Width;
            int height = frame.Height;
            int channels = frame.Channels();
            
            unsafe
            {
                var ptr = (byte*)frame.DataPointer;
                int stride = (int)frame.Step();
                
                for (int y = 0; y < height; y += SampleStride)
                {
                    var row = ptr + y * stride;
                    for (int x = 0; x < width; x += SampleStride)
                    {
                        int offset = x * channels;
                        sum += row[offset] + row[offset + 1] + row[offset + 2];
                    }
                }
            }
            
            int diff = Math.Abs(sum - _lastSampleSum);
            _lastSampleSum = sum;
            
            int sampleCount = (width / SampleStride) * (height / SampleStride);
            int threshold = sampleCount * 15;
            
            return diff < threshold;
        }
        catch
        {
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
            LoopMode = _config.AppSettings.EnableSmartMode ? "Smart" : "Default" 
        };
        
        SkillRuntimeState? skill = null;
        foreach (var s in _strategies) 
            if (s.CanExecute(context) && (skill = s.SelectSkill(context)) != null) 
                break;
        
        _nextSkillName = skill?.Config.Name;
        
        if (skill == null) return true;

        // 检查Buff条件
        var buffSatisfied = CheckBuffCondition(skill.Config);
        
        if (!buffSatisfied && skill.Config.PreCastKeyCode > 0)
        {
            Log($"联动触发: {skill.Config.Name} 缺少Buff [{skill.Config.PreCastConditionBuff}]，释放前置技能", 0);
            if (!_keyboard.PressAndRelease(skill.Config.PreCastKeyCode)) 
            { 
                Log("前置技能释放失败", 2); 
                return false; 
            }
            Thread.Sleep(skill.Config.ComboDelay);
            
            var newState = _stateDetector.DetectGameState();
            if (newState.IsCasting) 
            { 
                Log("前置技能读条中，等待完成...", 0); 
                return true; 
            }
            
            buffSatisfied = CheckBuffCondition(skill.Config);
            if (!buffSatisfied) 
            { 
                Log($"Buff [{skill.Config.PreCastConditionBuff}] 未获得，等待下次循环", 1); 
                return true; 
            }
            Log($"Buff [{skill.Config.PreCastConditionBuff}] 已获得", 0);
        }
        else if (!buffSatisfied && skill.Config.PreCastKeyCode <= 0) 
        { 
            Log($"技能 {skill.Config.Name} Buff条件不满足，跳过", 0); 
            return true; 
        }
        
        return ExecuteSkill(skill);
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
        if (skill.ConsecutiveFailures >= 5)
        {
            Log($"技能 {skill.Config.Name} 连续失败5次，触发ESC重置", 2);
            _keyboard.PressAndRelease(27);
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
        if (_keyboard.PressAndRelease(skill.Config.KeyCode))
        {
            skill.MarkAsUsed();
            skill.ConsecutiveFailures = 0;
            _cooldownTracker.RecordSkillUse(skill.Config.Name, skill.Config.Cooldown);
            Log($"释放: {skill.Config.Name} [瞬发]", 0);
            return true;
        }
        
        skill.ConsecutiveFailures++;
        return false;
    }
    
    /// <summary>
    /// 执行正读条技能
    /// </summary>
    private bool ExecuteCastTimeSkill(SkillRuntimeState skill)
    {
        var config = skill.Config;
        
        if (_keyboard.PressAndRelease(config.KeyCode))
        {
            skill.MarkAsUsed();
            skill.ConsecutiveFailures = 0;
            _cooldownTracker.RecordSkillUse(config.Name, config.Cooldown);
            
            var castTime = config.CastDuration;
            if (castTime > 0)
            {
                Log($"释放: {config.Name} [读条 {castTime}ms]", 0);
                // 等待读条完成
                Thread.Sleep(castTime);
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
    
    /// <summary>
    /// 执行引导技能
    /// </summary>
    private bool ExecuteChanneledSkill(SkillRuntimeState skill)
    {
        var config = skill.Config;
        
        // 引导技能需要按住，这里用 PressKey + Sleep + ReleaseKey 模拟
        if (_keyboard.PressKey(config.KeyCode))
        {
            skill.MarkAsUsed();
            skill.ConsecutiveFailures = 0;
            _cooldownTracker.RecordSkillUse(config.Name, config.Cooldown);
            
            // 计算实际引导时间
            var channelTime = config.ChannelInterruptTime > 0 
                ? config.ChannelInterruptTime 
                : config.CastDuration;
            
            if (channelTime > 0)
            {
                var interruptInfo = config.ChannelInterruptTime > 0 
                    ? $"打断于 {config.ChannelInterruptTime}ms" 
                    : "完整引导";
                Log($"释放: {config.Name} [引导 {channelTime}ms, {interruptInfo}]", 0);
                
                // 引导指定时间
                Thread.Sleep(channelTime);
            }
            else
            {
                Log($"释放: {config.Name} [引导]", 0);
            }
            
            // 释放按键（打断引导或自然结束）
            _keyboard.ReleaseKey(config.KeyCode);
            
            return true;
        }
        
        skill.ConsecutiveFailures++;
        return false;
    }

    public SkillCooldownTracker CooldownTracker => _cooldownTracker;
    public SkillStatistics GetSkillStatistics(string skillName) => _cooldownTracker.GetStatistics(skillName);
    
    private void Log(string msg, int level) 
    { 
        if (level >= _config.AppSettings.LogLevel) 
            LogMessage?.Invoke($"[{DateTime.Now:HH:mm:ss}] {msg}", level); 
    }
    
    private void NotifyStatus() => StatusChanged?.Invoke(GetStatus());
    public void ReloadConfig() => LoadSkills();
}
