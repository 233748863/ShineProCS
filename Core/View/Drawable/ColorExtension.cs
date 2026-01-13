namespace ShineProCS.Core.View.Drawable;

/// <summary>
/// 颜色转换扩展方法
/// </summary>
public static class ColorExtension
{
    /// <summary>
    /// 将 System.Drawing.Color 转换为 System.Windows.Media.Color
    /// </summary>
    public static System.Windows.Media.Color ToWindowsColor(this System.Drawing.Color color)
    {
        return System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
    }

    /// <summary>
    /// 将 System.Windows.Media.Color 转换为 System.Drawing.Color
    /// </summary>
    public static System.Drawing.Color ToDrawingColor(this System.Windows.Media.Color color)
    {
        return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
    }
}
