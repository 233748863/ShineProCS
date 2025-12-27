using System.Drawing;
using System.Runtime.InteropServices;
using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Services;
using OpenCvSharp;

namespace ShineProCS.Infrastructure;

public class OpenCvImageInterface : IImageInterface, IDisposable
{
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern uint GetPixel(IntPtr hdc, int x, int y);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);
    
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    private WgcCaptureInterface? _wgc;
    private bool _useWgc;
    private IntPtr _targetHwnd;
    private bool _disposed;
    
    // 窗口客户区在屏幕上的位置（用于坐标转换）
    private int _clientX, _clientY;
    
    // Mat 对象池
    private readonly MatPool _matPool = new(30);

    public bool UseWgc => _useWgc;
    
    /// <summary>
    /// 获取对象池统计信息
    /// </summary>
    public (int Created, int Reused, int PoolSize) PoolStats => _matPool.GetStats();

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
            
            // 获取窗口客户区在屏幕上的位置
            UpdateWindowPosition();
            
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
    /// 更新窗口位置（窗口移动后需要调用）
    /// </summary>
    public void UpdateWindowPosition()
    {
        if (_targetHwnd == IntPtr.Zero) return;
        
        var pt = new POINT { X = 0, Y = 0 };
        if (ClientToScreen(_targetHwnd, ref pt))
        {
            _clientX = pt.X;
            _clientY = pt.Y;
        }
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
            try
            {
                // 将屏幕坐标转换为窗口客户区坐标
                int clientX = x - _clientX;
                int clientY = y - _clientY;
                
                // 检查坐标是否在窗口范围内
                if (clientX >= 0 && clientY >= 0)
                {
                    var region = _wgc.CaptureRegion(clientX, clientY, w, h);
                    if (region != null)
                        return region;
                }
            }
            catch { }
        }
        
        // 回退到GDI截图（使用屏幕坐标）
        return GetScreenRegionGdi(x, y, w, h);
    }

    private Mat? GetScreenRegionGdi(int x, int y, int w, int h)
    {
        Bitmap? bmp = null;
        try
        {
            bmp = new Bitmap(w, h);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(w, h));
            return BitmapToMatPooled(bmp);
        }
        catch 
        { 
            return null; 
        }
        finally
        {
            bmp?.Dispose();
        }
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
        
        Mat? result = null;
        try 
        { 
            result = _matPool.RentEmpty();
            Cv2.MatchTemplate(src, tpl, result, TemplateMatchModes.CCoeffNormed); 
            Cv2.MinMaxLoc(result, out _, out double max, out _, out _); 
            return max; 
        }
        catch { return 0; }
        finally
        {
            _matPool.Return(result);
        }
    }

    public void ReturnMat(Mat m)
    {
        if (m == null) return;
        _matPool.Return(m);
    }

    /// <summary>
    /// 使用对象池的 Bitmap 转 Mat
    /// </summary>
    private Mat BitmapToMatPooled(Bitmap bmp)
    {
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        
        // 从对象池获取 Mat
        var mat = _matPool.Rent(bmp.Height, bmp.Width, MatType.CV_8UC3);
        
        unsafe 
        { 
            for (int y = 0; y < bmp.Height; y++) 
                Buffer.MemoryCopy(
                    (byte*)data.Scan0 + y * data.Stride, 
                    mat.DataPointer + y * bmp.Width * 3, 
                    bmp.Width * 3, 
                    bmp.Width * 3); 
        }
        
        bmp.UnlockBits(data);
        return mat;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _wgc?.Dispose();
        _matPool.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
