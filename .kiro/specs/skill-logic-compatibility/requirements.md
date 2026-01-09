# 需求文档

## 简介

本功能旨在通过扩展C#版本(ShineProCS)的配置系统和策略逻辑，使其能够完全模拟Python版本(ShineProRe)的技能释放行为。重点是通过配置驱动的方式实现气劲状态检测、素柯技能判断、七情和合状态追踪等Python版本的核心逻辑，而不是硬编码特定游戏的技能循环。

## 术语表

- **技能引擎(Skill_Engine)**: 技能循环引擎，负责协调技能选择和释放
- **状态检测器(State_Detector)**: 状态检测器，负责检测游戏状态（HP/MP/Buff/技能可用性）
- **技能策略(Skill_Strategy)**: 技能选择策略，决定下一个释放的技能
- **Buff状态(Buff_State)**: Buff状态，表示角色身上的增益/减益效果
- **条件技能(Conditional_Skill)**: 条件技能，需要满足特定条件才能释放的技能
- **技能组(Skill_Group)**: 技能组，一组相关联的技能，共享某些条件或状态
- **优先级覆盖(Priority_Override)**: 优先级覆盖，在特定条件下临时改变技能优先级

## 需求列表

### 需求1: Buff状态检测扩展

**用户故事:** 作为用户，我希望能够配置基于Buff的技能释放条件，以便技能可以根据Buff存在与否来决定是否释放。

#### 验收标准

1. 当技能配置了ConditionBuff时，技能策略应在选择技能前检查该Buff是否存在
2. 当ConditionBuff不存在时，技能策略应跳过该技能并继续检查下一个优先级的技能
3. 当ConditionBuff存在时，技能策略应将该技能纳入候选列表
4. 状态检测器应支持在单个检测周期内检测多个Buff状态

### 需求2: 条件优先级覆盖

**用户故事:** 作为用户，我希望能够配置基于Buff状态的优先级覆盖，以便技能优先级可以动态变化。

#### 验收标准

1. 当技能配置了PriorityOverrideCondition时，技能策略应在应用优先级前评估该条件
2. 当PriorityOverrideCondition满足时，技能策略应使用PriorityOverrideValue替代基础Priority
3. 当PriorityOverrideCondition不满足时，技能策略应使用基础Priority值
4. SkillConfig模型应包含PriorityOverrideCondition和PriorityOverrideValue属性

### 需求3: 技能组联动配置

**用户故事:** 作为用户，我希望能够配置具有共享条件的技能组，以便相关技能可以统一管理。

#### 验收标准

1. 当技能属于某个技能组时，技能策略应首先评估组级别的条件
2. 当组条件不满足时，技能策略应跳过该组中的所有技能
3. AppSettings应包含SkillGroups集合用于定义技能组
4. 当技能同时具有个人条件和组条件时，技能策略应要求两者都满足

### 需求4: 蓝量条件扩展

**用户故事:** 作为用户，我希望能够配置基于MP的技能选择逻辑，以便技能可以根据MP水平进行优先级调整。

#### 验收标准

1. 当技能配置了MpPriorityBoost且当前MP高于MpThresholdForBoost时，技能策略应将MpPriorityBoost加到技能的有效优先级上
2. 当当前MP低于MpThresholdForBoost时，技能策略应使用基础优先级而不加成
3. SkillConfig模型应包含MpPriorityBoost和MpThresholdForBoost属性
4. 当MpPriorityBoost为0时，技能策略应忽略该技能的MP优先级加成

### 需求5: 前置技能链配置

**用户故事:** 作为用户，我希望能够配置技能链，使一个技能可以触发另一个技能，以便自动化连招序列。

#### 验收标准

1. 当技能配置了PreCastSkillName时，技能引擎应首先尝试释放前置技能
2. 当前置技能成功释放后，技能引擎应等待ComboDelay后再释放主技能
3. 当前置技能释放失败时，技能引擎应在本周期跳过主技能
4. SkillConfig模型应包含PreCastSkillName属性用于通过名称引用另一个技能

### 需求6: 状态追踪器

**用户故事:** 作为用户，我希望系统能够跨周期追踪某些状态，以便实现复杂的技能循环。

#### 验收标准

1. 技能引擎应维护一个StateTracker字典用于存储命名的布尔状态
2. 当技能配置了SetStateOnCast时，技能引擎应在成功施法后将指定状态设为true
3. 当技能配置了ClearStateOnCast时，技能引擎应在成功施法后将指定状态设为false
4. 当技能配置了RequireState时，技能策略应仅在该状态为true时选择该技能
5. StateTracker应在多个周期间持久化状态，直到被显式清除

### 需求7: 默认技能配置模板

**用户故事:** 作为用户，我希望有一个匹配Python版本行为的预配置技能模板，以便快速设置相同的技能循环。

#### 验收标准

1. 系统应提供一个默认的skills.json模板，其中技能配置与Python版本的优先级顺序匹配
2. 模板应包含Python版本的全部9个技能：青川濯莲、七情和合、千枝绽蕊、逐云寒蕊、当归四逆、银光照雪、赤芍寒香、绿野蔓生、白芷含芳
3. 模板应配置适当的Priority值以匹配Python版本的固定顺序
4. 模板应为具有连招关系的技能配置PreCastSkillName和ConditionBuff

### 需求8: 智能策略增强

**用户故事:** 作为用户，我希望SmartStrategy支持所有新的配置选项，以便复杂的技能循环能够正确工作。

#### 验收标准

1. 在评估技能时，SmartStrategy应按顺序应用所有条件检查：Enabled、ConditionBuff、RequireState、MinMp、HpCondition
2. 在计算有效优先级时，SmartStrategy应求和：BasePriority + PriorityOverride + MpPriorityBoost + ComboBonus
3. SmartStrategy应在所有有效候选中选择有效优先级最高的技能
4. 当多个技能具有相同的有效优先级时，SmartStrategy应根据配置顺序选择
