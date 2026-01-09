# Design Document

## Overview

本设计文档描述了 ShineProCS 项目中10个业务逻辑缺陷的修复方案。修复将遵循最小改动原则，确保向后兼容性。

## Architecture

修复涉及以下核心组件：

```
┌─────────────────────────────────────────────────────────────┐
│                     SkillLoopEngine                          │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │ CaptureLoop │  │  MainLoop   │  │ ExecuteSkillCycle   │  │
│  └──────┬──────┘  └──────┬──────┘  └──────────┬──────────┘  │
│         │                │                     │             │
│         ▼                ▼                     ▼             │
│  ┌─────────────────────────────────────────────────────────┐│
│  │              StateDetector (修复 #3, #7, #9)            ││
│  └─────────────────────────────────────────────────────────┘│
│         │                │                     │             │
│         ▼                ▼                     ▼             │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │ ConfigMgr  │  │ CdTracker   │  │    Strategies       │  │
│  │ (修复 #2)  │  │ (修复 #5)   │  │    (修复 #6)        │  │
│  └─────────────┘  └─────────────┘  └─────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

## Components and Interfaces

### 1. SkillLoopEngine 修改

#### 1.1 引导技能按键释放安全性 (Requirement 1)

```csharp
// 修改 ExecuteChanneledSkill 方法
private bool ExecuteChanneledSkill(SkillRuntimeState skill)
{
    var config = skill.Config;
    
    if (!_keyboard.PressKey(config.KeyCode))
    {
        skill.ConsecutiveFailures++;
        return false;
    }
    
    try
    {
        skill.MarkAsUsed();
        skill.ConsecutiveFailures = 0;
        _cooldownTracker.RecordSkillUse(config.Name, config.Cooldown);
        
        // 执行引导逻辑...
        ExecuteChannelLogic(config);
        
        return true;
    }
    finally
    {
        // 确保按键始终被释放
        _keyboard.ReleaseKey(config.KeyCode);
    }
}
```

#### 1.2 配置热重载线程安全 (Requirement 2)

```csharp
// 新增读写锁
private readonly ReaderWriterLockSlim _skillStatesLock = new();

private void LoadSkills()
{
    try
    {
        _config.LoadConfigs();
        var newStates = _config.Skills.Select(s => new SkillRuntimeState(s)).ToList();
        
        _skillStatesLock.EnterWriteLock();
        try
        {
            _skillStates = newStates;
        }
        finally
        {
            _skillStatesLock.ExitWriteLock();
        }
        
        Log($"已加载 {_skillStates.Count} 个技能", 1);
    }
    catch (Exception ex)
    {
        Log($"加载技能配置失败: {ex.Message}", 2);
    }
}

// MainLoop 中读取时使用读锁
_skillStatesLock.EnterReadLock();
try
{
    _stateDetector.UpdateSkillStatesParallel(_skillStates);
    // ...
}
finally
{
    _skillStatesLock.ExitReadLock();
}
```

#### 1.3 Buff条件检查时序优化 (Requirement 4)

```csharp
// SkillConfig 新增属性
public int BuffCheckDelay { get; set; } = 200;  // Buff检查延迟(ms)
public int BuffCheckRetries { get; set; } = 3;  // Buff检查重试次数

// ExecuteSkillCycle 修改
if (!buffSatisfied && skill.Config.PreCastKeyCode > 0)
{
    // 释放前置技能
    _keyboard.PressAndRelease(skill.Config.PreCastKeyCode);
    
    // 等待前置技能生效
    Thread.Sleep(skill.Config.ComboDelay);
    
    // 重试检查Buff
    for (int i = 0; i < skill.Config.BuffCheckRetries; i++)
    {
        Thread.Sleep(skill.Config.BuffCheckDelay);
        buffSatisfied = CheckBuffCondition(skill.Config);
        if (buffSatisfied) break;
    }
}
```

#### 1.4 帧变化检测阈值可配置 (Requirement 8)

```csharp
// AppSettings 新增
public int FrameChangeThreshold { get; set; } = 15;  // 0=禁用检测

// IsFrameUnchanged 修改
private bool IsFrameUnchanged(Mat frame)
{
    var threshold = _config.AppSettings.FrameChangeThreshold;
    if (threshold <= 0) return false;  // 禁用检测
    
    // ... 现有逻辑
    int dynamicThreshold = sampleCount * threshold;
    return diff < dynamicThreshold;
}
```

### 2. StateDetector 修改

#### 2.1 HP/MP检测失败处理 (Requirement 3)

```csharp
// 新增缓存字段
private double _lastValidHpPercent = 100.0;
private double _lastValidMpPercent = 100.0;
private int _consecutiveHpFailures = 0;
private int _consecutiveMpFailures = 0;
private const int MaxConsecutiveFailures = 5;

private double DetectBarPercent(int[] region, bool isHealth)
{
    if (region.Length < 4 || region[2] <= 0 || region[3] <= 0)
        return isHealth ? _lastValidHpPercent : _lastValidMpPercent;
    
    var frame = _image.GetScreenRegion(region[0], region[1], region[2], region[3]);
    if (frame == null)
    {
        // 检测失败，返回缓存值
        if (isHealth)
        {
            _consecutiveHpFailures++;
            if (_consecutiveHpFailures >= MaxConsecutiveFailures)
                Log("HP检测连续失败，使用缓存值", 2);
            return _lastValidHpPercent;
        }
        else
        {
            _consecutiveMpFailures++;
            if (_consecutiveMpFailures >= MaxConsecutiveFailures)
                Log("MP检测连续失败，使用缓存值", 2);
            return _lastValidMpPercent;
        }
    }
    
    try
    {
        // ... 现有检测逻辑
        var percent = CalculateBarPercent(frame, isHealth);
        
        // 更新缓存
        if (isHealth)
        {
            _lastValidHpPercent = percent;
            _consecutiveHpFailures = 0;
        }
        else
        {
            _lastValidMpPercent = percent;
            _consecutiveMpFailures = 0;
        }
        
        return percent;
    }
    finally
    {
        _image.ReturnMat(frame);
    }
}
```

#### 2.2 公共CD检测逻辑统一 (Requirement 7)

```csharp
// AppSettings 新增
public enum GcdDetectionMode { Auto = 0, Color = 1, Brightness = 2 }
public GcdDetectionMode GlobalCdDetectionMode { get; set; } = GcdDetectionMode.Auto;

private bool DetectGlobalCd(int[] point)
{
    if (point.Length < 2) return false;
    
    var color = _image.GetPixelColor(point[0], point[1]);
    if (color == null) return false;
    
    var settings = _config.AppSettings;
    var mode = settings.GlobalCdDetectionMode;
    
    // Auto模式：有颜色配置用颜色，否则用亮度
    if (mode == GcdDetectionMode.Auto)
    {
        mode = settings.GlobalCdColor.Any(v => v > 0) 
            ? GcdDetectionMode.Color 
            : GcdDetectionMode.Brightness;
    }
    
    return mode switch
    {
        GcdDetectionMode.Color => DetectGcdByColor(color.Value, settings),
        GcdDetectionMode.Brightness => DetectGcdByBrightness(color.Value, settings),
        _ => false
    };
}
```

#### 2.3 模板缓存LRU策略 (Requirement 9)

```csharp
// 新增LRU缓存类
private class LruTemplateCache
{
    private readonly int _capacity;
    private readonly Dictionary<string, (Mat Template, DateTime LastAccess)> _cache = new();
    private readonly object _lock = new();
    
    public LruTemplateCache(int capacity) => _capacity = capacity;
    
    public Mat? Get(string path)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(path, out var entry))
            {
                _cache[path] = (entry.Template, DateTime.Now);
                return entry.Template;
            }
            return null;
        }
    }
    
    public void Set(string path, Mat template)
    {
        lock (_lock)
        {
            if (_cache.Count >= _capacity)
            {
                // 移除最久未使用的
                var oldest = _cache.OrderBy(x => x.Value.LastAccess).First();
                oldest.Value.Template.Dispose();
                _cache.Remove(oldest.Key);
            }
            _cache[path] = (template, DateTime.Now);
        }
    }
}
```

### 3. SkillCooldownTracker 修改 (Requirement 5)

```csharp
// SkillRuntimeState 修改
public class SkillRuntimeState
{
    private readonly SkillCooldownTracker? _tracker;
    
    public SkillRuntimeState(SkillConfig config, SkillCooldownTracker? tracker = null)
    {
        Config = config;
        _tracker = tracker;
    }
    
    public bool IsAvailable
    {
        get
        {
            // 优先使用CooldownTracker
            if (_tracker != null)
            {
                var record = _tracker.GetRecord(Config.Name);
                if (record != null)
                    return record.IsEstimatedReady;
            }
            // 回退到本地计算
            return (DateTime.Now - LastUsedTime).TotalSeconds >= Config.Cooldown;
        }
    }
}

// Engine中调用RecordSkillReady
private void UpdateSkillStates()
{
    foreach (var skill in _skillStates)
    {
        if (skill.IsVisuallyReady && !skill.WasVisuallyReady)
        {
            _cooldownTracker.RecordSkillReady(skill.Config.Name);
        }
        skill.WasVisuallyReady = skill.IsVisuallyReady;
    }
}
```

### 4. Strategy 修改 (Requirement 6)

```csharp
// AppSettings 新增
public int ComboSkillPriorityBonus { get; set; } = 50;

// SmartStrategy 修改
public SkillRuntimeState? SelectSkill(StrategyContext context)
{
    var bonus = context.Settings?.ComboSkillPriorityBonus ?? 50;
    
    var selectedSkill = availableSkills
        .OrderByDescending(s => s.Config.Priority + (s.Config.PreCastKeyCode > 0 ? bonus : 0))
        .FirstOrDefault();
    
    return selectedSkill;
}
```

### 5. OpenCvImageInterface 修改 (Requirement 10)

```csharp
public Mat? GetScreenRegion(int x, int y, int w, int h)
{
    if (w <= 0 || h <= 0) return null;
    
    if (_useWgc && _wgc != null)
    {
        try
        {
            int clientX = x - _clientX;
            int clientY = y - _clientY;
            
            // 完整边界检查
            if (clientX < 0 || clientY < 0)
            {
                Log($"WGC坐标越界: ({clientX}, {clientY})", 1);
                return GetScreenRegionGdi(x, y, w, h);
            }
            
            // 检查是否超出窗口范围
            if (clientX + w > _wgc.Width || clientY + h > _wgc.Height)
            {
                Log($"WGC区域超出窗口: ({clientX},{clientY},{w},{h}) > ({_wgc.Width},{_wgc.Height})", 1);
                return GetScreenRegionGdi(x, y, w, h);
            }
            
            var region = _wgc.CaptureRegion(clientX, clientY, w, h);
            if (region != null)
                return region;
        }
        catch { }
    }
    
    return GetScreenRegionGdi(x, y, w, h);
}
```

## Data Models

### GameState 扩展

```csharp
public class GameState
{
    // 现有属性...
    
    /// <summary>
    /// HP值是否为缓存值（检测失败时）
    /// </summary>
    public bool IsHpCached { get; set; }
    
    /// <summary>
    /// MP值是否为缓存值（检测失败时）
    /// </summary>
    public bool IsMpCached { get; set; }
}
```

### AppSettings 扩展

```csharp
public partial class AppSettings : ObservableObject
{
    // 新增配置项
    
    /// <summary>
    /// 帧变化检测阈值 (0=禁用, 默认15)
    /// </summary>
    [ObservableProperty] private int _frameChangeThreshold = 15;
    
    /// <summary>
    /// 公共CD检测模式 (0=自动, 1=颜色, 2=亮度)
    /// </summary>
    [ObservableProperty] private int _globalCdDetectionMode = 0;
    
    /// <summary>
    /// 联动技能优先级加成
    /// </summary>
    [ObservableProperty] private int _comboSkillPriorityBonus = 50;
    
    /// <summary>
    /// 模板缓存大小
    /// </summary>
    [ObservableProperty] private int _templateCacheSize = 50;
}
```

### SkillConfig 扩展

```csharp
public partial class SkillConfig : ObservableObject
{
    // 新增配置项
    
    /// <summary>
    /// Buff检查延迟（毫秒）
    /// </summary>
    [ObservableProperty] private int _buffCheckDelay = 200;
    
    /// <summary>
    /// Buff检查重试次数
    /// </summary>
    [ObservableProperty] private int _buffCheckRetries = 3;
}
```



## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*


### Property 1: Key Release Guarantee for Channeled Skills

*For any* channeled skill execution, regardless of whether it completes normally, throws an exception, or is interrupted, the key SHALL be released after execution.

**Validates: Requirements 1.1, 1.2, 1.3**

---

### Property 2: Thread-Safe Skill State Access

*For any* concurrent access to skill states (configuration reload + main loop iteration), the system SHALL not throw ConcurrentModificationException and skill state data SHALL remain consistent.

**Validates: Requirements 2.1, 2.2**

---

### Property 3: Configuration Reload Preserves Valid State

*For any* configuration reload that fails (invalid JSON, file not found, etc.), the previous valid configuration SHALL be preserved and accessible.

**Validates: Requirements 2.3**

---

### Property 4: HP/MP Detection Failure Returns Cached Value

*For any* HP/MP detection failure, the returned value SHALL equal the last successfully detected value, not the default 100%.

**Validates: Requirements 3.1**

---

### Property 5: GameState Cache Flag Consistency

*For any* GameState returned by DetectGameState(), if the HP/MP value equals the cached value due to detection failure, the corresponding IsHpCached/IsMpCached flag SHALL be true.

**Validates: Requirements 3.3**

---

### Property 6: Buff Check Retry Behavior

*For any* skill with PreCastKeyCode configured, when buff check fails after pre-cast, the system SHALL retry exactly BuffCheckRetries times with BuffCheckDelay interval between retries.

**Validates: Requirements 4.1, 4.3**

---

### Property 7: CooldownTracker as Single Source of Truth

*For any* SkillRuntimeState with an associated CooldownTracker, the IsAvailable property SHALL return the value from CooldownTracker.GetRecord().IsEstimatedReady.

**Validates: Requirements 5.2, 5.3**

---

### Property 8: Visual Ready Triggers CooldownTracker Update

*For any* skill that transitions from IsVisuallyReady=false to IsVisuallyReady=true, the CooldownTracker.RecordSkillReady() SHALL be called.

**Validates: Requirements 5.1**

---

### Property 9: Strategy Priority Calculation Uses Config

*For any* skill selection in SmartStrategy, the priority bonus for combo skills SHALL equal AppSettings.ComboSkillPriorityBonus, not a hardcoded value.

**Validates: Requirements 6.1**

---

### Property 10: Skill Selection Respects Priority Order

*For any* set of available skills with different priorities, the Strategy SHALL select the skill with the highest effective priority (base priority + combo bonus if applicable).

**Validates: Requirements 6.2**

---

### Property 11: GCD Detection Mode Selection

*For any* GlobalCd detection, when GlobalCdDetectionMode is Auto and GlobalCdColor is configured (any value > 0), color detection SHALL be used; otherwise brightness detection SHALL be used.

**Validates: Requirements 7.1, 7.2**

---

### Property 12: Frame Change Detection Disabled When Threshold Zero

*For any* frame when FrameChangeThreshold is set to 0, IsFrameUnchanged() SHALL return false (detection disabled).

**Validates: Requirements 8.2**

---

### Property 13: LRU Cache Eviction

*For any* template cache at capacity, when a new template is added, the least recently accessed template SHALL be removed.

**Validates: Requirements 9.1, 9.2**

---

### Property 14: Coordinate Validation and Fallback

*For any* screen region request where coordinates extend beyond window bounds, the system SHALL fall back to GDI capture with the original screen coordinates.

**Validates: Requirements 10.2, 10.3, 10.4**

---

## Error Handling

### Exception Handling Strategy

1. **Channeled Skill Execution**: Use try-finally to guarantee key release
2. **Configuration Reload**: Catch all exceptions, preserve previous state, log error
3. **Detection Failures**: Return cached values, increment failure counter, log after threshold
4. **WGC Capture Failures**: Silent fallback to GDI, log at debug level

### Recovery Mechanisms

1. **Key Stuck**: ESC key press after MaxConsecutiveFailures
2. **Detection Failure**: Use cached values with conservative skill selection
3. **Config Corruption**: Backup and restore from last valid state

## Testing Strategy

### Unit Tests

- Test each fix in isolation
- Mock dependencies (IKeyboardInterface, IImageInterface)
- Verify edge cases (null inputs, boundary values)

### Property-Based Tests

- Use FsCheck or similar library for C#
- Generate random skill configurations
- Verify properties hold across all inputs
- Minimum 100 iterations per property

### Integration Tests

- Test configuration hot-reload during engine operation
- Test detection failure recovery
- Test LRU cache behavior under load

