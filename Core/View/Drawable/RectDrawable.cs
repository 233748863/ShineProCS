using System.Drawing;
using System.Windows;

namespace ShineProCS.Core.View.Drawable;

/// <summary>
/// 可绘制的矩形
/// 移植自 BetterGI
/// </summary>
[Serializable]
public class RectDrawable
{
    public string? Name { get; set; }
    public Rect Rect { get; }
    public Pen Pen { get; } = new(Color.Red, 2);

    public RectDrawable(Rect rect, Pen? pen = null, string? name = null)
    {
        Rect = rect;
        Name = name;

        if (pen != null)
        {
            Pen = pen;
        }
    }

    public RectDrawable(Rect rect, string? name)
    {
        Rect = rect;
        Name = name;
    }

    public override bool Equals(object? obj)
    {
        if (obj == null || GetType() != obj.GetType())
        {
            return false;
        }

        var other = (RectDrawable)obj;
        return Rect.Equals(other.Rect);
    }

    public override int GetHashCode()
    {
        return Rect.GetHashCode();
    }

    public bool IsEmpty => Rect.IsEmpty;
}

/// <summary>
/// RectDrawable 扩展方法
/// </summary>
public static class RectDrawableExtension
{
    /// <summary>
    /// 将 System.Windows.Rect 转换为 RectDrawable
    /// </summary>
    public static RectDrawable ToRectDrawable(this Rect rect, Pen? pen = null, string? name = null)
    {
        return new RectDrawable(rect, pen, name);
    }

    /// <summary>
    /// 将 System.Windows.Rect 转换为 RectDrawable（带缩放）
    /// </summary>
    public static RectDrawable ToRectDrawable(this Rect rect, double scale, Pen? pen = null, string? name = null)
    {
        Rect newRect = new(rect.X / scale, rect.Y / scale, rect.Width / scale, rect.Height / scale);
        return new RectDrawable(newRect, pen, name);
    }

    /// <summary>
    /// 将 OpenCvSharp.Rect 转换为 RectDrawable
    /// </summary>
    public static RectDrawable ToRectDrawable(this OpenCvSharp.Rect rect, Pen? pen = null, string? name = null)
    {
        return new RectDrawable(new Rect(rect.X, rect.Y, rect.Width, rect.Height), pen, name);
    }

    /// <summary>
    /// 将 OpenCvSharp.Rect 转换为 RectDrawable（带缩放）
    /// </summary>
    public static RectDrawable ToRectDrawable(this OpenCvSharp.Rect rect, double scale, Pen? pen = null, string? name = null)
    {
        OpenCvSharp.Rect newRect = new((int)(rect.X / scale), (int)(rect.Y / scale), (int)(rect.Width / scale), (int)(rect.Height / scale));
        return new RectDrawable(new Rect(newRect.X, newRect.Y, newRect.Width, newRect.Height), pen, name);
    }

    /// <summary>
    /// 将 OpenCvSharp.Rect 转换为 RectDrawable（带偏移和缩放）
    /// </summary>
    public static RectDrawable ToRectDrawable(this OpenCvSharp.Rect rect, int offsetX, int offsetY, double scale, Pen? pen = null, string? name = null)
    {
        OpenCvSharp.Rect newRect = new(offsetX + (int)(rect.X / scale), offsetY + (int)(rect.Y / scale), (int)(rect.Width / scale), (int)(rect.Height / scale));
        return new RectDrawable(new Rect(newRect.X, newRect.Y, newRect.Width, newRect.Height), pen, name);
    }
}
