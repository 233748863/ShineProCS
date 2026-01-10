# 实现计划: 排除Buff条件功能

## 概述

通过扩展SkillConfig模型和ConditionEvaluator服务，添加"排除Buff条件"功能，使C#版本能够完美复现Python版本的"七情气劲已开启则跳过七情和合"逻辑。

## 任务列表

- [x] 1. 扩展SkillConfig模型
  - [x] 1.1 添加ExcludeConditionBuff属性
    - 在SkillConfig.cs中添加ExcludeConditionBuff字符串属性
    - 添加ObservableProperty特性
    - 添加XML文档注释
    - _需求: 1.4_

- [x] 2. 扩展ConditionEvaluator服务
  - [x] 2.1 添加EvaluateExcludeConditionBuff方法
    - 在ConditionEvaluator.cs中添加私有方法
    - 实现Buff存在时返回false的逻辑
    - 处理空字符串和null情况
    - _需求: 2.1, 2.4_
  - [x] 2.2 更新EvaluateSkillConditions方法
    - 在ConditionBuff检查之后添加ExcludeConditionBuff检查
    - 确保检查顺序正确
    - _需求: 2.2, 2.3_
  - [x] 2.3 编写排除Buff条件属性测试
    - **Property 1: 排除Buff条件检查一致性**
    - **验证: 需求 1.1, 1.2, 1.3**
  - [x] 2.4 编写条件组合属性测试
    - **Property 2: 条件组合正确性**
    - **验证: 需求 2.3**

- [x] 3. 检查点 - 确保所有测试通过
  - 确保所有测试通过，如有问题请询问用户

- [x] 4. 更新预设配置
  - [x] 4.1 更新suke-skills.json中的七情和合技能
    - 为七情和合技能添加ExcludeConditionBuff="七情气劲"
    - _需求: 3.1, 3.2_
  - [x] 4.2 更新suke-skills.json中的Skills数组所有技能
    - 为所有技能添加ExcludeConditionBuff字段（默认为空字符串）
    - 确保JSON结构与SkillConfig模型一致
    - _需求: 1.4_
  - [x] 4.3 验证BuffLibrary配置
    - 确保七情气劲Buff已在BuffLibrary中正确配置
    - 确保检测区域和模板路径配置正确
    - _需求: 3.3, 3.4_

- [x] 5. 更新UI支持
  - [x] 5.1 在SkillCardControl中添加排除Buff配置
    - 添加ExcludeConditionBuff下拉选择框
    - 绑定到BuffLibrary数据源
    - 支持清除选择
    - _需求: 4.1, 4.2, 4.3, 4.4_

- [x] 6. 最终检查点 - 确保所有测试通过
  - 确保所有测试通过，如有问题请询问用户

## 备注

- 所有任务都是必需的，包括测试任务
- 每个任务都引用了具体的需求以确保可追溯性
- 属性测试验证了系统的正确性属性
- 检查点用于确保增量验证
- 此功能是对现有skill-logic-compatibility spec的补充，用于完美复现Python版本的技能释放逻辑
