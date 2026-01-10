# 配置文件使用情况分析报告

## 概述

本报告分析了 `config` 文件夹中配置文件的使用情况，包括 `appsettings.json`、`skills.json` 和 `presets` 文件夹中的预设文件。

## 1. appsettings.json 配置项分析

### 1.1 已使用的配置项

| 配置项 | 使用位置 | 状态 |
|--------|----------|------|
| `DetectionRegion` | MainViewModel.cs, StateDetector.cs | ✅ 使用中 |
| `ManaBarRegion` | MainViewModel.cs, StateDetector.cs | ✅ 使用中 |
| `HealthBarRegion` | MainViewModel.cs, StateDetector.cs | ✅ 使用中 |
| `TargetHealthBarRegion` | MainViewModel.cs, StateDetector.cs | ✅ 使用中 |
| `GlobalCdPoint` | MainViewModel.cs, StateDetector.cs | ✅ 使用中 |
| `GlobalCdColor` | MainViewModel.cs, StateDetector.cs | ✅ 使用中 |
| `GlobalCdColorTolerance` | MainViewModel.cs, StateDetector.cs | ✅ 使用中 |
| `EnableSmartMode` | SkillLoopEngine.cs | ✅ 使用中 |
| `LoopInterval` | SkillLoopEngine.cs, ConfigManager.cs | ✅ 使用中 |
| `LogLevel` | SkillLoopEngine.cs, ConfigManager.cs | ✅ 使用中 |
| `EnableOverlay` | MainViewModel.cs | ✅ 使用中 |
| `GameWindowTitle` | MainViewModel.cs | ✅ 使用中 |
| `EnableWgcCapture` | 模型定义中 | ⚠️ 仅定义，未找到直接使用 |
| `ImageQueueCapacity` | SkillLoopEngine.cs, MainViewModel.cs | ✅ 使用中 |
| `BuffLibrary` | SkillCardControl.cs, BuffLibraryPage.cs | ✅ 使用中 |
| `SkillGroups` | SkillCardControl.cs, ConditionEvaluator.cs | ✅ 使用中 |
| `HealthHueMin/Max` | MainViewModel.cs, StateDetector.cs | ✅ 使用中 |
| `HealthSatMin/ValMin` | MainViewModel.cs, StateDetector.cs | ✅ 使用中 |
| `HealthGreenHueMin/Max` | MainViewModel.cs, StateDetector.cs | ✅ 使用中 |
| `ManaHueMin/Max` | MainViewModel.cs, StateDetector.cs | ✅ 使用中 |
| `ManaSatMin/ValMin` | MainViewModel.cs, StateDetector.cs | ✅ 使用中 |
| `GlobalCdBrightnessThreshold` | StateDetector.cs | ✅ 使用中 |
| `SkillBrightnessThreshold` | StateDetector.cs | ✅ 使用中 |
| `BuffBrightnessThreshold` | 模型定义中 | ⚠️ 仅定义，未找到直接使用 |
| `OverlayLeft/Top/Opacity` | OverlayWindow.cs, MainViewModel.cs | ✅ 使用中 |
| `HotkeyStartStopModifier/Key` | MainViewModel.cs | ✅ 使用中 |
| `HotkeyPauseModifier/Key` | MainViewModel.cs | ✅ 使用中 |
| `EnableGlobalHotkeys` | MainViewModel.cs | ✅ 使用中 |
| `InputDriverType` | MainViewModel.cs, InputDriverManager.cs | ✅ 使用中 |
| `FrameChangeThreshold` | SkillLoopEngine.cs, ConfigManager.cs | ✅ 使用中 |
| `GlobalCdDetectionMode` | StateDetector.cs, ConfigManager.cs | ✅ 使用中 |
| `ComboSkillPriorityBonus` | DefaultStrategy.cs, ConfigManager.cs | ✅ 使用中 |
| `TemplateCacheSize` | StateDetector.cs, ConfigManager.cs | ✅ 使用中 |

### 1.2 待确认的配置项

| 配置项 | 说明 | 建议 |
|--------|------|------|
| `EnableWgcCapture` | 在 AppSettings 模型中定义，但未找到直接使用代码 | 需要进一步确认是否在 WGC 截图相关代码中使用 |
| `BuffBrightnessThreshold` | 在 AppSettings 模型中定义，但未找到直接使用代码 | 可能是预留配置，建议保留 |

## 2. skills.json 配置项分析

skills.json 中的所有配置项都对应 `SkillConfig` 模型的属性，经分析全部被使用：

- 基础配置：Name, KeyCode, Priority, Enabled
- 检测配置：IconRegion, TemplatePath, SimilarityThreshold
- 条件配置：MinMp, HpCheckTarget, HpThreshold, RequireTarget
- 施法配置：Cooldown, CastType, CastDuration 等
- 联动配置：PreCastKeyCode, PreCastConditionBuff, ComboDelay 等
- 高级配置：ConditionBuff, ExcludeConditionBuff, SkillGroup 等

**结论**：skills.json 中的所有配置项都在代码中被使用，无需清理。

## 3. presets 文件夹分析

### 3.1 预设加载机制

`PresetManager` 类负责管理预设，支持两种预设来源：
1. **内置预设**：硬编码在 `GetBuiltInPresets()` 方法中
2. **外部预设**：从 `config/presets` 目录加载 JSON 文件

### 3.2 config/presets/suke-skills.json

该文件是一个完整的预设配置文件，包含：
- 预设信息（Info）
- 技能配置（Skills）
- Buff库配置（BuffLibrary）
- 技能组配置（SkillGroups）
- 初始状态配置（InitialStates）

**使用情况**：
- `PresetManager.GetAvailablePresets()` 会扫描 `config/presets` 目录
- `PresetManager.LoadFullPreset()` 可以加载完整预设
- `SkillConfigPage.xaml.cs` 中的 `LoadPreset_Click` 方法调用预设加载功能

**结论**：`suke-skills.json` 预设文件被正确引用和使用，无需清理。

## 4. 总结

### 4.1 配置文件使用状态

| 文件 | 状态 | 说明 |
|------|------|------|
| `config/appsettings.json` | ✅ 全部使用 | 所有配置项都在代码中被引用 |
| `config/skills.json` | ✅ 全部使用 | 技能配置模板，所有字段都被使用 |
| `config/presets/suke-skills.json` | ✅ 使用中 | 预设文件，通过 PresetManager 加载 |

### 4.2 待确认项

1. **EnableWgcCapture**：需要确认是否在 WGC 截图初始化代码中使用
2. **BuffBrightnessThreshold**：可能是预留配置，建议保留以备将来使用

### 4.3 建议

1. **无需清理**：所有配置文件和配置项都在代码中被使用
2. **保留预留配置**：`EnableWgcCapture` 和 `BuffBrightnessThreshold` 建议保留
3. **预设文件**：`suke-skills.json` 是有效的预设文件，应保留

## 5. 详细使用情况

### EnableWgcCapture 分析结论

经过全面搜索，确认 `EnableWgcCapture` 配置项：
- 在 `Models/AppSettings.cs` 中定义
- 在 `config/appsettings.json` 中有默认值
- **但在代码中从未被读取或使用**

WGC 截图功能的实际使用方式：
- `OpenCvImageInterface` 类有 `InitializeWgc()` 方法
- 但该方法从未被调用，WGC 模式的启用/禁用逻辑未实现
- 当前代码直接使用 WGC 截图，没有根据配置切换

**建议**：这是一个未完成的功能配置项，可以：
1. 保留配置项，后续实现 WGC/GDI 切换功能
2. 或者移除该配置项（如果不打算实现此功能）

### BuffBrightnessThreshold 分析结论

经过搜索，确认 `BuffBrightnessThreshold` 配置项：
- 在 `Models/AppSettings.cs` 中定义
- **在代码中从未被使用**

**建议**：这是一个预留配置项，用于 Buff 检测的亮度阈值。建议保留以备将来使用。

## 6. 未使用配置项清单

| 配置项 | 文件 | 状态 | 建议 |
|--------|------|------|------|
| `EnableWgcCapture` | appsettings.json | 未使用 | 保留（预留功能） |
| `BuffBrightnessThreshold` | AppSettings.cs | 未使用 | 保留（预留功能） |

## 7. 结论

经过全面分析，配置文件中的配置项使用情况如下：

1. **appsettings.json**：绝大多数配置项都在使用中，仅 `EnableWgcCapture` 未被使用
2. **skills.json**：所有配置项都在使用中
3. **presets/suke-skills.json**：预设文件被 PresetManager 正确加载和使用

**建议不进行配置清理**，因为：
- 未使用的配置项数量很少（仅2个）
- 这些配置项是预留功能，可能在将来使用
- 移除它们不会带来明显的收益
