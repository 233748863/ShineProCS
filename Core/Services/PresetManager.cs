using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using ShineProCS.Models;

namespace ShineProCS.Core.Services;

/// <summary>
/// 配置预设管理器
/// 提供预置的技能配置方案，帮助新用户快速上手
/// </summary>
public class PresetManager
{
    private readonly string _presetDir;
    private readonly string _configPresetDir;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PresetManager(string? presetDir = null)
    {
        _presetDir = presetDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "presets");
        _configPresetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "presets");
        EnsurePresetsExist();
    }

    /// <summary>
    /// 获取所有可用的预设
    /// </summary>
    public List<PresetInfo> GetAvailablePresets()
    {
        var presets = new List<PresetInfo>();
        
        // 添加内置预设
        presets.AddRange(GetBuiltInPresets());
        
        // 添加config/presets目录下的预设
        if (Directory.Exists(_configPresetDir))
        {
            foreach (var file in Directory.GetFiles(_configPresetDir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var preset = JsonSerializer.Deserialize<PresetData>(json, JsonOptions);
                    if (preset?.Info != null)
                    {
                        preset.Info.FilePath = file;
                        preset.Info.IsBuiltIn = false;
                        presets.Add(preset.Info);
                    }
                }
                catch { }
            }
        }
        
        // 添加用户自定义预设
        if (Directory.Exists(_presetDir))
        {
            foreach (var file in Directory.GetFiles(_presetDir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var preset = JsonSerializer.Deserialize<PresetInfo>(json, JsonOptions);
                    if (preset != null)
                    {
                        preset.FilePath = file;
                        preset.IsBuiltIn = false;
                        presets.Add(preset);
                    }
                }
                catch { }
            }
        }
        
        return presets;
    }

    /// <summary>
    /// 加载预设的技能配置
    /// </summary>
    public List<SkillConfig>? LoadPreset(PresetInfo preset)
    {
        if (preset.IsBuiltIn)
        {
            return GetBuiltInPresetSkills(preset.Id);
        }
        
        if (string.IsNullOrEmpty(preset.FilePath) || !File.Exists(preset.FilePath))
            return null;
        
        try
        {
            var json = File.ReadAllText(preset.FilePath);
            var data = JsonSerializer.Deserialize<PresetData>(json, JsonOptions);
            return data?.Skills;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 加载完整预设配置（包括技能、Buff库、技能组和初始状态）
    /// </summary>
    /// <param name="preset">预设信息</param>
    /// <returns>完整预设数据，如果加载失败返回null</returns>
    public FullPresetData? LoadFullPreset(PresetInfo preset)
    {
        if (preset.IsBuiltIn)
        {
            // 内置预设只返回技能配置
            var skills = GetBuiltInPresetSkills(preset.Id);
            if (skills == null) return null;
            return new FullPresetData { Skills = skills };
        }
        
        if (string.IsNullOrEmpty(preset.FilePath) || !File.Exists(preset.FilePath))
            return null;
        
        try
        {
            var json = File.ReadAllText(preset.FilePath);
            var data = JsonSerializer.Deserialize<FullPresetData>(json, JsonOptions);
            return data;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 从文件路径加载完整预设配置
    /// </summary>
    /// <param name="filePath">预设文件路径</param>
    /// <returns>完整预设数据，如果加载失败返回null</returns>
    public FullPresetData? LoadFullPresetFromFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return null;
        
        try
        {
            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<FullPresetData>(json, JsonOptions);
            return data;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 应用完整预设到AppSettings和技能列表
    /// </summary>
    /// <param name="presetData">预设数据</param>
    /// <param name="appSettings">应用设置</param>
    /// <param name="skills">技能列表</param>
    /// <param name="stateTracker">状态追踪器（可选）</param>
    public void ApplyFullPreset(
        FullPresetData presetData, 
        AppSettings appSettings, 
        ObservableCollection<SkillConfig> skills,
        StateTracker? stateTracker = null)
    {
        // 应用技能配置
        skills.Clear();
        foreach (var skill in presetData.Skills)
        {
            skills.Add(skill);
        }
        
        // 应用Buff库配置
        if (presetData.BuffLibrary != null && presetData.BuffLibrary.Count > 0)
        {
            foreach (var buff in presetData.BuffLibrary)
            {
                // 检查是否已存在同名Buff
                var existing = appSettings.BuffLibrary.FirstOrDefault(b => b.Name == buff.Name);
                if (existing != null)
                {
                    // 更新现有Buff
                    existing.DisplayName = buff.DisplayName;
                    existing.IconRegion = buff.IconRegion;
                    existing.TemplatePath = buff.TemplatePath;
                    existing.SimilarityThreshold = buff.SimilarityThreshold;
                    existing.IsDebuff = buff.IsDebuff;
                    existing.Description = buff.Description;
                    existing.Enabled = buff.Enabled;
                }
                else
                {
                    // 添加新Buff
                    appSettings.BuffLibrary.Add(buff);
                }
            }
        }
        
        // 应用技能组配置
        if (presetData.SkillGroups != null && presetData.SkillGroups.Count > 0)
        {
            foreach (var group in presetData.SkillGroups)
            {
                // 检查是否已存在同名技能组
                var existing = appSettings.SkillGroups.FirstOrDefault(g => g.Name == group.Name);
                if (existing != null)
                {
                    // 更新现有技能组
                    existing.ConditionBuff = group.ConditionBuff;
                    existing.Enabled = group.Enabled;
                }
                else
                {
                    // 添加新技能组
                    appSettings.SkillGroups.Add(group);
                }
            }
        }
        
        // 应用初始状态
        if (stateTracker != null && presetData.InitialStates != null)
        {
            foreach (var state in presetData.InitialStates)
            {
                stateTracker.SetState(state.Key, state.Value);
            }
        }
    }

    /// <summary>
    /// 保存当前配置为预设
    /// </summary>
    public bool SaveAsPreset(string name, string description, List<SkillConfig> skills)
    {
        try
        {
            if (!Directory.Exists(_presetDir))
                Directory.CreateDirectory(_presetDir);
            
            var preset = new PresetData
            {
                Info = new PresetInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    Description = description,
                    Author = Environment.UserName,
                    CreatedAt = DateTime.Now,
                    IsBuiltIn = false
                },
                Skills = skills
            };
            
            var fileName = SanitizeFileName(name) + ".json";
            var filePath = Path.Combine(_presetDir, fileName);
            
            var json = JsonSerializer.Serialize(preset, JsonOptions);
            File.WriteAllText(filePath, json);
            
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 删除用户预设
    /// </summary>
    public bool DeletePreset(PresetInfo preset)
    {
        if (preset.IsBuiltIn || string.IsNullOrEmpty(preset.FilePath))
            return false;
        
        try
        {
            if (File.Exists(preset.FilePath))
            {
                File.Delete(preset.FilePath);
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
    /// 确保预设目录存在
    /// </summary>
    private void EnsurePresetsExist()
    {
        if (!Directory.Exists(_presetDir))
            Directory.CreateDirectory(_presetDir);
    }

    /// <summary>
    /// 获取内置预设列表
    /// </summary>
    private static List<PresetInfo> GetBuiltInPresets()
    {
        return
        [
            new PresetInfo
            {
                Id = "basic-rotation",
                Name = "基础循环",
                Description = "基础技能循环示例，适合入门学习",
                Author = "ShineProCS",
                IsBuiltIn = true,
                Tags = ["通用", "入门"]
            },
            new PresetInfo
            {
                Id = "buff-combo",
                Name = "Buff联动示例",
                Description = "展示如何配置Buff条件触发的技能联动",
                Author = "ShineProCS",
                IsBuiltIn = true,
                Tags = ["通用", "进阶"]
            },
            new PresetInfo
            {
                Id = "suke-preset",
                Name = "素柯门派技能模板",
                Description = "素柯门派完整技能配置，包含气劲状态检测、七情和合状态追踪等高级功能",
                Author = "ShineProCS",
                IsBuiltIn = true,
                Tags = ["素柯", "门派", "完整配置"]
            },
            new PresetInfo
            {
                Id = "empty",
                Name = "空白模板",
                Description = "从零开始配置，适合自定义需求",
                Author = "ShineProCS",
                IsBuiltIn = true,
                Tags = ["通用"]
            }
        ];
    }

    /// <summary>
    /// 获取内置预设的技能配置
    /// </summary>
    private static List<SkillConfig>? GetBuiltInPresetSkills(string presetId)
    {
        return presetId switch
        {
            "basic-rotation" => GetBasicRotationPreset(),
            "buff-combo" => GetBuffComboPreset(),
            "suke-preset" => GetSukePreset(),
            "empty" => [],
            _ => null
        };
    }

    /// <summary>
    /// 基础循环预设
    /// </summary>
    private static List<SkillConfig> GetBasicRotationPreset()
    {
        return
        [
            new SkillConfig { Name = "技能1", KeyCode = 49, Priority = 100, Enabled = true },
            new SkillConfig { Name = "技能2", KeyCode = 50, Priority = 90, Enabled = true },
            new SkillConfig { Name = "技能3", KeyCode = 51, Priority = 80, Enabled = true },
            new SkillConfig { Name = "技能4", KeyCode = 52, Priority = 70, Enabled = true },
            new SkillConfig { Name = "技能5", KeyCode = 53, Priority = 60, Enabled = true }
        ];
    }

    /// <summary>
    /// Buff联动示例预设
    /// </summary>
    private static List<SkillConfig> GetBuffComboPreset()
    {
        return
        [
            new SkillConfig { Name = "主技能", KeyCode = 49, Priority = 100, Enabled = true },
            new SkillConfig { Name = "辅助技能", KeyCode = 50, Priority = 90, Enabled = true },
            new SkillConfig 
            { 
                Name = "联动技能", 
                KeyCode = 81, 
                Priority = 80, 
                Enabled = true,
                PreCastKeyCode = 87,
                PreCastConditionBuff = "增益Buff",
                ComboDelay = 150
            },
            new SkillConfig { Name = "填充技能1", KeyCode = 51, Priority = 70, Enabled = true },
            new SkillConfig { Name = "填充技能2", KeyCode = 52, Priority = 60, Enabled = true }
        ];
    }

    /// <summary>
    /// 素柯门派技能预设
    /// </summary>
    private static List<SkillConfig> GetSukePreset()
    {
        return
        [
            // 青川濯莲 - 通用最高优先级
            new SkillConfig 
            { 
                Name = "青川濯莲", 
                KeyCode = 49, 
                Priority = 100, 
                Enabled = true 
            },
            // 逐云寒蕊 - 需要素柯状态
            new SkillConfig 
            { 
                Name = "逐云寒蕊", 
                KeyCode = 50, 
                Priority = 90, 
                Enabled = true,
                SkillGroup = "素柯技能组"
            },
            // 当归四逆 - 普通技能，无素柯检测
            new SkillConfig 
            { 
                Name = "当归四逆", 
                KeyCode = 51, 
                Priority = 80, 
                Enabled = true 
            },
            // 银光照雪 - 需要素柯状态
            new SkillConfig 
            { 
                Name = "银光照雪", 
                KeyCode = 52, 
                Priority = 70, 
                Enabled = true,
                SkillGroup = "素柯技能组"
            },
            // 赤芍寒香 - 有前置技能和MP加成
            new SkillConfig 
            { 
                Name = "赤芍寒香", 
                KeyCode = 53, 
                Priority = 60, 
                Enabled = true,
                PriorityOverrideCondition = "千枝气劲",
                PriorityOverrideValue = 180,
                MpPriorityBoost = 50,
                MpThresholdForBoost = 30,
                PreCastSkillName = "千枝绽蕊",
                SkillGroup = "素柯技能组"
            },
            // 绿野蔓生 - 需要素柯状态
            new SkillConfig 
            { 
                Name = "绿野蔓生", 
                KeyCode = 54, 
                Priority = 50, 
                Enabled = true,
                SkillGroup = "素柯技能组"
            },
            // 白芷含芳 - 需要素柯状态
            new SkillConfig 
            { 
                Name = "白芷含芳", 
                KeyCode = 55, 
                Priority = 40, 
                Enabled = true,
                SkillGroup = "素柯技能组"
            },
            // 七情和合 - 需要千枝气劲和状态追踪
            new SkillConfig 
            { 
                Name = "七情和合", 
                KeyCode = 56, 
                Priority = 200, 
                Enabled = true,
                ConditionBuff = "千枝气劲",
                ClearStateOnCast = "七情和合启用",
                RequireState = "七情和合启用"
            },
            // 千枝绽蕊 - 需要千枝气劲
            new SkillConfig 
            { 
                Name = "千枝绽蕊", 
                KeyCode = 57, 
                Priority = 150, 
                Enabled = true,
                ConditionBuff = "千枝气劲",
                PriorityOverrideCondition = "千枝气劲",
                PriorityOverrideValue = 170
            }
        ];
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }
}

/// <summary>
/// 预设信息
/// </summary>
public class PresetInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsBuiltIn { get; set; }
    public List<string> Tags { get; set; } = [];
    public string? FilePath { get; set; }
}

/// <summary>
/// 预设数据（包含信息和技能配置）
/// </summary>
public class PresetData
{
    public PresetInfo Info { get; set; } = new();
    public List<SkillConfig> Skills { get; set; } = [];
}

/// <summary>
/// 完整预设数据（包含技能、Buff库、技能组和初始状态）
/// </summary>
public class FullPresetData
{
    /// <summary>
    /// 预设信息
    /// </summary>
    public PresetInfo Info { get; set; } = new();
    
    /// <summary>
    /// 技能配置列表
    /// </summary>
    public List<SkillConfig> Skills { get; set; } = [];
    
    /// <summary>
    /// Buff库配置
    /// </summary>
    public List<BuffConfig> BuffLibrary { get; set; } = [];
    
    /// <summary>
    /// 技能组配置
    /// </summary>
    public List<SkillGroupConfig> SkillGroups { get; set; } = [];
    
    /// <summary>
    /// 初始状态配置（状态名称 -> 初始值）
    /// </summary>
    public Dictionary<string, bool> InitialStates { get; set; } = [];
}
