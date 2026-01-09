using FsCheck;
using FsCheck.Xunit;
using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Strategies;
using ShineProCS.Models;

namespace ShineProCS.Tests;

/// <summary>
/// Property-based tests for Strategy Priority Calculation
/// **Feature: business-logic-fixes, Property 9: Strategy Priority Calculation Uses Config**
/// **Validates: Requirements 6.1**
/// </summary>
public class StrategyPriorityCalculationTests
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
        // 设置技能为可用状态
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
            MpPercentage = 1.0, // 100% MP
            HpPercentage = 1.0, // 100% HP
            CurrentHpPercent = 100,
            TargetHpPercent = 100,
            IsCasting = false,
            IsGlobalCdActive = false
        };
    }

    /// <summary>
    /// Property 9: Strategy Priority Calculation Uses Config
    /// For any skill selection in SmartStrategy, the priority bonus for combo skills 
    /// SHALL equal AppSettings.ComboSkillPriorityBonus, not a hardcoded value.
    /// **Validates: Requirements 6.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool ComboSkillPriorityUsesConfigValue(
        PositiveInt basePriorityGen,
        PositiveInt comboBonusGen)
    {
        // Arrange - 配置参数
        var basePriority = basePriorityGen.Get % 100; // 0-99
        var comboBonus = (comboBonusGen.Get % 200) + 1; // 1-200
        
        // 创建两个技能：一个普通技能，一个联动技能
        // 普通技能优先级 = basePriority + comboBonus + 1 (比联动技能基础优先级高)
        // 联动技能优先级 = basePriority (但有联动加成)
        var normalSkill = CreateAvailableSkill("NormalSkill", basePriority + comboBonus + 1, 0x41);
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
        
        // Assert: 
        // 联动技能有效优先级 = basePriority + comboBonus
        // 普通技能有效优先级 = basePriority + comboBonus + 1
        // 所以普通技能应该被选中（因为它的有效优先级更高）
        return selected?.Config.Name == "NormalSkill";
    }
    
    /// <summary>
    /// Property 9.1: Combo skill gets selected when its effective priority is higher
    /// **Validates: Requirements 6.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool ComboSkillSelectedWhenEffectivePriorityHigher(
        PositiveInt basePriorityGen,
        PositiveInt comboBonusGen)
    {
        // Arrange
        var basePriority = basePriorityGen.Get % 100;
        var comboBonus = (comboBonusGen.Get % 200) + 10; // 至少10的加成
        
        // 联动技能基础优先级 = basePriority
        // 普通技能优先级 = basePriority + comboBonus - 1 (比联动技能有效优先级低1)
        // 联动技能有效优先级 = basePriority + comboBonus
        var normalSkill = CreateAvailableSkill("NormalSkill", basePriority + comboBonus - 1, 0x41);
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
    /// Property 9.2: Different config values produce different selection results
    /// **Validates: Requirements 6.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool DifferentConfigValuesProduceDifferentResults(
        PositiveInt basePriorityGen)
    {
        // Arrange
        var basePriority = (basePriorityGen.Get % 50) + 10; // 10-59
        
        // 创建两个技能
        // 普通技能优先级 = basePriority + 25
        // 联动技能优先级 = basePriority
        var normalSkill = CreateAvailableSkill("NormalSkill", basePriority + 25, 0x41);
        var comboSkill = CreateAvailableSkill("ComboSkill", basePriority, 0x42, preCastKeyCode: 0x43);
        
        // 测试两种配置
        var lowBonusSettings = new AppSettings
        {
            ComboSkillPriorityBonus = 20, // 联动有效优先级 = basePriority + 20 < basePriority + 25
            EnableSmartMode = true
        };
        
        var highBonusSettings = new AppSettings
        {
            ComboSkillPriorityBonus = 30, // 联动有效优先级 = basePriority + 30 > basePriority + 25
            EnableSmartMode = true
        };
        
        var strategy = new SmartStrategy();
        
        // Act - 低加成配置
        var lowBonusContext = new StrategyContext
        {
            SkillStates = [
                CreateAvailableSkill("NormalSkill", basePriority + 25, 0x41),
                CreateAvailableSkill("ComboSkill", basePriority, 0x42, preCastKeyCode: 0x43)
            ],
            GameState = CreateDefaultGameState(),
            LoopMode = "Smart",
            Settings = lowBonusSettings
        };
        var lowBonusSelected = strategy.SelectSkill(lowBonusContext);
        
        // Act - 高加成配置
        var highBonusContext = new StrategyContext
        {
            SkillStates = [
                CreateAvailableSkill("NormalSkill", basePriority + 25, 0x41),
                CreateAvailableSkill("ComboSkill", basePriority, 0x42, preCastKeyCode: 0x43)
            ],
            GameState = CreateDefaultGameState(),
            LoopMode = "Smart",
            Settings = highBonusSettings
        };
        var highBonusSelected = strategy.SelectSkill(highBonusContext);
        
        // Assert: 不同配置应该产生不同的选择结果
        return lowBonusSelected?.Config.Name == "NormalSkill" && 
               highBonusSelected?.Config.Name == "ComboSkill";
    }
    
    /// <summary>
    /// Property 9.3: Default bonus is used when Settings is null
    /// **Validates: Requirements 6.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool DefaultBonusUsedWhenSettingsNull(PositiveInt basePriorityGen)
    {
        // Arrange
        var basePriority = (basePriorityGen.Get % 50) + 10;
        
        // 普通技能优先级 = basePriority + 49 (比默认加成50低1)
        // 联动技能优先级 = basePriority
        // 联动有效优先级 = basePriority + 50 (默认值)
        var normalSkill = CreateAvailableSkill("NormalSkill", basePriority + 49, 0x41);
        var comboSkill = CreateAvailableSkill("ComboSkill", basePriority, 0x42, preCastKeyCode: 0x43);
        
        var context = new StrategyContext
        {
            SkillStates = [normalSkill, comboSkill],
            GameState = CreateDefaultGameState(),
            LoopMode = "Smart",
            Settings = null // 没有设置
        };
        
        var strategy = new SmartStrategy();
        
        // Act
        var selected = strategy.SelectSkill(context);
        
        // Assert: 联动技能应该被选中（使用默认加成50）
        return selected?.Config.Name == "ComboSkill";
    }
    
    /// <summary>
    /// Property 9.4: Zero bonus means no priority boost for combo skills
    /// **Validates: Requirements 6.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool ZeroBonusMeansNoPriorityBoost(PositiveInt basePriorityGen)
    {
        // Arrange
        var basePriority = (basePriorityGen.Get % 50) + 10;
        
        // 普通技能优先级 = basePriority + 1
        // 联动技能优先级 = basePriority
        // 当加成为0时，联动有效优先级 = basePriority
        var normalSkill = CreateAvailableSkill("NormalSkill", basePriority + 1, 0x41);
        var comboSkill = CreateAvailableSkill("ComboSkill", basePriority, 0x42, preCastKeyCode: 0x43);
        
        var settings = new AppSettings
        {
            ComboSkillPriorityBonus = 0, // 无加成
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
        
        // Assert: 普通技能应该被选中（因为联动技能没有加成）
        return selected?.Config.Name == "NormalSkill";
    }
}
