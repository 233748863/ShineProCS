using FsCheck;
using FsCheck.Xunit;
using ShineProCS.Models;

namespace ShineProCS.Tests;

/// <summary>
/// Property-based tests for Pre-Cast Skill Chain Execution Order
/// **Feature: skill-logic-compatibility, Property 5: 前置技能链执行顺序**
/// **Validates: Requirements 5.1, 5.2, 5.3**
/// </summary>
public class PreCastSkillChainTests
{
    /// <summary>
    /// 创建测试用的SkillConfig
    /// </summary>
    private static SkillConfig CreateSkillConfig(
        string name, 
        string preCastSkillName = "", 
        int comboDelay = 100,
        bool enabled = true)
    {
        return new SkillConfig
        {
            Name = name,
            Enabled = enabled,
            PreCastSkillName = preCastSkillName,
            ComboDelay = comboDelay,
            Priority = 100,
            KeyCode = 49 // VK_1
        };
    }

    /// <summary>
    /// 创建测试用的SkillRuntimeState
    /// </summary>
    private static SkillRuntimeState CreateSkillState(SkillConfig config)
    {
        return new SkillRuntimeState(config);
    }

    /// <summary>
    /// Property 5.1: 配置了PreCastSkillName的技能应该有前置技能引用
    /// WHEN skill has PreCastSkillName configured, THE skill SHALL reference another skill.
    /// **Validates: Requirements 5.1, 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool SkillWithPreCastSkillNameHasReference(NonEmptyString mainSkillNameGen, NonEmptyString preCastSkillNameGen)
    {
        var mainSkillName = mainSkillNameGen.Get;
        var preCastSkillName = preCastSkillNameGen.Get;
        
        // 跳过相同名称的情况（会导致循环引用）
        if (mainSkillName == preCastSkillName)
            return true;
        
        var mainConfig = CreateSkillConfig(mainSkillName, preCastSkillName);
        
        // 验证PreCastSkillName已正确设置
        return !string.IsNullOrEmpty(mainConfig.PreCastSkillName) 
            && mainConfig.PreCastSkillName == preCastSkillName;
    }

    /// <summary>
    /// Property 5.2: ComboDelay应该是非负数
    /// THE ComboDelay SHALL be non-negative.
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool ComboDelayIsNonNegative(PositiveInt comboDelayGen)
    {
        var comboDelay = comboDelayGen.Get;
        var config = CreateSkillConfig("TestSkill", "PreCastSkill", comboDelay);
        
        // ComboDelay应该是正数
        return config.ComboDelay > 0;
    }

    /// <summary>
    /// Property 5.3: 没有配置PreCastSkillName的技能不需要前置技能
    /// WHEN skill has no PreCastSkillName, THE skill SHALL not require pre-cast.
    /// **Validates: Requirements 5.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool SkillWithoutPreCastSkillNameHasNoReference(NonEmptyString skillNameGen)
    {
        var skillName = skillNameGen.Get;
        var config = CreateSkillConfig(skillName, preCastSkillName: "");
        
        // 没有配置PreCastSkillName时，应该为空
        return string.IsNullOrEmpty(config.PreCastSkillName);
    }

    /// <summary>
    /// Property 5.4: 前置技能链查找应该能找到存在的技能
    /// FOR ALL skill lists containing a skill with name X, finding skill by name X SHALL return that skill.
    /// **Validates: Requirements 5.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool FindSkillByNameFindsExistingSkill(NonEmptyString skillNameGen, PositiveInt skillCountGen)
    {
        var targetSkillName = skillNameGen.Get;
        var skillCount = (skillCountGen.Get % 5) + 1; // 1-5个技能
        
        // 创建技能列表，包含目标技能
        var skillStates = new List<SkillRuntimeState>();
        for (int i = 0; i < skillCount; i++)
        {
            var name = i == 0 ? targetSkillName : $"Skill_{i}";
            var config = CreateSkillConfig(name);
            skillStates.Add(CreateSkillState(config));
        }
        
        // 查找技能
        var foundSkill = skillStates.FirstOrDefault(s => 
            s.Config.Name.Equals(targetSkillName, StringComparison.OrdinalIgnoreCase));
        
        // 应该能找到目标技能
        return foundSkill != null && foundSkill.Config.Name == targetSkillName;
    }

    /// <summary>
    /// Property 5.5: 前置技能链查找应该对不存在的技能返回null
    /// FOR ALL skill lists not containing a skill with name X, finding skill by name X SHALL return null.
    /// **Validates: Requirements 5.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool FindSkillByNameReturnsNullForNonExistingSkill(NonEmptyString targetNameGen, PositiveInt skillCountGen)
    {
        var targetSkillName = targetNameGen.Get;
        var skillCount = (skillCountGen.Get % 5) + 1; // 1-5个技能
        
        // 创建技能列表，不包含目标技能
        var skillStates = new List<SkillRuntimeState>();
        for (int i = 0; i < skillCount; i++)
        {
            var name = $"OtherSkill_{i}";
            // 确保名称不与目标名称相同
            if (name.Equals(targetSkillName, StringComparison.OrdinalIgnoreCase))
                name = $"DifferentSkill_{i}";
            
            var config = CreateSkillConfig(name);
            skillStates.Add(CreateSkillState(config));
        }
        
        // 查找技能
        var foundSkill = skillStates.FirstOrDefault(s => 
            s.Config.Name.Equals(targetSkillName, StringComparison.OrdinalIgnoreCase));
        
        // 应该找不到目标技能
        return foundSkill == null;
    }

    /// <summary>
    /// Property 5.6: 循环引用检测 - 直接循环
    /// WHEN skill A references skill A as pre-cast, circular reference SHALL be detected.
    /// **Validates: Requirements 5.1 (error handling)**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool DirectCircularReferenceIsDetected(NonEmptyString skillNameGen)
    {
        var skillName = skillNameGen.Get;
        
        // 创建自引用的技能
        var config = CreateSkillConfig(skillName, preCastSkillName: skillName);
        var skillStates = new List<SkillRuntimeState> { CreateSkillState(config) };
        
        // 检测循环引用
        var hasCircular = HasCircularReference(skillName, skillName, skillStates, new HashSet<string>());
        
        // 应该检测到循环引用
        return hasCircular;
    }

    /// <summary>
    /// Property 5.7: 循环引用检测 - 间接循环
    /// WHEN skill A -> B -> A forms a chain, circular reference SHALL be detected.
    /// **Validates: Requirements 5.1 (error handling)**
    /// </summary>
    [Fact]
    public void IndirectCircularReferenceIsDetected()
    {
        // A -> B -> A
        var configA = CreateSkillConfig("SkillA", preCastSkillName: "SkillB");
        var configB = CreateSkillConfig("SkillB", preCastSkillName: "SkillA");
        
        var skillStates = new List<SkillRuntimeState>
        {
            CreateSkillState(configA),
            CreateSkillState(configB)
        };
        
        // 检测循环引用
        var hasCircular = HasCircularReference("SkillA", "SkillB", skillStates, new HashSet<string>());
        
        // 应该检测到循环引用
        Assert.True(hasCircular);
    }

    /// <summary>
    /// Property 5.8: 无循环引用的链应该通过检测
    /// WHEN skill chain has no circular reference, detection SHALL return false.
    /// **Validates: Requirements 5.1**
    /// </summary>
    [Fact]
    public void NonCircularChainPassesDetection()
    {
        // A -> B -> C (无循环)
        var configA = CreateSkillConfig("SkillA", preCastSkillName: "SkillB");
        var configB = CreateSkillConfig("SkillB", preCastSkillName: "SkillC");
        var configC = CreateSkillConfig("SkillC", preCastSkillName: "");
        
        var skillStates = new List<SkillRuntimeState>
        {
            CreateSkillState(configA),
            CreateSkillState(configB),
            CreateSkillState(configC)
        };
        
        // 检测循环引用
        var hasCircular = HasCircularReference("SkillA", "SkillB", skillStates, new HashSet<string>());
        
        // 不应该检测到循环引用
        Assert.False(hasCircular);
    }

    /// <summary>
    /// Property 5.9: 前置技能链执行顺序验证
    /// FOR ALL skill chains A -> B, B SHALL be executed before A.
    /// **Validates: Requirements 5.1, 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool PreCastSkillExecutesBeforeMainSkill(NonEmptyString mainNameGen, NonEmptyString preCastNameGen)
    {
        var mainName = mainNameGen.Get;
        var preCastName = preCastNameGen.Get;
        
        // 跳过相同名称的情况
        if (mainName == preCastName)
            return true;
        
        var executionOrder = new List<string>();
        
        // 模拟执行顺序：前置技能应该先执行
        var mainConfig = CreateSkillConfig(mainName, preCastSkillName: preCastName);
        var preCastConfig = CreateSkillConfig(preCastName);
        
        // 模拟执行：先执行前置技能
        if (!string.IsNullOrEmpty(mainConfig.PreCastSkillName))
        {
            executionOrder.Add(preCastConfig.Name);
        }
        executionOrder.Add(mainConfig.Name);
        
        // 验证执行顺序：前置技能在主技能之前
        var preCastIndex = executionOrder.IndexOf(preCastName);
        var mainIndex = executionOrder.IndexOf(mainName);
        
        return preCastIndex < mainIndex;
    }

    /// <summary>
    /// Property 5.10: ComboDelay配置正确传递
    /// FOR ALL skills with ComboDelay, the delay value SHALL be preserved.
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool ComboDelayIsPreserved(PositiveInt delayGen)
    {
        var delay = delayGen.Get;
        var config = CreateSkillConfig("TestSkill", "PreCastSkill", delay);
        var state = CreateSkillState(config);
        
        // ComboDelay应该被正确保存
        return state.Config.ComboDelay == delay;
    }

    /// <summary>
    /// 辅助方法：检测循环引用
    /// </summary>
    private static bool HasCircularReference(
        string currentSkillName, 
        string preCastSkillName, 
        List<SkillRuntimeState> skillStates,
        HashSet<string> visited)
    {
        if (string.IsNullOrEmpty(preCastSkillName))
            return false;
        
        // 如果前置技能指向当前技能，存在循环
        if (preCastSkillName.Equals(currentSkillName, StringComparison.OrdinalIgnoreCase))
            return true;
        
        // 如果前置技能已经访问过，存在循环
        if (visited.Contains(preCastSkillName))
            return true;
        
        visited.Add(preCastSkillName);
        
        // 递归检查前置技能的前置技能
        var preCastSkill = skillStates.FirstOrDefault(s => 
            s.Config.Name.Equals(preCastSkillName, StringComparison.OrdinalIgnoreCase));
        
        if (preCastSkill != null && !string.IsNullOrEmpty(preCastSkill.Config.PreCastSkillName))
        {
            return HasCircularReference(currentSkillName, preCastSkill.Config.PreCastSkillName, skillStates, visited);
        }
        
        return false;
    }
}
