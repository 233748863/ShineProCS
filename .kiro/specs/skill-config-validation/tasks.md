# 实现计划: 技能配置引用验证

## 概述

本实现计划将技能配置页面中的引用下拉框改为不可编辑模式，并添加无效引用的验证和警告显示功能。

## 任务

- [x] 1. 修改XAML下拉框属性
  - 将 SkillCardControl.xaml 中所有引用下拉框的 IsEditable 属性改为 False
  - 涉及的下拉框：ConditionBuffComboBox, ExcludeConditionBuffComboBox, PriorityOverrideConditionComboBox, SkillGroupComboBox, PreCastSkillNameComboBox, BuffComboBox
  - _需求: 3.1_

- [x] 2. 添加警告图标元素
  - [x] 2.1 在每个引用下拉框旁添加警告图标 TextBlock
    - 图标使用 "⚠" 字符，默认隐藏
    - 添加 ToolTip 属性用于显示警告信息
    - _需求: 1.2, 2.2, 4.4_

- [x] 3. 实现验证方法
  - [x] 3.1 在 SkillCardControl.xaml.cs 中添加 IsValidSkillGroup() 方法
    - 检查技能组名称是否在 AppSettings.SkillGroups 中存在且启用
    - 空字符串返回 true
    - _需求: 1.1, 1.2_
  
  - [x] 3.2 在 SkillCardControl.xaml.cs 中添加 IsValidBuff() 方法
    - 检查Buff名称是否在 AppSettings.BuffLibrary 中存在且启用
    - 空字符串返回 true
    - _需求: 2.1, 2.2_
  
  - [x] 3.3 添加 ValidateReferences() 方法
    - 检查当前技能的所有引用字段
    - 更新各警告图标的可见性和工具提示
    - _需求: 4.1, 4.2_

- [x] 4. 修改下拉框刷新逻辑
  - [x] 4.1 修改 RefreshSkillGroupComboBox() 方法
    - 如果当前值无效，将其添加到选项列表并标记警告
    - 调用 ValidateReferences() 更新警告状态
    - _需求: 1.2, 3.3_
  
  - [x] 4.2 修改所有 Buff 相关的 Refresh*ComboBox() 方法
    - RefreshConditionBuffComboBox()
    - RefreshExcludeConditionBuffComboBox()
    - RefreshPriorityOverrideConditionComboBox()
    - RefreshBuffComboBox()
    - 如果当前值无效，将其添加到选项列表并标记警告
    - _需求: 2.2, 3.3_

- [x] 5. 添加选择变更时的验证
  - 在各 ComboBox_SelectionChanged 方法中调用 ValidateReferences()
  - 确保用户选择后立即更新警告状态
  - _需求: 4.1_

- [x] 6. 检查点 - 确保所有修改编译通过
  - 确保所有修改编译通过，如有问题请询问用户

- [x] 7. 测试验证
  - [x] 7.1 手动测试下拉框不可编辑
    - 验证所有引用下拉框无法手动输入
    - _需求: 3.1_
  
  - [x] 7.2 手动测试无效引用警告
    - 创建一个引用不存在技能组/Buff的技能配置
    - 验证警告图标正确显示
    - _需求: 1.2, 2.2_
