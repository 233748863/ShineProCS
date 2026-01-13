using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Services;
using ShineProCS.Core.GameTask.Triggers;
using ShineProCS.Core.GameTask.Common;
using ShineProCS.Core.Recognition.OCR;
using ShineProCS.Core.Recognition.YOLO;
using ShineProCS.Models;
using OpenCvSharp;

namespace ShineProCS.Core.GameTask.Tasks;

/// <summary>
/// 自动秘境任务配置
/// </summary>
public class AutoDomainConfig
{
    /// <summary>
    /// 刷取次数（0 表示无限制，直到体力耗尽）
    /// </summary>
    public int RunCount { get; set; } = 1;
    
    /// <summary>
    /// 每次秘境的超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;
    
    /// <summary>
    /// 是否自动使用体力恢复道具
    /// </summary>
    public bool UseResinRecovery { get; set; } = false;
    
    /// <summary>
    /// 战斗超时时间（秒）
    /// </summary>
    public int CombatTimeoutSeconds { get; set; } = 180;
    
    /// <summary>
    /// 是否启用自动领取奖励
    /// </summary>
    public bool AutoClaimReward { get; set; } = true;
    
    /// <summary>
    /// 领取奖励后的等待时间（毫秒）
    /// </summary>
    public int RewardClaimDelayMs { get; set; } = 2000;
    
    /// <summary>
    /// 秘境入口检测区域 [X, Y, Width, Height]
    /// </summary>
    public int[] DomainEntranceRegion { get; set; } = [0, 0, 400, 300];
    
    /// <summary>
    /// 古树检测区域 [X, Y, Width, Height]
    /// </summary>
    public int[] TreeRegion { get; set; } = [0, 0, 400, 300];
    
    /// <summary>
    /// 体力检测区域 [X, Y, Width, Height]
    /// </summary>
    public int[] ResinRegion { get; set; } = [0, 0, 200, 50];
    
    /// <summary>
    /// 奖励界面检测区域 [X, Y, Width, Height]
    /// </summary>
    public int[] RewardRegion { get; set; } = [0, 0, 600, 400];
}

/// <summary>
/// 秘境阶段枚举
/// </summary>
public enum DomainPhase
{
    /// <summary>
    /// 空闲状态
    /// </summary>
    Idle,
    
    /// <summary>
    /// 进入秘境
    /// </summary>
    Entering,
    
    /// <summary>
    /// 战斗中
    /// </summary>
    Fighting,
    
    /// <summary>
    /// 领取奖励
    /// </summary>
    ClaimingReward,
    
    /// <summary>
    /// 退出秘境
    /// </summary>
    Exiting,
    
    /// <summary>
    /// 完成
    /// </summary>
    Completed,
    
    /// <summary>
    /// 失败
    /// </summary>
    Failed
}

/// <summary>
/// 自动秘境任务
/// 实现 ISoloTask 接口，自动完成秘境挑战
/// 需求: 19.1 - 自动秘境作为 ISoloTask 实现
/// 需求: 19.2 - 支持自动进入秘境、战斗、领取奖励的完整流程
/// 需求: 19.3 - 集成技能循环引擎进行自动战斗
/// 需求: 19.4 - 支持配置刷取次数
/// 需求: 19.5 - 当秘境完成或体力耗尽时，任务应自动结束
/// 需求: 19.6 - 支持自动识别秘境入口和古树位置
/// </summary>
public class AutoDomainTask : ISoloTask, IDisposable
{
    #region 常量定义
    
    private const int DefaultLoopIntervalMs = 500;
    private const int EnterDomainTimeoutMs = 30000;
    private const int LoadingTimeoutMs = 20000;
    private const int RewardDetectionIntervalMs = 1000;
    private const int MinResinRequired = 20;
    
    // 虚拟键码
    private const int VK_F = 0x46;
    private const int VK_ESCAPE = 0x1B;
    private const int VK_SPACE = 0x20;
    
    #endregion
    
    #region 依赖组件
    
    private readonly ICaptureService _captureService;
    private readonly IInputService _inputService;
    private readonly ILogService _logService;
    private readonly INotificationService _notificationService;
    private readonly ConfigManager _configManager;
    private readonly SkillLoopTrigger? _skillLoopTrigger;
    private readonly IOcrService? _ocrService;
    private readonly IYoloService? _yoloService;
    
    // 识别服务
    private readonly DomainRecognition _domainRecognition;
    
    #endregion
    
    #region 运行状态
    
    private DomainPhase _currentPhase = DomainPhase.Idle;
    private int _completedRuns;
    private int _currentResin;
    private bool _disposed;
    private readonly object _stateLock = new();
    
    /// <summary>
    /// 任务配置
    /// </summary>
    public AutoDomainConfig Config { get; set; } = new();
    
    #endregion
    
    #region ISoloTask 实现
    
    /// <summary>
    /// 任务名称
    /// </summary>
    public string Name => "自动秘境";
    
    /// <summary>
    /// 任务描述
    /// </summary>
    public string Description => "自动完成秘境挑战，包括进入、战斗和领取奖励";
    
    #endregion
    
    #region 事件
    
    /// <summary>
    /// 阶段变化事件
    /// </summary>
    public event Action<DomainPhase>? PhaseChanged;
    
    /// <summary>
    /// 进度更新事件
    /// </summary>
    public event Action<int, int>? ProgressUpdated;
    
    /// <summary>
    /// 日志消息事件
    /// </summary>
    public event Action<string, int>? LogMessage;
    
    #endregion

    #region 构造函数
    
    /// <summary>
    /// 创建自动秘境任务
    /// </summary>
    public AutoDomainTask(
        ICaptureService captureService,
        IInputService inputService,
        ILogService logService,
        INotificationService notificationService,
        ConfigManager configManager,
        SkillLoopTrigger? skillLoopTrigger = null,
        IOcrService? ocrService = null,
        IYoloService? yoloService = null)
    {
        _captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
        _inputService = inputService ?? throw new ArgumentNullException(nameof(inputService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _skillLoopTrigger = skillLoopTrigger;
        _ocrService = ocrService;
        _yoloService = yoloService;
        
        // 初始化识别服务
        _domainRecognition = new DomainRecognition(
            captureService, logService, configManager, ocrService, yoloService);
    }
    
    #endregion
    
    #region ISoloTask 接口方法
    
    /// <summary>
    /// 启动自动秘境任务
    /// 需求: 19.1 - 自动秘境作为 ISoloTask 实现
    /// </summary>
    public async Task Start(CancellationToken ct)
    {
        Log("自动秘境任务开始", 1);
        _notificationService.ShowInfo("自动秘境任务已启动");
        
        _completedRuns = 0;
        SetPhase(DomainPhase.Idle);
        
        // 从 AppSettings 加载配置
        LoadConfigFromAppSettings();
        
        try
        {
            // 检查初始体力
            // 需求: 19.5 - 体力检测
            _currentResin = await DetectResinAsync(ct);
            Log($"当前体力: {_currentResin}", 1);
            
            var minResin = _configManager.AppSettings.AutoDomainMinResin;
            if (_currentResin < minResin)
            {
                Log($"体力不足（当前: {_currentResin}, 需要: {minResin}），无法开始秘境", 2);
                _notificationService.ShowWarning("体力不足，无法开始秘境");
                SetPhase(DomainPhase.Failed);
                return;
            }
            
            // 主循环
            // 需求: 19.4 - 支持配置刷取次数
            var targetRuns = Config.RunCount > 0 ? Config.RunCount : int.MaxValue;
            
            while (!ct.IsCancellationRequested && _completedRuns < targetRuns)
            {
                // 检查体力
                if (_currentResin < minResin)
                {
                    Log("体力耗尽，任务结束", 1);
                    _notificationService.ShowInfo($"体力耗尽，已完成 {_completedRuns} 次秘境");
                    break;
                }
                
                // 执行单次秘境
                var success = await ExecuteSingleRunAsync(ct);
                
                if (success)
                {
                    _completedRuns++;
                    ProgressUpdated?.Invoke(_completedRuns, targetRuns);
                    Log($"秘境完成 ({_completedRuns}/{targetRuns})", 1);
                    
                    // 更新体力
                    _currentResin = await DetectResinAsync(ct);
                    Log($"剩余体力: {_currentResin}", 1);
                    
                    // 检查是否继续挑战
                    if (!_configManager.AppSettings.AutoDomainContinueChallenge)
                    {
                        Log("配置为不继续挑战，任务结束", 1);
                        break;
                    }
                }
                else
                {
                    Log("秘境执行失败，尝试恢复...", 2);
                    await TryRecoverAsync(ct);
                }
                
                // 短暂等待
                await Task.Delay(DefaultLoopIntervalMs, ct);
            }
            
            SetPhase(DomainPhase.Completed);
            Log($"自动秘境任务完成，共完成 {_completedRuns} 次", 1);
            _notificationService.ShowSuccess($"自动秘境完成，共 {_completedRuns} 次");
        }
        catch (OperationCanceledException)
        {
            Log("自动秘境任务已取消", 1);
            throw;
        }
        catch (Exception ex)
        {
            Log($"自动秘境任务异常: {ex.Message}", 3);
            _notificationService.ShowError($"秘境任务异常: {ex.Message}");
            SetPhase(DomainPhase.Failed);
            throw;
        }
        finally
        {
            // 确保技能循环停止
            StopSkillLoop();
        }
    }
    
    /// <summary>
    /// 从 AppSettings 加载配置
    /// 需求: 19.4 - 支持配置刷取次数
    /// 需求: 19.5 - 体力检测配置
    /// </summary>
    private void LoadConfigFromAppSettings()
    {
        var settings = _configManager.AppSettings;
        
        Config.RunCount = settings.AutoDomainRunCount;
        Config.TimeoutSeconds = settings.AutoDomainTimeoutSeconds;
        Config.CombatTimeoutSeconds = settings.AutoDomainCombatTimeoutSeconds;
        Config.UseResinRecovery = settings.AutoDomainUseResinRecovery;
        Config.AutoClaimReward = settings.AutoDomainAutoClaimReward;
        Config.RewardClaimDelayMs = settings.AutoDomainRewardClaimDelayMs;
        Config.DomainEntranceRegion = settings.AutoDomainEntranceRegion;
        Config.TreeRegion = settings.AutoDomainTreeRegion;
        Config.ResinRegion = settings.AutoDomainResinRegion;
        Config.RewardRegion = settings.AutoDomainRewardRegion;
        
        Log($"配置已加载: 刷取次数={Config.RunCount}, 超时={Config.TimeoutSeconds}s", 0);
    }
    
    #endregion
    
    #region 秘境流程控制
    
    /// <summary>
    /// 执行单次秘境
    /// 需求: 19.2 - 支持自动进入秘境、战斗、领取奖励的完整流程
    /// </summary>
    private async Task<bool> ExecuteSingleRunAsync(CancellationToken ct)
    {
        try
        {
            // 阶段1: 进入秘境
            SetPhase(DomainPhase.Entering);
            if (!await EnterDomainAsync(ct))
            {
                Log("进入秘境失败", 2);
                return false;
            }
            
            // 阶段2: 战斗
            // 需求: 19.3 - 集成技能循环引擎进行自动战斗
            SetPhase(DomainPhase.Fighting);
            if (!await ExecuteCombatAsync(ct))
            {
                Log("战斗失败", 2);
                return false;
            }
            
            // 阶段3: 领取奖励
            if (Config.AutoClaimReward)
            {
                SetPhase(DomainPhase.ClaimingReward);
                if (!await ClaimRewardAsync(ct))
                {
                    Log("领取奖励失败", 2);
                    // 奖励领取失败不算整体失败
                }
            }
            
            // 阶段4: 退出秘境
            SetPhase(DomainPhase.Exiting);
            await ExitDomainAsync(ct);
            
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log($"秘境执行异常: {ex.Message}", 3);
            return false;
        }
    }
    
    /// <summary>
    /// 进入秘境
    /// 需求: 19.6 - 支持自动识别秘境入口
    /// </summary>
    private async Task<bool> EnterDomainAsync(CancellationToken ct)
    {
        Log("正在进入秘境...", 1);
        
        var startTime = DateTime.Now;
        var timeout = TimeSpan.FromMilliseconds(EnterDomainTimeoutMs);
        
        while (!ct.IsCancellationRequested && DateTime.Now - startTime < timeout)
        {
            // 检测秘境入口
            if (await DetectDomainEntranceAsync(ct))
            {
                // 按 F 键交互
                _inputService.Keyboard.PressAndRelease(VK_F);
                Log("检测到秘境入口，尝试进入", 0);
                
                // 等待加载
                await Task.Delay(2000, ct);
                
                // 检测是否成功进入（通过检测战斗界面或加载完成）
                if (await WaitForDomainLoadAsync(ct))
                {
                    Log("成功进入秘境", 1);
                    return true;
                }
            }
            
            await Task.Delay(DefaultLoopIntervalMs, ct);
        }
        
        Log("进入秘境超时", 2);
        return false;
    }
    
    /// <summary>
    /// 执行战斗
    /// 需求: 19.3 - 集成技能循环引擎进行自动战斗
    /// </summary>
    private async Task<bool> ExecuteCombatAsync(CancellationToken ct)
    {
        Log("开始战斗...", 1);
        
        // 启动技能循环
        StartSkillLoop();
        
        var startTime = DateTime.Now;
        var timeout = TimeSpan.FromSeconds(Config.CombatTimeoutSeconds);
        
        try
        {
            while (!ct.IsCancellationRequested && DateTime.Now - startTime < timeout)
            {
                // 检测战斗是否结束（通过检测奖励界面或古树）
                if (await DetectCombatEndAsync(ct))
                {
                    Log("战斗结束", 1);
                    return true;
                }
                
                await Task.Delay(RewardDetectionIntervalMs, ct);
            }
            
            Log("战斗超时", 2);
            return false;
        }
        finally
        {
            // 停止技能循环
            StopSkillLoop();
        }
    }
    
    /// <summary>
    /// 领取奖励
    /// 需求: 19.2 - 自动领取奖励
    /// </summary>
    private async Task<bool> ClaimRewardAsync(CancellationToken ct)
    {
        Log("正在领取奖励...", 1);
        
        // 等待奖励界面出现
        await Task.Delay(1000, ct);
        
        // 检测古树位置并交互
        // 需求: 19.6 - 支持自动识别古树位置
        if (await DetectAndInteractTreeAsync(ct))
        {
            // 等待奖励界面
            await Task.Delay(Config.RewardClaimDelayMs, ct);
            
            // 点击领取
            _inputService.Keyboard.PressAndRelease(VK_F);
            Log("已领取奖励", 1);
            
            await Task.Delay(1000, ct);
            return true;
        }
        
        Log("未检测到古树", 2);
        return false;
    }
    
    /// <summary>
    /// 退出秘境
    /// </summary>
    private async Task ExitDomainAsync(CancellationToken ct)
    {
        Log("正在退出秘境...", 1);
        
        // 按 ESC 打开菜单
        _inputService.Keyboard.PressAndRelease(VK_ESCAPE);
        await Task.Delay(500, ct);
        
        // 选择离开秘境选项（通常是点击或按键）
        // 这里简化处理，实际需要根据游戏界面调整
        _inputService.Keyboard.PressAndRelease(VK_F);
        await Task.Delay(500, ct);
        
        // 确认离开
        _inputService.Keyboard.PressAndRelease(VK_F);
        
        // 等待加载
        await Task.Delay(LoadingTimeoutMs / 2, ct);
        
        Log("已退出秘境", 1);
    }
    
    #endregion

    #region 识别方法
    
    /// <summary>
    /// 检测体力
    /// 需求: 19.5 - 体力检测
    /// </summary>
    private Task<int> DetectResinAsync(CancellationToken ct)
    {
        try
        {
            var resinInfo = _domainRecognition.DetectResin();
            return Task.FromResult(resinInfo.Current);
        }
        catch (Exception ex)
        {
            Log($"体力检测异常: {ex.Message}", 2);
            return Task.FromResult(160);
        }
    }
    
    /// <summary>
    /// 检测秘境入口
    /// 需求: 19.6 - 支持自动识别秘境入口
    /// </summary>
    private Task<bool> DetectDomainEntranceAsync(CancellationToken ct)
    {
        try
        {
            var entranceInfo = _domainRecognition.DetectDomainEntrance();
            
            if (entranceInfo.Found)
            {
                Log($"检测到秘境入口: {entranceInfo.DomainName}, 可进入: {entranceInfo.CanEnter}", 0);
            }
            
            return Task.FromResult(entranceInfo.Found && entranceInfo.CanEnter);
        }
        catch (Exception ex)
        {
            Log($"秘境入口检测异常: {ex.Message}", 2);
            return Task.FromResult(false);
        }
    }
    
    /// <summary>
    /// 等待秘境加载完成
    /// </summary>
    private async Task<bool> WaitForDomainLoadAsync(CancellationToken ct)
    {
        var startTime = DateTime.Now;
        var timeout = TimeSpan.FromMilliseconds(LoadingTimeoutMs);
        
        while (!ct.IsCancellationRequested && DateTime.Now - startTime < timeout)
        {
            // 检测是否进入战斗场景
            var detectionRegion = _configManager.AppSettings.DetectionRegion;
            var screenshot = _captureService.GetScreenRegion(
                detectionRegion[0], detectionRegion[1], 
                detectionRegion[2], detectionRegion[3]);
            
            if (screenshot != null)
            {
                try
                {
                    // 简单判断：如果截图不是纯黑/加载画面，认为加载完成
                    var brightness = CalculateAverageBrightness(screenshot);
                    if (brightness > 30) // 非加载画面
                    {
                        return true;
                    }
                }
                finally
                {
                    _captureService.ReturnMat(screenshot);
                }
            }
            
            await Task.Delay(500, ct);
        }
        
        return false;
    }
    
    /// <summary>
    /// 检测战斗是否结束
    /// </summary>
    private Task<bool> DetectCombatEndAsync(CancellationToken ct)
    {
        try
        {
            return Task.FromResult(_domainRecognition.DetectCombatEnd());
        }
        catch (Exception ex)
        {
            Log($"战斗结束检测异常: {ex.Message}", 2);
            return Task.FromResult(false);
        }
    }
    
    /// <summary>
    /// 检测并交互古树
    /// 需求: 19.6 - 支持自动识别古树位置
    /// </summary>
    private Task<bool> DetectAndInteractTreeAsync(CancellationToken ct)
    {
        try
        {
            var treeInfo = _domainRecognition.DetectTree();
            
            if (treeInfo.Found)
            {
                Log($"检测到古树，可交互: {treeInfo.CanInteract}", 0);
                
                // 如果有位置信息，可以移动到古树位置
                // 这里简化处理，直接按 F 键交互
                _inputService.Keyboard.PressAndRelease(VK_F);
                return Task.FromResult(true);
            }
            
            // 未检测到古树，尝试默认交互
            _inputService.Keyboard.PressAndRelease(VK_F);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Log($"古树检测异常: {ex.Message}", 2);
            return Task.FromResult(false);
        }
    }
    
    #endregion
    
    #region 技能循环控制
    
    /// <summary>
    /// 启动技能循环
    /// 需求: 19.3 - 集成技能循环引擎进行自动战斗
    /// </summary>
    private void StartSkillLoop()
    {
        if (_skillLoopTrigger != null)
        {
            _skillLoopTrigger.IsEnabled = true;
            _skillLoopTrigger.Start();
            Log("技能循环已启动", 0);
        }
        else
        {
            Log("技能循环触发器不可用", 2);
        }
    }
    
    /// <summary>
    /// 停止技能循环
    /// </summary>
    private void StopSkillLoop()
    {
        if (_skillLoopTrigger != null)
        {
            _skillLoopTrigger.Stop();
            Log("技能循环已停止", 0);
        }
    }
    
    #endregion
    
    #region 恢复和辅助方法
    
    /// <summary>
    /// 尝试从异常状态恢复
    /// </summary>
    private async Task TryRecoverAsync(CancellationToken ct)
    {
        Log("尝试恢复...", 1);
        
        // 停止技能循环
        StopSkillLoop();
        
        // 按 ESC 关闭可能的弹窗
        _inputService.Keyboard.PressAndRelease(VK_ESCAPE);
        await Task.Delay(500, ct);
        
        // 再按一次确保关闭
        _inputService.Keyboard.PressAndRelease(VK_ESCAPE);
        await Task.Delay(1000, ct);
        
        SetPhase(DomainPhase.Idle);
    }
    
    /// <summary>
    /// 计算图像平均亮度
    /// </summary>
    private static double CalculateAverageBrightness(Mat image)
    {
        try
        {
            using var gray = new Mat();
            if (image.Channels() == 3)
            {
                Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
            }
            else
            {
                image.CopyTo(gray);
            }
            
            return Cv2.Mean(gray).Val0;
        }
        catch
        {
            return 0;
        }
    }
    
    /// <summary>
    /// 设置当前阶段
    /// </summary>
    private void SetPhase(DomainPhase phase)
    {
        lock (_stateLock)
        {
            if (_currentPhase == phase) return;
            _currentPhase = phase;
        }
        
        Log($"阶段变更: {phase}", 0);
        PhaseChanged?.Invoke(phase);
    }
    
    /// <summary>
    /// 获取当前阶段
    /// </summary>
    public DomainPhase GetCurrentPhase()
    {
        lock (_stateLock)
        {
            return _currentPhase;
        }
    }
    
    /// <summary>
    /// 获取已完成次数
    /// </summary>
    public int GetCompletedRuns()
    {
        lock (_stateLock)
        {
            return _completedRuns;
        }
    }
    
    /// <summary>
    /// 获取当前体力
    /// </summary>
    public int GetCurrentResin()
    {
        lock (_stateLock)
        {
            return _currentResin;
        }
    }
    
    #endregion
    
    #region 日志方法
    
    private void Log(string message, int level)
    {
        var logLevel = level switch
        {
            0 => Interfaces.LogLevel.Debug,
            1 => Interfaces.LogLevel.Info,
            2 => Interfaces.LogLevel.Warning,
            3 => Interfaces.LogLevel.Error,
            _ => Interfaces.LogLevel.Info
        };
        
        _logService.Log($"[自动秘境] {message}", logLevel, "AutoDomainTask");
        LogMessage?.Invoke(message, level);
    }
    
    #endregion
    
    #region IDisposable 实现
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        StopSkillLoop();
        
        GC.SuppressFinalize(this);
    }
    
    #endregion
}
