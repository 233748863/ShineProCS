using OpenCvSharp;

namespace ShineProCS.Core.Recognition.YOLO;

/// <summary>
/// YOLO 目标检测结果
/// </summary>
public class DetectionResult
{
    /// <summary>
    /// 检测到的目标类别名称
    /// </summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// 目标类别索引
    /// </summary>
    public int ClassId { get; set; }

    /// <summary>
    /// 目标边界框
    /// </summary>
    public Rect BoundingBox { get; set; }

    /// <summary>
    /// 检测置信度 (0-1)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// 目标中心点 X 坐标
    /// </summary>
    public int CenterX => BoundingBox.X + BoundingBox.Width / 2;

    /// <summary>
    /// 目标中心点 Y 坐标
    /// </summary>
    public int CenterY => BoundingBox.Y + BoundingBox.Height / 2;

    /// <summary>
    /// 目标中心点
    /// </summary>
    public OpenCvSharp.Point Center => new(CenterX, CenterY);

    public override string ToString()
    {
        return $"{ClassName} ({Confidence:P1}) at [{BoundingBox.X}, {BoundingBox.Y}, {BoundingBox.Width}x{BoundingBox.Height}]";
    }
}

/// <summary>
/// YOLO 检测结果集合
/// </summary>
public class DetectionResults
{
    /// <summary>
    /// 检测到的所有目标
    /// </summary>
    public List<DetectionResult> Detections { get; set; } = new();

    /// <summary>
    /// 推理耗时（毫秒）
    /// </summary>
    public double InferenceTimeMs { get; set; }

    /// <summary>
    /// 是否有检测结果
    /// </summary>
    public bool HasDetections => Detections.Count > 0;

    /// <summary>
    /// 检测到的目标数量
    /// </summary>
    public int Count => Detections.Count;

    /// <summary>
    /// 获取指定类别的检测结果
    /// </summary>
    /// <param name="className">类别名称</param>
    /// <returns>该类别的所有检测结果</returns>
    public IEnumerable<DetectionResult> GetByClass(string className)
    {
        return Detections.Where(d => d.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 获取置信度最高的检测结果
    /// </summary>
    /// <returns>置信度最高的结果，如果没有检测结果则返回 null</returns>
    public DetectionResult? GetTopConfidence()
    {
        return Detections.MaxBy(d => d.Confidence);
    }
}
