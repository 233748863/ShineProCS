# 需求文档

## 简介

本功能旨在扩展技能配置系统，添加"排除Buff条件"功能。当指定的Buff存在时，技能应被跳过而不是释放。这是对现有"条件Buff"功能的补充，用于实现Python版本中"七情气劲已开启则跳过七情和合"等逻辑。

## 术语表

- **条件Buff(Condition_Buff)**: 技能释放的前置条件，Buff存在时才能释放
- **排除Buff(Exclude_Buff)**: 技能释放的排除条件，Buff存在时应跳过该技能
- **技能策略(Skill_Strategy)**: 技能选择策略，决定下一个释放的技能
- **条件评估器(Condition_Evaluator)**: 负责评估技能释放条件的服务

## 需求列表

### 需求1: 排除Buff条件配置

**用户故事:** 作为用户，我希望能够配置"当某个Buff存在时跳过技能"的条件，以便实现更复杂的技能循环逻辑。

#### 验收标准

1. 当技能配置了ExcludeConditionBuff时，技能策略应在选择技能前检查该Buff是否存在
2. 当ExcludeConditionBuff存在时，技能策略应跳过该技能并继续检查下一个优先级的技能
3. 当ExcludeConditionBuff不存在时，技能策略应继续评估其他条件
4. SkillConfig模型应包含ExcludeConditionBuff属性

### 需求2: 条件评估器扩展

**用户故事:** 作为用户，我希望ConditionEvaluator能够正确处理排除Buff条件，以便与现有条件检查逻辑无缝集成。

#### 验收标准

1. ConditionEvaluator应在EvaluateSkillConditions方法中检查ExcludeConditionBuff
2. 排除Buff检查应在条件Buff检查之后执行
3. 当技能同时配置了ConditionBuff和ExcludeConditionBuff时，两个条件都必须满足才能释放技能
4. 当ExcludeConditionBuff为空时，条件评估器应忽略该检查

### 需求3: 预设配置更新

**用户故事:** 作为用户，我希望素柯门派预设配置能够使用排除Buff条件，以便完美复现Python版本的七情和合逻辑。

#### 验收标准

1. 七情和合技能应配置ExcludeConditionBuff="七情气劲"
2. 当七情气劲Buff存在时，七情和合技能应被跳过
3. 当七情气劲Buff不存在且千枝气劲存在时，七情和合技能应可以释放
4. 预设配置应包含七情气劲Buff的检测配置

### 需求4: UI支持

**用户故事:** 作为用户，我希望能够在技能配置界面中设置排除Buff条件，以便方便地配置复杂的技能逻辑。

#### 验收标准

1. 技能配置界面应显示ExcludeConditionBuff下拉选择框
2. 下拉选择框应列出BuffLibrary中的所有可用Buff
3. 用户应能够清除已选择的排除Buff条件
4. 配置变更应能够正确保存和加载

