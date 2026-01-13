using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Services;
using OpenCvSharp;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;

namespace ShineProCS.Core.Pathing;

/// <summary>
/// 小地图配置
/// </summary>
public class MinimapConfig
{
    /// <summary>
    /// 小地图区域 [X, Y, Width, Height]
    /// </summary>
    public int[] Region { get; set; } = [20, 20, 200, 200];
    
    /// <summary>
    /// 玩家指示器颜色范围（HSV 最小值）
    /// </summary>
    public Scalar PlayerIndicatorHsvMin { get; set; } = new(0, 0, 200);
    
    /// <summary>
    /// 玩家指示器颜色范围（HSV 最大值）
    /// </summary>
    public Scalar PlayerIndicatorHsvMax { get; set; } = new(180, 50, 255);
    
    /// <summary>
    /// 箭头检测半径
    /// </summary>
    public int ArrowDetectionRadius { get; set; } = 15;
    
    /// <summary>
    /// 位置平滑因子（0-1，越大越平滑）
    /// </summary>
    public double SmoothingFactor { get; set; } = 0.3;
}


/// <summary>
/// 位置识别器
/// 需求: 20.1 - 通过小地图识别当前位置和方向
/// </summary>
public class PositionRecognizer : IDisposable
{
    #region 依赖组件
    
    private readonly ICaptureService _captureService;
    private readonly ILogService _logService;
    
    #endregion
    
    #region 配置和状态
    
    private MinimapConfig _config = new();
    private PositionInfo _lastPosition = new();
    private readonly object _lock = new();
    private bool _disposed;
    
    #endregion
    
    #region 构造函数
    
    /// <summary>
    /// 创建位置识别器
    /// </summary>
    public PositionRecognizer(ICaptureService captureService, ILogService logService)
    {
        _captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    }
    
    #endregion
    
    #region 配置
    
    /// <summary>
    /// 设置小地图配置
    /// </summary>
    public void SetConfig(MinimapConfig config)
    {
        _config = config ?? new MinimapConfig();
    }
    
    /// <summary>
    /// 设置小地图区域
    /// </summary>
    public void SetMinimapRegion(int x, int y, int width, int height)
    {
        _config.Region = [x, y, width, height];
    }
    
    #endregion
    
    #region 位置识别
    
    /// <summary>
    /// 识别当前位置
    /// 需求: 20.1 - 通过小地图识别当前位置和方向
    /// </summary>
    public PositionInfo RecognizePosition()
    {
        try
        {
            // 获取小地图截图
            var region = _config.Region;
            var screenshot = _captureService.GetScreenRegion(region[0], region[1], region[2], region[3]);
            
            if (screenshot == null)
            {
                return CreateInvalidPosition("无法获取小地图截图");
            }
            
            try
            {
                return AnalyzeMinimapInternal(screenshot);
            }
            finally
            {
                _captureService.ReturnMat(screenshot);
            }
        }
        catch (Exception ex)
        {
            Log($"位置识别异常: {ex.Message}", 2);
            return CreateInvalidPosition(ex.Message);
        }
    }
    
    /// <summary>
    /// 分析小地图
    /// </summary>
    private PositionInfo AnalyzeMinimapInternal(Mat minimap)
    {
        var position = new PositionInfo { Timestamp = DateTime.Now };
        
        // 1. 找到玩家指示器
        var playerPos = FindPlayerIndicator(minimap);
        if (playerPos.X < 0)
        {
            return CreateInvalidPosition("未找到玩家指示器");
        }
        
        // 2. 计算相对位置（以小地图中心为原点）
        var mapCenter = new Point(minimap.Width / 2, minimap.Height / 2);
        var rawX = playerPos.X - mapCenter.X;
        var rawY = playerPos.Y - mapCenter.Y;
        
        // 3. 识别角色朝向
        var heading = DetectPlayerHeading(minimap, playerPos);
        
        // 4. 应用平滑处理
        lock (_lock)
        {
            if (_lastPosition.IsValid)
            {
                var factor = _config.SmoothingFactor;
                position.X = rawX * (1 - factor) + _lastPosition.X * factor;
                position.Y = rawY * (1 - factor) + _lastPosition.Y * factor;
                position.Heading = SmoothAngle(heading, _lastPosition.Heading, factor);
            }
            else
            {
                position.X = rawX;
                position.Y = rawY;
                position.Heading = heading;
            }
            
            position.IsValid = true;
            position.Confidence = CalculateConfidence(minimap, playerPos);
            _lastPosition = position;
        }
        
        return position;
    }


    /// <summary>
    /// 找到玩家指示器位置
    /// </summary>
    private Point FindPlayerIndicator(Mat minimap)
    {
        try
        {
            // 转换到 HSV 色彩空间
            using var hsv = new Mat();
            Cv2.CvtColor(minimap, hsv, ColorConversionCodes.BGR2HSV);
            
            // 根据配置的颜色范围创建掩码
            using var mask = new Mat();
            Cv2.InRange(hsv, _config.PlayerIndicatorHsvMin, _config.PlayerIndicatorHsvMax, mask);
            
            // 形态学操作去噪
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(3, 3));
            Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);
            Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);
            
            // 找到轮廓
            Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            
            if (contours.Length == 0)
            {
                // 如果没找到，返回中心点作为默认值
                return new Point(minimap.Width / 2, minimap.Height / 2);
            }
            
            // 找到最接近中心且面积合适的轮廓
            var center = new Point(minimap.Width / 2, minimap.Height / 2);
            Point bestPoint = center;
            double bestScore = double.MaxValue;
            
            foreach (var contour in contours)
            {
                var area = Cv2.ContourArea(contour);
                
                // 过滤太小或太大的轮廓
                if (area < 10 || area > 1000)
                    continue;
                
                var moments = Cv2.Moments(contour);
                if (moments.M00 > 0)
                {
                    var cx = (int)(moments.M10 / moments.M00);
                    var cy = (int)(moments.M01 / moments.M00);
                    
                    // 计算到中心的距离
                    var dist = Math.Sqrt(Math.Pow(cx - center.X, 2) + Math.Pow(cy - center.Y, 2));
                    
                    // 评分：距离越近越好，面积适中更好
                    var score = dist + Math.Abs(area - 100) * 0.1;
                    
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestPoint = new Point(cx, cy);
                    }
                }
            }
            
            return bestPoint;
        }
        catch (Exception ex)
        {
            Log($"查找玩家指示器异常: {ex.Message}", 2);
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
            var radius = _config.ArrowDetectionRadius;
            var samples = new List<(double angle, double brightness)>();
            
            // 在玩家位置周围采样
            for (int angle = 0; angle < 360; angle += 10)
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
            
            // 使用加权平均找到最亮的方向
            var maxBrightness = samples.Max(s => s.brightness);
            var threshold = maxBrightness * 0.8;
            
            var brightSamples = samples.Where(s => s.brightness >= threshold).ToList();
            if (brightSamples.Count == 0)
                return samples.OrderByDescending(s => s.brightness).First().angle;
            
            // 计算加权平均角度
            double sumX = 0, sumY = 0;
            foreach (var sample in brightSamples)
            {
                var rad = sample.angle * Math.PI / 180;
                sumX += Math.Cos(rad) * sample.brightness;
                sumY += Math.Sin(rad) * sample.brightness;
            }
            
            var avgAngle = Math.Atan2(sumY, sumX) * 180 / Math.PI;
            return (avgAngle + 360) % 360;
        }
        catch
        {
            return 0;
        }
    }


    /// <summary>
    /// 计算识别置信度
    /// </summary>
    private double CalculateConfidence(Mat minimap, Point playerPos)
    {
        try
        {
            // 基于玩家指示器与中心的距离计算置信度
            var center = new Point(minimap.Width / 2, minimap.Height / 2);
            var dist = Math.Sqrt(Math.Pow(playerPos.X - center.X, 2) + Math.Pow(playerPos.Y - center.Y, 2));
            var maxDist = Math.Min(minimap.Width, minimap.Height) / 2.0;
            
            // 距离越近置信度越高
            var distConfidence = Math.Max(0, 1 - dist / maxDist);
            
            // 综合置信度
            return distConfidence * 0.8 + 0.2; // 最低 0.2
        }
        catch
        {
            return 0.5;
        }
    }
    
    /// <summary>
    /// 平滑角度变化
    /// </summary>
    private static double SmoothAngle(double current, double previous, double factor)
    {
        // 处理角度跨越 0/360 的情况
        var diff = current - previous;
        if (diff > 180) diff -= 360;
        if (diff < -180) diff += 360;
        
        var smoothed = previous + diff * (1 - factor);
        return (smoothed + 360) % 360;
    }
    
    /// <summary>
    /// 创建无效位置信息
    /// </summary>
    private static PositionInfo CreateInvalidPosition(string reason = "")
    {
        return new PositionInfo
        {
            IsValid = false,
            Confidence = 0,
            Timestamp = DateTime.Now
        };
    }
    
    #endregion
    
    #region 辅助方法
    
    /// <summary>
    /// 重置位置历史
    /// </summary>
    public void ResetHistory()
    {
        lock (_lock)
        {
            _lastPosition = new PositionInfo();
        }
    }
    
    /// <summary>
    /// 获取上次识别的位置
    /// </summary>
    public PositionInfo GetLastPosition()
    {
        lock (_lock)
        {
            return _lastPosition;
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
        
        _logService.Log($"[位置识别] {message}", logLevel, "PositionRecognizer");
    }
    
    #endregion
    
    #region IDisposable 实现
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
    
    #endregion
}
