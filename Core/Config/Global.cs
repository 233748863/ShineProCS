using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShineProCS.Core.Config;

/// <summary>
/// 全局配置和路径管理
/// </summary>
public static class Global
{
    /// <summary>
    /// 应用程序版本号
    /// </summary>
    public static string Version { get; } = Assembly.GetEntryAssembly()?
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion ?? "1.0.0";

    /// <summary>
    /// 应用程序启动路径
    /// </summary>
    public static string StartUpPath { get; set; } = AppContext.BaseDirectory;

    /// <summary>
    /// JSON 序列化选项
    /// </summary>
    public static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// 将相对路径转换为绝对路径
    /// </summary>
    /// <param name="relativePath">相对路径</param>
    /// <returns>绝对路径</returns>
    public static string Absolute(string relativePath)
    {
        return Path.Combine(StartUpPath, relativePath);
    }

    /// <summary>
    /// 如果文件存在则读取全部内容
    /// </summary>
    /// <param name="relativePath">相对路径</param>
    /// <returns>文件内容，不存在则返回 null</returns>
    public static string? ReadAllTextIfExist(string relativePath)
    {
        var path = Absolute(relativePath);
        if (File.Exists(path)) return File.ReadAllText(path);
        return null;
    }

    /// <summary>
    /// 写入文件内容
    /// </summary>
    /// <param name="relativePath">相对路径</param>
    /// <param name="content">文件内容</param>
    public static void WriteAllText(string relativePath, string content)
    {
        var path = Absolute(relativePath);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(path, content);
    }
}
