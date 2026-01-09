# 实现计划: 技能逻辑兼容性

## 概述

通过扩展配置系统和策略逻辑，使C#版本能够模拟Python版本的技能释放行为。采用配置驱动的方式实现，不硬编码特定游戏逻辑。

## 任务列表

- [x] 1. 扩展SkillConfig模型
  - [x] 1.1 添加条件Buff属性（ConditionBuff）
    - 在SkillConfig.cs中添加ConditionBuff属性
    - 添加ObservableProperty特性
    - _需求: 1.1, 1.2, 1.3_
  - [x] 1.2 添加优先级覆盖属性（PriorityOverrideCondition, PriorityOverrideValue）
    - 添加PriorityOverrideCondition字符串属性
    - 添加PriorityOverrideValue整数属性
    - _需求: 2.1, 2.2, 2.3, 2.4_
  - [x] 1.3 添加MP优先级加成属性（MpPriorityBoost, MpThresholdForBoost）
    - 添加MpPriorityBoost整数属性
    - 添加MpThresholdForBoost双精度属性
    - _需求: 4.1, 4.2, 4.3_
  - [x] 1.4 添加前置技能名称属性（PreCastSkillName）
    - 添加PreCastSkillName字符串属性
    - _需求: 5.4_
  - [x] 1.5 添加技能组和状态追踪属性
    - 添加SkillGroup字符串属性
    - 添加SetStateOnCast字符串属性
    - 添加ClearStateOnCast字符串属性
    - 添加RequireState字符串属性
    - _需求: 3.1, 6.2, 6.3, 6.4_

- [x] 2. 扩展AppSettings模型
  - [x] 2.1 创建SkillGroupConfig类
    - 在Models文件夹创建SkillGroupConfig.cs
    - 包含Name、ConditionBuff、Enabled属性
    - _需求: 3.3_
  - [x] 2.2 在AppSettings中添加SkillGroups集合
    - 添加ObservableCollection<SkillGroupConfig>属性
    - _需求: 3.3_

- [x] 3. 实现StateTracker服务
  - [x] 3.1 创建StateTracker类
    - 在Core/Services文件夹创建StateTracker.cs
    - 实现GetState、SetState、ClearState、ClearAll方法
    - 使用Dictionary<string, bool>存储状态
    - _需求: 6.1_
  - [x] 3.2 编写StateTracker属性测试
    - **Property 6: 状态追踪持久性**
    - **验证: 需求 6.1, 6.2, 6.3, 6.4, 6.5**

- [x] 4. 实现ConditionEvaluator服务
  - [x] 4.1 创建ConditionEvaluator类
    - 在Core/Services文件夹创建ConditionEvaluator.cs
    - 实现EvaluateSkillConditions方法
    - 实现CalculateEffectivePriority方法
    - _需求: 8.1, 8.2_
  - [x] 4.2 编写Buff条件检查属性测试
    - **Property 1: Buff条件检查一致性**
    - **验证: 需求 1.1, 1.2, 1.3**
  - [x] 4.3 编写优先级覆盖属性测试
    - **Property 2: 优先级覆盖正确性**
    - **验证: 需求 2.1, 2.2, 2.3**
  - [x] 4.4 编写MP优先级加成属性测试
    - **Property 4: MP优先级加成计算**
    - **验证: 需求 4.1, 4.2**

- [x] 5. 检查点 - 确保所有测试通过
  - 确保所有测试通过，如有问题请询问用户

- [x] 6. 增强SmartStrategy
  - [x] 6.1 集成ConditionEvaluator
    - 在SmartStrategy中注入ConditionEvaluator
    - 修改SelectSkill方法使用ConditionEvaluator
    - _需求: 8.1, 8.2, 8.3, 8.4_
  - [x] 6.2 实现技能组条件检查
    - 在SelectSkill中添加技能组条件评估
    - 从AppSettings获取SkillGroups配置
    - _需求: 3.1, 3.2, 3.4_
  - [x] 6.3 编写技能组条件属性测试
    - **Property 3: 技能组条件传递性**
    - **验证: 需求 3.1, 3.2, 3.4**
  - [x] 6.4 编写智能策略选择属性测试
    - **Property 7: 智能策略选择正确性**
    - **验证: 需求 8.1, 8.2, 8.3, 8.4**

- [x] 7. 扩展SkillLoopEngine
  - [x] 7.1 集成StateTracker
    - 在SkillLoopEngine中添加StateTracker实例
    - 在ExecuteSkill成功后更新状态
    - _需求: 6.1, 6.2, 6.3_
  - [x] 7.2 实现前置技能链逻辑
    - 修改ExecuteSkillCycle支持PreCastSkillName
    - 通过技能名称查找前置技能
    - 实现前置技能释放和延迟等待
    - _需求: 5.1, 5.2, 5.3_
  - [x] 7.3 编写前置技能链属性测试
    - **Property 5: 前置技能链执行顺序**
    - **验证: 需求 5.1, 5.2, 5.3**

- [x] 8. 检查点 - 确保所有测试通过
  - 确保所有测试通过，如有问题请询问用户

- [x] 9. 创建默认技能配置模板
  - [x] 9.1 创建素柯门派技能模板
    - 创建config/presets/suke-skills.json
    - 包含9个技能的完整配置
    - 配置正确的优先级顺序
    - 配置联动关系和条件Buff
    - _需求: 7.1, 7.2, 7.3, 7.4_
  - [x] 9.2 配置Python版本的技能优先级逻辑
    - 青川濯莲: Priority=100（通用最高优先级）
    - 逐云寒蕊: Priority=90
    - 当归四逆: Priority=80（普通技能，无素柯检测）
    - 银光照雪: Priority=70
    - 赤芍寒香: Priority=60, PreCastSkillName="千枝绽蕊", MpThresholdForBoost=30
    - 绿野蔓生: Priority=50
    - 白芷含芳: Priority=40
    - 七情和合: Priority=200, ConditionBuff="千枝气劲", RequireState="七情和合启用"
    - 千枝绽蕊: Priority=150, ConditionBuff="千枝气劲"
    - _需求: 7.3, 7.4_
  - [x] 9.3 配置气劲状态Buff检测
    - 在BuffLibrary中添加"千枝气劲"Buff配置
    - 在BuffLibrary中添加"七情气劲"Buff配置
    - 在BuffLibrary中添加"素柯状态"Buff配置
    - 配置对应的检测区域和模板
    - _需求: 1.4, 7.4_
  - [x] 9.4 配置技能组（素柯技能组）
    - 创建"素柯技能组"，ConditionBuff="素柯状态"
    - 将需要素柯检测的技能加入该组
    - _需求: 3.1, 3.2, 3.3_
  - [x] 9.5 配置千枝气劲开启时的优先级覆盖
    - 赤芍寒香: PriorityOverrideCondition="千枝气劲", PriorityOverrideValue=180
    - 千枝绽蕊: PriorityOverrideCondition="千枝气劲", PriorityOverrideValue=170
    - _需求: 2.1, 2.2_
  - [x] 9.6 配置七情和合状态追踪
    - 七情和合技能: SetStateOnCast="", ClearStateOnCast="七情和合启用"
    - 在appsettings中添加初始状态配置
    - _需求: 6.2, 6.3, 6.4_
  - [x] 9.7 更新PresetManager支持加载预设
    - 添加加载预设配置的方法
    - 支持同时加载技能配置和Buff配置
    - _需求: 7.1_

- [x] 10. 更新UI支持新配置项
  - [x] 10.1 在SkillConfigPage添加新配置项UI
    - 添加ConditionBuff下拉选择
    - 添加优先级覆盖配置
    - 添加MP优先级加成配置
    - 添加前置技能名称选择
    - 添加状态追踪配置
  - [x] 10.2 在AppSettings页面添加技能组配置
    - 添加技能组列表管理UI

- [x] 11. 最终检查点 - 确保所有测试通过
  - 确保所有测试通过，如有问题请询问用户

## 备注

- 所有任务都是必需的，包括测试任务
- 每个任务都引用了具体的需求以确保可追溯性
- 属性测试验证了系统的正确性属性
- 检查点用于确保增量验证
- 任务9包含了Python版本的完整配置逻辑，确保C#版本能够模拟相同的技能释放行为
