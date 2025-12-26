using System.IO;
using System.Runtime.InteropServices;
using OpenCvSharp;

namespace ShineProCS.Infrastructure;

/// <summary>
/// Windows Graphics Capture API 截图接口
/// 使用WGC.dll实现高效窗口截图
/// </summary>
public class WgcCaptureInterface : IDisposable
{
    private static readonly string DllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libs", "WGC.dll");
    
    // 动态加载DLL
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);
    
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);
    
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }
    
    // 委托定义
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool InitCaptureDelegate(IntPtr hwnd, int cropX, int cropY, int cropW, int cropH);
    
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool GetLatestFrameDelegate(IntPtr outputBuffer, int bufferSize);
    
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void CleanupCaptureDelegate();
    
    private IntPtr _dllHandle;
    private InitCaptureDelegate? _initCapture;
    private GetLatestFrameDelegate? _getLatestFrame;
    private CleanupCaptureDelegate? _cleanupCapture;
    
    private IntPtr _hwnd;
    private int _roiX, _roiY, _roiW, _roiH;
    private byte[]? _buffer;
    private GCHandle _bufferHandle;
    private bool _isInitialized;
    private bool _dllLoaded;
    private bool _disposed;
    private static readonly object _lock = new();

    public bool IsInitialized => _isInitialized;
    public bool IsDllLoaded => _dllLoaded;
    public int Width => _roiW;
    public int Height => _roiH;

    public WgcCaptureInterface()
    {
        LoadDll();
    }

    private void LoadDll()
    {
        if (!File.Exists(DllPath))
        {
            _dllLoaded = false;
            return;
        }
        
        _dllHandle = LoadLibrary(DllPath);
        if (_dllHandle == IntPtr.Zero)
        {
            _dllLoaded = false;
            return;
        }
        
        var initPtr = GetProcAddress(_dllHandle, "InitCapture");
        var getFramePtr = GetProcAddress(_dllHandle, "GetLatestFrame");
        var cleanupPtr = GetProcAddress(_dllHandle, "CleanupCapture");
        
        if (initPtr == IntPtr.Zero || getFramePtr == IntPtr.Zero || cleanupPtr == IntPtr.Zero)
        {
            FreeLibrary(_dllHandle);
            _dllHandle = IntPtr.Zero;
            _dllLoaded = false;
            return;
        }
        
        _initCapture = Marshal.GetDelegateForFunctionPointer<InitCaptureDelegate>(initPtr);
        _getLatestFrame = Marshal.GetDelegateForFunctionPointer<GetLatestFrameDelegate>(getFramePtr);
        _cleanupCapture = Marshal.GetDelegateForFunctionPointer<CleanupCaptureDelegate>(cleanupPtr);
        _dllLoaded = true;
    }

    /// <summary>
    /// 初始化WGC截图会话
    /// </summary>
    public bool Initialize(IntPtr hwnd, int roiX = 0, int roiY = 0, int roiW = 0, int roiH = 0)
    {
        if (!_dllLoaded || _initCapture == null)
            return false;
        
        lock (_lock)
        {
            if (_isInitialized) Cleanup();
            
            _hwnd = hwnd;
            
            // 获取窗口尺寸
            if (!GetWindowRect(hwnd, out var rect))
                return false;
            
            var windowW = rect.Right - rect.Left;
            var windowH = rect.Bottom - rect.Top;
            
            if (windowW <= 0 || windowH <= 0)
                return false;
            
            // 设置ROI
            if (roiW > 0 && roiH > 0)
            {
                _roiX = roiX;
                _roiY = roiY;
                _roiW = Math.Min(roiW, windowW - roiX);
                _roiH = Math.Min(roiH, windowH - roiY);
            }
            else
            {
                _roiX = 0;
                _roiY = 0;
                _roiW = windowW;
                _roiH = windowH;
            }
            
            // 调用DLL初始化
            if (!_initCapture(hwnd, _roiX, _roiY, _roiW, _roiH))
                return false;
            
            // 分配缓冲区 (BGRA格式，每像素4字节)
            var bufferSize = _roiW * _roiH * 4;
            _buffer = new byte[bufferSize];
            _bufferHandle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
            
            _isInitialized = true;
            
            // 等待WGC预热
            Thread.Sleep(100);
            
            return true;
        }
    }

    /// <summary>
    /// 通过窗口标题初始化
    /// </summary>
    public bool InitializeByTitle(string windowTitle, int roiX = 0, int roiY = 0, int roiW = 0, int roiH = 0)
    {
        var hwnd = FindWindow(null, windowTitle);
        if (hwnd == IntPtr.Zero)
            return false;
        return Initialize(hwnd, roiX, roiY, roiW, roiH);
    }

    /// <summary>
    /// 获取最新帧 (返回BGRA格式的Mat)
    /// </summary>
    public Mat? CaptureFrame()
    {
        if (_getLatestFrame == null) return null;
        
        lock (_lock)
        {
            if (!_isInitialized || _buffer == null || !_bufferHandle.IsAllocated)
                return null;
            
            try
            {
                var ptr = _bufferHandle.AddrOfPinnedObject();
                if (!_getLatestFrame(ptr, _buffer.Length))
                    return null;
                
                // 创建Mat (BGRA格式)
                var mat = new Mat(_roiH, _roiW, MatType.CV_8UC4);
                Marshal.Copy(_buffer, 0, mat.Data, _buffer.Length);
                
                return mat;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// 获取最新帧并转换为BGR格式
    /// </summary>
    public Mat? CaptureFrameBgr()
    {
        Mat? bgra = null;
        Mat? bgr = null;
        try
        {
            bgra = CaptureFrame();
            if (bgra == null) return null;
            
            bgr = new Mat();
            Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
            return bgr;
        }
        catch
        {
            bgr?.Dispose();
            return null;
        }
        finally
        {
            bgra?.Dispose();
        }
    }

    /// <summary>
    /// 截取指定区域 (相对于ROI)
    /// </summary>
    public Mat? CaptureRegion(int x, int y, int width, int height)
    {
        Mat? frame = null;
        Mat? region = null;
        try
        {
            frame = CaptureFrameBgr();
            if (frame == null) return null;
            
            // 边界检查
            x = Math.Max(0, Math.Min(x, frame.Width - 1));
            y = Math.Max(0, Math.Min(y, frame.Height - 1));
            width = Math.Min(width, frame.Width - x);
            height = Math.Min(height, frame.Height - y);
            
            if (width <= 0 || height <= 0)
                return null;
            
            region = new Mat(frame, new Rect(x, y, width, height));
            return region.Clone();
        }
        catch
        {
            return null;
        }
        finally
        {
            region?.Dispose();
            frame?.Dispose();
        }
    }

    /// <summary>
    /// 获取指定点的像素颜色 (BGR)
    /// </summary>
    public (byte B, byte G, byte R)? GetPixelColor(int x, int y)
    {
        Mat? frame = null;
        try
        {
            frame = CaptureFrame();
            if (frame == null) return null;
            
            if (x < 0 || x >= frame.Width || y < 0 || y >= frame.Height)
                return null;
            
            var pixel = frame.At<Vec4b>(y, x);
            return (pixel.Item0, pixel.Item1, pixel.Item2);
        }
        catch
        {
            return null;
        }
        finally
        {
            frame?.Dispose();
        }
    }

    /// <summary>
    /// 清理资源
    /// </summary>
    public void Cleanup()
    {
        lock (_lock)
        {
            if (_isInitialized && _cleanupCapture != null)
            {
                try { _cleanupCapture(); } catch { }
                _isInitialized = false;
            }
            
            // 确保 GCHandle 在 buffer 置空前释放
            if (_bufferHandle.IsAllocated)
            {
                try { _bufferHandle.Free(); } catch { }
            }
            
            _buffer = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        Cleanup();
        if (_dllHandle != IntPtr.Zero)
        {
            FreeLibrary(_dllHandle);
            _dllHandle = IntPtr.Zero;
        }
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~WgcCaptureInterface() => Dispose();
}
