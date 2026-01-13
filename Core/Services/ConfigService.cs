using ShineProCS.Core.Interfaces;
using ShineProCS.Models;

namespace ShineProCS.Core.Services;

/// <summary>
/// 配置服务实现
/// 包装 ConfigManager 并提供防抖动自动保存功能
/// 参考 BetterGI 的配置服务设计
/// </summary>
public class ConfigService : IConfigService, IDisposable
{
    private readonly ConfigManager _configManager;
    private readonly System.Threading.Timer _saveTimer;
    private readonly object _saveLock = new();
    private bool _pendingSave;
    private bool _disposed;
    
    /// <summary>
    /// 防抖动延迟时间（毫秒）
    /// </summary>
    private const int DebounceDelayMs = 500;
    
    /// <summary>
    /// 配置变更事件
    /// </summary>
    public event Action<string>? ConfigChanged;
    
    /// <summary>
    /// 应用程序设置
    /// </summary>
    public AppSettings AppSettings => _configManager.AppSettings;
    
    /// <summary>
    /// 技能配置列表
    /// </summary>
    public List<SkillConfig> Skills => _configManager.Skills;
    
    public ConfigService()
    {
        _configManager = new ConfigManager();
        _configManager.ConfigChanged += OnConfigManagerChanged;
        
        // 初始化防抖动定时器
        _saveTimer = new System.Threading.Timer(SaveCallback, null, Timeout.Infinite, Timeout.Infinite);
        
        // 加载配置
        Load();
    }
    
    public ConfigService(ConfigManager configManager)
    {
        _configManager = configManager;
        _configManager.ConfigChanged += OnConfigManagerChanged;
        
        // 初始化防抖动定时器
        _saveTimer = new System.Threading.Timer(SaveCallback, null, Timeout.Infinite, Timeout.Infinite);
    }
    
    private void OnConfigManagerChanged(string path)
    {
        ConfigChanged?.Invoke(path);
    }
    
    /// <summary>
    /// 加载所有配置
    /// </summary>
    public void Load()
    {
        _configManager.LoadConfigs();
    }
    
    /// <summary>
    /// 保存所有配置（带防抖动）
    /// 多次调用会合并为一次保存操作
    /// </summary>
    public void Save()
    {
        lock (_saveLock)
        {
            _pendingSave = true;
            // 重置定时器，延迟保存
            _saveTimer.Change(DebounceDelayMs, Timeout.Infinite);
        }
    }
    
    /// <summary>
    /// 立即保存所有配置（不防抖动）
    /// </summary>
    public void SaveImmediate()
    {
        lock (_saveLock)
        {
            _pendingSave = false;
            _saveTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
        
        _configManager.SaveAll();
    }
    
    /// <summary>
    /// 防抖动定时器回调
    /// </summary>
    private void SaveCallback(object? state)
    {
        lock (_saveLock)
        {
            if (!_pendingSave) return;
            _pendingSave = false;
        }
        
        try
        {
            _configManager.SaveAll();
        }
        catch (Exception ex)
        {
            // 保存失败时记录日志，但不抛出异常
            System.Diagnostics.Debug.WriteLine($"配置保存失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 保存应用设置
    /// </summary>
    public void SaveAppSettings()
    {
        _configManager.SaveAppSettings();
    }
    
    /// <summary>
    /// 保存技能配置
    /// </summary>
    public void SaveSkills()
    {
        _configManager.SaveSkills();
    }
    
    /// <summary>
    /// 获取可用的配置方案列表
    /// </summary>
    public List<string> GetAvailableProfiles()
    {
        return _configManager.GetAvailableProfiles();
    }
    
    /// <summary>
    /// 切换配置方案
    /// </summary>
    public void SwitchProfile(string profileName)
    {
        _configManager.SwitchProfile(profileName);
    }
    
    /// <summary>
    /// 创建新的配置方案
    /// </summary>
    public void CreateProfile(string profileName)
    {
        _configManager.CreateProfile(profileName);
    }
    
    /// <summary>
    /// 删除配置方案
    /// </summary>
    public void DeleteProfile(string profileName)
    {
        _configManager.DeleteProfile(profileName);
    }
    
    /// <summary>
    /// 导出配置到文件
    /// </summary>
    public void ExportConfig(string exportPath, bool includeTemplates = true)
    {
        _configManager.ExportConfig(exportPath, includeTemplates);
    }
    
    /// <summary>
    /// 从文件导入配置
    /// </summary>
    public string ImportConfig(string importPath, bool overwrite = false)
    {
        return _configManager.ImportConfig(importPath, overwrite);
    }
    
    /// <summary>
    /// 获取内部的 ConfigManager 实例
    /// 用于需要直接访问 ConfigManager 的场景
    /// </summary>
    public ConfigManager GetConfigManager() => _configManager;
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        // 停止定时器
        _saveTimer.Change(Timeout.Infinite, Timeout.Infinite);
        
        // 如果有待保存的更改，立即保存
        lock (_saveLock)
        {
            if (_pendingSave)
            {
                _pendingSave = false;
                try
                {
                    _configManager.SaveAll();
                }
                catch { /* 忽略保存错误 */ }
            }
        }
        
        _saveTimer.Dispose();
        _configManager.ConfigChanged -= OnConfigManagerChanged;
        
        GC.SuppressFinalize(this);
    }
}
