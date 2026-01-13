using OpenCvSharp;

namespace ShineProCS.Core.Services;

/// <summary>
/// 多尺度模板匹配器
/// 支持不同 UI 缩放比例下的模板匹配，适应游戏窗口大小变化
/// 需求 2.3: 支持多尺度匹配以适应不同 UI 缩放
/// </summary>
public class MultiScaleTemplateMatcher
{
    /// <summary>
    /// 默认缩放比例数组（0.8 到 1.2，步长 0.1）
    /// </summary>
    private static readonly double[] DefaultScales = { 0.8, 0.9, 1.0, 1.1, 1.2 };
    
    /// <summary>
    /// 当前使用的缩放比例数组
    /// </summary>
    private readonly double[] _scales;
    
    /// <summary>
    /// 最小模板尺寸（像素），小于此尺寸的缩放将被跳过
    /// </summary>
    private readonly int _minTemplateSize;
    
    /// <summary>
    /// 创建多尺度模板匹配器（使用默认缩放范围 0.8-1.2）
    /// </summary>
    public MultiScaleTemplateMatcher() : this(DefaultScales, 8)
    {
    }
    
    /// <summary>
    /// 创建多尺度模板匹配器
    /// </summary>
    /// <param name="scales">缩放比例数组，例如 [0.8, 0.9, 1.0, 1.1, 1.2]</param>
    /// <param name="minTemplateSize">最小模板尺寸（像素），默认 8</param>
    public MultiScaleTemplateMatcher(double[] scales, int minTemplateSize = 8)
    {
        if (scales == null || scales.Length == 0)
            throw new ArgumentException("缩放比例数组不能为空", nameof(scales));
        
        // 验证所有缩放比例都是正数
        foreach (var scale in scales)
        {
            if (scale <= 0)
                throw new ArgumentException("缩放比例必须大于 0", nameof(scales));
        }
        
        _scales = scales;
        _minTemplateSize = Math.Max(1, minTemplateSize);
    }
    
    /// <summary>
    /// 创建多尺度模板匹配器（指定缩放范围）
    /// </summary>
    /// <param name="minScale">最小缩放比例（例如 0.8）</param>
    /// <param name="maxScale">最大缩放比例（例如 1.2）</param>
    /// <param name="step">缩放步长（例如 0.1）</param>
    /// <param name="minTemplateSize">最小模板尺寸（像素），默认 8</param>
    public MultiScaleTemplateMatcher(double minScale, double maxScale, double step, int minTemplateSize = 8)
    {
        if (minScale <= 0 || maxScale <= 0 || step <= 0)
            throw new ArgumentException("缩放参数必须大于 0");
        
        if (minScale > maxScale)
            throw new ArgumentException("最小缩放比例不能大于最大缩放比例");
        
        // 生成缩放比例数组
        var scales = new List<double>();
        for (double s = minScale; s <= maxScale + 0.001; s += step)
        {
            scales.Add(Math.Round(s, 2));
        }
        
        _scales = scales.ToArray();
        _minTemplateSize = Math.Max(1, minTemplateSize);
    }
    
    /// <summary>
    /// 获取当前使用的缩放比例数组
    /// </summary>
    public IReadOnlyList<double> Scales => _scales;
    
    /// <summary>
    /// 执行多尺度模板匹配
    /// 在多个缩放比例下进行模板匹配，返回最佳匹配结果
    /// </summary>
    /// <param name="source">源图像</param>
    /// <param name="template">模板图像</param>
    /// <param name="threshold">匹配阈值（0-1），低于此阈值视为不匹配</param>
    /// <returns>最佳匹配结果（相似度和对应的缩放比例）</returns>
    public MultiScaleMatchResult Match(Mat source, Mat template, double threshold = 0.8)
    {
        if (source == null || source.Empty())
            return MultiScaleMatchResult.NoMatch;
        
        if (template == null || template.Empty())
            return MultiScaleMatchResult.NoMatch;
        
        double bestSimilarity = 0;
        double bestScale = 1.0;
        OpenCvSharp.Point bestLocation = default;
        
        foreach (var scale in _scales)
        {
            // 计算缩放后的模板尺寸
            int scaledWidth = (int)(template.Width * scale);
            int scaledHeight = (int)(template.Height * scale);
            
            // 跳过太小的缩放结果
            if (scaledWidth < _minTemplateSize || scaledHeight < _minTemplateSize)
                continue;
            
            // 跳过比源图像还大的模板
            if (scaledWidth > source.Width || scaledHeight > source.Height)
                continue;
            
            try
            {
                // 缩放模板
                using var scaledTemplate = new Mat();
                if (Math.Abs(scale - 1.0) < 0.001)
                {
                    // 缩放比例为 1.0，直接使用原模板
                    template.CopyTo(scaledTemplate);
                }
                else
                {
                    // 使用 INTER_AREA 进行缩小，INTER_LINEAR 进行放大
                    var interpolation = scale < 1.0 ? InterpolationFlags.Area : InterpolationFlags.Linear;
                    Cv2.Resize(template, scaledTemplate, new OpenCvSharp.Size(scaledWidth, scaledHeight), 0, 0, interpolation);
                }
                
                // 执行模板匹配
                using var result = new Mat();
                Cv2.MatchTemplate(source, scaledTemplate, result, TemplateMatchModes.CCoeffNormed);
                
                // 获取最佳匹配位置和相似度
                Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);
                
                // 更新最佳结果
                if (maxVal > bestSimilarity)
                {
                    bestSimilarity = maxVal;
                    bestScale = scale;
                    bestLocation = maxLoc;
                }
            }
            catch (Exception ex)
            {
                // 记录错误但继续尝试其他缩放比例
                System.Diagnostics.Debug.WriteLine($"[MultiScaleTemplateMatcher] 缩放 {scale:F2} 匹配失败: {ex.Message}");
            }
        }
        
        // 检查是否达到阈值
        bool isMatch = bestSimilarity >= threshold;
        
        return new MultiScaleMatchResult
        {
            IsMatch = isMatch,
            Similarity = bestSimilarity,
            Scale = bestScale,
            Location = bestLocation,
            MatchedWidth = (int)(template.Width * bestScale),
            MatchedHeight = (int)(template.Height * bestScale)
        };
    }
    
    /// <summary>
    /// 快速多尺度匹配（优化版：找到第一个超过阈值的结果就返回）
    /// 适用于只需要判断是否匹配的场景
    /// </summary>
    /// <param name="source">源图像</param>
    /// <param name="template">模板图像</param>
    /// <param name="threshold">匹配阈值（0-1）</param>
    /// <returns>是否匹配成功</returns>
    public bool QuickMatch(Mat source, Mat template, double threshold = 0.8)
    {
        if (source == null || source.Empty())
            return false;
        
        if (template == null || template.Empty())
            return false;
        
        // 优先尝试 1.0 缩放（最常见的情况）
        var prioritizedScales = _scales.OrderBy(s => Math.Abs(s - 1.0)).ToArray();
        
        foreach (var scale in prioritizedScales)
        {
            int scaledWidth = (int)(template.Width * scale);
            int scaledHeight = (int)(template.Height * scale);
            
            if (scaledWidth < _minTemplateSize || scaledHeight < _minTemplateSize)
                continue;
            
            if (scaledWidth > source.Width || scaledHeight > source.Height)
                continue;
            
            try
            {
                using var scaledTemplate = new Mat();
                if (Math.Abs(scale - 1.0) < 0.001)
                {
                    template.CopyTo(scaledTemplate);
                }
                else
                {
                    var interpolation = scale < 1.0 ? InterpolationFlags.Area : InterpolationFlags.Linear;
                    Cv2.Resize(template, scaledTemplate, new OpenCvSharp.Size(scaledWidth, scaledHeight), 0, 0, interpolation);
                }
                
                using var result = new Mat();
                Cv2.MatchTemplate(source, scaledTemplate, result, TemplateMatchModes.CCoeffNormed);
                
                Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out _);
                
                // 找到第一个超过阈值的结果就返回
                if (maxVal >= threshold)
                    return true;
            }
            catch
            {
                // 忽略错误，继续尝试
            }
        }
        
        return false;
    }
}

/// <summary>
/// 多尺度模板匹配结果
/// </summary>
public class MultiScaleMatchResult
{
    /// <summary>
    /// 是否匹配成功（相似度 >= 阈值）
    /// </summary>
    public bool IsMatch { get; set; }
    
    /// <summary>
    /// 最佳匹配的相似度（0-1）
    /// </summary>
    public double Similarity { get; set; }
    
    /// <summary>
    /// 最佳匹配对应的缩放比例
    /// </summary>
    public double Scale { get; set; } = 1.0;
    
    /// <summary>
    /// 匹配位置（左上角坐标）
    /// </summary>
    public OpenCvSharp.Point Location { get; set; }
    
    /// <summary>
    /// 匹配区域的宽度（缩放后的模板宽度）
    /// </summary>
    public int MatchedWidth { get; set; }
    
    /// <summary>
    /// 匹配区域的高度（缩放后的模板高度）
    /// </summary>
    public int MatchedHeight { get; set; }
    
    /// <summary>
    /// 匹配区域
    /// </summary>
    public Rect MatchedRegion => new(Location.X, Location.Y, MatchedWidth, MatchedHeight);
    
    /// <summary>
    /// 匹配中心点
    /// </summary>
    public OpenCvSharp.Point Center => new(Location.X + MatchedWidth / 2, Location.Y + MatchedHeight / 2);
    
    /// <summary>
    /// 创建不匹配的结果
    /// </summary>
    public static MultiScaleMatchResult NoMatch => new()
    {
        IsMatch = false,
        Similarity = 0,
        Scale = 1.0
    };
    
    public override string ToString()
    {
        return IsMatch
            ? $"Match at [{Location.X}, {Location.Y}] with similarity {Similarity:P1} at scale {Scale:F2}"
            : "No match";
    }
}
