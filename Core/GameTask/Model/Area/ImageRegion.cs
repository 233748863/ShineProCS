using System.Drawing;
using OpenCvSharp;
using ShineProCS.Core.View.Drawable;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ShineProCS.Core.GameTask.Model.Area;

/// <summary>
/// 图像区域（与 BetterGI 原版一致）
/// 封装 Mat 图像数据，提供缓存的灰度图和 ImageSharp 图像
/// </summary>
public class ImageRegion : IDisposable
{
    private Mat? _cacheGreyMat;
    private Image<Rgb24>? _cacheImage;
    private bool _disposed;

    /// <summary>
    /// 源图像 Mat
    /// </summary>
    public Mat SrcMat { get; }

    /// <summary>
    /// X 坐标
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Y 坐标
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// 宽度
    /// </summary>
    public int Width => SrcMat.Width;

    /// <summary>
    /// 高度
    /// </summary>
    public int Height => SrcMat.Height;

    /// <summary>
    /// 父区域
    /// </summary>
    public ImageRegion? Owner { get; }

    /// <summary>
    /// 绘制内容管理器
    /// </summary>
    protected readonly DrawContent DrawContent;

    /// <summary>
    /// 缓存的灰度图
    /// </summary>
    public Mat CacheGreyMat
    {
        get
        {
            if (_cacheGreyMat != null)
                return _cacheGreyMat;
            _cacheGreyMat = new Mat();
            Cv2.CvtColor(SrcMat, _cacheGreyMat, ColorConversionCodes.BGR2GRAY);
            return _cacheGreyMat;
        }
    }

    /// <summary>
    /// 缓存的 ImageSharp 图像（RGB24 格式）
    /// 用于 YoloSharp 等需要 ImageSharp 图像的场景
    /// </summary>
    public unsafe Image<Rgb24> CacheImage
    {
        get
        {
            if (_cacheImage != null)
                return _cacheImage;

            using var mat = SrcMat.CvtColor(ColorConversionCodes.BGR2RGB);
            var bufferSize = (int)SrcMat.Step() * SrcMat.Height;
            using var image = SixLabors.ImageSharp.Image.WrapMemory<Rgb24>(mat.DataPointer, bufferSize, mat.Width, mat.Height);
            _cacheImage = image.Clone();

            return _cacheImage;
        }
    }

    /// <summary>
    /// 创建图像区域
    /// </summary>
    /// <param name="mat">源图像</param>
    /// <param name="x">X 坐标</param>
    /// <param name="y">Y 坐标</param>
    /// <param name="owner">父区域</param>
    /// <param name="drawContent">绘制内容管理器</param>
    public ImageRegion(Mat mat, int x = 0, int y = 0, ImageRegion? owner = null, DrawContent? drawContent = null)
    {
        SrcMat = mat;
        X = x;
        Y = y;
        Owner = owner;
        DrawContent = drawContent ?? VisionContext.Instance().DrawContent;
    }

    /// <summary>
    /// 从 Mat 创建图像区域
    /// </summary>
    public static ImageRegion FromMat(Mat mat)
    {
        return new ImageRegion(mat);
    }

    /// <summary>
    /// 剪裁派生新区域
    /// </summary>
    public ImageRegion DeriveCrop(int x, int y, int w, int h)
    {
        return new ImageRegion(new Mat(SrcMat, new Rect(x, y, w, h)), x, y, this, DrawContent);
    }

    /// <summary>
    /// 剪裁派生新区域（浮点坐标）
    /// </summary>
    public ImageRegion DeriveCrop(double dx, double dy, double dw, double dh)
    {
        var x = (int)Math.Round(dx);
        var y = (int)Math.Round(dy);
        var w = (int)Math.Round(dw);
        var h = (int)Math.Round(dh);
        return new ImageRegion(new Mat(SrcMat, new Rect(x, y, w, h)), x, y, this, DrawContent);
    }

    /// <summary>
    /// 剪裁派生新区域
    /// </summary>
    public ImageRegion DeriveCrop(Rect rect)
    {
        return DeriveCrop(rect.X, rect.Y, rect.Width, rect.Height);
    }

    /// <summary>
    /// 转换为 OpenCvSharp Rect
    /// </summary>
    public Rect ToRect()
    {
        return new Rect(X, Y, Width, Height);
    }

    /// <summary>
    /// 转换指定区域到可绘制矩形
    /// </summary>
    public RectDrawable ToRectDrawable(Rect rect, string name, Pen? pen = null)
    {
        return ToRectDrawable(rect.X, rect.Y, rect.Width, rect.Height, name, pen);
    }

    /// <summary>
    /// 转换指定区域到可绘制矩形
    /// </summary>
    public RectDrawable ToRectDrawable(int x, int y, int w, int h, string name, Pen? pen = null)
    {
        // 转换到根坐标系
        var (rootX, rootY) = ConvertToRootCoordinates(x, y);
        var windowsRect = new System.Windows.Rect(rootX, rootY, w, h);
        return new RectDrawable(windowsRect, pen, name);
    }

    /// <summary>
    /// 转换自身到可绘制矩形
    /// </summary>
    public RectDrawable SelfToRectDrawable(string name, Pen? pen = null)
    {
        return ToRectDrawable(0, 0, Width, Height, name, pen);
    }

    /// <summary>
    /// 在遮罩窗口绘制自身
    /// </summary>
    public void DrawSelf(string name, Pen? pen = null)
    {
        DrawRect(0, 0, Width, Height, name, pen);
    }

    /// <summary>
    /// 在遮罩窗口绘制指定区域
    /// </summary>
    public void DrawRect(int x, int y, int w, int h, string name, Pen? pen = null)
    {
        var drawable = ToRectDrawable(x, y, w, h, name, pen);
        DrawContent.PutRect(name, drawable);
    }

    /// <summary>
    /// 在遮罩窗口绘制指定区域
    /// </summary>
    public void DrawRect(Rect rect, string name, Pen? pen = null)
    {
        DrawRect(rect.X, rect.Y, rect.Width, rect.Height, name, pen);
    }

    /// <summary>
    /// 转换坐标到根坐标系
    /// </summary>
    private (int X, int Y) ConvertToRootCoordinates(int x, int y)
    {
        var currentX = x + X;
        var currentY = y + Y;
        var current = Owner;

        while (current != null)
        {
            currentX += current.X;
            currentY += current.Y;
            current = current.Owner;
        }

        return (currentX, currentY);
    }

    /// <summary>
    /// 检查区域是否为空
    /// </summary>
    public bool IsEmpty()
    {
        return Width == 0 && Height == 0 && X == 0 && Y == 0;
    }

    /// <summary>
    /// 检查区域是否存在（非空）
    /// </summary>
    public bool IsExist()
    {
        return !IsEmpty();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _cacheImage?.Dispose();
            _cacheGreyMat?.Dispose();
            SrcMat.Dispose();
        }

        _disposed = true;
    }

    ~ImageRegion()
    {
        Dispose(false);
    }
}
