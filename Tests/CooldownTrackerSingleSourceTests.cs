using FsCheck;
using FsCheck.Xunit;
using ShineProCS.Core.Services;
using ShineProCS.Models;

namespace ShineProCS.Tests;

/// <summary>
/// Property-based tests for CooldownTracker as single source of truth
/// **Feature: business-logic-fixes, Property 7: CooldownTracker as Single Source of Truth**
/// **Validates: Requirements 5.2, 5.3**
/// </summary>
public class CooldownTrackerSingleSourceTests
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
    /// Property 7: CooldownTracker as Single Source of Truth
    /// 对于任何关联了 CooldownTracker 的 SkillRuntimeState，
    /// IsAvailable 属性应该返回 CooldownTracker.GetRecord().IsEstimatedReady 的值
    /// </summary>
    [Property(MaxTest = 100)]
    public bool IsAvailableUsesTrackerWhenAvailable(PositiveInt cooldownGen, PositiveInt delayGen)
    {
        // Arrange
        var cooldown = (cooldownGen.Get % 60) + 1; // 1-60秒
        var skillName = $"TestSkill_{Guid.NewGuid():N}";
        var config = CreateSkillConfig(skillName, cooldown);
        var tracker = new SkillCooldownTracker();
        var skill = new SkillRuntimeState(config, tracker);
        
        // Act - 记录技能使用
        tracker.RecordSkillUse(skillName, cooldown);
        
        // 获取 tracker 的记录
        var record = tracker.GetRecord(skillName);
        if (record == null) return false;
        
        // Assert: SkillRuntimeState.IsAvailable 应该等于 tracker 的 IsEstimatedReady
        return skill.IsAvailable == record.IsEstimatedReady;
    }
    
    /// <summary>
    /// Property 7.1: 当 CooldownTracker 记录技能就绪时，IsAvailable 应该返回 true
    /// </summary>
    [Property(MaxTest = 100)]
    public bool IsAvailableReturnsTrueWhenTrackerSaysReady(PositiveInt cooldownGen)
    {
        // Arrange
        var cooldown = (cooldownGen.Get % 60) + 1;
        var skillName = $"TestSkill_{Guid.NewGuid():N}";
        var config = CreateSkillConfig(skillName, cooldown);
        var tracker = new SkillCooldownTracker();
        var skill = new SkillRuntimeState(config, tracker);
        
        // Act - 记录技能使用，然后立即记录就绪
        tracker.RecordSkillUse(skillName, cooldown);
        tracker.RecordSkillReady(skillName);
        
        // 获取 tracker 的记录
        var record = tracker.GetRecord(skillName);
        if (record == null) return false;
        
        // Assert: 两者都应该返回 true
        return skill.IsAvailable && record.IsEstimatedReady;
    }
    
    /// <summary>
    /// Property 7.2: 当 CooldownTracker 记录技能在冷却中时，IsAvailable 应该返回 false
    /// </summary>
    [Property(MaxTest = 100)]
    public bool IsAvailableReturnsFalseWhenTrackerSaysOnCooldown(PositiveInt cooldownGen)
    {
        // Arrange - 使用较长的冷却时间确保技能在冷却中
        var cooldown = (cooldownGen.Get % 60) + 60; // 60-120秒
        var skillName = $"TestSkill_{Guid.NewGuid():N}";
        var config = CreateSkillConfig(skillName, cooldown);
        var tracker = new SkillCooldownTracker();
        var skill = new SkillRuntimeState(config, tracker);
        
        // Act - 记录技能使用（不记录就绪）
        tracker.RecordSkillUse(skillName, cooldown);
        
        // 获取 tracker 的记录
        var record = tracker.GetRecord(skillName);
        if (record == null) return false;
        
        // Assert: 两者都应该返回 false（因为冷却时间很长）
        return skill.IsAvailable == record.IsEstimatedReady && !skill.IsAvailable;
    }
    
    /// <summary>
    /// Property 7.3: 没有 CooldownTracker 时，回退到本地计算
    /// </summary>
    [Property(MaxTest = 100)]
    public bool FallsBackToLocalCalculationWithoutTracker(PositiveInt cooldownGen)
    {
        // Arrange - 不传入 tracker
        var cooldown = (cooldownGen.Get % 60) + 1;
        var skillName = $"TestSkill_{Guid.NewGuid():N}";
        var config = CreateSkillConfig(skillName, cooldown);
        var skill = new SkillRuntimeState(config, tracker: null);
        
        // Act - 新创建的技能应该是可用的（从未使用过）
        var isAvailableBeforeUse = skill.IsAvailable;
        
        // 标记为已使用
        skill.MarkAsUsed();
        
        // 立即检查应该不可用（除非冷却时间为0）
        var isAvailableAfterUse = skill.IsAvailable;
        
        // Assert: 使用前应该可用，使用后应该不可用（假设冷却时间 > 0）
        return isAvailableBeforeUse && (cooldown == 0 || !isAvailableAfterUse);
    }
    
    /// <summary>
    /// Property 7.4: CooldownTracker 记录不存在时，回退到本地计算
    /// </summary>
    [Property(MaxTest = 100)]
    public bool FallsBackToLocalCalculationWhenNoRecord(PositiveInt cooldownGen)
    {
        // Arrange
        var cooldown = (cooldownGen.Get % 60) + 1;
        var skillName = $"TestSkill_{Guid.NewGuid():N}";
        var config = CreateSkillConfig(skillName, cooldown);
        var tracker = new SkillCooldownTracker();
        var skill = new SkillRuntimeState(config, tracker);
        
        // Act - 不记录任何使用，tracker 中没有记录
        // 此时应该回退到本地计算
        
        // Assert: 新技能应该是可用的（从未使用过）
        return skill.IsAvailable;
    }
}
