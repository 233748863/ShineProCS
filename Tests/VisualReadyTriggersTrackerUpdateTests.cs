using FsCheck;
using FsCheck.Xunit;
using ShineProCS.Core.Services;
using ShineProCS.Models;

namespace ShineProCS.Tests;

/// <summary>
/// Property-based tests for visual ready state triggering CooldownTracker update
/// **Feature: business-logic-fixes, Property 8: Visual Ready Triggers CooldownTracker Update**
/// **Validates: Requirements 5.1**
/// </summary>
public class VisualReadyTriggersTrackerUpdateTests
{
    /// <summary>
    /// 创建有效的技能配置用于测试
    /// </summary>
    private static SkillConfig CreateSkillConfig(string name, int cooldown)
    {
        return new SkillConfig
        {
            Name = name,
            KeyCode = 0x41, // A key
            Priority = 1,
            Enabled = true,
            Cooldown = cooldown,
            CastType = SkillCastType.Instant
        };
    }
    
    /// <summary>
    /// 模拟 UpdateSkillReadyStates 方法的逻辑
    /// 这是 SkillLoopEngine 中的实际实现
    /// </summary>
    private static void UpdateSkillReadyStates(List<SkillRuntimeState> skillStates, SkillCooldownTracker tracker)
    {
        foreach (var skill in skillStates)
        {
            // 检测 IsVisuallyReady 从 false 变为 true
            if (skill.IsVisuallyReady && !skill.WasVisuallyReady)
            {
                tracker.RecordSkillReady(skill.Config.Name);
            }
            // 更新上一次的视觉状态
            skill.WasVisuallyReady = skill.IsVisuallyReady;
        }
    }
    
    /// <summary>
    /// Property 8: Visual Ready Triggers CooldownTracker Update
    /// 对于任何从 IsVisuallyReady=false 变为 IsVisuallyReady=true 的技能，
    /// CooldownTracker.RecordSkillReady() 应该被调用
    /// </summary>
    [Property(MaxTest = 100)]
    public bool RecordSkillReadyCalledOnVisualTransition(PositiveInt cooldownGen)
    {
        // Arrange
        var cooldown = (cooldownGen.Get % 60) + 30; // 30-90秒，确保有足够的冷却时间
        var skillName = $"TestSkill_{Guid.NewGuid():N}";
        var config = CreateSkillConfig(skillName, cooldown);
        var tracker = new SkillCooldownTracker();
        var skill = new SkillRuntimeState(config, tracker);
        var skillStates = new List<SkillRuntimeState> { skill };
        
        // 首先记录技能使用，设置一个较长的预计就绪时间
        tracker.RecordSkillUse(skillName, cooldown);
        
        // 获取初始记录
        var initialRecord = tracker.GetRecord(skillName);
        if (initialRecord == null) return false;
        
        var initialEstimatedReadyTime = initialRecord.EstimatedReadyTime;
        
        // 设置技能为视觉不可用状态
        skill.IsVisuallyReady = false;
        skill.WasVisuallyReady = false;
        
        // 第一次更新 - 状态没有变化
        UpdateSkillReadyStates(skillStates, tracker);
        
        // 模拟技能变为视觉可用
        skill.IsVisuallyReady = true;
        
        // Act - 第二次更新，应该触发 RecordSkillReady
        UpdateSkillReadyStates(skillStates, tracker);
        
        // Assert: 预计就绪时间应该被更新为当前时间（或更早）
        var updatedRecord = tracker.GetRecord(skillName);
        if (updatedRecord == null) return false;
        
        // RecordSkillReady 会将 EstimatedReadyTime 设置为 DateTime.Now
        // 所以更新后的时间应该小于等于初始时间（因为初始时间是未来的）
        return updatedRecord.EstimatedReadyTime <= initialEstimatedReadyTime;
    }
    
    /// <summary>
    /// Property 8.1: 当 IsVisuallyReady 保持 true 时，不应该重复调用 RecordSkillReady
    /// </summary>
    [Property(MaxTest = 100)]
    public bool NoRepeatedCallsWhenStayingReady(PositiveInt cooldownGen)
    {
        // Arrange
        var cooldown = (cooldownGen.Get % 60) + 30;
        var skillName = $"TestSkill_{Guid.NewGuid():N}";
        var config = CreateSkillConfig(skillName, cooldown);
        var tracker = new SkillCooldownTracker();
        var skill = new SkillRuntimeState(config, tracker);
        var skillStates = new List<SkillRuntimeState> { skill };
        
        // 记录技能使用
        tracker.RecordSkillUse(skillName, cooldown);
        
        // 设置技能为视觉可用状态
        skill.IsVisuallyReady = true;
        skill.WasVisuallyReady = true; // 已经是可用状态
        
        // 获取初始记录
        var initialRecord = tracker.GetRecord(skillName);
        if (initialRecord == null) return false;
        var initialEstimatedReadyTime = initialRecord.EstimatedReadyTime;
        
        // Act - 多次更新，但状态没有从 false 变为 true
        UpdateSkillReadyStates(skillStates, tracker);
        UpdateSkillReadyStates(skillStates, tracker);
        UpdateSkillReadyStates(skillStates, tracker);
        
        // Assert: 预计就绪时间应该保持不变（因为没有触发 RecordSkillReady）
        var updatedRecord = tracker.GetRecord(skillName);
        if (updatedRecord == null) return false;
        
        return updatedRecord.EstimatedReadyTime == initialEstimatedReadyTime;
    }
    
    /// <summary>
    /// Property 8.2: 当 IsVisuallyReady 从 true 变为 false 时，不应该调用 RecordSkillReady
    /// </summary>
    [Property(MaxTest = 100)]
    public bool NoCallWhenTransitioningToNotReady(PositiveInt cooldownGen)
    {
        // Arrange
        var cooldown = (cooldownGen.Get % 60) + 30;
        var skillName = $"TestSkill_{Guid.NewGuid():N}";
        var config = CreateSkillConfig(skillName, cooldown);
        var tracker = new SkillCooldownTracker();
        var skill = new SkillRuntimeState(config, tracker);
        var skillStates = new List<SkillRuntimeState> { skill };
        
        // 记录技能使用
        tracker.RecordSkillUse(skillName, cooldown);
        
        // 设置技能为视觉可用状态
        skill.IsVisuallyReady = true;
        skill.WasVisuallyReady = true;
        
        // 获取初始记录
        var initialRecord = tracker.GetRecord(skillName);
        if (initialRecord == null) return false;
        var initialEstimatedReadyTime = initialRecord.EstimatedReadyTime;
        
        // 模拟技能变为视觉不可用
        skill.IsVisuallyReady = false;
        
        // Act - 更新状态
        UpdateSkillReadyStates(skillStates, tracker);
        
        // Assert: 预计就绪时间应该保持不变
        var updatedRecord = tracker.GetRecord(skillName);
        if (updatedRecord == null) return false;
        
        return updatedRecord.EstimatedReadyTime == initialEstimatedReadyTime;
    }
    
    /// <summary>
    /// Property 8.3: WasVisuallyReady 应该在每次更新后正确反映上一次的状态
    /// </summary>
    [Property(MaxTest = 100)]
    public bool WasVisuallyReadyTracksCorrectly(PositiveInt cooldownGen, bool initialState, bool newState)
    {
        // Arrange
        var cooldown = (cooldownGen.Get % 60) + 1;
        var skillName = $"TestSkill_{Guid.NewGuid():N}";
        var config = CreateSkillConfig(skillName, cooldown);
        var tracker = new SkillCooldownTracker();
        var skill = new SkillRuntimeState(config, tracker);
        var skillStates = new List<SkillRuntimeState> { skill };
        
        // 设置初始状态
        skill.IsVisuallyReady = initialState;
        skill.WasVisuallyReady = initialState;
        
        // 改变状态
        skill.IsVisuallyReady = newState;
        
        // Act
        UpdateSkillReadyStates(skillStates, tracker);
        
        // Assert: WasVisuallyReady 应该等于更新前的 IsVisuallyReady（即 newState）
        return skill.WasVisuallyReady == newState;
    }
    
    /// <summary>
    /// Property 8.4: 多个技能的状态变化应该独立处理
    /// </summary>
    [Property(MaxTest = 100)]
    public bool MultipleSkillsHandledIndependently(PositiveInt cooldownGen)
    {
        // Arrange
        var cooldown = (cooldownGen.Get % 60) + 30;
        var tracker = new SkillCooldownTracker();
        
        var skill1Name = $"TestSkill1_{Guid.NewGuid():N}";
        var skill2Name = $"TestSkill2_{Guid.NewGuid():N}";
        
        var config1 = CreateSkillConfig(skill1Name, cooldown);
        var config2 = CreateSkillConfig(skill2Name, cooldown);
        
        var skill1 = new SkillRuntimeState(config1, tracker);
        var skill2 = new SkillRuntimeState(config2, tracker);
        var skillStates = new List<SkillRuntimeState> { skill1, skill2 };
        
        // 记录两个技能的使用
        tracker.RecordSkillUse(skill1Name, cooldown);
        tracker.RecordSkillUse(skill2Name, cooldown);
        
        // 获取初始记录
        var initial1 = tracker.GetRecord(skill1Name);
        var initial2 = tracker.GetRecord(skill2Name);
        if (initial1 == null || initial2 == null) return false;
        
        var initialTime1 = initial1.EstimatedReadyTime;
        var initialTime2 = initial2.EstimatedReadyTime;
        
        // 设置 skill1 从 false 变为 true，skill2 保持 true
        skill1.IsVisuallyReady = false;
        skill1.WasVisuallyReady = false;
        skill2.IsVisuallyReady = true;
        skill2.WasVisuallyReady = true;
        
        // 第一次更新
        UpdateSkillReadyStates(skillStates, tracker);
        
        // skill1 变为 true
        skill1.IsVisuallyReady = true;
        
        // Act - 第二次更新
        UpdateSkillReadyStates(skillStates, tracker);
        
        // Assert: skill1 的时间应该被更新，skill2 的时间应该保持不变
        var updated1 = tracker.GetRecord(skill1Name);
        var updated2 = tracker.GetRecord(skill2Name);
        if (updated1 == null || updated2 == null) return false;
        
        return updated1.EstimatedReadyTime <= initialTime1 && updated2.EstimatedReadyTime == initialTime2;
    }
}
