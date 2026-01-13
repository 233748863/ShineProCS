using OpenCvSharp;

namespace ShineProCS.Core.Recognition.Template;

/// <summary>
/// 模板匹配服务接口
/// </summary>
public interface ITemplateMatchService
{
    /// <summary>
    /// 在图像中查找模板
    /// </summary>
    /// <param name="source">源图像</param>
    /// <param name="template">模板图像</param>
    /// <param name="threshold">匹配阈值 (0-1)，默认 0.8</param>
    /// <returns>匹配结果</returns>
    TemplateMatchResult Match(Mat source, Mat template, double threshold = 0.8);

    /// <summary>
    /// 在图像指定区域中查找模板
    /// </summary>
    /// <param name="source">源图像</param>
    /// <param name="template">模板图像</param>
    /// <param name="searchRegion">搜索区域</param>
    /// <param name="threshold">匹配阈值 (0-1)，默认 0.8</param>
    /// <returns>匹配结果</returns>
    TemplateMatchResult Match(Mat source, Mat template, Rect searchRegion, double threshold = 0.8);

    /// <summary>
    /// 在图像中查找所有匹配的模板位置
    /// </summary>
    /// <param name="source">源图像</param>
    /// <param name="template">模板图像</param>
    /// <param name="threshold">匹配阈值 (0-1)，默认 0.8</param>
    /// <param name="maxMatches">最大匹配数量，默认 10</param>
    /// <returns>所有匹配结果</returns>
    MultiTemplateMatchResult MatchAll(Mat source, Mat template, double threshold = 0.8, int maxMatches = 10);

    /// <summary>
    /// 使用模板名称在图像中查找模板（从缓存加载模板）
    /// </summary>
    /// <param name="source">源图像</param>
    /// <param name="templateName">模板名称（不含扩展名）</param>
    /// <param name="threshold">匹配阈值 (0-1)，默认 0.8</param>
    /// <returns>匹配结果</returns>
    TemplateMatchResult MatchByName(Mat source, string templateName, double threshold = 0.8);

    /// <summary>
    /// 使用模板名称在图像指定区域中查找模板
    /// </summary>
    /// <param name="source">源图像</param>
    /// <param name="templateName">模板名称（不含扩展名）</param>
    /// <param name="searchRegion">搜索区域</param>
    /// <param name="threshold">匹配阈值 (0-1)，默认 0.8</param>
    /// <returns>匹配结果</returns>
    TemplateMatchResult MatchByName(Mat source, string templateName, Rect searchRegion, double threshold = 0.8);

    /// <summary>
    /// 预加载模板到缓存
    /// </summary>
    /// <param name="templatePath">模板文件路径</param>
    /// <param name="templateName">模板名称（用于后续引用）</param>
    /// <returns>是否加载成功</returns>
    bool PreloadTemplate(string templatePath, string templateName);

    /// <summary>
    /// 清除模板缓存
    /// </summary>
    void ClearCache();

    /// <summary>
    /// 获取已缓存的模板名称列表
    /// </summary>
    IReadOnlyList<string> CachedTemplateNames { get; }
}
