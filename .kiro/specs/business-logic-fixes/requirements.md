# Requirements Document

## Introduction

本文档描述了 ShineProCS 项目中发现的业务逻辑缺陷及其修复需求。每个需求对应一个独立的缺陷，用户可以选择性地修复。

## Glossary

- **Engine**: 技能循环引擎 (SkillLoopEngine)，负责协调截屏、状态检测、技能选择和释放
- **StateDetector**: 游戏状态检测器，负责检测HP/MP、技能状态、Buff状态
- **CooldownTracker**: 技能冷却追踪器，记录技能CD时间和预测可用时间
- **Strategy**: 技能选择策略，决定下一个要释放的技能
- **WGC**: Windows Graphics Capture，高效窗口截图API
- **Buff**: 游戏中的增益/减益效果
- **Channeled_Skill**: 引导技能，需要持续按住按键

## Requirements

### Requirement 1: 引导技能按键释放安全性

**User Story:** As a user, I want channeled skills to always release the key properly, so that the game doesn't get stuck with a key held down.

**Priority:** 高 (可能导致游戏操作异常)

#### Acceptance Criteria

1. WHEN an exception occurs during channeled skill execution, THE Engine SHALL ensure the key is released
2. WHEN the channeled skill completes normally, THE Engine SHALL release the key
3. WHEN the channeled skill is interrupted, THE Engine SHALL release the key immediately
4. IF the key release fails, THEN THE Engine SHALL log the error and attempt recovery

---

### Requirement 2: 配置热重载线程安全

**User Story:** As a user, I want to modify configuration while the engine is running, so that I can adjust settings without restarting.

**Priority:** 高 (可能导致程序崩溃)

#### Acceptance Criteria

1. WHEN configuration is reloaded, THE Engine SHALL acquire appropriate locks before modifying skill states
2. WHEN the main loop is iterating skill states, THE Engine SHALL prevent concurrent modification
3. WHEN configuration reload fails, THE Engine SHALL maintain the previous valid configuration
4. THE Engine SHALL notify the user when configuration reload completes successfully

---

### Requirement 3: HP/MP检测失败处理

**User Story:** As a user, I want the system to handle detection failures gracefully, so that my character doesn't die due to false readings.

**Priority:** 高 (可能导致保命技能不触发)

#### Acceptance Criteria

1. WHEN HP/MP detection fails, THE StateDetector SHALL return the last known valid value instead of 100%
2. WHEN detection fails multiple times consecutively, THE StateDetector SHALL log a warning
3. THE GameState SHALL include a flag indicating whether the HP/MP values are fresh or cached
4. WHEN using cached values, THE Engine SHALL apply more conservative skill selection logic

---

### Requirement 4: Buff条件检查时序优化

**User Story:** As a user, I want buff-dependent skills to work reliably, so that my skill combos execute correctly.

**Priority:** 中 (影响技能连招效果)

#### Acceptance Criteria

1. WHEN a pre-cast skill is executed, THE Engine SHALL wait for the skill's cast time before checking buff
2. THE SkillConfig SHALL include a configurable buff check delay parameter
3. WHEN buff check fails after pre-cast, THE Engine SHALL retry up to a configurable number of times
4. THE Engine SHALL log detailed timing information for buff-dependent skill execution

---

### Requirement 5: 技能冷却追踪统一

**User Story:** As a user, I want accurate cooldown tracking, so that I can see reliable skill statistics.

**Priority:** 中 (影响统计准确性)

#### Acceptance Criteria

1. WHEN a skill becomes visually ready, THE Engine SHALL call CooldownTracker.RecordSkillReady()
2. THE CooldownTracker SHALL be the single source of truth for cooldown information
3. WHEN SkillRuntimeState.IsAvailable is queried, THE System SHALL use CooldownTracker data
4. THE CooldownTracker SHALL provide accurate average cooldown statistics

---

### Requirement 6: 策略优先级逻辑修正

**User Story:** As a user, I want skill priority to work as expected, so that important skills are used first.

**Priority:** 中 (影响技能选择逻辑)

#### Acceptance Criteria

1. THE SmartStrategy SHALL use a configurable bonus value for combo skills instead of hardcoded 100
2. WHEN selecting skills, THE Strategy SHALL respect the configured priority order
3. THE AppSettings SHALL include a combo skill priority bonus configuration
4. THE Strategy SHALL log the priority calculation for debugging purposes

---

### Requirement 7: 公共CD检测逻辑统一

**User Story:** As a user, I want global cooldown detection to work consistently, so that skills are not wasted during GCD.

**Priority:** 中 (影响技能释放时机)

#### Acceptance Criteria

1. WHEN GlobalCdColor is configured, THE StateDetector SHALL use color detection exclusively
2. WHEN GlobalCdColor is not configured, THE StateDetector SHALL fall back to brightness detection
3. THE AppSettings SHALL include a detection mode selector (color/brightness/auto)
4. THE StateDetector SHALL log which detection method is being used

---

### Requirement 8: 帧变化检测阈值可配置

**User Story:** As a user, I want to adjust frame change detection sensitivity, so that it works well with my specific game.

**Priority:** 低 (影响性能优化)

#### Acceptance Criteria

1. THE AppSettings SHALL include a configurable frame change threshold parameter
2. WHEN the threshold is set to 0, THE Engine SHALL disable frame change detection
3. THE Engine SHALL provide statistics on frame change detection hit rate
4. THE default threshold SHALL be calculated based on detection region size

---

### Requirement 9: 模板缓存LRU策略

**User Story:** As a user, I want frequently used templates to stay in cache, so that detection performance is optimal.

**Priority:** 低 (影响性能)

#### Acceptance Criteria

1. WHEN template cache reaches capacity, THE StateDetector SHALL remove least recently used templates
2. THE template cache SHALL track last access time for each template
3. THE StateDetector SHALL provide cache hit/miss statistics
4. THE cache size limit SHALL be configurable in AppSettings

---

### Requirement 10: WGC坐标边界检查

**User Story:** As a user, I want screen capture to handle edge cases properly, so that detection works near window borders.

**Priority:** 低 (边缘情况)

#### Acceptance Criteria

1. WHEN requested region extends beyond window bounds, THE ImageInterface SHALL log a warning
2. THE ImageInterface SHALL return null instead of partial capture when region is invalid
3. WHEN WGC capture fails due to bounds, THE System SHALL fall back to GDI with correct coordinates
4. THE ImageInterface SHALL validate coordinates before attempting capture

---

## Summary Table

| # | Issue | Priority | Risk | Complexity |
|---|-------|----------|------|------------|
| 1 | 引导技能按键释放安全性 | 高 | 游戏操作异常 | 低 |
| 2 | 配置热重载线程安全 | 高 | 程序崩溃 | 中 |
| 3 | HP/MP检测失败处理 | 高 | 角色死亡 | 中 |
| 4 | Buff条件检查时序优化 | 中 | 连招失败 | 中 |
| 5 | 技能冷却追踪统一 | 中 | 统计不准 | 低 |
| 6 | 策略优先级逻辑修正 | 中 | 技能选择错误 | 低 |
| 7 | 公共CD检测逻辑统一 | 中 | 技能浪费 | 低 |
| 8 | 帧变化检测阈值可配置 | 低 | 性能问题 | 低 |
| 9 | 模板缓存LRU策略 | 低 | 性能问题 | 中 |
| 10 | WGC坐标边界检查 | 低 | 边缘异常 | 低 |

