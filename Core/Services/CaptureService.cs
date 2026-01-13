using OpenCvSharp;
using ShineProCS.Core.Interfaces;
using ShineProCS.Infrastructure;

namespace ShineProCS.Core.Services;

/// <summary>
/// 截图服务实现
/// 封装 OpenCvImageInterface，提供统一的截图功能
/// 需求: 3.6 - 截图方式选择器（WGC/BitBlt）
/// 需求: 7.4 - 作为单例服务注册
/// </summary>
public class CaptureService : ICaptureService
{
    private readonly OpenCvImageInterface _imageInterface;
    private bool _disposed;
    
    public CaptureService()
    {
        _imageInterface = new OpenCvImageInterface();
    }
    
    /// <summary>
    /// 当前是否使用 WGC 截图模式
    /// </summary>
    public bool UseWgc => _imageInterface.UseWgc;
    
    /// <summary>
    /// 获取对象池统计信息
    /// </summary>
    public (int Created, int Reused, int PoolSize) PoolStats => _imageInterface.PoolStats;
    
    /// <summary>
    /// 初始化 WGC 截图模式
    /// </summary>
    /// <param name="windowTitle">目标窗口标题，为空则使用前台窗口</param>
    /// <returns>初始化是否成功</returns>
    public bool InitializeWgc(string? windowTitle = null)
    {
        return _imageInterface.InitializeWgc(windowTitle);
    }
    
    /// <summary>
    /// 切换到 GDI 截图模式
    /// </summary>
    public void UseGdiMode()
    {
        _imageInterface.UseGdiMode();
    }
    
    /// <summary>
    /// 更新窗口位置（窗口移动后需要调用）
    /// </summary>
    public void UpdateWindowPosition()
    {
        _imageInterface.UpdateWindowPosition();
    }
    
    /// <summary>
    /// 获取屏幕指定区域的截图
    /// </summary>
    public Mat? GetScreenRegion(int x, int y, int width, int height)
    {
        return _imageInterface.GetScreenRegion(x, y, width, height);
    }
    
    /// <summary>
    /// 获取屏幕指定位置的像素颜色
    /// </summary>
    public (byte r, byte g, byte b)? GetPixelColor(int x, int y)
    {
        return _imageInterface.GetPixelColor(x, y);
    }
    
    /// <summary>
    /// 执行模板匹配
    /// </summary>
    public double MatchTemplate(Mat source, Mat template)
    {
        return _imageInterface.MatchTemplate(source, template);
    }
    
    /// <summary>
    /// 归还Mat对象（释放资源）
    /// </summary>
    public void ReturnMat(Mat mat)
    {
        _imageInterface.ReturnMat(mat);
    }
    
    /// <summary>
    /// 设置日志回调函数
    /// </summary>
    public void SetLogCallback(Action<string, int>? callback)
    {
        _imageInterface.SetLogCallback(callback);
    }
    
    /// <summary>
    /// 获取内部图像接口
    /// 用于需要 IImageInterface 的组件（如 SkillLoopTrigger）
    /// </summary>
    /// <returns>图像接口实例</returns>
    public IImageInterface GetImageInterface() => _imageInterface;
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _imageInterface.Dispose();
        GC.SuppressFinalize(this);
    }
}
