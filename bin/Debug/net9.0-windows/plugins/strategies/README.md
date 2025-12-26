# 策略插件目录

将自定义策略 DLL 文件放置在此目录，程序启动时会自动加载。

## 创建自定义策略

1. 创建一个 .NET 类库项目
2. 引用 ShineProCS 的 Core 程序集
3. 实现 `ISkillStrategy` 接口
4. 添加 `[StrategyMetadata]` 特性（可选）
5. 编译后将 DLL 复制到此目录

## 示例代码

```csharp
using ShineProCS.Core.Interfaces;
using ShineProCS.Models;

namespace MyStrategies;

[StrategyMetadata("my-strategy", "我的策略", 
    Description = "自定义技能选择逻辑", 
    Version = "1.0.0", 
    Author = "YourName")]
public class MyCustomStrategy : ISkillStrategy
{
    public string Name => "我的策略";
    public string Description => "自定义技能选择逻辑";
    public int Priority => 50;

    public bool CanExecute(StrategyContext context) => true;

    public SkillRuntimeState? SelectSkill(StrategyContext context)
    {
        // 实现你的技能选择逻辑
        return context.SkillStates
            .Where(s => s.Config.Enabled && s.IsAvailable)
            .OrderByDescending(s => s.Config.Priority)
            .FirstOrDefault();
    }
}
```

## 注意事项

- 策略 DLL 必须与主程序使用相同的 .NET 版本
- 策略类必须有无参构造函数
- 策略 ID 必须唯一，重复的 ID 会被忽略