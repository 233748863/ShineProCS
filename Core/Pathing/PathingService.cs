using System.Text.Json;
using System.IO;
using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Services;
using OpenCvSharp;
using Point = OpenCvSharp.Point;

namespace ShineProCS.Core.Pathing;

/// <summary>
/// 追踪状态枚举
/// </summary>
public enum PathingState
{
    /// <summary>
    /// 空闲状态
    /// </summary>
    Idle,
    
    /// <summary>
    /// 正在追踪
    /// </summary>
    Tracking,
    
    /// <summary>
    /// 已暂停
    /// 需求: 20.6 - 支持追踪暂停
    /// </summary>
    Paused,
    
    /// <summary>
    /// 执行动作中
    /// </summary>
    ExecutingAction,
    
    /// <summary>
    /// 已完成
    /// </summary>
    Completed,
    
    /// <summary>
    /// 已取消
    /// </summary>
    Cancelled,
    
    /// <summary>
    /// 错误状态
    /// </summary>
    Error
}


/// <summary>
/// 位置信息
/// 需求: 20.1 - 通过小地图识别当前位置和方向
/// </summary>
public class PositionInfo
{
    /// <summary>
    /// X 坐标
    /// </summary>
    public double X { get; set; }
    
    /// <summary>
    /// Y 坐标
    /// </summary>
    public double Y { get; set; }
    
    /// <summary>
    /// 角色朝向（度数，0-360，0 为正北）
    /// </summary>
    public double Heading { get; set; }
    
    /// <summary>
    /// 位置识别置信度
    /// </summary>
    public double Confidence { get; set; }
    
    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid { get; set; }
    
    /// <summary>
    /// 识别时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

/// <summary>
/// 追踪进度信息
/// </summary>
public class PathingProgress
{
    /// <summary>
    /// 当前路径点索引
    /// </summary>
    public int CurrentPointIndex { get; set; }
    
    /// <summary>
    /// 总路径点数
    /// </summary>
    public int TotalPoints { get; set; }
    
    /// <summary>
    /// 当前循环次数
    /// </summary>
    public int CurrentLoop { get; set; }
    
    /// <summary>
    /// 总循环次数
    /// </summary>
    public int TotalLoops { get; set; }
    
    /// <summary>
    /// 到当前目标点的距离
    /// </summary>
    public double DistanceToTarget { get; set; }
    
    /// <summary>
    /// 当前状态
    /// </summary>
    public PathingState State { get; set; }
    
    /// <summary>
    /// 进度百分比 (0-100)
    /// </summary>
    public double ProgressPercent => TotalPoints > 0 
        ? (CurrentPointIndex * 100.0 / TotalPoints) 
        : 0;
}


/// <summary>
/// 地图追踪服务
/// 需求: 20.1 - 地图追踪通过小地图识别当前位置和方向
/// 需求: 20.2 - 支持加载预设的路径文件（JSON 格式）
/// 需求: 20.3 - 自动控制角色移动到目标点
/// 需求: 20.4 - 支持在路径点执行自定义动作
/// 需求: 20.5 - 当偏离路径时，自动修正方向
/// 需求: 20.6 - 支持暂停和恢复
/// </summary>
public class PathingService : IDisposable
{
    #region 常量定义
    
    // 默认配置
    private const int DefaultLoopIntervalMs = 100;
    private const int DefaultPointTimeoutMs = 30000;
    private const double DefaultTolerance = 10.0;
    private const double HeadingCorrectionThreshold = 15.0; // 角度偏差超过此值时修正
    private const int MovementCheckIntervalMs = 200;
    
    // 虚拟键码
    private const int VK_W = 0x57;
    private const int VK_A = 0x41;
    private const int VK_S = 0x53;
    private const int VK_D = 0x44;
    private const int VK_F = 0x46;
    private const int VK_SPACE = 0x20;
    private const int VK_SHIFT = 0x10;
    private const int VK_LSHIFT = 0xA0;
    
    #endregion
    
    #region 依赖组件
    
    private readonly ICaptureService _captureService;
    private readonly IInputService _inputService;
    private readonly ILogService _logService;
    private readonly ConfigManager _configManager;
    
    #endregion
    
    #region 运行状态
    
    private PathData? _currentPath;
    private int _currentPointIndex;
    private int _currentLoop;
    private PathingState _state = PathingState.Idle;
    private CancellationTokenSource? _cts;
    private Task? _trackingTask;
    private readonly object _stateLock = new();
    private bool _disposed;
    
    // 暂停控制
    // 需求: 20.6 - 支持追踪暂停和恢复
    private readonly ManualResetEventSlim _pauseEvent = new(true);
    
    #endregion


    #region 事件
    
    /// <summary>
    /// 状态变化事件
    /// </summary>
    public event Action<PathingState>? StateChanged;
    
    /// <summary>
    /// 进度更新事件
    /// </summary>
    public event Action<PathingProgress>? ProgressUpdated;
    
    /// <summary>
    /// 到达路径点事件
    /// </summary>
    public event Action<PathPoint>? PointReached;
    
    /// <summary>
    /// 动作执行事件
    /// </summary>
    public event Action<PathPoint, PathPointAction>? ActionExecuting;
    
    /// <summary>
    /// 日志消息事件
    /// </summary>
    public event Action<string, int>? LogMessage;
    
    #endregion
    
    #region 属性
    
    /// <summary>
    /// 当前状态
    /// </summary>
    public PathingState State
    {
        get { lock (_stateLock) return _state; }
        private set
        {
            lock (_stateLock)
            {
                if (_state == value) return;
                _state = value;
            }
            StateChanged?.Invoke(value);
        }
    }
    
    /// <summary>
    /// 当前路径
    /// </summary>
    public PathData? CurrentPath => _currentPath;
    
    /// <summary>
    /// 当前路径点索引
    /// </summary>
    public int CurrentPointIndex => _currentPointIndex;
    
    /// <summary>
    /// 是否正在追踪
    /// </summary>
    public bool IsTracking => State == PathingState.Tracking || State == PathingState.ExecutingAction;
    
    /// <summary>
    /// 是否已暂停
    /// </summary>
    public bool IsPaused => State == PathingState.Paused;
    
    #endregion
    
    #region 构造函数
    
    /// <summary>
    /// 创建地图追踪服务
    /// </summary>
    public PathingService(
        ICaptureService captureService,
        IInputService inputService,
        ILogService logService,
        ConfigManager configManager)
    {
        _captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
        _inputService = inputService ?? throw new ArgumentNullException(nameof(inputService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
    }
    
    #endregion


    #region 路径加载
    
    /// <summary>
    /// 从文件加载路径
    /// 需求: 20.2 - 支持加载预设的路径文件（JSON 格式）
    /// </summary>
    public PathData? LoadPath(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Log($"路径文件不存在: {filePath}", 2);
                return null;
            }
            
            var json = File.ReadAllText(filePath);
            var pathData = JsonSerializer.Deserialize<PathData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (pathData == null || pathData.Points.Count == 0)
            {
                Log("路径文件为空或格式错误", 2);
                return null;
            }
            
            // 验证路径点
            ValidatePathData(pathData);
            
            Log($"已加载路径: {pathData.Name}, 共 {pathData.PointCount} 个点", 1);
            return pathData;
        }
        catch (JsonException ex)
        {
            Log($"路径文件解析失败: {ex.Message}", 3);
            return null;
        }
        catch (Exception ex)
        {
            Log($"加载路径文件异常: {ex.Message}", 3);
            return null;
        }
    }
    
    /// <summary>
    /// 从 JSON 字符串加载路径
    /// </summary>
    public PathData? LoadPathFromJson(string json)
    {
        try
        {
            var pathData = JsonSerializer.Deserialize<PathData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (pathData == null || pathData.Points.Count == 0)
            {
                Log("路径数据为空或格式错误", 2);
                return null;
            }
            
            ValidatePathData(pathData);
            return pathData;
        }
        catch (Exception ex)
        {
            Log($"解析路径数据异常: {ex.Message}", 3);
            return null;
        }
    }
    
    /// <summary>
    /// 验证路径数据
    /// </summary>
    private void ValidatePathData(PathData pathData)
    {
        // 确保所有点都有唯一 ID
        var ids = new HashSet<int>();
        for (int i = 0; i < pathData.Points.Count; i++)
        {
            var point = pathData.Points[i];
            if (point.Id == 0)
            {
                point.Id = i + 1;
            }
            
            if (!ids.Add(point.Id))
            {
                Log($"警告: 路径点 ID {point.Id} 重复", 2);
            }
            
            // 应用默认值
            if (point.Tolerance <= 0)
                point.Tolerance = pathData.DefaultTolerance;
            if (point.TimeoutMs <= 0)
                point.TimeoutMs = pathData.DefaultTimeoutMs;
        }
    }
    
    /// <summary>
    /// 保存路径到文件
    /// </summary>
    public bool SavePath(PathData pathData, string filePath)
    {
        try
        {
            var json = JsonSerializer.Serialize(pathData, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            File.WriteAllText(filePath, json);
            Log($"路径已保存: {filePath}", 1);
            return true;
        }
        catch (Exception ex)
        {
            Log($"保存路径失败: {ex.Message}", 3);
            return false;
        }
    }
    
    #endregion


    #region 追踪控制
    
    /// <summary>
    /// 开始追踪
    /// 需求: 20.3 - 自动控制角色移动到目标点
    /// </summary>
    public async Task<bool> StartTrackingAsync(PathData pathData, CancellationToken externalCt = default)
    {
        if (IsTracking)
        {
            Log("追踪已在进行中", 2);
            return false;
        }
        
        if (pathData == null || pathData.Points.Count == 0)
        {
            Log("路径数据无效", 2);
            return false;
        }
        
        _currentPath = pathData;
        _currentPointIndex = 0;
        _currentLoop = 0;
        
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        _pauseEvent.Set(); // 确保未暂停
        
        State = PathingState.Tracking;
        Log($"开始追踪路径: {pathData.Name}", 1);
        
        _trackingTask = TrackingLoopAsync(_cts.Token);
        
        try
        {
            await _trackingTask;
            return State == PathingState.Completed;
        }
        catch (OperationCanceledException)
        {
            State = PathingState.Cancelled;
            return false;
        }
        catch (Exception ex)
        {
            Log($"追踪异常: {ex.Message}", 3);
            State = PathingState.Error;
            return false;
        }
        finally
        {
            StopMovement();
        }
    }
    
    /// <summary>
    /// 停止追踪
    /// </summary>
    public void StopTracking()
    {
        if (!IsTracking && State != PathingState.Paused)
            return;
        
        Log("停止追踪", 1);
        _cts?.Cancel();
        _pauseEvent.Set(); // 解除暂停以便退出
        StopMovement();
        State = PathingState.Cancelled;
    }
    
    /// <summary>
    /// 暂停追踪
    /// 需求: 20.6 - 支持追踪暂停
    /// </summary>
    public void Pause()
    {
        if (State != PathingState.Tracking && State != PathingState.ExecutingAction)
            return;
        
        Log("暂停追踪", 1);
        _pauseEvent.Reset();
        StopMovement();
        State = PathingState.Paused;
    }
    
    /// <summary>
    /// 恢复追踪
    /// 需求: 20.6 - 支持追踪恢复
    /// </summary>
    public void Resume()
    {
        if (State != PathingState.Paused)
            return;
        
        Log("恢复追踪", 1);
        State = PathingState.Tracking;
        _pauseEvent.Set();
    }
    
    #endregion


    #region 追踪循环
    
    /// <summary>
    /// 追踪主循环
    /// </summary>
    private async Task TrackingLoopAsync(CancellationToken ct)
    {
        var targetLoops = _currentPath!.Loop 
            ? (_currentPath.LoopCount > 0 ? _currentPath.LoopCount : int.MaxValue) 
            : 1;
        
        while (!ct.IsCancellationRequested && _currentLoop < targetLoops)
        {
            _currentPointIndex = 0;
            
            while (!ct.IsCancellationRequested && _currentPointIndex < _currentPath.Points.Count)
            {
                // 检查暂停
                // 需求: 20.6 - 支持追踪暂停和恢复
                _pauseEvent.Wait(ct);
                
                var targetPoint = _currentPath.Points[_currentPointIndex];
                
                // 移动到目标点
                // 需求: 20.3 - 自动控制角色移动到目标点
                var reached = await MoveToPointAsync(targetPoint, ct);
                
                if (reached)
                {
                    PointReached?.Invoke(targetPoint);
                    Log($"到达路径点: {targetPoint}", 0);
                    
                    // 执行动作
                    // 需求: 20.4 - 支持在路径点执行自定义动作
                    if (targetPoint.Action != PathPointAction.None)
                    {
                        State = PathingState.ExecutingAction;
                        await ExecuteActionAsync(targetPoint, ct);
                        State = PathingState.Tracking;
                    }
                    
                    _currentPointIndex++;
                }
                else if (!targetPoint.IsKeyPoint)
                {
                    // 非关键点可以跳过
                    Log($"跳过非关键点: {targetPoint}", 2);
                    _currentPointIndex++;
                }
                else
                {
                    // 关键点无法到达，报错
                    Log($"无法到达关键点: {targetPoint}", 3);
                    State = PathingState.Error;
                    return;
                }
                
                UpdateProgress();
                await Task.Delay(DefaultLoopIntervalMs, ct);
            }
            
            _currentLoop++;
            Log($"完成第 {_currentLoop} 轮追踪", 1);
        }
        
        State = PathingState.Completed;
        Log("路径追踪完成", 1);
    }
    
    /// <summary>
    /// 更新进度
    /// </summary>
    private void UpdateProgress()
    {
        var progress = new PathingProgress
        {
            CurrentPointIndex = _currentPointIndex,
            TotalPoints = _currentPath?.PointCount ?? 0,
            CurrentLoop = _currentLoop,
            TotalLoops = _currentPath?.Loop == true 
                ? (_currentPath.LoopCount > 0 ? _currentPath.LoopCount : -1) 
                : 1,
            State = State
        };
        
        ProgressUpdated?.Invoke(progress);
    }
    
    #endregion


    #region 位置识别
    
    /// <summary>
    /// 识别当前位置
    /// 需求: 20.1 - 通过小地图识别当前位置和方向
    /// </summary>
    public PositionInfo GetCurrentPosition()
    {
        try
        {
            // 获取小地图区域截图
            var minimapRegion = GetMinimapRegion();
            var screenshot = _captureService.GetScreenRegion(
                minimapRegion[0], minimapRegion[1],
                minimapRegion[2], minimapRegion[3]);
            
            if (screenshot == null)
            {
                return new PositionInfo { IsValid = false };
            }
            
            try
            {
                // 分析小地图获取位置和朝向
                var position = AnalyzeMinimapPosition(screenshot);
                return position;
            }
            finally
            {
                _captureService.ReturnMat(screenshot);
            }
        }
        catch (Exception ex)
        {
            Log($"位置识别异常: {ex.Message}", 2);
            return new PositionInfo { IsValid = false };
        }
    }
    
    /// <summary>
    /// 获取小地图区域配置
    /// </summary>
    private int[] GetMinimapRegion()
    {
        // 默认小地图区域（左上角）
        // 实际应该从配置中读取
        return [20, 20, 200, 200];
    }
    
    /// <summary>
    /// 分析小地图获取位置信息
    /// 需求: 20.1 - 识别角色朝向
    /// </summary>
    private PositionInfo AnalyzeMinimapPosition(Mat minimap)
    {
        var position = new PositionInfo
        {
            Timestamp = DateTime.Now
        };
        
        try
        {
            // 1. 找到角色指示器（通常是小地图中心的箭头或圆点）
            var center = FindPlayerIndicator(minimap);
            if (center.X < 0)
            {
                position.IsValid = false;
                return position;
            }
            
            // 2. 计算相对位置（以小地图中心为原点）
            var mapCenter = new Point(minimap.Width / 2, minimap.Height / 2);
            position.X = center.X - mapCenter.X;
            position.Y = center.Y - mapCenter.Y;
            
            // 3. 识别角色朝向
            position.Heading = DetectPlayerHeading(minimap, center);
            
            position.IsValid = true;
            position.Confidence = 0.8; // 简化处理，实际应该根据识别质量计算
            
            return position;
        }
        catch (Exception ex)
        {
            Log($"小地图分析异常: {ex.Message}", 2);
            position.IsValid = false;
            return position;
        }
    }
    
    /// <summary>
    /// 找到玩家指示器位置
    /// </summary>
    private Point FindPlayerIndicator(Mat minimap)
    {
        try
        {
            // 简化实现：假设玩家指示器在小地图中心附近
            // 实际应该使用模板匹配或颜色检测找到玩家箭头
            
            using var hsv = new Mat();
            Cv2.CvtColor(minimap, hsv, ColorConversionCodes.BGR2HSV);
            
            // 查找白色/亮色区域（玩家指示器通常是亮色）
            using var mask = new Mat();
            Cv2.InRange(hsv, new Scalar(0, 0, 200), new Scalar(180, 50, 255), mask);
            
            // 找到轮廓
            Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            
            if (contours.Length == 0)
            {
                // 如果没找到，返回中心点
                return new Point(minimap.Width / 2, minimap.Height / 2);
            }
            
            // 找到最接近中心的轮廓
            var center = new Point(minimap.Width / 2, minimap.Height / 2);
            Point bestPoint = center;
            double minDist = double.MaxValue;
            
            foreach (var contour in contours)
            {
                var moments = Cv2.Moments(contour);
                if (moments.M00 > 0)
                {
                    var cx = (int)(moments.M10 / moments.M00);
                    var cy = (int)(moments.M01 / moments.M00);
                    var dist = Math.Sqrt(Math.Pow(cx - center.X, 2) + Math.Pow(cy - center.Y, 2));
                    
                    if (dist < minDist)
                    {
                        minDist = dist;
                        bestPoint = new Point(cx, cy);
                    }
                }
            }
            
            return bestPoint;
        }
        catch
        {
            return new Point(minimap.Width / 2, minimap.Height / 2);
        }
    }


    /// <summary>
    /// 检测玩家朝向
    /// 需求: 20.1 - 识别角色朝向
    /// </summary>
    private double DetectPlayerHeading(Mat minimap, Point playerPos)
    {
        try
        {
            // 简化实现：通过分析玩家指示器周围的像素来确定朝向
            // 实际应该使用更精确的方法，如检测箭头方向
            
            // 在玩家位置周围采样
            var radius = 15;
            var samples = new List<(double angle, double brightness)>();
            
            for (int angle = 0; angle < 360; angle += 15)
            {
                var rad = angle * Math.PI / 180;
                var x = (int)(playerPos.X + radius * Math.Cos(rad));
                var y = (int)(playerPos.Y + radius * Math.Sin(rad));
                
                if (x >= 0 && x < minimap.Width && y >= 0 && y < minimap.Height)
                {
                    var pixel = minimap.At<Vec3b>(y, x);
                    var brightness = (pixel.Item0 + pixel.Item1 + pixel.Item2) / 3.0;
                    samples.Add((angle, brightness));
                }
            }
            
            if (samples.Count == 0)
                return 0;
            
            // 找到最亮的方向（假设箭头指向的方向更亮）
            var maxSample = samples.OrderByDescending(s => s.brightness).First();
            return maxSample.angle;
        }
        catch
        {
            return 0;
        }
    }
    
    #endregion
    
    #region 移动控制
    
    /// <summary>
    /// 移动到目标点
    /// 需求: 20.3 - 自动控制角色移动到目标点
    /// 需求: 20.5 - 当偏离路径时，自动修正方向
    /// </summary>
    private async Task<bool> MoveToPointAsync(PathPoint target, CancellationToken ct)
    {
        var timeout = target.TimeoutMs > 0 ? target.TimeoutMs : DefaultPointTimeoutMs;
        var startTime = DateTime.Now;
        var tolerance = target.Tolerance > 0 ? target.Tolerance : DefaultTolerance;
        
        while (!ct.IsCancellationRequested)
        {
            // 检查超时
            if ((DateTime.Now - startTime).TotalMilliseconds > timeout)
            {
                Log($"移动到 {target} 超时", 2);
                StopMovement();
                return false;
            }
            
            // 获取当前位置
            var currentPos = GetCurrentPosition();
            if (!currentPos.IsValid)
            {
                Log("无法获取当前位置", 2);
                await Task.Delay(MovementCheckIntervalMs, ct);
                continue;
            }
            
            // 计算到目标的距离
            var distance = target.DistanceTo(currentPos.X, currentPos.Y);
            
            // 检查是否到达
            if (distance <= tolerance)
            {
                StopMovement();
                return true;
            }
            
            // 计算目标方向
            var targetAngle = target.AngleFromDegrees(currentPos.X, currentPos.Y);
            
            // 计算需要转向的角度
            // 需求: 20.5 - 当偏离路径时，自动修正方向
            var angleDiff = NormalizeAngle(targetAngle - currentPos.Heading);
            
            // 控制移动
            ControlMovement(angleDiff, target.MoveType, distance);
            
            await Task.Delay(MovementCheckIntervalMs, ct);
        }
        
        StopMovement();
        return false;
    }
    
    /// <summary>
    /// 控制角色移动
    /// </summary>
    private void ControlMovement(double angleDiff, PathPointMoveType moveType, double distance)
    {
        var keyboard = _inputService.Keyboard;
        
        // 先停止所有移动键
        ReleaseMovementKeys();
        
        // 根据角度差决定转向
        if (Math.Abs(angleDiff) > HeadingCorrectionThreshold)
        {
            // 需要转向
            if (angleDiff > 0 && angleDiff < 180)
            {
                // 向右转
                keyboard.PressKey(VK_D);
            }
            else
            {
                // 向左转
                keyboard.PressKey(VK_A);
            }
        }
        
        // 前进
        keyboard.PressKey(VK_W);
        
        // 根据移动类型决定是否冲刺
        if (moveType == PathPointMoveType.Sprint || 
            (moveType == PathPointMoveType.Run && distance > 50))
        {
            keyboard.PressKey(VK_LSHIFT);
        }
    }
    
    /// <summary>
    /// 停止移动
    /// </summary>
    private void StopMovement()
    {
        ReleaseMovementKeys();
    }
    
    /// <summary>
    /// 释放所有移动键
    /// </summary>
    private void ReleaseMovementKeys()
    {
        var keyboard = _inputService.Keyboard;
        keyboard.ReleaseKey(VK_W);
        keyboard.ReleaseKey(VK_A);
        keyboard.ReleaseKey(VK_S);
        keyboard.ReleaseKey(VK_D);
        keyboard.ReleaseKey(VK_LSHIFT);
        keyboard.ReleaseKey(VK_SPACE);
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
    
    #endregion


    #region 动作执行
    
    /// <summary>
    /// 执行路径点动作
    /// 需求: 20.4 - 支持在路径点执行自定义动作
    /// </summary>
    private async Task ExecuteActionAsync(PathPoint point, CancellationToken ct)
    {
        ActionExecuting?.Invoke(point, point.Action);
        Log($"执行动作: {point.Action} at {point}", 0);
        
        var keyboard = _inputService.Keyboard;
        
        switch (point.Action)
        {
            case PathPointAction.Collect:
                // 采集动作：按 F 键
                keyboard.PressAndRelease(VK_F);
                await Task.Delay(500, ct);
                break;
                
            case PathPointAction.Combat:
                // 战斗动作：等待战斗结束
                // 这里简化处理，实际应该集成技能循环触发器
                var combatDuration = ParseActionParam(point.ActionParam, 10000);
                await Task.Delay(combatDuration, ct);
                break;
                
            case PathPointAction.Interact:
                // 交互动作：按 F 键并等待
                keyboard.PressAndRelease(VK_F);
                var interactDelay = ParseActionParam(point.ActionParam, 1000);
                await Task.Delay(interactDelay, ct);
                break;
                
            case PathPointAction.Wait:
                // 等待动作
                var waitTime = ParseActionParam(point.ActionParam, 1000);
                await Task.Delay(waitTime, ct);
                break;
                
            case PathPointAction.Jump:
                // 跳跃动作
                keyboard.PressAndRelease(VK_SPACE);
                await Task.Delay(500, ct);
                break;
                
            case PathPointAction.Sprint:
                // 冲刺动作
                keyboard.PressKey(VK_LSHIFT);
                await Task.Delay(1000, ct);
                keyboard.ReleaseKey(VK_LSHIFT);
                break;
                
            case PathPointAction.Teleport:
                // 传送动作（需要打开地图，这里简化处理）
                Log("传送动作需要手动实现", 2);
                break;
                
            case PathPointAction.CustomKey:
                // 自定义按键
                if (int.TryParse(point.ActionParam, out var keyCode))
                {
                    keyboard.PressAndRelease(keyCode);
                    await Task.Delay(200, ct);
                }
                break;
                
            case PathPointAction.None:
            default:
                break;
        }
    }
    
    /// <summary>
    /// 解析动作参数
    /// </summary>
    private static int ParseActionParam(string param, int defaultValue)
    {
        if (string.IsNullOrEmpty(param))
            return defaultValue;
        
        if (int.TryParse(param, out var value))
            return value;
        
        return defaultValue;
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
        
        _logService.Log($"[地图追踪] {message}", logLevel, "PathingService");
        LogMessage?.Invoke(message, level);
    }
    
    #endregion
    
    #region IDisposable 实现
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        StopTracking();
        _cts?.Dispose();
        _pauseEvent.Dispose();
        
        GC.SuppressFinalize(this);
    }
    
    #endregion
}
