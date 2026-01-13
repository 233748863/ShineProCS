using OpenCvSharp;

namespace ShineProCS.Core.Interfaces;

/// <summary>
/// 截图服务接口
/// 提供屏幕截图、像素获取和模板匹配功能
/// 需求: 3.6 - 截图方式选择器（WGC/BitBlt）
/// </summary>
public interface ICaptureService : IDisposable
{
    /// <summary>
    /// 当前是否使用 WGC 截图模式
    /// </summary>
    bool UseWgc { get; }
    
    /// <summary>
    /// 初始化 WGC 截图模式
    /// </summary>
    /// <param name="windowTitle">目标窗口标题，为空则使用前台窗口</param>
    /// <returns>初始化是否成功</returns>
    bool InitializeWgc(string? windowTitle = null);
    
    /// <summary>
    /// 切换到 GDI 截图模式
    /// </summary>
    void UseGdiMode();
    
    /// <summary>
    /// 更新窗口位置（窗口移动后需要调用）
    /// </summary>
    void UpdateWindowPosition();
    
    /// <summary>
    /// 获取屏幕指定区域的截图
    /// </summary>
    /// <param name="x">区域左上角X坐标</param>
    /// <param name="y">区域左上角Y坐标</param>
    /// <param name="width">区域宽度</param>
    /// <param name="height">区域高度</param>
    /// <returns>截图的Mat对象，失败返回null</returns>
    Mat? GetScreenRegion(int x, int y, int width, int height);
    
    /// <summary>
    /// 获取屏幕指定位置的像素颜色
    /// </summary>
    /// <param name="x">X坐标</param>
    /// <param name="y">Y坐标</param>
    /// <returns>RGB颜色值元组，失败返回null</returns>
    (byte r, byte g, byte b)? GetPixelColor(int x, int y);
    
    /// <summary>
    /// 执行模板匹配
    /// </summary>
    /// <param name="source">源图像</param>
    /// <param name="template">模板图像</param>
    /// <returns>匹配相似度 (0.0-1.0)</returns>
    double MatchTemplate(Mat source, Mat template);
    
    /// <summary>
    /// 归还Mat对象（释放资源）
    /// </summary>
    /// <param name="mat">要释放的Mat对象</param>
    void ReturnMat(Mat mat);
    
    /// <summary>
    /// 获取对象池统计信息
    /// </summary>
    (int Created, int Reused, int PoolSize) PoolStats { get; }
    
    /// <summary>
    /// 设置日志回调函数
    /// </summary>
    /// <param name="callback">日志回调，参数为消息和日志级别(0=调试,1=信息,2=警告)</param>
    void SetLogCallback(Action<string, int>? callback);
    
    /// <summary>
    /// 获取内部图像接口
    /// 用于需要 IImageInterface 的组件（如 SkillLoopTrigger）
    /// </summary>
    /// <returns>图像接口实例</returns>
    IImageInterface GetImageInterface();
}
