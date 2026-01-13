using OpenCvSharp;

namespace ShineProCS.Core.Recognition.Template;

/// <summary>
/// 模板匹配结果
/// </summary>
public class TemplateMatchResult
{
    /// <summary>
    /// 是否匹配成功
    /// </summary>
    public bool IsMatch { get; set; }

    /// <summary>
    /// 匹配位置（左上角坐标）
    /// </summary>
    public OpenCvSharp.Point Location { get; set; }

    /// <summary>
    /// 匹配区域
    /// </summary>
    public Rect Region { get; set; }

    /// <summary>
    /// 匹配相似度 (0-1)
    /// </summary>
    public double Similarity { get; set; }

    /// <summary>
    /// 匹配中心点
    /// </summary>
    public OpenCvSharp.Point Center => new(Region.X + Region.Width / 2, Region.Y + Region.Height / 2);

    /// <summary>
    /// 创建匹配失败的结果
    /// </summary>
    public static TemplateMatchResult NoMatch => new()
    {
        IsMatch = false,
        Similarity = 0
    };

    public override string ToString()
    {
        return IsMatch
            ? $"Match at [{Location.X}, {Location.Y}] with similarity {Similarity:P1}"
            : "No match";
    }
}

/// <summary>
/// 多模板匹配结果
/// </summary>
public class MultiTemplateMatchResult
{
    /// <summary>
    /// 所有匹配结果
    /// </summary>
    public List<TemplateMatchResult> Matches { get; set; } = new();

    /// <summary>
    /// 是否有匹配结果
    /// </summary>
    public bool HasMatches => Matches.Count > 0;

    /// <summary>
    /// 匹配数量
    /// </summary>
    public int Count => Matches.Count;

    /// <summary>
    /// 获取相似度最高的匹配结果
    /// </summary>
    public TemplateMatchResult? BestMatch => Matches.MaxBy(m => m.Similarity);
}
