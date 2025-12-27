using System.Collections.Concurrent;
using System.IO;
using OpenCvSharp;

namespace ShineProCS.Core.Services;

/// <summary>
/// 模板预加载服务
/// 启动时预热所有模板图片到内存，避免运行时IO
/// </summary>
public class TemplatePreloader : IDisposable
{
    private readonly ConcurrentDictionary<string, Mat> _templates = new();
    private bool _disposed;

    /// <summary>
    /// 预加载所有模板
    /// </summary>
    /// <param name="templatePaths">模板路径列表</param>
    /// <returns>成功加载的数量</returns>
    public int PreloadTemplates(IEnumerable<string> templatePaths)
    {
        int loaded = 0;
        
        foreach (var path in templatePaths.Where(p => !string.IsNullOrEmpty(p)))
        {
            if (LoadTemplate(path))
                loaded++;
        }
        
        return loaded;
    }

    /// <summary>
    /// 从配置中预加载所有模板
    /// </summary>
    public int PreloadFromConfig(ConfigManager config)
    {
        var paths = new HashSet<string>();
        
        // 收集所有技能模板路径
        foreach (var skill in config.Skills)
        {
            if (!string.IsNullOrEmpty(skill.TemplatePath))
                paths.Add(skill.TemplatePath);
            
            // 收集Buff模板路径
            foreach (var buff in skill.BuffRequirements)
            {
                if (!string.IsNullOrEmpty(buff.TemplatePath))
                    paths.Add(buff.TemplatePath);
            }
        }
        
        return PreloadTemplates(paths);
    }

    /// <summary>
    /// 加载单个模板
    /// </summary>
    private bool LoadTemplate(string path)
    {
        if (_templates.ContainsKey(path))
            return true;
        
        if (!File.Exists(path))
            return false;
        
        try
        {
            var mat = Cv2.ImRead(path, ImreadModes.Color);
            if (!mat.Empty())
            {
                _templates[path] = mat;
                return true;
            }
            mat.Dispose();
        }
        catch
        {
            // 加载失败时静默处理，不影响程序运行
        }
        
        return false;
    }

    /// <summary>
    /// 获取已缓存的模板
    /// </summary>
    public Mat? GetTemplate(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        
        if (_templates.TryGetValue(path, out var mat))
            return mat;
        
        // 尝试即时加载
        if (LoadTemplate(path))
            return _templates.GetValueOrDefault(path);
        
        return null;
    }

    /// <summary>
    /// 检查模板是否已缓存
    /// </summary>
    public bool IsLoaded(string path) => _templates.ContainsKey(path);

    /// <summary>
    /// 获取已缓存的模板数量
    /// </summary>
    public int CachedCount => _templates.Count;

    /// <summary>
    /// 清除指定模板缓存
    /// </summary>
    public void RemoveTemplate(string path)
    {
        if (_templates.TryRemove(path, out var mat))
            mat.Dispose();
    }

    /// <summary>
    /// 清除所有缓存
    /// </summary>
    public void Clear()
    {
        foreach (var kvp in _templates)
            kvp.Value.Dispose();
        _templates.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clear();
        GC.SuppressFinalize(this);
    }
}
