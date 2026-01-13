using System.IO;
using System.Text.Json;
using ShineProCS.Core.Interfaces;

namespace ShineProCS.Core.Pathing;

/// <summary>
/// 路径加载器
/// 需求: 20.2 - 支持加载预设的路径文件（JSON 格式）
/// </summary>
public class PathLoader
{
    #region 常量
    
    private const string DefaultPathDirectory = "paths";
    private const string PathFileExtension = ".json";
    
    #endregion
    
    #region 依赖组件
    
    private readonly ILogService _logService;
    private readonly string _basePath;
    
    #endregion
    
    #region 构造函数
    
    /// <summary>
    /// 创建路径加载器
    /// </summary>
    public PathLoader(ILogService logService, string? basePath = null)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _basePath = basePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultPathDirectory);
        
        // 确保目录存在
        EnsureDirectoryExists();
    }
    
    #endregion
    
    #region 路径加载
    
    /// <summary>
    /// 从文件加载路径
    /// 需求: 20.2 - 支持加载预设的路径文件（JSON 格式）
    /// </summary>
    public PathData? LoadFromFile(string filePath)
    {
        try
        {
            // 如果是相对路径，则相对于基础目录
            if (!Path.IsPathRooted(filePath))
            {
                filePath = Path.Combine(_basePath, filePath);
            }
            
            // 自动添加扩展名
            if (!filePath.EndsWith(PathFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                filePath += PathFileExtension;
            }
            
            if (!File.Exists(filePath))
            {
                Log($"路径文件不存在: {filePath}", 2);
                return null;
            }
            
            var json = File.ReadAllText(filePath);
            return ParseJson(json, filePath);
        }
        catch (Exception ex)
        {
            Log($"加载路径文件失败: {ex.Message}", 3);
            return null;
        }
    }
    
    /// <summary>
    /// 从 JSON 字符串解析路径
    /// </summary>
    public PathData? ParseJson(string json, string? sourceName = null)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            
            var pathData = JsonSerializer.Deserialize<PathData>(json, options);
            
            if (pathData == null)
            {
                Log("路径数据解析结果为空", 2);
                return null;
            }
            
            if (pathData.Points.Count == 0)
            {
                Log("路径不包含任何路径点", 2);
                return null;
            }
            
            // 验证和修复路径数据
            ValidateAndFixPathData(pathData);
            
            Log($"成功加载路径: {pathData.Name}, 共 {pathData.PointCount} 个点", 1);
            return pathData;
        }
        catch (JsonException ex)
        {
            Log($"JSON 解析错误: {ex.Message}", 3);
            return null;
        }
    }


    /// <summary>
    /// 验证并修复路径数据
    /// </summary>
    private void ValidateAndFixPathData(PathData pathData)
    {
        var ids = new HashSet<int>();
        var nextId = 1;
        
        for (int i = 0; i < pathData.Points.Count; i++)
        {
            var point = pathData.Points[i];
            
            // 确保 ID 唯一
            if (point.Id == 0 || ids.Contains(point.Id))
            {
                while (ids.Contains(nextId)) nextId++;
                point.Id = nextId;
            }
            ids.Add(point.Id);
            
            // 应用默认值
            if (point.Tolerance <= 0)
                point.Tolerance = pathData.DefaultTolerance;
            
            if (point.TimeoutMs <= 0)
                point.TimeoutMs = pathData.DefaultTimeoutMs;
            
            // 验证坐标
            if (double.IsNaN(point.X) || double.IsInfinity(point.X))
            {
                Log($"警告: 路径点 {point.Id} 的 X 坐标无效，已重置为 0", 2);
                point.X = 0;
            }
            
            if (double.IsNaN(point.Y) || double.IsInfinity(point.Y))
            {
                Log($"警告: 路径点 {point.Id} 的 Y 坐标无效，已重置为 0", 2);
                point.Y = 0;
            }
        }
    }
    
    #endregion
    
    #region 路径保存
    
    /// <summary>
    /// 保存路径到文件
    /// </summary>
    public bool SaveToFile(PathData pathData, string filePath)
    {
        try
        {
            // 如果是相对路径，则相对于基础目录
            if (!Path.IsPathRooted(filePath))
            {
                filePath = Path.Combine(_basePath, filePath);
            }
            
            // 自动添加扩展名
            if (!filePath.EndsWith(PathFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                filePath += PathFileExtension;
            }
            
            // 确保目录存在
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            
            var json = JsonSerializer.Serialize(pathData, options);
            File.WriteAllText(filePath, json);
            
            Log($"路径已保存: {filePath}", 1);
            return true;
        }
        catch (Exception ex)
        {
            Log($"保存路径失败: {ex.Message}", 3);
            return false;
        }
    }
    
    /// <summary>
    /// 序列化路径为 JSON 字符串
    /// </summary>
    public string? ToJson(PathData pathData, bool indented = true)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = indented
            };
            
            return JsonSerializer.Serialize(pathData, options);
        }
        catch (Exception ex)
        {
            Log($"序列化路径失败: {ex.Message}", 3);
            return null;
        }
    }
    
    #endregion
    
    #region 路径列表
    
    /// <summary>
    /// 获取所有可用的路径文件
    /// </summary>
    public List<string> GetAvailablePaths()
    {
        var paths = new List<string>();
        
        try
        {
            if (!Directory.Exists(_basePath))
                return paths;
            
            var files = Directory.GetFiles(_basePath, $"*{PathFileExtension}", SearchOption.AllDirectories);
            
            foreach (var file in files)
            {
                // 返回相对路径
                var relativePath = Path.GetRelativePath(_basePath, file);
                paths.Add(relativePath);
            }
        }
        catch (Exception ex)
        {
            Log($"获取路径列表失败: {ex.Message}", 2);
        }
        
        return paths;
    }
    
    /// <summary>
    /// 获取路径文件的简要信息
    /// </summary>
    public PathData? GetPathInfo(string filePath)
    {
        return LoadFromFile(filePath);
    }
    
    #endregion
    
    #region 辅助方法
    
    /// <summary>
    /// 确保目录存在
    /// </summary>
    private void EnsureDirectoryExists()
    {
        try
        {
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
                Log($"已创建路径目录: {_basePath}", 0);
            }
        }
        catch (Exception ex)
        {
            Log($"创建路径目录失败: {ex.Message}", 2);
        }
    }
    
    /// <summary>
    /// 获取基础路径
    /// </summary>
    public string GetBasePath() => _basePath;
    
    #endregion
    
    #region 日志方法
    
    private void Log(string message, int level)
    {
        var logLevel = level switch
        {
            0 => Interfaces.LogLevel.Debug,
            1 => Interfaces.LogLevel.Info,
            2 => Interfaces.LogLevel.Warning,
            3 => Interfaces.LogLevel.Error,
            _ => Interfaces.LogLevel.Info
        };
        
        _logService.Log($"[路径加载] {message}", logLevel, "PathLoader");
    }
    
    #endregion
}
