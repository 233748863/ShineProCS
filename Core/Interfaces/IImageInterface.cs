using OpenCvSharp;

namespace ShineProCS.Core.Interfaces;

/// <summary>
/// 图像处理接口
/// 定义屏幕截图、像素获取和模板匹配等图像操作
/// </summary>
public interface IImageInterface
{
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
}
