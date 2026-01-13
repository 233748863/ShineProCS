using OpenCvSharp;

namespace ShineProCS.Core.Recognition.OCR;

/// <summary>
/// OCR 识别结果区域（BetterGI 风格）
/// </summary>
public record struct OcrResultRegion(RotatedRect Rect, string Text, float Score);

/// <summary>
/// OCR 识别器结果
/// </summary>
public readonly record struct OcrRecognizerResult
{
    /// <summary>
    /// 识别的文本
    /// </summary>
    public string Text { get; init; }

    /// <summary>
    /// 置信度分数
    /// </summary>
    public float Score { get; init; }

    public OcrRecognizerResult(string text, float score)
    {
        Text = text;
        Score = score;
    }
}

/// <summary>
/// OCR 识别结果（BetterGI 风格）
/// </summary>
public record OcrResult
{
    /// <summary>
    /// 识别到的所有区域
    /// </summary>
    public OcrResultRegion[] Regions { get; }

    /// <summary>
    /// 识别到的完整文本（所有区域文本按位置排序后拼接）
    /// </summary>
    public string Text => string.Join("\n", Regions
        .OrderBy(x => x.Rect.Center.Y)
        .ThenBy(x => x.Rect.Center.X)
        .Select(x => x.Text));

    public OcrResult(OcrResultRegion[] regions)
    {
        Regions = regions;
    }

    /// <summary>
    /// 是否识别成功
    /// </summary>
    public bool IsSuccess => Regions.Length > 0 && !string.IsNullOrEmpty(Text);
}
