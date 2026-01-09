using FsCheck;
using FsCheck.Xunit;
using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Services;
using ShineProCS.Models;

namespace ShineProCS.Tests;

/// <summary>
/// Property-based tests for MP Priority Boost Calculation
/// **Feature: skill-logic-compatibility, Property 4: MP优先级加成计算**
/// **Validates: Requirements 4.1, 4.2**
/// </summary>
public class MpPriorityBoostTests
{
    /// <summary>
    /// 创建测试用的SkillRuntimeState
    /// </summary>
    private static SkillRuntimeState CreateSkillState(
        int basePriority,
        int mpPriorityBoost = 0,
        double mpThresholdForBoost = 0)
    {
        var config = new SkillConfig
        {
            Name = "TestSkill",
            Enabled = true,
            Priority = basePriority,
            MpPriorityBoost = mpPriorityBoost,
            MpThresholdForBoost = mpThresholdForBoost
        };
        return new SkillRuntimeState(config);
    }

    /// <summary>
    /// 创建测试用的StrategyContext
    /// </summary>
    private static StrategyContext CreateContext(double mpPercent)
    {
        return new StrategyContext
        {
            GameState = new GameState
            {
                CurrentMpPercent = mpPercent,
                CurrentHpPercent = 100
            },
            Settings = new AppSettings()
        };
    }

    /// <summary>
    /// Property 4.1: 当MP高于MpThresholdForBoost时，应用MpPriorityBoost加成
    /// WHEN MP is above MpThresholdForBoost, THE effective priority SHALL include MpPriorityBoost.
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool AppliesBoostWhenMpAboveThreshold(
        PositiveInt basePriorityGen,
        PositiveInt boostGen)
    {
        var basePriority = basePriorityGen.Get;
        var boost = boostGen.Get;
        var threshold = 30.0; // 固定阈值30%
        var currentMp = 50.0; // 当前MP 50%，高于阈值
        
        var mockChecker = new MockBuffChecker(buffExists: true);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        
        var skill = CreateSkillState(basePriority, boost, threshold);
        var context = CreateContext(currentMp);
        
        var effectivePriority = evaluator.CalculateEffectivePriority(skill, context, stateTracker);
        
        // 当MP高于阈值时，有效优先级应该等于基础优先级 + 加成
        return effectivePriority == basePriority + boost;
    }

    /// <summary>
    /// Property 4.2: 当MP低于或等于MpThresholdForBoost时，不应用加成
    /// WHEN MP is at or below MpThresholdForBoost, THE effective priority SHALL NOT include MpPriorityBoost.
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool NoBoostWhenMpBelowOrEqualThreshold(
        PositiveInt basePriorityGen,
        PositiveInt boostGen)
    {
        var basePriority = basePriorityGen.Get;
        var boost = boostGen.Get;
        var threshold = 50.0; // 固定阈值50%
        var currentMp = 30.0; // 当前MP 30%，低于阈值
        
        var mockChecker = new MockBuffChecker(buffExists: true);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        
        var skill = CreateSkillState(basePriority, boost, threshold);
        var context = CreateContext(currentMp);
        
        var effectivePriority = evaluator.CalculateEffectivePriority(skill, context, stateTracker);
        
        // 当MP低于阈值时，有效优先级应该等于基础优先级（不加成）
        return effectivePriority == basePriority;
    }

    /// <summary>
    /// Property 4.3: 当MpPriorityBoost为0时，不影响优先级
    /// WHEN MpPriorityBoost is 0, THE effective priority SHALL equal base Priority.
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool NoEffectWhenBoostIsZero(
        PositiveInt basePriorityGen,
        PositiveInt mpPercentGen)
    {
        var basePriority = basePriorityGen.Get;
        var mpPercent = (mpPercentGen.Get % 100) + 1; // 1-100%
        
        var mockChecker = new MockBuffChecker(buffExists: true);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        
        // MpPriorityBoost为0
        var skill = CreateSkillState(basePriority, mpPriorityBoost: 0, mpThresholdForBoost: 30);
        var context = CreateContext(mpPercent);
        
        var effectivePriority = evaluator.CalculateEffectivePriority(skill, context, stateTracker);
        
        // 当加成为0时，有效优先级应该等于基础优先级
        return effectivePriority == basePriority;
    }

    /// <summary>
    /// Property 4.4: MP加成与MP水平的关系
    /// FOR ALL MP levels, boost is applied if and only if MP > threshold.
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool BoostAppliedIfAndOnlyIfMpAboveThreshold(
        PositiveInt basePriorityGen,
        PositiveInt boostGen,
        PositiveInt thresholdGen,
        PositiveInt mpPercentGen)
    {
        var basePriority = basePriorityGen.Get;
        var boost = boostGen.Get;
        var threshold = (thresholdGen.Get % 100) + 1; // 1-100%
        var mpPercent = (mpPercentGen.Get % 100) + 1; // 1-100%
        
        var mockChecker = new MockBuffChecker(buffExists: true);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        
        var skill = CreateSkillState(basePriority, boost, threshold);
        var context = CreateContext(mpPercent);
        
        var effectivePriority = evaluator.CalculateEffectivePriority(skill, context, stateTracker);
        
        // 当MP高于阈值时应用加成，否则不加成
        var expectedPriority = mpPercent > threshold ? basePriority + boost : basePriority;
        return effectivePriority == expectedPriority;
    }

    /// <summary>
    /// Property 4.5: MP刚好等于阈值时不应用加成
    /// WHEN MP equals MpThresholdForBoost exactly, THE boost SHALL NOT be applied.
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool NoBoostWhenMpEqualsThreshold(
        PositiveInt basePriorityGen,
        PositiveInt boostGen,
        PositiveInt thresholdGen)
    {
        var basePriority = basePriorityGen.Get;
        var boost = boostGen.Get;
        var threshold = (thresholdGen.Get % 100) + 1; // 1-100%
        var mpPercent = (double)threshold; // MP刚好等于阈值
        
        var mockChecker = new MockBuffChecker(buffExists: true);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        
        var skill = CreateSkillState(basePriority, boost, threshold);
        var context = CreateContext(mpPercent);
        
        var effectivePriority = evaluator.CalculateEffectivePriority(skill, context, stateTracker);
        
        // 当MP等于阈值时，不应用加成（需要严格大于）
        return effectivePriority == basePriority;
    }
}
