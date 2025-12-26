using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShineProCS.Models;

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
    /// 技能冷却时间（秒）
    /// </summary>
    [ObservableProperty] private double _cooldown;
    
    /// <summary>
    /// Buff依赖列表
    /// </summary>
    [ObservableProperty] private ObservableCollection<BuffRequirement> _buffRequirements = [];
    
    /// <summary>
    /// 前置技能按键码（联动技能）
    /// 当Buff条件不满足时，先释放此按键对应的技能
    /// </summary>
    [ObservableProperty] private int _preCastKeyCode;
    
    /// <summary>
    /// 前置技能触发条件Buff名称
    /// </summary>
    [ObservableProperty] private string _preCastConditionBuff = "";
    
    /// <summary>
    /// 连招延迟（毫秒）
    /// 前置技能释放后等待的时间
    /// </summary>
    [ObservableProperty] private int _comboDelay = 100;
}

/// <summary>
/// Buff依赖配置
/// 定义技能释放所需的Buff条件
/// </summary>
public partial class BuffRequirement : ObservableObject
{
    /// <summary>
    /// Buff名称
    /// </summary>
    [ObservableProperty] private string _name = "";
    
    /// <summary>
    /// Buff图标检测区域 [X, Y, Width, Height]
    /// </summary>
    [ObservableProperty] private int[] _iconRegion = [0, 0, 0, 0];
    
    /// <summary>
    /// Buff图标模板图片路径
    /// </summary>
    [ObservableProperty] private string _templatePath = "";
    
    /// <summary>
    /// 模板匹配相似度阈值 (0.0-1.0)
    /// </summary>
    [ObservableProperty] private double _similarityThreshold = 0.8;
    
    /// <summary>
    /// 是否为Debuff（负面效果）
    /// </summary>
    [ObservableProperty] private bool _isDebuff;
    
    /// <summary>
    /// 是否要求Buff存在
    /// true: 需要Buff存在才能释放技能
    /// false: 需要Buff不存在才能释放技能
    /// </summary>
    [ObservableProperty] private bool _isRequired = true;
}
