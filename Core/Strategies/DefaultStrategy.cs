using ShineProCS.Core.Interfaces;
using ShineProCS.Models;

namespace ShineProCS.Core.Strategies;

/// <summary>
/// 默认技能选择策略
/// 按照技能列表顺序选择第一个可用的技能
/// </summary>
[StrategyMetadata("default", "默认策略", Description = "按顺序选择第一个可用技能", Version = "1.0.0")]
public class DefaultStrategy : ISkillStrategy
{
    /// <inheritdoc/>
    public string Name => "默认策略";
    
    /// <inheritdoc/>
    public string Description => "按顺序选择第一个可用技能";
    
    /// <inheritdoc/>
    public int Priority => 0;
    
    /// <inheritdoc/>
    public bool CanExecute(StrategyContext context) => true;

    /// <inheritdoc/>
    public SkillRuntimeState? SelectSkill(StrategyContext context)
    {
        foreach (var skill in context.SkillStates)
        {
            // 跳过禁用、不可用、视觉未就绪的技能
            if (!skill.Config.Enabled || !skill.IsAvailable || !skill.IsVisuallyReady) 
                continue;
            
            // 检查HP要求
            if (skill.Config.MinHp > 0 && context.GameState.HpPercentage * 100 < skill.Config.MinHp) 
                continue;
            
            // 检查MP要求
            if (skill.Config.MinMp > 0 && context.GameState.MpPercentage * 100 < skill.Config.MinMp) 
                continue;
            
            // 注意：Buff检查和联动触发在Engine层处理，这里只做基础筛选
            return skill;
        }
        return null;
    }
}

/// <summary>
/// 智能技能选择策略
/// 优先选择有联动配置的技能，适用于需要技能连招的场景
/// </summary>
[StrategyMetadata("smart", "智能策略", Description = "优先选择有联动配置的技能", Version = "1.0.0")]
public class SmartStrategy : ISkillStrategy
{
    /// <inheritdoc/>
    public string Name => "智能策略";
    
    /// <inheritdoc/>
    public string Description => "优先选择有联动配置的技能";
    
    /// <inheritdoc/>
    public int Priority => 100;
    
    /// <inheritdoc/>
    public bool CanExecute(StrategyContext context) => context.LoopMode == "Smart";

    /// <inheritdoc/>
    public SkillRuntimeState? SelectSkill(StrategyContext context)
    {
        var hpPercent = context.GameState.HpPercentage * 100;
        var mpPercent = context.GameState.MpPercentage * 100;
        
        // 智能策略：优先选择有联动配置的技能（联动技能通常优先级更高）
        // 1. 先筛选出所有可用技能
        var availableSkills = context.SkillStates
            .Where(s => s.Config.Enabled && s.IsVisuallyReady && s.IsAvailable)
            .Where(s => s.Config.MinHp <= 0 || hpPercent >= s.Config.MinHp)
            .Where(s => s.Config.MinMp <= 0 || mpPercent >= s.Config.MinMp)
            .ToList();
        
        if (!availableSkills.Any()) return null;
        
        // 2. 联动技能优先级提升：有PreCastKeyCode配置的技能额外加权
        // 这样可以确保联动技能（如赤芍寒香）在条件满足时优先被选中
        var selectedSkill = availableSkills
            .OrderByDescending(s => s.Config.Priority + (s.Config.PreCastKeyCode > 0 ? 100 : 0))
            .FirstOrDefault();
        
        return selectedSkill;
    }
}
