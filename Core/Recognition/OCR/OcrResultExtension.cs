using OpenCvSharp;

namespace ShineProCS.Core.Recognition.OCR;

/// <summary>
/// OCR 结果扩展方法（BetterGI 风格）
/// </summary>
public static class OcrResultExtension
{
    /// <summary>
    /// 检查结果中是否包含指定文本
    /// </summary>
    public static bool RegionHasText(this OcrResult result, ReadOnlySpan<char> text)
    {
        foreach (ref readonly var item in result.Regions.AsSpan())
            if (item.Text.AsSpan().Contains(text, StringComparison.InvariantCulture))
                return true;

        return false;
    }

    /// <summary>
    /// 查找包含指定文本的区域
    /// </summary>
    public static OcrResultRegion FindRegionByText(this OcrResult result, ReadOnlySpan<char> text)
    {
        foreach (ref readonly var item in result.Regions.AsSpan())
            if (item.Text.AsSpan().Contains(text, StringComparison.InvariantCulture))
                return item;

        return default;
    }

    /// <summary>
    /// 查找包含指定文本的矩形区域
    /// </summary>
    public static Rect FindRectByText(this OcrResult result, string text)
    {
        foreach (ref var item in result.Regions.AsSpan())
            if (item.Text.Contains(text))
                return item.Rect.BoundingRect();

        return default;
    }
}
