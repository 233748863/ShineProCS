using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Services;
using ShineProCS.Core.Pathing;
using ShineProCS.Core.GameTask.Triggers;

namespace ShineProCS.Core.GameTask.Tasks;

/// <summary>
/// 地图追踪任务配置
/// </summary>
public class PathingTaskConfig
{
    /// <summary>
    /// 路径文件路径
    /// </summary>
    public string PathFilePath { get; set; } = "";
    
    /// <summary>
    /// 是否循环执行
    /// </summary>
    public bool Loop { get; set; } = false;
    
    /// <summary>
    /// 循环次数（0 表示无限循环）
    /// </summary>
    public int LoopCount { get; set; } = 0;
    
    /// <summary>
    /// 任务超时时间（秒，0 表示无限制）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 0;
}


/// <summary>
/// 地图追踪任务
/// 实现 ISoloTask 接口，自动沿着预设路径移动
/// 需求: 20.1 - 地图追踪通过小地图识别当前位置和方向
/// 需求: 20.2 - 支持加载预设的路径文件（JSON 格式）
/// 需求: 20.3 - 自动控制角色移动到目标点
/// 需求: 20.4 - 支持在路径点执行自定义动作
/// 需求: 20.5 - 当偏离路径时，自动修正方向
/// 需求: 20.6 - 支持暂停和恢复
/// </summary>
public class PathingTask : ISoloTask, IDisposable
{
    #region 依赖组件
    
    private readonly ICaptureService _captureService;
    private readonly IInputService _inputService;
    private readonly ILogService _logService;
    private readonly INotificationService _notificationService;
    private readonly ConfigManager _configManager;
    private readonly SkillLoopTrigger? _skillLoopTrigger;
    
    // 子组件
    private readonly PathingService _pathingService;
    private readonly PathLoader _pathLoader;
    private readonly PositionRecognizer _positionRecognizer;
    private readonly MovementController _movementController;
    private readonly ActionExecutor _actionExecutor;
    
    #endregion
    
    #region 状态
    
    private bool _disposed;
    private readonly object _stateLock = new();
    
    /// <summary>
    /// 任务配置
    /// </summary>
    public PathingTaskConfig Config { get; set; } = new();
    
    #endregion
    
    #region ISoloTask 实现
    
    /// <summary>
    /// 任务名称
    /// </summary>
    public string Name => "地图追踪";
    
    /// <summary>
    /// 任务描述
    /// </summary>
    public string Description => "自动沿着预设路径移动，支持采集、战斗等动作";
    
    #endregion
    
    #region 事件
    
    /// <summary>
    /// 进度更新事件
    /// </summary>
    public event Action<PathingProgress>? ProgressUpdated;
    
    /// <summary>
    /// 状态变化事件
    /// </summary>
    public event Action<PathingState>? StateChanged;
    
    /// <summary>
    /// 日志消息事件
    /// </summary>
    public event Action<string, int>? LogMessage;
    
    #endregion
    
    #region 构造函数
    
    /// <summary>
    /// 创建地图追踪任务
    /// </summary>
    public PathingTask(
        ICaptureService captureService,
        IInputService inputService,
        ILogService logService,
        INotificationService notificationService,
        ConfigManager configManager,
        SkillLoopTrigger? skillLoopTrigger = null)
    {
        _captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
        _inputService = inputService ?? throw new ArgumentNullException(nameof(inputService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _skillLoopTrigger = skillLoopTrigger;
        
        // 初始化子组件
        _pathingService = new PathingService(captureService, inputService, logService, configManager);
        _pathLoader = new PathLoader(logService);
        _positionRecognizer = new PositionRecognizer(captureService, logService);
        _movementController = new MovementController(inputService, logService);
        _actionExecutor = new ActionExecutor(inputService, logService, skillLoopTrigger);
        
        // 订阅事件
        _pathingService.ProgressUpdated += progress => ProgressUpdated?.Invoke(progress);
        _pathingService.StateChanged += state => StateChanged?.Invoke(state);
        _pathingService.LogMessage += (msg, level) => LogMessage?.Invoke(msg, level);
    }
    
    #endregion


    #region ISoloTask 接口方法
    
    /// <summary>
    /// 启动地图追踪任务
    /// </summary>
    public async Task Start(CancellationToken ct)
    {
        Log("地图追踪任务开始", 1);
        _notificationService.ShowInfo("地图追踪任务已启动");
        
        try
        {
            // 加载路径
            // 需求: 20.2 - 支持加载预设的路径文件（JSON 格式）
            var pathData = _pathLoader.LoadFromFile(Config.PathFilePath);
            if (pathData == null)
            {
                Log($"无法加载路径文件: {Config.PathFilePath}", 3);
                _notificationService.ShowError("无法加载路径文件");
                return;
            }
            
            // 应用配置覆盖
            if (Config.Loop)
            {
                pathData.Loop = true;
                pathData.LoopCount = Config.LoopCount;
            }
            
            Log($"已加载路径: {pathData.Name}, 共 {pathData.PointCount} 个点", 1);
            
            // 开始追踪
            // 需求: 20.3 - 自动控制角色移动到目标点
            var success = await _pathingService.StartTrackingAsync(pathData, ct);
            
            if (success)
            {
                Log("地图追踪任务完成", 1);
                _notificationService.ShowSuccess("地图追踪完成");
            }
            else
            {
                Log("地图追踪任务未完成", 2);
                _notificationService.ShowWarning("地图追踪未完成");
            }
        }
        catch (OperationCanceledException)
        {
            Log("地图追踪任务已取消", 1);
            throw;
        }
        catch (Exception ex)
        {
            Log($"地图追踪任务异常: {ex.Message}", 3);
            _notificationService.ShowError($"追踪异常: {ex.Message}");
            throw;
        }
        finally
        {
            _movementController.StopMovement();
        }
    }
    
    #endregion
    
    #region 控制方法
    
    /// <summary>
    /// 暂停追踪
    /// 需求: 20.6 - 支持追踪暂停
    /// </summary>
    public void Pause()
    {
        _pathingService.Pause();
        _movementController.StopMovement();
        Log("追踪已暂停", 1);
    }
    
    /// <summary>
    /// 恢复追踪
    /// 需求: 20.6 - 支持追踪恢复
    /// </summary>
    public void Resume()
    {
        _pathingService.Resume();
        Log("追踪已恢复", 1);
    }
    
    /// <summary>
    /// 停止追踪
    /// </summary>
    public void Stop()
    {
        _pathingService.StopTracking();
        _movementController.StopMovement();
        Log("追踪已停止", 1);
    }
    
    /// <summary>
    /// 获取当前状态
    /// </summary>
    public PathingState GetState() => _pathingService.State;
    
    /// <summary>
    /// 是否正在追踪
    /// </summary>
    public bool IsTracking => _pathingService.IsTracking;
    
    /// <summary>
    /// 是否已暂停
    /// </summary>
    public bool IsPaused => _pathingService.IsPaused;
    
    #endregion
    
    #region 路径管理
    
    /// <summary>
    /// 获取可用的路径列表
    /// </summary>
    public List<string> GetAvailablePaths()
    {
        return _pathLoader.GetAvailablePaths();
    }
    
    /// <summary>
    /// 加载路径信息
    /// </summary>
    public PathData? LoadPathInfo(string filePath)
    {
        return _pathLoader.LoadFromFile(filePath);
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
        
        _logService.Log($"[地图追踪] {message}", logLevel, "PathingTask");
        LogMessage?.Invoke(message, level);
    }
    
    #endregion
    
    #region IDisposable 实现
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        Stop();
        
        _pathingService.Dispose();
        _positionRecognizer.Dispose();
        _movementController.Dispose();
        _actionExecutor.Dispose();
        
        GC.SuppressFinalize(this);
    }
    
    #endregion
}
