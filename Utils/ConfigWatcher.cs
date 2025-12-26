using System.IO;

namespace ShineProCS.Utils;

/// <summary>
/// 配置监听器
/// 监听配置文件变化并触发热重载
/// </summary>
public class ConfigWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private System.Threading.Timer? _debounceTimer;
    private const int DebounceDelayMs = 500;

    /// <summary>
    /// 配置更新事件
    /// </summary>
    public event Action<string>? ConfigChanged;

    public ConfigWatcher(string configDirectory)
    {
        var fullPath = Path.GetFullPath(configDirectory);
        
        if (!Directory.Exists(fullPath))
            Directory.CreateDirectory(fullPath);

        _watcher = new FileSystemWatcher(fullPath)
        {
            Filter = "*.json",
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Renamed += OnFileChanged;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // 忽略备份文件（.bak*.json 和 .corrupted_*.json）
        var fileName = Path.GetFileName(e.FullPath);
        if (fileName.Contains(".bak") || fileName.Contains(".corrupted_"))
            return;

        // 防抖处理
        _debounceTimer?.Dispose();
        _debounceTimer = new System.Threading.Timer(_ => 
        {
            ConfigChanged?.Invoke(e.FullPath);
        }, null, DebounceDelayMs, Timeout.Infinite);
    }

    public void Stop() => _watcher.EnableRaisingEvents = false;
    public void Start() => _watcher.EnableRaisingEvents = true;

    public void Dispose()
    {
        _debounceTimer?.Dispose();
        _watcher.Dispose();
    }
}
