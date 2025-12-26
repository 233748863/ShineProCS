using System.IO;
using OpenCvSharp;
using ShineProCS.Core.Interfaces;

namespace ShineProCS.Core.Services;

/// <summary>
/// 技能模板截取服务
/// 支持一键截取技能图标作为模板
/// </summary>
public class TemplateCapture
{
    private readonly IImageInterface _image;
    private readonly string _templateDir;

    /// <summary>
    /// 创建模板截取服务
    /// </summary>
    /// <param name="image">图像接口</param>
    /// <param name="templateDir">模板保存目录</param>
    public TemplateCapture(IImageInterface image, string? templateDir = null)
    {
        _image = image;
        _templateDir = templateDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates");
        
        // 确保目录存在
        if (!Directory.Exists(_templateDir))
            Directory.CreateDirectory(_templateDir);
    }

    /// <summary>
    /// 截取指定区域作为模板
    /// </summary>
    /// <param name="region">区域 [X, Y, Width, Height]</param>
    /// <param name="name">模板名称（不含扩展名）</param>
    /// <returns>保存的文件路径，失败返回null</returns>
    public string? CaptureTemplate(int[] region, string name)
    {
        if (region.Length < 4 || region[2] <= 0 || region[3] <= 0)
            return null;
        
        try
        {
            var mat = _image.GetScreenRegion(region[0], region[1], region[2], region[3]);
            if (mat == null) return null;
            
            try
            {
                // 生成唯一文件名
                var safeName = SanitizeFileName(name);
                var fileName = $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                var filePath = Path.Combine(_templateDir, fileName);
                
                // 保存图片
                Cv2.ImWrite(filePath, mat);
                
                return filePath;
            }
            finally
            {
                _image.ReturnMat(mat);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 截取技能图标模板
    /// </summary>
    /// <param name="skillName">技能名称</param>
    /// <param name="iconRegion">图标区域</param>
    /// <returns>保存的文件路径</returns>
    public string? CaptureSkillTemplate(string skillName, int[] iconRegion)
    {
        return CaptureTemplate(iconRegion, $"skill_{skillName}");
    }

    /// <summary>
    /// 截取Buff图标模板
    /// </summary>
    /// <param name="buffName">Buff名称</param>
    /// <param name="iconRegion">图标区域</param>
    /// <returns>保存的文件路径</returns>
    public string? CaptureBuffTemplate(string buffName, int[] iconRegion)
    {
        return CaptureTemplate(iconRegion, $"buff_{buffName}");
    }

    /// <summary>
    /// 获取模板目录路径
    /// </summary>
    public string TemplateDirectory => _templateDir;

    /// <summary>
    /// 获取所有已保存的模板文件
    /// </summary>
    public IEnumerable<string> GetAllTemplates()
    {
        if (!Directory.Exists(_templateDir))
            return [];
        
        return Directory.GetFiles(_templateDir, "*.png")
            .Concat(Directory.GetFiles(_templateDir, "*.jpg"))
            .Concat(Directory.GetFiles(_templateDir, "*.bmp"));
    }

    /// <summary>
    /// 删除模板文件
    /// </summary>
    public bool DeleteTemplate(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 清理文件名中的非法字符
    /// </summary>
    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }
}
