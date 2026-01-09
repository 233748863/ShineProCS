using CommunityToolkit.Mvvm.ComponentModel;

namespace ShineProCS.Models;

/// <summary>
/// 技能组配置模型
/// 用于定义一组相关联的技能，共享某些条件或状态
/// </summary>
public partial class SkillGroupConfig : ObservableObject
{
    /// <summary>
    /// 技能组名称
    /// </summary>
    [ObservableProperty] private string _name = "";

    /// <summary>
    /// 条件Buff名称
    /// 当此Buff存在时，组内技能才会被评估
    /// </summary>
    [ObservableProperty] private string _conditionBuff = "";

    /// <summary>
    /// 是否启用此技能组
    /// </summary>
    [ObservableProperty] private bool _enabled = true;
}
