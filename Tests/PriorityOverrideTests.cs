using FsCheck;
using FsCheck.Xunit;
using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Services;
using ShineProCS.Models;

namespace ShineProCS.Tests;

/// <summary>
/// Property-based tests for Priority Override Correctness
/// **Feature: skill-logic-compatibility, Property 2: 优先级覆盖正确性**
/// **Validates: Requirements 2.1, 2.2, 2.3**
/// </summary>
public class PriorityOverrideTests
{
    /// <summary>
    /// 创建测试用的SkillRuntimeState
    /// </summary>
    private static SkillRuntimeState CreateSkillState(
        int basePriority,
        string priorityOverrideCondition = "",
        int priorityOverrideValue = 0)
    {
        var config = new SkillConfig
        {
            Name = "TestSkill",
            Enabled = true,
            Priority = basePriority,
            PriorityOverrideCondition = priorityOverrideCondition,
            PriorityOverrideValue = priorityOverrideValue
        };
        return new SkillRuntimeState(config);
    }

    /// <summary>
    /// 创建测试用的StrategyContext
    /// </summary>
    private static StrategyContext CreateContext()
    {
        return new StrategyContext
        {
            GameState = new GameState
            {
                CurrentMpPercent = 100,
                CurrentHpPercent = 100
            },
            Settings = new AppSettings()
        };
    }

    /// <summary>
    /// Property 2.1: 当PriorityOverrideCondition满足时，使用PriorityOverrideValue
    /// WHEN PriorityOverrideCondition is satisfied, THE effective priority SHALL be PriorityOverrideValue.
    /// **Validates: Requirements 2.1, 2.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool UsesOverrideValueWhenConditionSatisfied(
        PositiveInt basePriorityGen,
        PositiveInt overrideValueGen,
        NonEmptyString conditionBuffGen)
    {
        var basePriority = basePriorityGen.Get;
        var overrideValue = overrideValueGen.Get;
        var conditionBuff = conditionBuffGen.Get;
        
        // 创建一个返回Buff存在的模拟检测器
        var mockChecker = new MockBuffChecker(buffExists: true);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        
        var skill = CreateSkillState(basePriority, conditionBuff, overrideValue);
        var context = CreateContext();
        
        var effectivePriority = evaluator.CalculateEffectivePriority(skill, context, stateTracker);
        
        // 当条件满足时，有效优先级应该等于覆盖值
        return effectivePriority == overrideValue;
    }

    /// <summary>
    /// Property 2.2: 当PriorityOverrideCondition不满足时，使用基础Priority
    /// WHEN PriorityOverrideCondition is not satisfied, THE effective priority SHALL be base Priority.
    /// **Validates: Requirements 2.1, 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool UsesBasePriorityWhenConditionNotSatisfied(
        PositiveInt basePriorityGen,
        PositiveInt overrideValueGen,
        NonEmptyString conditionBuffGen)
    {
        var basePriority = basePriorityGen.Get;
        var overrideValue = overrideValueGen.Get;
        var conditionBuff = conditionBuffGen.Get;
        
        // 创建一个返回Buff不存在的模拟检测器
        var mockChecker = new MockBuffChecker(buffExists: false);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        
        var skill = CreateSkillState(basePriority, conditionBuff, overrideValue);
        var context = CreateContext();
        
        var effectivePriority = evaluator.CalculateEffectivePriority(skill, context, stateTracker);
        
        // 当条件不满足时，有效优先级应该等于基础优先级
        return effectivePriority == basePriority;
    }

    /// <summary>
    /// Property 2.3: 当没有配置PriorityOverrideCondition时，使用基础Priority
    /// WHEN PriorityOverrideCondition is not configured, THE effective priority SHALL be base Priority.
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool UsesBasePriorityWhenNoOverrideConfigured(
        PositiveInt basePriorityGen,
        bool buffExists)
    {
        var basePriority = basePriorityGen.Get;
        
        // 创建模拟检测器（无论返回什么都不影响结果）
        var mockChecker = new MockBuffChecker(buffExists: buffExists);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        
        // 没有配置优先级覆盖条件
        var skill = CreateSkillState(basePriority, priorityOverrideCondition: "");
        var context = CreateContext();
        
        var effectivePriority = evaluator.CalculateEffectivePriority(skill, context, stateTracker);
        
        // 没有配置覆盖条件时，有效优先级应该等于基础优先级
        return effectivePriority == basePriority;
    }

    /// <summary>
    /// Property 2.4: 优先级覆盖与Buff状态一致
    /// FOR ALL skills with PriorityOverrideCondition, the effective priority SHALL match buff existence.
    /// **Validates: Requirements 2.1, 2.2, 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool PriorityOverrideMatchesBuffExistence(
        PositiveInt basePriorityGen,
        PositiveInt overrideValueGen,
        NonEmptyString conditionBuffGen,
        bool buffExists)
    {
        var basePriority = basePriorityGen.Get;
        var overrideValue = overrideValueGen.Get;
        var conditionBuff = conditionBuffGen.Get;
        
        var mockChecker = new MockBuffChecker(buffExists: buffExists);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        
        var skill = CreateSkillState(basePriority, conditionBuff, overrideValue);
        var context = CreateContext();
        
        var effectivePriority = evaluator.CalculateEffectivePriority(skill, context, stateTracker);
        
        // 当Buff存在时使用覆盖值，否则使用基础优先级
        var expectedPriority = buffExists ? overrideValue : basePriority;
        return effectivePriority == expectedPriority;
    }

    /// <summary>
    /// Property 2.5: 优先级覆盖值可以高于或低于基础优先级
    /// THE PriorityOverrideValue can be higher or lower than base Priority.
    /// **Validates: Requirements 2.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool OverrideValueCanBeHigherOrLowerThanBase(
        PositiveInt basePriorityGen,
        int overrideValueGen,
        NonEmptyString conditionBuffGen)
    {
        var basePriority = basePriorityGen.Get;
        // 允许覆盖值为任意整数（可以高于或低于基础优先级）
        var overrideValue = overrideValueGen;
        var conditionBuff = conditionBuffGen.Get;
        
        var mockChecker = new MockBuffChecker(buffExists: true);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        
        var skill = CreateSkillState(basePriority, conditionBuff, overrideValue);
        var context = CreateContext();
        
        var effectivePriority = evaluator.CalculateEffectivePriority(skill, context, stateTracker);
        
        // 无论覆盖值是高于还是低于基础优先级，都应该正确应用
        return effectivePriority == overrideValue;
    }
}
