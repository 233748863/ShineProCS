using System.Collections.ObjectModel;
using FsCheck;
using FsCheck.Xunit;
using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Services;
using ShineProCS.Core.Strategies;
using ShineProCS.Models;

namespace ShineProCS.Tests;

/// <summary>
/// Property-based tests for Smart Strategy Selection Correctness
/// **Feature: skill-logic-compatibility, Property 7: 智能策略选择正确性**
/// **Validates: Requirements 8.1, 8.2, 8.3, 8.4**
/// </summary>
public class SmartStrategySelectionTests
{
    /// <summary>
    /// 创建一个可用的技能运行时状态
    /// </summary>
    private static SkillRuntimeState CreateAvailableSkill(
        string name,
        int priority,
        bool enabled = true,
        string conditionBuff = "",
        string requireState = "",
        double minMp = 0,
        int hpCheckTarget = 0,
        double hpThreshold = 0,
        string priorityOverrideCondition = "",
        int priorityOverrideValue = 0,
        int mpPriorityBoost = 0,
        double mpThresholdForBoost = 0,
        int preCastKeyCode = 0)
    {
        var config = new SkillConfig
        {
            Name = name,
            KeyCode = 0x41,
            Priority = priority,
            Enabled = enabled,
            ConditionBuff = conditionBuff,
            RequireState = requireState,
            MinMp = minMp,
            HpCheckTarget = hpCheckTarget,
            HpThreshold = hpThreshold,
            PriorityOverrideCondition = priorityOverrideCondition,
            PriorityOverrideValue = priorityOverrideValue,
            MpPriorityBoost = mpPriorityBoost,
            MpThresholdForBoost = mpThresholdForBoost,
            PreCastKeyCode = preCastKeyCode
        };
        
        var state = new SkillRuntimeState(config);
        state.IsVisuallyReady = true;
        return state;
    }


    /// <summary>
    /// 创建测试用的StrategyContext
    /// </summary>
    private static StrategyContext CreateContext(
        List<SkillRuntimeState> skills,
        double mpPercent = 100,
        double hpPercent = 100,
        double targetHpPercent = 100,
        ObservableCollection<SkillGroupConfig>? skillGroups = null,
        int comboBonus = 50)
    {
        return new StrategyContext
        {
            SkillStates = skills,
            GameState = new GameState
            {
                CurrentMpPercent = mpPercent,
                CurrentHpPercent = hpPercent,
                TargetHpPercent = targetHpPercent,
                MpPercentage = mpPercent / 100.0
            },
            LoopMode = "Smart",
            Settings = new AppSettings
            {
                SkillGroups = skillGroups ?? [],
                ComboSkillPriorityBonus = comboBonus
            }
        };
    }

    /// <summary>
    /// Property 7.1: SmartStrategy应选择满足所有条件且有效优先级最高的技能
    /// FOR ALL skill lists, SmartStrategy SHALL select the skill with highest effective priority that satisfies all conditions.
    /// **Validates: Requirements 8.1, 8.2, 8.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool SelectsHighestEffectivePrioritySkill(
        PositiveInt priority1Gen,
        PositiveInt priority2Gen,
        PositiveInt priority3Gen)
    {
        var priority1 = priority1Gen.Get % 100;
        var priority2 = priority2Gen.Get % 100;
        var priority3 = priority3Gen.Get % 100;
        
        // 创建三个技能，都满足条件
        var skill1 = CreateAvailableSkill("Skill1", priority1);
        var skill2 = CreateAvailableSkill("Skill2", priority2);
        var skill3 = CreateAvailableSkill("Skill3", priority3);
        
        var skills = new List<SkillRuntimeState> { skill1, skill2, skill3 };
        var context = CreateContext(skills);
        
        // 创建带ConditionEvaluator的SmartStrategy
        var mockChecker = new MockBuffChecker(buffExists: true);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        var strategy = new SmartStrategy(evaluator, stateTracker);
        
        var selected = strategy.SelectSkill(context);
        
        // 应该选择优先级最高的技能
        var maxPriority = Math.Max(priority1, Math.Max(priority2, priority3));
        return selected != null && selected.Config.Priority == maxPriority;
    }


    /// <summary>
    /// Property 7.2: 条件检查顺序正确（Enabled、ConditionBuff、RequireState、MinMp、HpCondition）
    /// SmartStrategy SHALL apply condition checks in order: Enabled, ConditionBuff, RequireState, MinMp, HpCondition.
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool ConditionChecksAppliedInOrder(PositiveInt priorityGen)
    {
        var priority = priorityGen.Get % 100 + 100; // 确保高优先级
        
        // 创建一个高优先级但禁用的技能
        var disabledSkill = CreateAvailableSkill("DisabledSkill", priority + 50, enabled: false);
        
        // 创建一个低优先级但启用的技能
        var enabledSkill = CreateAvailableSkill("EnabledSkill", priority);
        
        var skills = new List<SkillRuntimeState> { disabledSkill, enabledSkill };
        var context = CreateContext(skills);
        
        var mockChecker = new MockBuffChecker(buffExists: true);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        var strategy = new SmartStrategy(evaluator, stateTracker);
        
        var selected = strategy.SelectSkill(context);
        
        // 应该选择启用的技能，而不是禁用的高优先级技能
        return selected?.Config.Name == "EnabledSkill";
    }

    /// <summary>
    /// Property 7.3: 有效优先级计算正确（BasePriority + PriorityOverride + MpPriorityBoost + ComboBonus）
    /// SmartStrategy SHALL calculate effective priority as: BasePriority + PriorityOverride + MpPriorityBoost + ComboBonus.
    /// **Validates: Requirements 8.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool EffectivePriorityCalculatedCorrectly(
        PositiveInt basePriorityGen,
        PositiveInt overrideValueGen,
        PositiveInt mpBoostGen)
    {
        var basePriority = basePriorityGen.Get % 50;
        var overrideValue = overrideValueGen.Get % 100 + 100; // 确保覆盖值较高
        var mpBoost = mpBoostGen.Get % 50;
        
        // 创建一个有优先级覆盖的技能（条件满足时使用覆盖值）
        var skillWithOverride = CreateAvailableSkill(
            "SkillWithOverride",
            basePriority,
            priorityOverrideCondition: "TestBuff",
            priorityOverrideValue: overrideValue);
        
        // 创建一个普通技能，优先级介于基础优先级和覆盖值之间
        var normalSkill = CreateAvailableSkill("NormalSkill", basePriority + 50);
        
        var skills = new List<SkillRuntimeState> { skillWithOverride, normalSkill };
        var context = CreateContext(skills);
        
        // 创建模拟检测器，TestBuff存在
        var buffStates = new Dictionary<string, bool> { { "TestBuff", true } };
        var mockChecker = new MockBuffChecker(buffStates);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        var strategy = new SmartStrategy(evaluator, stateTracker);
        
        var selected = strategy.SelectSkill(context);
        
        // 当覆盖条件满足时，应该选择有覆盖值的技能（因为覆盖值更高）
        return selected?.Config.Name == "SkillWithOverride";
    }


    /// <summary>
    /// Property 7.4: 当多个技能具有相同的有效优先级时，按配置顺序选择
    /// WHEN multiple skills have the same effective priority, SmartStrategy SHALL select based on config order.
    /// **Validates: Requirements 8.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool SelectsByConfigOrderWhenSamePriority(PositiveInt priorityGen)
    {
        var priority = priorityGen.Get % 100 + 50;
        
        // 创建三个相同优先级的技能
        var skill1 = CreateAvailableSkill("FirstSkill", priority);
        var skill2 = CreateAvailableSkill("SecondSkill", priority);
        var skill3 = CreateAvailableSkill("ThirdSkill", priority);
        
        // 按顺序添加到列表
        var skills = new List<SkillRuntimeState> { skill1, skill2, skill3 };
        var context = CreateContext(skills);
        
        var mockChecker = new MockBuffChecker(buffExists: true);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        var strategy = new SmartStrategy(evaluator, stateTracker);
        
        var selected = strategy.SelectSkill(context);
        
        // 应该选择配置顺序中的第一个技能
        return selected?.Config.Name == "FirstSkill";
    }

    /// <summary>
    /// Property 7.5: 不满足条件的技能不会被选中，即使优先级最高
    /// Skills that do not satisfy conditions SHALL NOT be selected, even with highest priority.
    /// **Validates: Requirements 8.1, 8.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool SkillsNotSatisfyingConditionsNotSelected(
        PositiveInt highPriorityGen,
        PositiveInt lowPriorityGen)
    {
        var highPriority = highPriorityGen.Get % 100 + 100;
        var lowPriority = lowPriorityGen.Get % 50;
        
        // 创建一个高优先级但条件不满足的技能（需要不存在的Buff）
        var highPrioritySkill = CreateAvailableSkill(
            "HighPrioritySkill",
            highPriority,
            conditionBuff: "NonExistentBuff");
        
        // 创建一个低优先级但条件满足的技能
        var lowPrioritySkill = CreateAvailableSkill("LowPrioritySkill", lowPriority);
        
        var skills = new List<SkillRuntimeState> { highPrioritySkill, lowPrioritySkill };
        var context = CreateContext(skills);
        
        // 创建模拟检测器，NonExistentBuff不存在
        var buffStates = new Dictionary<string, bool> { { "NonExistentBuff", false } };
        var mockChecker = new MockBuffChecker(buffStates);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        var strategy = new SmartStrategy(evaluator, stateTracker);
        
        var selected = strategy.SelectSkill(context);
        
        // 应该选择低优先级但条件满足的技能
        return selected?.Config.Name == "LowPrioritySkill";
    }

    /// <summary>
    /// Property 7.6: RequireState条件正确评估
    /// Skills with RequireState SHALL only be selected when the state is true.
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool RequireStateConditionEvaluatedCorrectly(
        PositiveInt highPriorityGen,
        PositiveInt lowPriorityGen,
        NonEmptyString stateNameGen)
    {
        var highPriority = highPriorityGen.Get % 100 + 100;
        var lowPriority = lowPriorityGen.Get % 50;
        var stateName = stateNameGen.Get;
        
        // 创建一个高优先级但需要状态的技能
        var highPrioritySkill = CreateAvailableSkill(
            "HighPrioritySkill",
            highPriority,
            requireState: stateName);
        
        // 创建一个低优先级但无状态要求的技能
        var lowPrioritySkill = CreateAvailableSkill("LowPrioritySkill", lowPriority);
        
        var skills = new List<SkillRuntimeState> { highPrioritySkill, lowPrioritySkill };
        var context = CreateContext(skills);
        
        var mockChecker = new MockBuffChecker(buffExists: true);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        // 状态未设置（默认为false）
        var strategy = new SmartStrategy(evaluator, stateTracker);
        
        var selected = strategy.SelectSkill(context);
        
        // 应该选择低优先级但无状态要求的技能
        return selected?.Config.Name == "LowPrioritySkill";
    }
}
