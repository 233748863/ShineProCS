using ShineProCS.Core.Interfaces;

namespace ShineProCS.Core.Pathing;

/// <summary>
/// 移动控制配置
/// </summary>
public class MovementConfig
{
    /// <summary>
    /// 角度修正阈值（度数）
    /// 需求: 20.5 - 当偏离路径时，自动修正方向
    /// </summary>
    public double HeadingCorrectionThreshold { get; set; } = 15.0;
    
    /// <summary>
    /// 大角度转向阈值（度数）
    /// </summary>
    public double LargeTurnThreshold { get; set; } = 45.0;
    
    /// <summary>
    /// 冲刺距离阈值（像素）
    /// </summary>
    public double SprintDistanceThreshold { get; set; } = 50.0;
    
    /// <summary>
    /// 停止移动的距离阈值（像素）
    /// </summary>
    public double StopDistanceThreshold { get; set; } = 5.0;
    
    /// <summary>
    /// 移动检查间隔（毫秒）
    /// </summary>
    public int MovementCheckIntervalMs { get; set; } = 100;
    
    /// <summary>
    /// 转向时是否停止前进
    /// </summary>
    public bool StopWhileTurning { get; set; } = false;
    
    /// <summary>
    /// 卡住检测时间（毫秒）
    /// </summary>
    public int StuckDetectionTimeMs { get; set; } = 3000;
    
    /// <summary>
    /// 卡住检测距离阈值（像素）
    /// </summary>
    public double StuckDistanceThreshold { get; set; } = 5.0;
}


/// <summary>
/// 移动控制器
/// 需求: 20.3 - 自动控制角色移动到目标点
/// 需求: 20.5 - 当偏离路径时，自动修正方向
/// </summary>
public class MovementController : IDisposable
{
    #region 虚拟键码
    
    private const int VK_W = 0x57;
    private const int VK_A = 0x41;
    private const int VK_S = 0x53;
    private const int VK_D = 0x44;
    private const int VK_SPACE = 0x20;
    private const int VK_LSHIFT = 0xA0;
    
    #endregion
    
    #region 依赖组件
    
    private readonly IInputService _inputService;
    private readonly ILogService _logService;
    
    #endregion
    
    #region 状态
    
    private MovementConfig _config = new();
    private bool _isMoving;
    private DateTime _lastPositionTime;
    private double _lastX, _lastY;
    private readonly object _lock = new();
    private bool _disposed;
    
    // 当前按下的键
    private bool _wPressed, _aPressed, _sPressed, _dPressed;
    private bool _shiftPressed, _spacePressed;
    
    #endregion
    
    #region 事件
    
    /// <summary>
    /// 卡住检测事件
    /// </summary>
    public event Action? StuckDetected;
    
    /// <summary>
    /// 移动状态变化事件
    /// </summary>
    public event Action<bool>? MovementStateChanged;
    
    #endregion
    
    #region 构造函数
    
    /// <summary>
    /// 创建移动控制器
    /// </summary>
    public MovementController(IInputService inputService, ILogService logService)
    {
        _inputService = inputService ?? throw new ArgumentNullException(nameof(inputService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    }
    
    #endregion
    
    #region 配置
    
    /// <summary>
    /// 设置移动配置
    /// </summary>
    public void SetConfig(MovementConfig config)
    {
        _config = config ?? new MovementConfig();
    }
    
    #endregion
    
    #region 移动控制
    
    /// <summary>
    /// 向目标点移动
    /// 需求: 20.3 - 自动控制角色移动到目标点
    /// 需求: 20.5 - 当偏离路径时，自动修正方向
    /// </summary>
    public void MoveTowards(PositionInfo currentPos, PathPoint target)
    {
        if (!currentPos.IsValid)
        {
            StopMovement();
            return;
        }
        
        // 计算到目标的距离和方向
        var distance = target.DistanceTo(currentPos.X, currentPos.Y);
        var targetAngle = target.AngleFromDegrees(currentPos.X, currentPos.Y);
        var angleDiff = NormalizeAngle(targetAngle - currentPos.Heading);
        
        // 检查是否到达
        if (distance <= _config.StopDistanceThreshold)
        {
            StopMovement();
            return;
        }
        
        // 检查是否卡住
        CheckStuck(currentPos.X, currentPos.Y);
        
        // 控制移动
        ControlMovement(angleDiff, target.MoveType, distance);
    }


    /// <summary>
    /// 控制移动
    /// </summary>
    private void ControlMovement(double angleDiff, PathPointMoveType moveType, double distance)
    {
        var keyboard = _inputService.Keyboard;
        var absAngleDiff = Math.Abs(angleDiff);
        
        // 判断是否需要大幅度转向
        var needLargeTurn = absAngleDiff > _config.LargeTurnThreshold;
        var needCorrection = absAngleDiff > _config.HeadingCorrectionThreshold;
        
        // 如果需要大幅度转向且配置为转向时停止
        if (needLargeTurn && _config.StopWhileTurning)
        {
            ReleaseKey(VK_W, ref _wPressed);
        }
        else
        {
            // 前进
            PressKey(VK_W, ref _wPressed);
        }
        
        // 转向控制
        // 需求: 20.5 - 当偏离路径时，自动修正方向
        if (needCorrection)
        {
            if (angleDiff > 0 && angleDiff < 180)
            {
                // 向右转
                PressKey(VK_D, ref _dPressed);
                ReleaseKey(VK_A, ref _aPressed);
            }
            else
            {
                // 向左转
                PressKey(VK_A, ref _aPressed);
                ReleaseKey(VK_D, ref _dPressed);
            }
        }
        else
        {
            // 不需要转向，释放左右键
            ReleaseKey(VK_A, ref _aPressed);
            ReleaseKey(VK_D, ref _dPressed);
        }
        
        // 冲刺控制
        var shouldSprint = moveType == PathPointMoveType.Sprint ||
                          (moveType == PathPointMoveType.Run && distance > _config.SprintDistanceThreshold);
        
        if (shouldSprint && !needLargeTurn)
        {
            PressKey(VK_LSHIFT, ref _shiftPressed);
        }
        else
        {
            ReleaseKey(VK_LSHIFT, ref _shiftPressed);
        }
        
        // 更新移动状态
        SetMovingState(true);
    }
    
    /// <summary>
    /// 停止移动
    /// </summary>
    public void StopMovement()
    {
        ReleaseAllKeys();
        SetMovingState(false);
    }
    
    /// <summary>
    /// 跳跃
    /// </summary>
    public void Jump()
    {
        _inputService.Keyboard.PressAndRelease(VK_SPACE);
    }
    
    /// <summary>
    /// 开始冲刺
    /// </summary>
    public void StartSprint()
    {
        PressKey(VK_LSHIFT, ref _shiftPressed);
    }
    
    /// <summary>
    /// 停止冲刺
    /// </summary>
    public void StopSprint()
    {
        ReleaseKey(VK_LSHIFT, ref _shiftPressed);
    }
    
    /// <summary>
    /// 前进
    /// </summary>
    public void MoveForward()
    {
        PressKey(VK_W, ref _wPressed);
        SetMovingState(true);
    }
    
    /// <summary>
    /// 后退
    /// </summary>
    public void MoveBackward()
    {
        PressKey(VK_S, ref _sPressed);
        SetMovingState(true);
    }
    
    /// <summary>
    /// 向左移动
    /// </summary>
    public void MoveLeft()
    {
        PressKey(VK_A, ref _aPressed);
        SetMovingState(true);
    }
    
    /// <summary>
    /// 向右移动
    /// </summary>
    public void MoveRight()
    {
        PressKey(VK_D, ref _dPressed);
        SetMovingState(true);
    }
    
    #endregion


    #region 卡住检测
    
    /// <summary>
    /// 检查是否卡住
    /// </summary>
    private void CheckStuck(double x, double y)
    {
        lock (_lock)
        {
            var now = DateTime.Now;
            var distance = Math.Sqrt(Math.Pow(x - _lastX, 2) + Math.Pow(y - _lastY, 2));
            
            if (distance > _config.StuckDistanceThreshold)
            {
                // 有移动，更新位置
                _lastX = x;
                _lastY = y;
                _lastPositionTime = now;
            }
            else if ((now - _lastPositionTime).TotalMilliseconds > _config.StuckDetectionTimeMs)
            {
                // 卡住了
                Log("检测到卡住", 2);
                StuckDetected?.Invoke();
                
                // 重置检测
                _lastPositionTime = now;
            }
        }
    }
    
    /// <summary>
    /// 重置卡住检测
    /// </summary>
    public void ResetStuckDetection()
    {
        lock (_lock)
        {
            _lastPositionTime = DateTime.Now;
            _lastX = 0;
            _lastY = 0;
        }
    }
    
    #endregion
    
    #region 按键控制
    
    /// <summary>
    /// 按下按键
    /// </summary>
    private void PressKey(int keyCode, ref bool pressed)
    {
        if (!pressed)
        {
            _inputService.Keyboard.PressKey(keyCode);
            pressed = true;
        }
    }
    
    /// <summary>
    /// 释放按键
    /// </summary>
    private void ReleaseKey(int keyCode, ref bool pressed)
    {
        if (pressed)
        {
            _inputService.Keyboard.ReleaseKey(keyCode);
            pressed = false;
        }
    }
    
    /// <summary>
    /// 释放所有按键
    /// </summary>
    private void ReleaseAllKeys()
    {
        var keyboard = _inputService.Keyboard;
        
        if (_wPressed) { keyboard.ReleaseKey(VK_W); _wPressed = false; }
        if (_aPressed) { keyboard.ReleaseKey(VK_A); _aPressed = false; }
        if (_sPressed) { keyboard.ReleaseKey(VK_S); _sPressed = false; }
        if (_dPressed) { keyboard.ReleaseKey(VK_D); _dPressed = false; }
        if (_shiftPressed) { keyboard.ReleaseKey(VK_LSHIFT); _shiftPressed = false; }
        if (_spacePressed) { keyboard.ReleaseKey(VK_SPACE); _spacePressed = false; }
    }
    
    #endregion
    
    #region 辅助方法
    
    /// <summary>
    /// 设置移动状态
    /// </summary>
    private void SetMovingState(bool isMoving)
    {
        if (_isMoving != isMoving)
        {
            _isMoving = isMoving;
            MovementStateChanged?.Invoke(isMoving);
        }
    }
    
    /// <summary>
    /// 标准化角度到 -180 到 180 范围
    /// </summary>
    private static double NormalizeAngle(double angle)
    {
        while (angle > 180) angle -= 360;
        while (angle < -180) angle += 360;
        return angle;
    }
    
    /// <summary>
    /// 是否正在移动
    /// </summary>
    public bool IsMoving => _isMoving;
    
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
        
        _logService.Log($"[移动控制] {message}", logLevel, "MovementController");
    }
    
    #endregion
    
    #region IDisposable 实现
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        StopMovement();
        GC.SuppressFinalize(this);
    }
    
    #endregion
}
