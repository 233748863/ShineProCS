using System.Drawing;
using System.Runtime.InteropServices;
using ShineProCS.Core.Interfaces;
using OpenCvSharp;

namespace ShineProCS.Infrastructure;

public class OpenCvImageInterface : IImageInterface, IDisposable
{
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern uint GetPixel(IntPtr hdc, int x, int y);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    private WgcCaptureInterface? _wgc;
    private bool _useWgc;
    private IntPtr _targetHwnd;
    private bool _disposed;

    public bool UseWgc => _useWgc;

    /// <summary>
    /// 初始化WGC截图模式
    /// </summary>
    public bool InitializeWgc(string? windowTitle = null)
    {
        try
        {
            _wgc?.Dispose();
            _wgc = new WgcCaptureInterface();
            
            IntPtr hwnd;
            if (string.IsNullOrEmpty(windowTitle))
                hwnd = GetForegroundWindow();
            else
                hwnd = FindWindow(null, windowTitle);
            
            if (hwnd == IntPtr.Zero)
                return false;
            
            _targetHwnd = hwnd;
            if (_wgc.Initialize(hwnd))
            {
                _useWgc = true;
                return true;
            }
        }
        catch { }
        
        _useWgc = false;
        return false;
    }

    /// <summary>
    /// 切换回传统GDI截图模式
    /// </summary>
    public void UseGdiMode()
    {
        _wgc?.Dispose();
        _wgc = null;
        _useWgc = false;
    }

    public Mat? GetScreenRegion(int x, int y, int w, int h)
    {
        if (w <= 0 || h <= 0) return null;
        
        // 优先使用WGC
        if (_useWgc && _wgc != null)
        {
            Mat? frame = null;
            Mat? region = null;
            try
            {
                frame = _wgc.CaptureFrameBgr();
                if (frame != null)
                {
                    // 如果请求的区域在WGC捕获范围内，直接裁剪
                    if (x >= 0 && y >= 0 && x + w <= frame.Width && y + h <= frame.Height)
                    {
                        region = new Mat(frame, new Rect(x, y, w, h));
                        return region.Clone();
                    }
                }
            }
            catch { }
            finally
            {
                region?.Dispose();
                frame?.Dispose();
            }
        }
        
        // 回退到GDI截图
        return GetScreenRegionGdi(x, y, w, h);
    }

    private Mat? GetScreenRegionGdi(int x, int y, int w, int h)
    {
        try
        {
            using var bmp = new Bitmap(w, h);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(w, h));
            return BitmapToMat(bmp);
        }
        catch { return null; }
    }

    public (byte r, byte g, byte b)? GetPixelColor(int x, int y)
    {
        // WGC模式下获取像素
        if (_useWgc && _wgc != null)
        {
            var color = _wgc.GetPixelColor(x, y);
            if (color.HasValue)
                return (color.Value.R, color.Value.G, color.Value.B);
        }
        
        // 回退到GDI
        try
        {
            var hdc = GetDC(IntPtr.Zero);
            var p = GetPixel(hdc, x, y);
            ReleaseDC(IntPtr.Zero, hdc);
            return ((byte)(p & 0xFF), (byte)((p >> 8) & 0xFF), (byte)((p >> 16) & 0xFF));
        }
        catch { return null; }
    }

    public double MatchTemplate(Mat src, Mat tpl)
    {
        if (src.Empty() || tpl.Empty()) return 0;
        try { using var r = new Mat(); Cv2.MatchTemplate(src, tpl, r, TemplateMatchModes.CCoeffNormed); Cv2.MinMaxLoc(r, out _, out double max, out _, out _); return max; }
        catch { return 0; }
    }

    public void ReturnMat(Mat m) => m?.Dispose();

    private static Mat BitmapToMat(Bitmap bmp)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        var mat = new Mat(bmp.Height, bmp.Width, MatType.CV_8UC3);
        unsafe { for (int y = 0; y < bmp.Height; y++) Buffer.MemoryCopy((byte*)data.Scan0 + y * data.Stride, mat.DataPointer + y * bmp.Width * 3, bmp.Width * 3, bmp.Width * 3); }
        bmp.UnlockBits(data);
        return mat;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _wgc?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
