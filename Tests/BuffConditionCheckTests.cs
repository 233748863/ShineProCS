using FsCheck;
using FsCheck.Xunit;
using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Services;
using ShineProCS.Models;

namespace ShineProCS.Tests;

/// <summary>
/// Property-based tests for Buff Condition Check Consistency
/// **Feature: skill-logic-compatibility, Property 1: Buff条件检查一致性**
/// **Validates: Requirements 1.1, 1.2, 1.3**
/// </summary>
public class BuffConditionCheckTests
{
    /// <summary>
    /// 创建测试用的SkillRuntimeState
    /// </summary>
    private static SkillRuntimeState CreateSkillState(string conditionBuff, bool enabled = true)
    {
        var config = new SkillConfig
        {
            Name = "TestSkill",
            Enabled = enabled,
            ConditionBuff = conditionBuff,
            Priority = 100
        };
        return new SkillRuntimeState(config);
    }

    /// <summary>
    /// 创建测试用的StrategyContext
    /// </summary>
    private static StrategyContext CreateContext(double mpPercent = 100, double hpPercent = 100)
    {
        return new StrategyContext
        {
            GameState = new GameState
            {
                CurrentMpPercent = mpPercent,
                CurrentHpPercent = hpPercent
            },
            Settings = new AppSettings()
        };
    }

    /// <summary>
    /// Property 1.1: 当ConditionBuff存在时，技能应被纳入候选
    /// WHEN ConditionBuff exists, THE skill SHALL be included in candidates.
    /// **Validates: Requirements 1.1, 1.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool SkillIncludedWhenConditionBuffExists(NonEmptyString buffNameGen)
    {
        var buffName = buffNameGen.Get;
        
        // 创建一个总是返回Buff存在的模拟检测器
        var mockChecker = new MockBuffChecker(buffExists: true);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        
        var skill = CreateSkillState(buffName);
        var context = CreateContext();
        
        // 当Buff存在时，技能应该满足条件
        var result = evaluator.EvaluateSkillConditions(skill, context, stateTracker);
        
        return result == true;
    }

    /// <summary>
    /// Property 1.2: 当ConditionBuff不存在时，技能应被跳过
    /// WHEN ConditionBuff does not exist, THE skill SHALL be skipped.
    /// **Validates: Requirements 1.1, 1.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool SkillSkippedWhenConditionBuffNotExists(NonEmptyString buffNameGen)
    {
        var buffName = buffNameGen.Get;
        
        // 创建一个总是返回Buff不存在的模拟检测器
        var mockChecker = new MockBuffChecker(buffExists: false);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        
        var skill = CreateSkillState(buffName);
        var context = CreateContext();
        
        // 当Buff不存在时，技能应该不满足条件
        var result = evaluator.EvaluateSkillConditions(skill, context, stateTracker);
        
        return result == false;
    }

    /// <summary>
    /// Property 1.3: 当没有配置ConditionBuff时，技能应通过Buff检查
    /// WHEN ConditionBuff is not configured, THE skill SHALL pass buff check.
    /// **Validates: Requirements 1.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool SkillPassesWhenNoConditionBuffConfigured(bool buffExists)
    {
        // 创建模拟检测器（无论返回什么都不影响结果）
        var mockChecker = new MockBuffChecker(buffExists: buffExists);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        
        // 没有配置ConditionBuff
        var skill = CreateSkillState(conditionBuff: "");
        var context = CreateContext();
        
        // 没有配置ConditionBuff时，技能应该通过检查
        var result = evaluator.EvaluateSkillConditions(skill, context, stateTracker);
        
        return result == true;
    }

    /// <summary>
    /// Property 1.4: Buff条件检查与Buff状态一致
    /// FOR ALL skills with ConditionBuff, the evaluation result SHALL match buff existence.
    /// **Validates: Requirements 1.1, 1.2, 1.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool BuffConditionMatchesBuffExistence(NonEmptyString buffNameGen, bool buffExists)
    {
        var buffName = buffNameGen.Get;
        
        var mockChecker = new MockBuffChecker(buffExists: buffExists);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        
        var skill = CreateSkillState(buffName);
        var context = CreateContext();
        
        var result = evaluator.EvaluateSkillConditions(skill, context, stateTracker);
        
        // 评估结果应该与Buff存在状态一致
        return result == buffExists;
    }

    /// <summary>
    /// Property 1.5: 禁用的技能不通过条件检查
    /// WHEN skill is disabled, THE skill SHALL not pass condition check regardless of buff.
    /// **Validates: Requirements 1.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool DisabledSkillFailsConditionCheck(NonEmptyString buffNameGen, bool buffExists)
    {
        var buffName = buffNameGen.Get;
        
        var mockChecker = new MockBuffChecker(buffExists: buffExists);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        
        // 创建禁用的技能
        var skill = CreateSkillState(buffName, enabled: false);
        var context = CreateContext();
        
        var result = evaluator.EvaluateSkillConditions(skill, context, stateTracker);
        
        // 禁用的技能应该不通过检查
        return result == false;
    }
}

/// <summary>
/// 模拟的Buff检测器，用于测试
/// </summary>
public class MockBuffChecker : IBuffChecker
{
    private readonly bool _defaultBuffExists;
    private readonly Dictionary<string, bool> _buffStates;

    /// <summary>
    /// 创建一个返回固定Buff状态的模拟检测器
    /// </summary>
    /// <param name="buffExists">所有Buff是否存在</param>
    public MockBuffChecker(bool buffExists)
    {
        _defaultBuffExists = buffExists;
        _buffStates = new Dictionary<string, bool>();
    }

    /// <summary>
    /// 创建一个可以为不同Buff返回不同状态的模拟检测器
    /// </summary>
    /// <param name="buffStates">Buff名称到状态的映射</param>
    public MockBuffChecker(Dictionary<string, bool> buffStates)
    {
        _defaultBuffExists = false;
        _buffStates = buffStates;
    }

    /// <summary>
    /// 检查Buff是否存在
    /// </summary>
    public bool CheckBuffExists(string buffName)
    {
        if (string.IsNullOrEmpty(buffName))
            return true;

        if (_buffStates.TryGetValue(buffName, out var exists))
            return exists;

        return _defaultBuffExists;
    }
}
