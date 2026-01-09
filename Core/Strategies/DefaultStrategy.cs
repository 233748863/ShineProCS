using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Services;
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
            
            // 检查自身MP要求（MP需高于指定值才能释放）
            if (skill.Config.MinMp > 0 && context.GameState.MpPercentage * 100 < skill.Config.MinMp) 
                continue;
            
            // 检查HP条件
            if (skill.Config.HpCheckTarget > 0 && skill.Config.HpThreshold > 0)
            {
                double hpToCheck = skill.Config.HpCheckTarget == 1 
                    ? context.GameState.CurrentHpPercent  // 自身HP
                    : context.GameState.TargetHpPercent;  // 目标HP
                
                // HP需低于阈值才释放
                if (hpToCheck > skill.Config.HpThreshold)
                    continue;
            }
            
            // 注意：Buff检查和联动触发在Engine层处理，这里只做基础筛选
            return skill;
        }
        return null;
    }
}

/// <summary>
/// 智能技能选择策略
/// 支持条件评估、技能组检查、优先级覆盖等高级功能
/// </summary>
[StrategyMetadata("smart", "智能策略", Description = "支持条件评估和优先级覆盖的智能策略", Version = "2.0.0")]
public class SmartStrategy : ISkillStrategy
{
    private readonly ConditionEvaluator? _conditionEvaluator;
    private readonly StateTracker? _stateTracker;
    
    /// <inheritdoc/>
    public string Name => "智能策略";
    
    /// <inheritdoc/>
    public string Description => "支持条件评估和优先级覆盖的智能策略";
    
    /// <inheritdoc/>
    public int Priority => 100;
    
    /// <summary>
    /// 默认构造函数（向后兼容）
    /// </summary>
    public SmartStrategy()
    {
        _conditionEvaluator = null;
        _stateTracker = null;
    }
    
    /// <summary>
    /// 带依赖注入的构造函数
    /// </summary>
    /// <param name="conditionEvaluator">条件评估器</param>
    /// <param name="stateTracker">状态追踪器</param>
    public SmartStrategy(ConditionEvaluator conditionEvaluator, StateTracker stateTracker)
    {
        _conditionEvaluator = conditionEvaluator;
        _stateTracker = stateTracker;
    }
    
    /// <inheritdoc/>
    public bool CanExecute(StrategyContext context) => context.LoopMode == "Smart";

    /// <inheritdoc/>
    public SkillRuntimeState? SelectSkill(StrategyContext context)
    {
        // 如果有条件评估器，使用增强的选择逻辑
        if (_conditionEvaluator != null && _stateTracker != null)
        {
            return SelectSkillWithConditionEvaluator(context);
        }
        
        // 否则使用原有的简化逻辑（向后兼容）
        return SelectSkillLegacy(context);
    }
    
    /// <summary>
    /// 使用ConditionEvaluator的增强技能选择逻辑
    /// 按顺序应用所有条件检查：Enabled、ConditionBuff、RequireState、MinMp、HpCondition
    /// 计算有效优先级：BasePriority + PriorityOverride + MpPriorityBoost + ComboBonus
    /// </summary>
    private SkillRuntimeState? SelectSkillWithConditionEvaluator(StrategyContext context)
    {
        // 1. 筛选出视觉就绪且可用的技能
        var candidateSkills = context.SkillStates
            .Where(s => s.IsVisuallyReady && s.IsAvailable)
            .ToList();
        
        if (!candidateSkills.Any()) return null;
        
        // 2. 使用ConditionEvaluator评估所有条件
        var validSkills = candidateSkills
            .Where(s => _conditionEvaluator!.EvaluateSkillConditions(s, context, _stateTracker!))
            .ToList();
        
        if (!validSkills.Any()) return null;
        
        // 3. 计算有效优先级并排序
        // 按有效优先级降序排列，平局时按配置顺序（在列表中的位置）
        var rankedSkills = validSkills
            .Select((skill, index) => new
            {
                Skill = skill,
                EffectivePriority = _conditionEvaluator!.CalculateEffectivePriority(skill, context, _stateTracker!),
                ConfigOrder = context.SkillStates.IndexOf(skill)
            })
            .OrderByDescending(x => x.EffectivePriority)
            .ThenBy(x => x.ConfigOrder)
            .ToList();
        
        // 4. 选择有效优先级最高的技能
        return rankedSkills.FirstOrDefault()?.Skill;
    }
    
    /// <summary>
    /// 原有的简化技能选择逻辑（向后兼容）
    /// </summary>
    private SkillRuntimeState? SelectSkillLegacy(StrategyContext context)
    {
        var mpPercent = context.GameState.MpPercentage * 100;
        
        // 智能策略：优先选择有联动配置的技能（联动技能通常优先级更高）
        // 1. 先筛选出所有可用技能
        var availableSkills = context.SkillStates
            .Where(s => s.Config.Enabled && s.IsVisuallyReady && s.IsAvailable)
            .Where(s => s.Config.MinMp <= 0 || mpPercent >= s.Config.MinMp)
            .Where(s => {
                // HP条件检查
                if (s.Config.HpCheckTarget <= 0 || s.Config.HpThreshold <= 0)
                    return true;
                double hpToCheck = s.Config.HpCheckTarget == 1 
                    ? context.GameState.CurrentHpPercent 
                    : context.GameState.TargetHpPercent;
                return hpToCheck <= s.Config.HpThreshold;
            })
            .ToList();
        
        if (!availableSkills.Any()) return null;
        
        // 2. 联动技能优先级提升：有PreCastKeyCode配置的技能额外加权
        // 从配置获取优先级加成，默认为50
        var comboBonus = context.Settings?.ComboSkillPriorityBonus ?? 50;
        
        // 这样可以确保联动技能（如赤芍寒香）在条件满足时优先被选中
        var selectedSkill = availableSkills
            .OrderByDescending(s => s.Config.Priority + (s.Config.PreCastKeyCode > 0 ? comboBonus : 0))
            .FirstOrDefault();
        
        return selectedSkill;
    }
}
