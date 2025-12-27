using System.IO;
using System.IO.Compression;
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
        EnsureDefaultConfigs();
    }

    private void EnsureConfigDirectory()
    {
        if (!Directory.Exists(_configPath))
            Directory.CreateDirectory(_configPath);
    }

    /// <summary>
    /// 首次运行时从 config_default 复制默认配置
    /// </summary>
    private void EnsureDefaultConfigs()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var defaultConfigDir = Path.Combine(baseDir, "config_default");
        
        // 如果 config_default 目录存在，复制缺失的配置文件
        if (Directory.Exists(defaultConfigDir))
        {
            // 复制 appsettings.json
            var defaultAppSettings = Path.Combine(defaultConfigDir, "appsettings.json");
            if (File.Exists(defaultAppSettings) && !File.Exists(_appSettingsPath))
            {
                File.Copy(defaultAppSettings, _appSettingsPath);
            }
            
            // 复制 skills.json
            var defaultSkills = Path.Combine(defaultConfigDir, "skills.json");
            if (File.Exists(defaultSkills) && !File.Exists(_skillsPath))
            {
                File.Copy(defaultSkills, _skillsPath);
            }
        }
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

    public void CreateProfile(string profileName)
    {
        var newPath = Path.Combine(_configPath, $"skills_{profileName}.json");
        if (File.Exists(newPath)) return;
        
        // 复制当前方案到新方案
        lock (_configLock)
        {
            File.Copy(_skillsPath, newPath);
            _skillsPath = newPath;
            LoadSkills();
        }
        ConfigChanged?.Invoke(_skillsPath);
    }

    public void DeleteProfile(string profileName)
    {
        if (profileName == "默认") return;
        
        var path = Path.Combine(_configPath, $"skills_{profileName}.json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        
        // 切换回默认方案
        SwitchProfile("默认");
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

    /// <summary>
    /// 导出配置到ZIP文件
    /// </summary>
    /// <param name="exportPath">导出路径</param>
    /// <param name="includeTemplates">是否包含模板图片</param>
    public void ExportConfig(string exportPath, bool includeTemplates = true)
    {
        lock (_configLock)
        {
            using var archive = System.IO.Compression.ZipFile.Open(exportPath, System.IO.Compression.ZipArchiveMode.Create);
            
            // 导出 appsettings.json
            if (File.Exists(_appSettingsPath))
                archive.CreateEntryFromFile(_appSettingsPath, "appsettings.json");
            
            // 导出当前技能配置
            if (File.Exists(_skillsPath))
                archive.CreateEntryFromFile(_skillsPath, Path.GetFileName(_skillsPath));
            
            // 导出模板图片
            if (includeTemplates && _skills != null)
            {
                var templateDir = Path.Combine(_configPath, "templates");
                if (Directory.Exists(templateDir))
                {
                    foreach (var file in Directory.GetFiles(templateDir, "*.*", SearchOption.AllDirectories))
                    {
                        var relativePath = Path.GetRelativePath(_configPath, file);
                        archive.CreateEntryFromFile(file, relativePath);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 从ZIP文件导入配置
    /// </summary>
    /// <param name="importPath">导入路径</param>
    /// <param name="overwrite">是否覆盖现有配置</param>
    /// <returns>导入结果信息</returns>
    public string ImportConfig(string importPath, bool overwrite = false)
    {
        if (!File.Exists(importPath))
            return "导入文件不存在";
        
        lock (_configLock)
        {
            try
            {
                using var archive = System.IO.Compression.ZipFile.OpenRead(importPath);
                int imported = 0;
                
                foreach (var entry in archive.Entries)
                {
                    var targetPath = Path.Combine(_configPath, entry.FullName);
                    var targetDir = Path.GetDirectoryName(targetPath);
                    
                    if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                        Directory.CreateDirectory(targetDir);
                    
                    if (File.Exists(targetPath) && !overwrite)
                    {
                        // 备份现有文件
                        var backupPath = $"{targetPath}.import_backup_{DateTime.Now:yyyyMMddHHmmss}";
                        File.Move(targetPath, backupPath);
                    }
                    
                    entry.ExtractToFile(targetPath, overwrite: true);
                    imported++;
                }
                
                // 重新加载配置
                LoadConfigs();
                
                return $"成功导入 {imported} 个文件";
            }
            catch (Exception ex)
            {
                return $"导入失败: {ex.Message}";
            }
        }
    }

    /// <summary>
    /// 导出单个方案
    /// </summary>
    public void ExportProfile(string profileName, string exportPath)
    {
        var skillsFile = profileName == "默认" 
            ? Path.Combine(_configPath, "skills.json")
            : Path.Combine(_configPath, $"skills_{profileName}.json");
        
        if (!File.Exists(skillsFile))
            return;
        
        lock (_configLock)
        {
            using var archive = System.IO.Compression.ZipFile.Open(exportPath, System.IO.Compression.ZipArchiveMode.Create);
            archive.CreateEntryFromFile(skillsFile, Path.GetFileName(skillsFile));
            
            // 读取技能配置，导出相关模板
            var json = File.ReadAllText(skillsFile);
            var skills = JsonSerializer.Deserialize<List<SkillConfig>>(json);
            if (skills != null)
            {
                foreach (var skill in skills)
                {
                    if (!string.IsNullOrEmpty(skill.TemplatePath) && File.Exists(skill.TemplatePath))
                    {
                        var relativePath = Path.GetRelativePath(_configPath, skill.TemplatePath);
                        try { archive.CreateEntryFromFile(skill.TemplatePath, relativePath); } catch { }
                    }
                    
                    foreach (var buff in skill.BuffRequirements)
                    {
                        if (!string.IsNullOrEmpty(buff.TemplatePath) && File.Exists(buff.TemplatePath))
                        {
                            var relativePath = Path.GetRelativePath(_configPath, buff.TemplatePath);
                            try { archive.CreateEntryFromFile(buff.TemplatePath, relativePath); } catch { }
                        }
                    }
                }
            }
        }
    }

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
