using FsCheck;
using FsCheck.Xunit;
using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Strategies;
using ShineProCS.Models;

namespace ShineProCS.Tests;

/// <summary>
/// Property-based tests for Skill Selection Priority Order
/// **Feature: business-logic-fixes, Property 10: Skill Selection Respects Priority Order**
/// **Validates: Requirements 6.2**
/// </summary>
public class SkillSelectionPriorityOrderTests
{
    /// <summary>
    /// 创建一个可用的技能运行时状态
    /// </summary>
    private static SkillRuntimeState CreateAvailableSkill(
        string name, 
        int priority, 
        int keyCode, 
        int preCastKeyCode = 0)
    {
        var config = new SkillConfig
        {
            Name = name,
            KeyCode = keyCode,
            Priority = priority,
            Enabled = true,
            PreCastKeyCode = preCastKeyCode,
            MinMp = 0,
            HpCheckTarget = 0,
            HpThreshold = 0
        };
        
        var state = new SkillRuntimeState(config);
        state.IsVisuallyReady = true;
        
        return state;
    }
    
    /// <summary>
    /// 创建默认的游戏状态
    /// </summary>
    private static GameState CreateDefaultGameState()
    {
        return new GameState
        {
            MpPercentage = 1.0,
            HpPercentage = 1.0,
            CurrentHpPercent = 100,
            TargetHpPercent = 100,
            IsCasting = false,
            IsGlobalCdActive = false
        };
    }
    
    /// <summary>
    /// 计算技能的有效优先级
    /// </summary>
    private static int CalculateEffectivePriority(SkillRuntimeState skill, int comboBonus)
    {
        return skill.Config.Priority + (skill.Config.PreCastKeyCode > 0 ? comboBonus : 0);
    }

    /// <summary>
    /// Property 10: Skill Selection Respects Priority Order
    /// For any set of available skills with different priorities, the Strategy SHALL select 
    /// the skill with the highest effective priority (base priority + combo bonus if applicable).
    /// **Validates: Requirements 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool SelectsHighestEffectivePrioritySkill(
        PositiveInt priority1Gen,
        PositiveInt priority2Gen,
        PositiveInt priority3Gen,
        PositiveInt comboBonusGen)
    {
        // Arrange - 创建三个不同优先级的技能
        var priority1 = priority1Gen.Get % 100;
        var priority2 = priority2Gen.Get % 100;
        var priority3 = priority3Gen.Get % 100;
        var comboBonus = (comboBonusGen.Get % 50) + 10;
        
        // 确保优先级不同
        if (priority1 == priority2) priority2 = (priority2 + 1) % 100;
        if (priority2 == priority3) priority3 = (priority3 + 2) % 100;
        if (priority1 == priority3) priority3 = (priority3 + 3) % 100;
        
        var skill1 = CreateAvailableSkill("Skill1", priority1, 0x41);
        var skill2 = CreateAvailableSkill("Skill2", priority2, 0x42);
        var skill3 = CreateAvailableSkill("Skill3", priority3, 0x43);
        
        var settings = new AppSettings
        {
            ComboSkillPriorityBonus = comboBonus,
            EnableSmartMode = true
        };
        
        var context = new StrategyContext
        {
            SkillStates = [skill1, skill2, skill3],
            GameState = CreateDefaultGameState(),
            LoopMode = "Smart",
            Settings = settings
        };
        
        var strategy = new SmartStrategy();
        
        // Act
        var selected = strategy.SelectSkill(context);
        
        // Assert: 应该选择有效优先级最高的技能
        var expectedPriority = Math.Max(Math.Max(priority1, priority2), priority3);
        var expectedSkillName = priority1 == expectedPriority ? "Skill1" 
            : priority2 == expectedPriority ? "Skill2" 
            : "Skill3";
        
        return selected?.Config.Name == expectedSkillName;
    }
    
    /// <summary>
    /// Property 10.1: Combo skill with lower base priority can win with bonus
    /// **Validates: Requirements 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool ComboSkillWithLowerBasePriorityCanWin(
        PositiveInt basePriorityGen,
        PositiveInt comboBonusGen)
    {
        // Arrange
        var basePriority = (basePriorityGen.Get % 50) + 10;
        var comboBonus = (comboBonusGen.Get % 100) + 20; // 至少20的加成
        
        // 普通技能优先级 = basePriority + comboBonus/2 (比联动有效优先级低)
        // 联动技能优先级 = basePriority
        // 联动有效优先级 = basePriority + comboBonus
        var normalSkill = CreateAvailableSkill("NormalSkill", basePriority + comboBonus / 2, 0x41);
        var comboSkill = CreateAvailableSkill("ComboSkill", basePriority, 0x42, preCastKeyCode: 0x43);
        
        var settings = new AppSettings
        {
            ComboSkillPriorityBonus = comboBonus,
            EnableSmartMode = true
        };
        
        var context = new StrategyContext
        {
            SkillStates = [normalSkill, comboSkill],
            GameState = CreateDefaultGameState(),
            LoopMode = "Smart",
            Settings = settings
        };
        
        var strategy = new SmartStrategy();
        
        // Act
        var selected = strategy.SelectSkill(context);
        
        // Assert: 联动技能应该被选中（有效优先级更高）
        return selected?.Config.Name == "ComboSkill";
    }
    
    /// <summary>
    /// Property 10.2: Selection is deterministic for same input
    /// **Validates: Requirements 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool SelectionIsDeterministic(
        PositiveInt priority1Gen,
        PositiveInt priority2Gen,
        PositiveInt comboBonusGen)
    {
        // Arrange
        var priority1 = priority1Gen.Get % 100;
        var priority2 = priority2Gen.Get % 100;
        var comboBonus = comboBonusGen.Get % 100;
        
        var settings = new AppSettings
        {
            ComboSkillPriorityBonus = comboBonus,
            EnableSmartMode = true
        };
        
        var strategy = new SmartStrategy();
        
        // 创建相同的上下文两次
        var context1 = new StrategyContext
        {
            SkillStates = [
                CreateAvailableSkill("Skill1", priority1, 0x41),
                CreateAvailableSkill("Skill2", priority2, 0x42, preCastKeyCode: 0x43)
            ],
            GameState = CreateDefaultGameState(),
            LoopMode = "Smart",
            Settings = settings
        };
        
        var context2 = new StrategyContext
        {
            SkillStates = [
                CreateAvailableSkill("Skill1", priority1, 0x41),
                CreateAvailableSkill("Skill2", priority2, 0x42, preCastKeyCode: 0x43)
            ],
            GameState = CreateDefaultGameState(),
            LoopMode = "Smart",
            Settings = settings
        };
        
        // Act
        var selected1 = strategy.SelectSkill(context1);
        var selected2 = strategy.SelectSkill(context2);
        
        // Assert: 相同输入应该产生相同输出
        return selected1?.Config.Name == selected2?.Config.Name;
    }
    
    /// <summary>
    /// Property 10.3: Multiple combo skills are compared correctly
    /// **Validates: Requirements 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool MultipleComboSkillsComparedCorrectly(
        PositiveInt priority1Gen,
        PositiveInt priority2Gen,
        PositiveInt comboBonusGen)
    {
        // Arrange - 两个联动技能
        var priority1 = priority1Gen.Get % 100;
        var priority2 = priority2Gen.Get % 100;
        var comboBonus = comboBonusGen.Get % 100;
        
        // 确保优先级不同
        if (priority1 == priority2) priority2 = (priority2 + 1) % 100;
        
        var comboSkill1 = CreateAvailableSkill("ComboSkill1", priority1, 0x41, preCastKeyCode: 0x44);
        var comboSkill2 = CreateAvailableSkill("ComboSkill2", priority2, 0x42, preCastKeyCode: 0x45);
        
        var settings = new AppSettings
        {
            ComboSkillPriorityBonus = comboBonus,
            EnableSmartMode = true
        };
        
        var context = new StrategyContext
        {
            SkillStates = [comboSkill1, comboSkill2],
            GameState = CreateDefaultGameState(),
            LoopMode = "Smart",
            Settings = settings
        };
        
        var strategy = new SmartStrategy();
        
        // Act
        var selected = strategy.SelectSkill(context);
        
        // Assert: 应该选择基础优先级更高的联动技能
        // 因为两个都是联动技能，加成相同，所以比较基础优先级
        var expectedName = priority1 > priority2 ? "ComboSkill1" : "ComboSkill2";
        return selected?.Config.Name == expectedName;
    }
    
    /// <summary>
    /// Property 10.4: Order of skills in list doesn't affect selection
    /// **Validates: Requirements 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool OrderInListDoesNotAffectSelection(
        PositiveInt priority1Gen,
        PositiveInt priority2Gen,
        PositiveInt priority3Gen,
        PositiveInt comboBonusGen)
    {
        // Arrange
        var priority1 = priority1Gen.Get % 100;
        var priority2 = priority2Gen.Get % 100;
        var priority3 = priority3Gen.Get % 100;
        var comboBonus = comboBonusGen.Get % 100;
        
        // 确保优先级不同
        if (priority1 == priority2) priority2 = (priority2 + 1) % 100;
        if (priority2 == priority3) priority3 = (priority3 + 2) % 100;
        if (priority1 == priority3) priority3 = (priority3 + 3) % 100;
        
        var settings = new AppSettings
        {
            ComboSkillPriorityBonus = comboBonus,
            EnableSmartMode = true
        };
        
        var strategy = new SmartStrategy();
        
        // 创建两个不同顺序的上下文
        var context1 = new StrategyContext
        {
            SkillStates = [
                CreateAvailableSkill("Skill1", priority1, 0x41),
                CreateAvailableSkill("Skill2", priority2, 0x42),
                CreateAvailableSkill("Skill3", priority3, 0x43)
            ],
            GameState = CreateDefaultGameState(),
            LoopMode = "Smart",
            Settings = settings
        };
        
        var context2 = new StrategyContext
        {
            SkillStates = [
                CreateAvailableSkill("Skill3", priority3, 0x43),
                CreateAvailableSkill("Skill1", priority1, 0x41),
                CreateAvailableSkill("Skill2", priority2, 0x42)
            ],
            GameState = CreateDefaultGameState(),
            LoopMode = "Smart",
            Settings = settings
        };
        
        // Act
        var selected1 = strategy.SelectSkill(context1);
        var selected2 = strategy.SelectSkill(context2);
        
        // Assert: 不同顺序应该选择相同的技能（按优先级）
        return selected1?.Config.Priority == selected2?.Config.Priority;
    }
    
    /// <summary>
    /// Property 10.5: Empty skill list returns null
    /// **Validates: Requirements 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool EmptySkillListReturnsNull(PositiveInt comboBonusGen)
    {
        // Arrange
        var comboBonus = comboBonusGen.Get % 100;
        
        var settings = new AppSettings
        {
            ComboSkillPriorityBonus = comboBonus,
            EnableSmartMode = true
        };
        
        var context = new StrategyContext
        {
            SkillStates = [],
            GameState = CreateDefaultGameState(),
            LoopMode = "Smart",
            Settings = settings
        };
        
        var strategy = new SmartStrategy();
        
        // Act
        var selected = strategy.SelectSkill(context);
        
        // Assert: 空列表应该返回null
        return selected == null;
    }
}
