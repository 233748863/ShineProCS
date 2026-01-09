using ShineProCS.Core.Interfaces;
using ShineProCS.Models;

namespace ShineProCS.Core.Services;

/// <summary>
/// 条件评估器
/// 负责评估技能的释放条件和计算有效优先级
/// </summary>
public class ConditionEvaluator
{
    private readonly IBuffChecker _buffChecker;

    /// <summary>
    /// 创建条件评估器实例
    /// </summary>
    /// <param name="buffChecker">Buff检测器，用于检测Buff状态</param>
    public ConditionEvaluator(IBuffChecker buffChecker)
    {
        _buffChecker = buffChecker;
    }

    /// <summary>
    /// 创建条件评估器实例（使用StateDetector）
    /// </summary>
    /// <param name="stateDetector">状态检测器，用于检测Buff状态</param>
    public ConditionEvaluator(StateDetector stateDetector)
    {
        _buffChecker = new StateDetectorBuffChecker(stateDetector);
    }

    /// <summary>
    /// 评估技能是否满足所有释放条件
    /// 按顺序检查：Enabled、ConditionBuff、RequireState、MinMp、HpCondition
    /// </summary>
    /// <param name="skill">技能运行时状态</param>
    /// <param name="context">策略上下文</param>
    /// <param name="stateTracker">状态追踪器</param>
    /// <returns>技能是否满足所有条件</returns>
    public bool EvaluateSkillConditions(
        SkillRuntimeState skill,
        StrategyContext context,
        StateTracker stateTracker)
    {
        var config = skill.Config;

        // 1. 检查技能是否启用
        if (!config.Enabled)
            return false;

        // 2. 检查技能组条件
        if (!EvaluateSkillGroupCondition(config, context))
            return false;

        // 3. 检查条件Buff（ConditionBuff）
        if (!EvaluateConditionBuff(config))
            return false;

        // 4. 检查状态要求（RequireState）
        if (!EvaluateRequireState(config, stateTracker))
            return false;

        // 5. 检查MP条件（MinMp）
        if (!EvaluateMinMpCondition(config, context))
            return false;

        // 6. 检查HP条件（HpCheckTarget + HpThreshold）
        if (!EvaluateHpCondition(config, context))
            return false;

        return true;
    }

    /// <summary>
    /// 计算技能的有效优先级
    /// 公式：BasePriority + PriorityOverride + MpPriorityBoost + ComboBonus
    /// </summary>
    /// <param name="skill">技能运行时状态</param>
    /// <param name="context">策略上下文</param>
    /// <param name="stateTracker">状态追踪器</param>
    /// <returns>计算后的有效优先级</returns>
    public int CalculateEffectivePriority(
        SkillRuntimeState skill,
        StrategyContext context,
        StateTracker stateTracker)
    {
        var config = skill.Config;
        var effectivePriority = config.Priority;

        // 1. 应用优先级覆盖（PriorityOverride）
        effectivePriority = ApplyPriorityOverride(config, effectivePriority);

        // 2. 应用MP优先级加成（MpPriorityBoost）
        effectivePriority = ApplyMpPriorityBoost(config, context, effectivePriority);

        // 3. 应用联动技能加成（ComboBonus）
        effectivePriority = ApplyComboBonus(config, context, effectivePriority);

        return effectivePriority;
    }

    #region 条件评估方法

    /// <summary>
    /// 评估技能组条件
    /// </summary>
    private bool EvaluateSkillGroupCondition(SkillConfig config, StrategyContext context)
    {
        // 如果技能没有指定技能组，直接通过
        if (string.IsNullOrEmpty(config.SkillGroup))
            return true;

        // 查找技能组配置
        var skillGroup = context.Settings?.SkillGroups
            .FirstOrDefault(g => g.Name == config.SkillGroup);

        // 如果技能组不存在，忽略组条件（视为通过）
        if (skillGroup == null)
            return true;

        // 如果技能组被禁用，跳过该组所有技能
        if (!skillGroup.Enabled)
            return false;

        // 如果技能组有条件Buff，检查Buff是否存在
        if (!string.IsNullOrEmpty(skillGroup.ConditionBuff))
        {
            return _buffChecker.CheckBuffExists(skillGroup.ConditionBuff);
        }

        return true;
    }

    /// <summary>
    /// 评估条件Buff
    /// 当ConditionBuff不存在时，技能应被跳过
    /// </summary>
    private bool EvaluateConditionBuff(SkillConfig config)
    {
        // 如果没有配置条件Buff，直接通过
        if (string.IsNullOrEmpty(config.ConditionBuff))
            return true;

        // 检查Buff是否存在
        return _buffChecker.CheckBuffExists(config.ConditionBuff);
    }

    /// <summary>
    /// 评估状态要求
    /// 只有当RequireState为true时才能选择该技能
    /// </summary>
    private static bool EvaluateRequireState(SkillConfig config, StateTracker stateTracker)
    {
        // 如果没有配置状态要求，直接通过
        if (string.IsNullOrEmpty(config.RequireState))
            return true;

        // 检查状态是否为true
        return stateTracker.GetState(config.RequireState);
    }

    /// <summary>
    /// 评估MP条件
    /// 当前MP需高于MinMp才能释放
    /// </summary>
    private static bool EvaluateMinMpCondition(SkillConfig config, StrategyContext context)
    {
        // 如果没有配置MP条件，直接通过
        if (config.MinMp <= 0)
            return true;

        // 检查当前MP是否满足条件
        return context.GameState.CurrentMpPercent >= config.MinMp;
    }

    /// <summary>
    /// 评估HP条件
    /// 根据HpCheckTarget检查自身或目标HP
    /// </summary>
    private static bool EvaluateHpCondition(SkillConfig config, StrategyContext context)
    {
        // 如果没有配置HP检测，直接通过
        if (config.HpCheckTarget <= 0 || config.HpThreshold <= 0)
            return true;

        // 根据检测对象获取HP值
        var hpPercent = config.HpCheckTarget switch
        {
            1 => context.GameState.CurrentHpPercent, // 自身HP
            2 => context.GameState.TargetHpPercent,  // 目标HP
            _ => 100.0
        };

        // HP需低于阈值才释放
        return hpPercent <= config.HpThreshold;
    }

    #endregion

    #region 优先级计算方法

    /// <summary>
    /// 应用优先级覆盖
    /// 当PriorityOverrideCondition满足时，使用PriorityOverrideValue
    /// </summary>
    private int ApplyPriorityOverride(SkillConfig config, int currentPriority)
    {
        // 如果没有配置优先级覆盖条件，返回当前优先级
        if (string.IsNullOrEmpty(config.PriorityOverrideCondition))
            return currentPriority;

        // 检查覆盖条件是否满足
        if (_buffChecker.CheckBuffExists(config.PriorityOverrideCondition))
        {
            // 条件满足，使用覆盖值
            return config.PriorityOverrideValue;
        }

        // 条件不满足，返回当前优先级
        return currentPriority;
    }

    /// <summary>
    /// 应用MP优先级加成
    /// 当MP高于MpThresholdForBoost时，将MpPriorityBoost加到优先级上
    /// </summary>
    private static int ApplyMpPriorityBoost(SkillConfig config, StrategyContext context, int currentPriority)
    {
        // 如果没有配置MP加成，返回当前优先级
        if (config.MpPriorityBoost <= 0)
            return currentPriority;

        // 检查MP是否高于阈值
        if (context.GameState.CurrentMpPercent > config.MpThresholdForBoost)
        {
            // MP高于阈值，应用加成
            return currentPriority + config.MpPriorityBoost;
        }

        // MP低于阈值，不加成
        return currentPriority;
    }

    /// <summary>
    /// 应用联动技能加成
    /// 当技能有前置技能配置时，可能获得额外优先级加成
    /// </summary>
    private static int ApplyComboBonus(SkillConfig config, StrategyContext context, int currentPriority)
    {
        // 如果没有配置前置技能，不加成
        if (config.PreCastKeyCode <= 0 && string.IsNullOrEmpty(config.PreCastSkillName))
            return currentPriority;

        // 从设置中获取联动加成值
        var comboBonus = context.Settings?.ComboSkillPriorityBonus ?? 0;
        
        return currentPriority + comboBonus;
    }

    #endregion
}


/// <summary>
/// StateDetector的IBuffChecker适配器
/// </summary>
internal class StateDetectorBuffChecker : IBuffChecker
{
    private readonly StateDetector _stateDetector;

    public StateDetectorBuffChecker(StateDetector stateDetector)
    {
        _stateDetector = stateDetector;
    }

    public bool CheckBuffExists(string buffName)
    {
        return _stateDetector.CheckBuffExists(buffName);
    }
}
