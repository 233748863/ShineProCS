using System.Collections.ObjectModel;
using FsCheck;
using FsCheck.Xunit;
using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Services;
using ShineProCS.Models;

namespace ShineProCS.Tests;

/// <summary>
/// Property-based tests for Skill Group Condition Transitivity
/// **Feature: skill-logic-compatibility, Property 3: 技能组条件传递性**
/// **Validates: Requirements 3.1, 3.2, 3.4**
/// </summary>
public class SkillGroupConditionTests
{
    /// <summary>
    /// 创建测试用的SkillRuntimeState
    /// </summary>
    private static SkillRuntimeState CreateSkillState(
        string name,
        string skillGroup = "",
        string conditionBuff = "",
        bool enabled = true,
        int priority = 100)
    {
        var config = new SkillConfig
        {
            Name = name,
            Enabled = enabled,
            SkillGroup = skillGroup,
            ConditionBuff = conditionBuff,
            Priority = priority
        };
        return new SkillRuntimeState(config);
    }

    /// <summary>
    /// 创建测试用的StrategyContext
    /// </summary>
    private static StrategyContext CreateContext(
        ObservableCollection<SkillGroupConfig>? skillGroups = null,
        double mpPercent = 100,
        double hpPercent = 100)
    {
        return new StrategyContext
        {
            GameState = new GameState
            {
                CurrentMpPercent = mpPercent,
                CurrentHpPercent = hpPercent
            },
            Settings = new AppSettings
            {
                SkillGroups = skillGroups ?? []
            }
        };
    }


    /// <summary>
    /// Property 3.1: 当组条件不满足时，该组所有技能都应被跳过
    /// WHEN group condition is not satisfied, ALL skills in the group SHALL be skipped.
    /// **Validates: Requirements 3.1, 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool AllSkillsInGroupSkippedWhenGroupConditionNotSatisfied(
        NonEmptyString groupNameGen,
        NonEmptyString groupConditionBuffGen,
        PositiveInt skillCountGen)
    {
        var groupName = groupNameGen.Get;
        var groupConditionBuff = groupConditionBuffGen.Get;
        var skillCount = Math.Min(skillCountGen.Get, 10); // 限制技能数量
        
        // 创建技能组配置，条件Buff不存在
        var skillGroups = new ObservableCollection<SkillGroupConfig>
        {
            new() { Name = groupName, ConditionBuff = groupConditionBuff, Enabled = true }
        };
        
        // 创建一个返回Buff不存在的模拟检测器
        var mockChecker = new MockBuffChecker(buffExists: false);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        var context = CreateContext(skillGroups);
        
        // 创建多个属于该组的技能
        var allSkillsSkipped = true;
        for (int i = 0; i < skillCount; i++)
        {
            var skill = CreateSkillState($"Skill{i}", skillGroup: groupName);
            var result = evaluator.EvaluateSkillConditions(skill, context, stateTracker);
            if (result)
            {
                allSkillsSkipped = false;
                break;
            }
        }
        
        // 当组条件不满足时，所有技能都应被跳过
        return allSkillsSkipped;
    }

    /// <summary>
    /// Property 3.2: 当组条件满足时，应继续评估个人条件
    /// WHEN group condition is satisfied, THE system SHALL continue to evaluate individual conditions.
    /// **Validates: Requirements 3.1, 3.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool IndividualConditionsEvaluatedWhenGroupConditionSatisfied(
        NonEmptyString groupNameGen,
        NonEmptyString groupConditionBuffGen,
        NonEmptyString individualConditionBuffGen,
        bool individualBuffExists)
    {
        var groupName = groupNameGen.Get;
        var groupConditionBuff = groupConditionBuffGen.Get;
        var individualConditionBuff = individualConditionBuffGen.Get;
        
        // 确保组条件Buff和个人条件Buff不同
        if (groupConditionBuff == individualConditionBuff)
            individualConditionBuff = individualConditionBuff + "_individual";
        
        // 创建技能组配置
        var skillGroups = new ObservableCollection<SkillGroupConfig>
        {
            new() { Name = groupName, ConditionBuff = groupConditionBuff, Enabled = true }
        };
        
        // 创建模拟检测器：组条件Buff存在，个人条件Buff根据参数决定
        var buffStates = new Dictionary<string, bool>
        {
            { groupConditionBuff, true },
            { individualConditionBuff, individualBuffExists }
        };
        var mockChecker = new MockBuffChecker(buffStates);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        var context = CreateContext(skillGroups);
        
        // 创建属于该组且有个人条件的技能
        var skill = CreateSkillState("TestSkill", skillGroup: groupName, conditionBuff: individualConditionBuff);
        var result = evaluator.EvaluateSkillConditions(skill, context, stateTracker);
        
        // 结果应该与个人条件Buff存在状态一致
        return result == individualBuffExists;
    }


    /// <summary>
    /// Property 3.3: 当技能组不存在时，忽略组条件
    /// WHEN skill group does not exist, THE system SHALL ignore group condition.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool GroupConditionIgnoredWhenGroupNotExists(
        NonEmptyString nonExistentGroupNameGen,
        bool buffExists)
    {
        var nonExistentGroupName = nonExistentGroupNameGen.Get;
        
        // 创建空的技能组配置（组不存在）
        var skillGroups = new ObservableCollection<SkillGroupConfig>();
        
        var mockChecker = new MockBuffChecker(buffExists: buffExists);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        var context = CreateContext(skillGroups);
        
        // 创建引用不存在组的技能
        var skill = CreateSkillState("TestSkill", skillGroup: nonExistentGroupName);
        var result = evaluator.EvaluateSkillConditions(skill, context, stateTracker);
        
        // 当组不存在时，应该忽略组条件，技能应该通过检查
        return result == true;
    }

    /// <summary>
    /// Property 3.4: 当技能组被禁用时，该组所有技能都应被跳过
    /// WHEN skill group is disabled, ALL skills in the group SHALL be skipped.
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool AllSkillsInGroupSkippedWhenGroupDisabled(
        NonEmptyString groupNameGen,
        PositiveInt skillCountGen)
    {
        var groupName = groupNameGen.Get;
        var skillCount = Math.Min(skillCountGen.Get, 10);
        
        // 创建禁用的技能组配置
        var skillGroups = new ObservableCollection<SkillGroupConfig>
        {
            new() { Name = groupName, ConditionBuff = "", Enabled = false }
        };
        
        // 创建一个返回Buff存在的模拟检测器（即使Buff存在，组禁用也应跳过）
        var mockChecker = new MockBuffChecker(buffExists: true);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        var context = CreateContext(skillGroups);
        
        // 创建多个属于该组的技能
        var allSkillsSkipped = true;
        for (int i = 0; i < skillCount; i++)
        {
            var skill = CreateSkillState($"Skill{i}", skillGroup: groupName);
            var result = evaluator.EvaluateSkillConditions(skill, context, stateTracker);
            if (result)
            {
                allSkillsSkipped = false;
                break;
            }
        }
        
        // 当组被禁用时，所有技能都应被跳过
        return allSkillsSkipped;
    }

    /// <summary>
    /// Property 3.5: 没有指定技能组的技能不受组条件影响
    /// WHEN skill has no skill group, THE skill SHALL not be affected by group conditions.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool SkillWithoutGroupNotAffectedByGroupConditions(
        NonEmptyString groupNameGen,
        NonEmptyString groupConditionBuffGen)
    {
        var groupName = groupNameGen.Get;
        var groupConditionBuff = groupConditionBuffGen.Get;
        
        // 创建技能组配置，条件Buff不存在
        var skillGroups = new ObservableCollection<SkillGroupConfig>
        {
            new() { Name = groupName, ConditionBuff = groupConditionBuff, Enabled = true }
        };
        
        // 创建一个返回Buff不存在的模拟检测器
        var mockChecker = new MockBuffChecker(buffExists: false);
        var evaluator = new ConditionEvaluator(mockChecker);
        var stateTracker = new StateTracker();
        var context = CreateContext(skillGroups);
        
        // 创建没有指定技能组的技能
        var skill = CreateSkillState("TestSkill", skillGroup: "");
        var result = evaluator.EvaluateSkillConditions(skill, context, stateTracker);
        
        // 没有指定技能组的技能应该通过检查
        return result == true;
    }
}
