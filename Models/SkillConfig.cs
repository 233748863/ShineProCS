using CommunityToolkit.Mvvm.ComponentModel;

namespace ShineProCS.Models;

/// <summary>
/// 技能施法类型
/// </summary>
public enum SkillCastType
{
    /// <summary>
    /// 瞬发技能 - 按下即释放，无需等待
    /// </summary>
    Instant = 0,
    
    /// <summary>
    /// 正读条技能 - 按下后开始读条，读条完成后释放
    /// </summary>
    CastTime = 1,
    
    /// <summary>
    /// 引导技能（倒读条） - 按下后持续引导，可提前打断
    /// </summary>
    Channeled = 2
}

/// <summary>
/// 技能配置模型
/// 定义单个技能的所有可配置属性
/// </summary>
public partial class SkillConfig : ObservableObject
{
    /// <summary>
    /// 技能名称，用于显示和日志
    /// </summary>
    [ObservableProperty] private string _name = "";
    
    /// <summary>
    /// 技能按键码（虚拟键码 VK_*）
    /// </summary>
    [ObservableProperty] private int _keyCode;
    
    /// <summary>
    /// 技能优先级，数值越大优先级越高
    /// </summary>
    [ObservableProperty] private int _priority;
    
    /// <summary>
    /// 是否启用此技能
    /// </summary>
    [ObservableProperty] private bool _enabled = true;
    
    /// <summary>
    /// 技能图标检测区域 [X, Y, Width, Height]
    /// </summary>
    [ObservableProperty] private int[] _iconRegion = [0, 0, 0, 0];
    
    /// <summary>
    /// 技能图标模板图片路径（用于模板匹配）
    /// </summary>
    [ObservableProperty] private string _templatePath = "";
    
    /// <summary>
    /// 模板匹配相似度阈值 (0.0-1.0)
    /// </summary>
    [ObservableProperty] private double _similarityThreshold = 0.8;
    
    /// <summary>
    /// 释放此技能所需的最低HP百分比 (0-100)
    /// </summary>
    [ObservableProperty] private double _minHp;
    
    /// <summary>
    /// 释放此技能所需的最低MP百分比 (0-100)
    /// </summary>
    [ObservableProperty] private double _minMp;
    
    /// <summary>
    /// 是否需要有目标才能释放
    /// </summary>
    [ObservableProperty] private bool _requireTarget;
    
    /// <summary>
    /// 技能冷却时间（秒）- 作为最大等待时间参考
    /// </summary>
    [ObservableProperty] private double _cooldown;
    
    /// <summary>
    /// 技能施法类型
    /// </summary>
    [ObservableProperty] private SkillCastType _castType = SkillCastType.Instant;
    
    /// <summary>
    /// 施法/引导时间（毫秒）- 作为最大等待时间
    /// 正读条：最大读条时间
    /// 引导：最大引导时间
    /// </summary>
    [ObservableProperty] private int _castDuration;
    
    /// <summary>
    /// 是否使用视觉检测判断读条/引导结束
    /// true = 检测到特定状态时结束
    /// false = 等待固定时间
    /// </summary>
    [ObservableProperty] private bool _useCastEndDetection;
    
    /// <summary>
    /// 读条/引导结束检测模式
    /// 0 = 检测读条条消失（点色变化）
    /// 1 = 检测技能图标变化（模板匹配）
    /// </summary>
    [ObservableProperty] private int _castEndDetectionMode;
    
    /// <summary>
    /// 读条结束检测点 [X, Y]
    /// </summary>
    [ObservableProperty] private int[] _castEndDetectionPoint = [0, 0];
    
    /// <summary>
    /// 读条结束目标颜色 [R, G, B] - 读条条消失后的背景色
    /// </summary>
    [ObservableProperty] private int[] _castEndColor = [0, 0, 0];
    
    /// <summary>
    /// 读条结束颜色容差
    /// </summary>
    [ObservableProperty] private int _castEndColorTolerance = 30;
    
    /// <summary>
    /// 引导打断时间（毫秒）
    /// 仅对引导技能有效，表示引导多久后打断
    /// 0 = 不打断，完整引导
    /// </summary>
    [ObservableProperty] private int _channelInterruptTime;
    
    /// <summary>
    /// 引导打断模式
    /// 0 = 固定时间打断
    /// 1 = 检测点色打断（当指定位置颜色变化时打断）
    /// </summary>
    [ObservableProperty] private int _channelInterruptMode;
    
    /// <summary>
    /// 引导打断检测点 [X, Y]
    /// 用于点色检测模式
    /// </summary>
    [ObservableProperty] private int[] _channelInterruptPoint = [0, 0];
    
    /// <summary>
    /// 引导打断目标颜色 [R, G, B]
    /// 当检测点颜色接近此颜色时打断
    /// </summary>
    [ObservableProperty] private int[] _channelInterruptColor = [255, 255, 255];
    
    /// <summary>
    /// 颜色匹配容差 (0-255)
    /// </summary>
    [ObservableProperty] private int _channelColorTolerance = 30;
    
    /// <summary>
    /// 前置技能按键码（联动技能）
    /// 当Buff条件不满足时，先释放此按键对应的技能
    /// </summary>
    [ObservableProperty] private int _preCastKeyCode;
    
    /// <summary>
    /// 前置技能触发条件Buff名称（引用Buff库中的Buff）
    /// </summary>
    [ObservableProperty] private string _preCastConditionBuff = "";
    
    /// <summary>
    /// 连招延迟（毫秒）
    /// 前置技能释放后等待的时间
    /// </summary>
    [ObservableProperty] private int _comboDelay = 100;
    
    /// <summary>
    /// 是否显示释放条件配置面板（UI状态）
    /// </summary>
    [ObservableProperty] private bool _showReleaseCondition;
    
    /// <summary>
    /// 是否显示联动配置面板（UI状态）
    /// </summary>
    [ObservableProperty] private bool _showComboConfig;
    
    /// <summary>
    /// 检查是否有有效的释放条件配置
    /// </summary>
    public bool HasReleaseCondition => MinHp > 0 || MinMp > 0;
    
    /// <summary>
    /// 检查是否有有效的联动配置
    /// </summary>
    public bool HasComboConfig => PreCastKeyCode > 0 || !string.IsNullOrEmpty(PreCastConditionBuff);
    
    /// <summary>
    /// 检查是否有施法时间配置
    /// </summary>
    public bool HasCastConfig => CastType != SkillCastType.Instant;
    
    /// <summary>
    /// 获取实际等待时间（毫秒）
    /// </summary>
    public int GetEffectiveWaitTime()
    {
        return CastType switch
        {
            SkillCastType.Instant => 0,
            SkillCastType.CastTime => CastDuration,
            SkillCastType.Channeled => ChannelInterruptTime > 0 ? ChannelInterruptTime : CastDuration,
            _ => 0
        };
    }
}
