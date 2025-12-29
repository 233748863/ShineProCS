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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PresetManager(string? presetDir = null)
    {
        _presetDir = presetDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "presets");
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
