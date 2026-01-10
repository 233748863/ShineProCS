# 未使用公共类和接口分析报告

## 概述

本报告分析了 ShineProCS 项目中所有公共类和接口的使用情况，识别出可能未被使用或可以清理的代码。

## 分析结果

### ✅ 已确认使用的类/接口

以下类和接口经过分析确认正在被项目使用：

| 类/接口名 | 位置 | 使用情况 |
|-----------|------|----------|
| AdaptiveDelay | Utils/ | SkillLoopEngine 使用 |
| MemoryMonitor | Utils/ | SkillLoopEngine, MainViewModel 使用 |
| ConfigWatcher | Utils/ | SkillLoopEngine 使用 |
| KeyCodeHelper | Utils/ | KeyCaptureWindow, KeyCodeToNameConverter, GlobalHotkeyService 使用 |
| LogTextColorConverter | Utils/ | MainWindow.xaml 使用 |
| EnumToIntConverter | Utils/ | SkillCardControl.xaml 使用 |
| InverseBooleanConverter | Utils/ | MainWindow.xaml 使用 |
| BoolToAddRemoveConverter | Utils/ | MainWindow.xaml 使用 |
| InverseBoolToVisibilityConverter | Utils/ | MainWindow.xaml 使用 |
| KeyCodeToNameConverter | Utils/ | MainWindow.xaml, SkillCardControl.xaml 使用 |
| InsertMarkerAdorner | Utils/ | DragDropBehavior 内部使用 |
| DragDropCompletedEventArgs | Utils/ | MainWindow.xaml.cs 使用 |
| ObjectPool<T> | Core/Services/ | GameStatePool 使用 |
| StateTracker | Core/Services/ | SkillLoopEngine, SmartStrategy, ConditionEvaluator 使用 |
| ConditionEvaluator | Core/Services/ | SmartStrategy 使用 |
| TemplateCapture | Core/Services/ | MainViewModel, SkillConfigPage, BuffLibraryPage 使用 |
| TemplatePreloader | Core/Services/ | SkillLoopEngine, StateDetector 使用 |
| StrategyLoader | Core/Services/ | SkillLoopEngine 使用 |
| LruTemplateCache | Core/Services/ | StateDetector 使用 |
| MatPool | Core/Services/ | OpenCvImageInterface 使用 |
| SkillCooldownTracker | Core/Services/ | SkillLoopEngine, SkillRuntimeState 使用 |
| CooldownRecord | Core/Services/ | SkillCooldownTracker 内部使用 |
| SkillStatistics | Core/Services/ | MainViewModel, SkillCooldownTracker 使用 |
| PresetManager | Core/Services/ | SkillConfigPage 使用 |
| PresetInfo | Core/Services/ | PresetManager 使用 |
| PresetData | Core/Services/ | PresetManager 使用 |
| FullPresetData | Core/Services/ | PresetManager 使用 |
| PerformanceMonitor | Core/Services/ | SkillLoopEngine 使用 |
| GlobalHotkeyService | Core/Services/ | MainViewModel 使用 |
| StateDetector | Core/Services/ | SkillLoopEngine 使用 |
| WindowEnumerationService | Core/Services/ | MainViewModel 使用 |
| InputDriverManager | Core/Services/ | MainViewModel 使用 |
| DriverChangedEventArgs | Core/Services/ | InputDriverManager, MainViewModel 使用 |
| ConfigManager | Core/Services/ | 多处使用 |
| DefaultStrategy | Core/Strategies/ | StrategyLoader 动态加载 |
| SmartStrategy | Core/Strategies/ | StrategyLoader 动态加载 |
| IBuffChecker | Core/Interfaces/ | ConditionEvaluator 使用 |
| IImageInterface | Core/Interfaces/ | 多处使用 |
| IKeyboardInterface | Core/Interfaces/ | 多处使用 |
| IMouseInterface | Core/Interfaces/ | 多处使用 |
| ISkillStrategy | Core/Interfaces/ | 多处使用 |
| IWindowEnumerationService | Core/Interfaces/ | MainViewModel 使用 |
| GhostBoxDeviceManager | Infrastructure/ | InputDriverManager, GhostBoxKeyboardInterface, GhostBoxMouseInterface 使用 |
| GhostBoxKeyboardInterface | Infrastructure/ | InputDriverManager 使用 |
| GhostBoxMouseInterface | Infrastructure/ | InputDriverManager 使用 |
| Win32KeyboardInterface | Infrastructure/ | InputDriverManager 使用 |
| OpenCvImageInterface | Infrastructure/ | MainViewModel 使用 |
| WgcCaptureInterface | Infrastructure/ | OpenCvImageInterface 使用 |
| ToastNotification | Views/ | ToastManager 使用 |
| ToastManager | Views/ | 多处使用 |
| ToastType | Views/ | ToastNotification 使用 |
| GameState | Models/ | StateDetector, SkillLoopEngine, ISkillStrategy 使用 |
| EngineStatus | Models/ | SkillLoopEngine, MainViewModel 使用 |
| PerformanceMetrics | Models/ | PerformanceMonitor, SkillLoopEngine 使用 |
| SkillRuntimeState | Models/ | 多处使用 |
| WindowInfo | Models/ | WindowEnumerationService, MainViewModel 使用 |
| InputDriverType | Models/ | InputDriverManager, AppSettings 使用 |
| SkillCastType | Models/ | SkillConfig 使用 |
| GcdDetectionMode | Models/ | AppSettings 使用 |
| SkillConfig | Models/ | 多处使用 |
| SkillGroupConfig | Models/ | PresetManager, AppSettings 使用 |
| BuffConfig | Models/ | 多处使用 |
| AppSettings | Models/ | 多处使用 |

### ⚠️ 可能未使用的类（需人工审查）

以下类在代码中定义但可能未被实际使用：

| 类名 | 位置 | 分析结果 | 建议 |
|------|------|----------|------|
| **LogMessage** | Utils/LogLevelColorConverter.cs | 类定义存在但未被实例化（`new LogMessage` 无匹配） | 🔴 可删除 |
| **LogLevelColorConverter** | Utils/LogLevelColorConverter.cs | 未在任何 XAML 中引用 | 🔴 可删除 |
| **GameStatePool** | Core/Services/ObjectPool.cs | 静态类定义存在但 `GameStatePool.Rent()` 和 `GameStatePool.Return()` 未被调用 | 🔴 可删除 |
| **StrategyLoader.StrategyInfo** | Core/Services/StrategyLoader.cs | 嵌套类，仅在 StrategyLoader 内部使用 | ✅ 保留（内部使用） |

### 📋 入口点类（已排除）

以下类是应用程序入口点，不应删除：

- `App` (App.xaml.cs)
- `MainWindow` (MainWindow.xaml.cs)
- `MainViewModel` (ViewModels/)
- 所有 Views/ 下的页面和窗口类

## 清理建议

### 高优先级（可安全删除）

1. **LogMessage 类** - `Utils/LogLevelColorConverter.cs` 第 10-18 行
   - 原因：定义了但从未实例化
   - 风险：低

2. **LogLevelColorConverter 类** - `Utils/LogLevelColorConverter.cs` 第 22-65 行
   - 原因：未在任何 XAML 中使用
   - 风险：低

3. **GameStatePool 静态类** - `Core/Services/ObjectPool.cs` 第 76-98 行
   - 原因：定义了但从未调用其方法
   - 风险：低

### 中优先级（建议保留）

- **ConditionEvaluator** - 虽然没有直接 `new ConditionEvaluator`，但被 SmartStrategy 引用，可能通过依赖注入使用
- **SmartStrategy** - 通过 StrategyLoader 反射加载，是有效的策略实现

## 总结

| 类别 | 数量 |
|------|------|
| 已确认使用 | 60+ |
| 可能未使用（建议删除） | 3 |
| 入口点类（已排除） | 10+ |

**建议操作**：删除 `LogMessage`、`LogLevelColorConverter` 和 `GameStatePool` 这三个未使用的类，可以减少代码冗余。
