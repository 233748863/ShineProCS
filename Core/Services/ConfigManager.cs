using System.IO;
using System.Text.Json;
using ShineProCS.Models;

namespace ShineProCS.Core.Services;

/// <summary>
/// 配置验证结果
/// </summary>
public class ConfigValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
}

public class ConfigManager
{
    private readonly string _configPath;
    private readonly string _appSettingsPath;
    private string _skillsPath;
    private AppSettings? _appSettings;
    private List<SkillConfig>? _skills;
    private readonly object _configLock = new(); // 读写锁保护

    public event Action<string>? ConfigChanged;

    public ConfigManager()
    {
        // 使用应用程序目录下的config文件夹
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _configPath = Path.Combine(baseDir, "config");
        _appSettingsPath = Path.Combine(_configPath, "appsettings.json");
        _skillsPath = Path.Combine(_configPath, "skills.json");
        
        EnsureConfigDirectory();
    }

    private void EnsureConfigDirectory()
    {
        if (!Directory.Exists(_configPath))
            Directory.CreateDirectory(_configPath);
    }

    public List<string> GetAvailableProfiles()
    {
        var profiles = new List<string> { "默认" };
        try
        {
            foreach (var file in Directory.GetFiles(_configPath, "skills_*.json"))
                profiles.Add(Path.GetFileNameWithoutExtension(file).Replace("skills_", ""));
        }
        catch { }
        return profiles;
    }

    public void SwitchProfile(string profileName)
    {
        lock (_configLock)
        {
            _skillsPath = profileName == "默认" 
                ? Path.Combine(_configPath, "skills.json")
                : Path.Combine(_configPath, $"skills_{profileName}.json");
            LoadSkills();
        }
        ConfigChanged?.Invoke(_skillsPath);
    }

    public void LoadConfigs()
    {
        lock (_configLock)
        {
            LoadAppSettings();
            LoadSkills();
        }
    }

    private void LoadAppSettings()
    {
        try
        {
            if (File.Exists(_appSettingsPath))
            {
                var json = File.ReadAllText(_appSettingsPath);
                _appSettings = JsonSerializer.Deserialize<AppSettings>(json);
                
                // 验证并修正配置
                if (_appSettings != null)
                    ValidateAndFixAppSettings(_appSettings);
            }
            else
            {
                _appSettings = new AppSettings();
                SaveAppSettings();
            }
        }
        catch (JsonException ex)
        {
            // JSON 解析错误，使用默认配置并备份损坏的文件
            BackupCorruptedFile(_appSettingsPath, ex.Message);
            _appSettings = new AppSettings();
        }
        catch
        {
            _appSettings = new AppSettings();
        }
    }

    private void LoadSkills()
    {
        try
        {
            if (File.Exists(_skillsPath))
            {
                var json = File.ReadAllText(_skillsPath);
                _skills = JsonSerializer.Deserialize<List<SkillConfig>>(json);
                if (_skills != null)
                {
                    foreach (var skill in _skills)
                    {
                        skill.BuffRequirements ??= [];
                        ValidateAndFixSkillConfig(skill);
                    }
                }
            }
            else
            {
                _skills = CreateDefaultSkills();
                SaveSkills();
            }
        }
        catch (JsonException ex)
        {
            BackupCorruptedFile(_skillsPath, ex.Message);
            _skills = CreateDefaultSkills();
        }
        catch
        {
            _skills = CreateDefaultSkills();
        }
    }

    /// <summary>
    /// 验证并修正 AppSettings
    /// </summary>
    private void ValidateAndFixAppSettings(AppSettings settings)
    {
        // 修正循环间隔
        if (settings.LoopInterval < 10)
            settings.LoopInterval = 10;
        else if (settings.LoopInterval > 5000)
            settings.LoopInterval = 5000;
        
        // 修正图像队列容量
        if (settings.ImageQueueCapacity < 2)
            settings.ImageQueueCapacity = 2;
        else if (settings.ImageQueueCapacity > 10)
            settings.ImageQueueCapacity = 10;
        
        // 修正日志级别
        if (settings.LogLevel < 0)
            settings.LogLevel = 0;
        else if (settings.LogLevel > 3)
            settings.LogLevel = 3;
        
        // 修正区域数组长度
        if (settings.DetectionRegion.Length != 4)
            settings.DetectionRegion = [0, 0, 100, 100];
        if (settings.ManaBarRegion.Length != 4)
            settings.ManaBarRegion = [0, 0, 100, 20];
        if (settings.HealthBarRegion.Length != 4)
            settings.HealthBarRegion = [0, 0, 100, 20];
        if (settings.GlobalCdPoint.Length != 2)
            settings.GlobalCdPoint = [0, 0];
        
        // 修正按键码
        if (settings.QianZhiKeyCode < 0 || settings.QianZhiKeyCode > 255)
            settings.QianZhiKeyCode = 87; // W键
    }

    /// <summary>
    /// 验证并修正 SkillConfig
    /// </summary>
    private void ValidateAndFixSkillConfig(SkillConfig skill)
    {
        // 修正按键码
        if (skill.KeyCode < 0 || skill.KeyCode > 255)
            skill.KeyCode = 49; // 1键
        
        // 修正优先级
        if (skill.Priority < 0)
            skill.Priority = 0;
        
        // 修正相似度阈值
        if (skill.SimilarityThreshold < 0)
            skill.SimilarityThreshold = 0;
        else if (skill.SimilarityThreshold > 1)
            skill.SimilarityThreshold = 1;
        
        // 修正 HP/MP 范围
        if (skill.MinHp < 0) skill.MinHp = 0;
        else if (skill.MinHp > 100) skill.MinHp = 100;
        
        if (skill.MinMp < 0) skill.MinMp = 0;
        else if (skill.MinMp > 100) skill.MinMp = 100;
        
        // 修正区域数组
        if (skill.IconRegion.Length != 4)
            skill.IconRegion = [0, 0, 0, 0];
        
        // 修正连招延迟
        if (skill.ComboDelay < 0)
            skill.ComboDelay = 0;
        else if (skill.ComboDelay > 5000)
            skill.ComboDelay = 5000;
        
        // 修正前置按键码
        if (skill.PreCastKeyCode < 0 || skill.PreCastKeyCode > 255)
            skill.PreCastKeyCode = 0;
        
        // 验证 Buff 配置
        foreach (var buff in skill.BuffRequirements)
        {
            if (buff.IconRegion.Length != 4)
                buff.IconRegion = [0, 0, 0, 0];
            
            if (buff.SimilarityThreshold < 0)
                buff.SimilarityThreshold = 0;
            else if (buff.SimilarityThreshold > 1)
                buff.SimilarityThreshold = 1;
        }
    }

    /// <summary>
    /// 备份损坏的配置文件
    /// </summary>
    private void BackupCorruptedFile(string filePath, string errorMessage)
    {
        try
        {
            if (!File.Exists(filePath)) return;
            
            var backupPath = $"{filePath}.corrupted_{DateTime.Now:yyyyMMdd_HHmmss}";
            File.Copy(filePath, backupPath, overwrite: true);
            
            // 写入错误日志
            var logPath = Path.Combine(_configPath, "config_errors.log");
            var logContent = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 文件 {Path.GetFileName(filePath)} 解析失败: {errorMessage}\n";
            File.AppendAllText(logPath, logContent);
        }
        catch { /* 备份失败时忽略 */ }
    }

    /// <summary>
    /// 验证当前配置
    /// </summary>
    public ConfigValidationResult ValidateCurrentConfig()
    {
        var result = new ConfigValidationResult();
        
        lock (_configLock)
        {
            // 验证 AppSettings
            if (_appSettings != null)
            {
                if (_appSettings.LoopInterval < 50)
                    result.Warnings.Add("循环间隔过小可能导致高CPU占用");
                
                if (_appSettings.DetectionRegion.All(v => v == 0))
                    result.Warnings.Add("检测区域未配置");
            }
            
            // 验证技能配置
            if (_skills != null)
            {
                var keyCodeUsage = new Dictionary<int, List<string>>();
                
                foreach (var skill in _skills)
                {
                    if (string.IsNullOrWhiteSpace(skill.Name))
                        result.Errors.Add("存在未命名的技能");
                    
                    if (skill.Enabled && skill.KeyCode <= 0)
                        result.Errors.Add($"技能[{skill.Name}]已启用但未配置按键");
                    
                    // 检查按键冲突
                    if (skill.KeyCode > 0)
                    {
                        if (!keyCodeUsage.ContainsKey(skill.KeyCode))
                            keyCodeUsage[skill.KeyCode] = [];
                        keyCodeUsage[skill.KeyCode].Add(skill.Name);
                    }
                    
                    // 检查模板文件
                    if (!string.IsNullOrEmpty(skill.TemplatePath) && !File.Exists(skill.TemplatePath))
                        result.Warnings.Add($"技能[{skill.Name}]的模板文件不存在: {skill.TemplatePath}");
                    
                    foreach (var buff in skill.BuffRequirements)
                    {
                        if (!string.IsNullOrEmpty(buff.TemplatePath) && !File.Exists(buff.TemplatePath))
                            result.Warnings.Add($"Buff[{buff.Name}]的模板文件不存在: {buff.TemplatePath}");
                    }
                }
                
                // 报告按键冲突
                foreach (var kvp in keyCodeUsage.Where(k => k.Value.Count > 1))
                {
                    result.Warnings.Add($"按键冲突: VK={kvp.Key} 被多个技能使用: {string.Join(", ", kvp.Value)}");
                }
            }
        }
        
        return result;
    }

    public void SaveAppSettings()
    {
        lock (_configLock)
        {
            if (_appSettings == null) return;
            
            // 保存前备份
            BackupBeforeSave(_appSettingsPath);
            
            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            File.WriteAllText(_appSettingsPath, JsonSerializer.Serialize(_appSettings, options));
        }
    }

    public void SaveSkills()
    {
        lock (_configLock)
        {
            if (_skills == null) return;
            
            // 保存前备份
            BackupBeforeSave(_skillsPath);
            
            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            File.WriteAllText(_skillsPath, JsonSerializer.Serialize(_skills, options));
        }
    }

    /// <summary>
    /// 保存前备份（保留最近3个备份）
    /// </summary>
    private void BackupBeforeSave(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return;
            
            var dir = Path.GetDirectoryName(filePath) ?? _configPath;
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var ext = Path.GetExtension(filePath);
            
            // 查找现有备份
            var backups = Directory.GetFiles(dir, $"{fileName}.bak*{ext}")
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .ToList();
            
            // 删除多余备份（保留2个）
            foreach (var oldBackup in backups.Skip(2))
            {
                try { File.Delete(oldBackup); } catch { }
            }
            
            // 创建新备份
            var backupPath = Path.Combine(dir, $"{fileName}.bak{DateTime.Now:yyyyMMddHHmmss}{ext}");
            File.Copy(filePath, backupPath, overwrite: true);
        }
        catch { /* 备份失败时忽略 */ }
    }

    public void SaveAll() { SaveAppSettings(); SaveSkills(); }

    private List<SkillConfig> CreateDefaultSkills() =>
    [
        new() { Name = "技能1", KeyCode = 49, Priority = 1, Enabled = true },
        new() { Name = "技能2", KeyCode = 50, Priority = 2, Enabled = true },
        new() { Name = "技能3", KeyCode = 51, Priority = 3, Enabled = true }
    ];

    public AppSettings AppSettings
    {
        get { lock (_configLock) { return _appSettings ?? new AppSettings(); } }
    }
    
    public List<SkillConfig> Skills
    {
        get { lock (_configLock) { return _skills ?? []; } }
    }
}
