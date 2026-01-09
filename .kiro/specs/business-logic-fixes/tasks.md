# Implementation Plan: Business Logic Fixes

## Overview

本实现计划将10个业务逻辑缺陷的修复分解为可执行的任务。任务按优先级排序，高优先级问题优先修复。

## Tasks

- [x] 1. 数据模型扩展
  - [x] 1.1 扩展 AppSettings 添加新配置项
    - 添加 FrameChangeThreshold (int, 默认15)
    - 添加 GlobalCdDetectionMode (int, 默认0)
    - 添加 ComboSkillPriorityBonus (int, 默认50)
    - 添加 TemplateCacheSize (int, 默认50)
    - _Requirements: 6.3, 7.3, 8.1, 9.4_

  - [x] 1.2 扩展 SkillConfig 添加Buff检查配置
    - 添加 BuffCheckDelay (int, 默认200)
    - 添加 BuffCheckRetries (int, 默认3)
    - _Requirements: 4.2_

  - [x] 1.3 扩展 GameState 添加缓存标志
    - 添加 IsHpCached (bool)
    - 添加 IsMpCached (bool)
    - _Requirements: 3.3_

  - [x] 1.4 更新 ConfigManager 验证新配置项
    - 在 ValidateAndFixAppSettings 中添加新字段验证
    - 在 ValidateAndFixSkillConfig 中添加新字段验证
    - _Requirements: 4.2, 6.3, 7.3, 8.1, 9.4_

- [x] 2. Checkpoint - 确保数据模型编译通过
  - 运行 `dotnet build` 确保无编译错误

- [x] 3. 高优先级修复：引导技能按键释放安全性 (Requirement 1)
  - [x] 3.1 重构 ExecuteChanneledSkill 方法
    - 使用 try-finally 确保按键释放
    - 将引导逻辑提取到独立方法
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 3.2 编写属性测试：Key Release Guarantee
    - **Property 1: Key Release Guarantee for Channeled Skills**
    - **Validates: Requirements 1.1, 1.2, 1.3**

- [x] 4. 高优先级修复：配置热重载线程安全 (Requirement 2)
  - [x] 4.1 添加 ReaderWriterLockSlim 到 SkillLoopEngine
    - 声明 _skillStatesLock 字段
    - 在 LoadSkills 中使用写锁
    - 在 MainLoop 中使用读锁
    - _Requirements: 2.1, 2.2_

  - [x] 4.2 修改 LoadSkills 方法保留失败时的旧配置
    - 捕获异常时不修改 _skillStates
    - 记录错误日志
    - _Requirements: 2.3_

  - [x] 4.3 编写属性测试：Thread-Safe Skill State Access
    - **Property 2: Thread-Safe Skill State Access**
    - **Validates: Requirements 2.1, 2.2**

  - [x] 4.4 编写属性测试：Configuration Reload Preserves Valid State
    - **Property 3: Configuration Reload Preserves Valid State**
    - **Validates: Requirements 2.3**

- [x] 5. 高优先级修复：HP/MP检测失败处理 (Requirement 3)
  - [x] 5.1 添加缓存字段到 StateDetector
    - 添加 _lastValidHpPercent, _lastValidMpPercent
    - 添加 _consecutiveHpFailures, _consecutiveMpFailures
    - 添加 MaxConsecutiveFailures 常量
    - _Requirements: 3.1, 3.2_

  - [x] 5.2 修改 DetectBarPercent 方法
    - 检测失败时返回缓存值
    - 更新连续失败计数
    - 超过阈值时记录警告
    - _Requirements: 3.1, 3.2_

  - [x] 5.3 修改 DetectGameState 设置缓存标志
    - 根据检测结果设置 IsHpCached/IsMpCached
    - _Requirements: 3.3_

  - [x] 5.4 编写属性测试：HP/MP Detection Failure Returns Cached Value
    - **Property 4: HP/MP Detection Failure Returns Cached Value**
    - **Validates: Requirements 3.1**

  - [x] 5.5 编写属性测试：GameState Cache Flag Consistency
    - **Property 5: GameState Cache Flag Consistency**
    - **Validates: Requirements 3.3**

- [x] 6. Checkpoint - 确保高优先级修复完成
  - 运行 `dotnet build` 确保无编译错误
  - 运行已有测试确保无回归

- [x] 7. 中优先级修复：Buff条件检查时序优化 (Requirement 4)
  - [x] 7.1 修改 ExecuteSkillCycle 中的Buff检查逻辑
    - 添加重试循环
    - 使用配置的 BuffCheckDelay 和 BuffCheckRetries
    - _Requirements: 4.1, 4.3_

  - [x] 7.2 编写属性测试：Buff Check Retry Behavior
    - **Property 6: Buff Check Retry Behavior**
    - **Validates: Requirements 4.1, 4.3**

- [x] 8. 中优先级修复：技能冷却追踪统一 (Requirement 5)
  - [x] 8.1 修改 SkillRuntimeState 构造函数
    - 添加可选的 CooldownTracker 参数
    - 修改 IsAvailable 属性使用 CooldownTracker
    - _Requirements: 5.2, 5.3_

  - [x] 8.2 添加 WasVisuallyReady 字段到 SkillRuntimeState
    - 用于检测视觉状态变化
    - _Requirements: 5.1_

  - [x] 8.3 在 MainLoop 中调用 RecordSkillReady
    - 检测 IsVisuallyReady 从 false 变为 true
    - 调用 _cooldownTracker.RecordSkillReady()
    - _Requirements: 5.1_

  - [x] 8.4 编写属性测试：CooldownTracker as Single Source of Truth
    - **Property 7: CooldownTracker as Single Source of Truth**
    - **Validates: Requirements 5.2, 5.3**

  - [x] 8.5 编写属性测试：Visual Ready Triggers CooldownTracker Update
    - **Property 8: Visual Ready Triggers CooldownTracker Update**
    - **Validates: Requirements 5.1**

- [x] 9. 中优先级修复：策略优先级逻辑修正 (Requirement 6)
  - [x] 9.1 修改 SmartStrategy.SelectSkill 方法
    - 从 StrategyContext 获取配置的优先级加成
    - 替换硬编码的 100
    - _Requirements: 6.1, 6.2_

  - [x] 9.2 修改 StrategyContext 添加 Settings 属性
    - 传递 AppSettings 到策略
    - _Requirements: 6.1_

  - [x] 9.3 编写属性测试：Strategy Priority Calculation Uses Config
    - **Property 9: Strategy Priority Calculation Uses Config**
    - **Validates: Requirements 6.1**

  - [x] 9.4 编写属性测试：Skill Selection Respects Priority Order
    - **Property 10: Skill Selection Respects Priority Order**
    - **Validates: Requirements 6.2**

- [x] 10. 中优先级修复：公共CD检测逻辑统一 (Requirement 7)
  - [x] 10.1 添加 GcdDetectionMode 枚举
    - Auto = 0, Color = 1, Brightness = 2
    - _Requirements: 7.3_

  - [x] 10.2 重构 DetectGlobalCd 方法
    - 根据 GlobalCdDetectionMode 选择检测方法
    - 提取 DetectGcdByColor 和 DetectGcdByBrightness 方法
    - _Requirements: 7.1, 7.2_

  - [x] 10.3 编写属性测试：GCD Detection Mode Selection
    - **Property 11: GCD Detection Mode Selection**
    - **Validates: Requirements 7.1, 7.2**

- [x] 11. Checkpoint - 确保中优先级修复完成
  - 运行 `dotnet build` 确保无编译错误
  - 运行已有测试确保无回归

- [x] 12. 低优先级修复：帧变化检测阈值可配置 (Requirement 8)
  - [x] 12.1 修改 IsFrameUnchanged 方法
    - 从配置读取阈值
    - 阈值为0时禁用检测
    - _Requirements: 8.1, 8.2_

  - [x] 12.2 编写属性测试：Frame Change Detection Disabled When Threshold Zero
    - **Property 12: Frame Change Detection Disabled When Threshold Zero**
    - **Validates: Requirements 8.2**

- [x] 13. 低优先级修复：模板缓存LRU策略 (Requirement 9)
  - [x] 13.1 创建 LruTemplateCache 类
    - 实现 Get/Set 方法
    - 跟踪最后访问时间
    - 容量满时移除最久未访问的
    - _Requirements: 9.1, 9.2_

  - [x] 13.2 替换 StateDetector 中的模板缓存
    - 使用 LruTemplateCache 替换 ConcurrentDictionary
    - 从配置读取缓存大小
    - _Requirements: 9.1, 9.4_

  - [x] 13.3 编写属性测试：LRU Cache Eviction
    - **Property 13: LRU Cache Eviction**
    - **Validates: Requirements 9.1, 9.2**

- [x] 14. 低优先级修复：WGC坐标边界检查 (Requirement 10)
  - [x] 14.1 修改 OpenCvImageInterface.GetScreenRegion 方法
    - 添加完整边界检查
    - 超出边界时回退到GDI
    - 记录调试日志
    - _Requirements: 10.2, 10.3, 10.4_

  - [x] 14.2 编写属性测试：Coordinate Validation and Fallback
    - **Property 14: Coordinate Validation and Fallback**
    - **Validates: Requirements 10.2, 10.3, 10.4**

- [x] 15. Final Checkpoint - 确保所有修复完成
  - 运行 `dotnet build` 确保无编译错误
  - 运行所有测试确保无回归
  - 验证配置文件向后兼容

## Notes

- 所有任务均为必需任务，包括属性测试
- 每个任务引用了具体的需求编号以便追溯
- Checkpoint 任务用于验证阶段性成果
- 属性测试验证设计文档中定义的正确性属性
