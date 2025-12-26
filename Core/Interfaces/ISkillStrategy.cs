using ShineProCS.Models;

namespace ShineProCS.Core.Interfaces;

/// <summary>
/// 策略执行上下文
/// 包含技能状态、游戏状态等策略决策所需的信息
/// </summary>
public class StrategyContext
{
    /// <summary>
    /// 所有技能的运行时状态列表
    /// </summary>
    public List<SkillRuntimeState> SkillStates { get; set; } = [];
    
    /// <summary>
    /// 当前游戏状态（HP/MP/CD等）
    /// </summary>
    public GameState GameState { get; set; } = new();
    
    /// <summary>
    /// 当前循环模式（Default/Smart）
    /// </summary>
    public string LoopMode { get; set; } = "Default";
}

/// <summary>
/// 技能选择策略接口
/// 实现此接口可创建自定义的技能选择逻辑
/// </summary>
public interface ISkillStrategy
{
    /// <summary>
    /// 策略名称，用于显示和日志
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 策略描述
    /// </summary>
    string Description => "";
    
    /// <summary>
    /// 策略优先级，数值越大优先级越高
    /// </summary>
    int Priority => 0;
    
    /// <summary>
    /// 根据上下文选择要释放的技能
    /// </summary>
    /// <param name="context">策略上下文</param>
    /// <returns>选中的技能状态，如果没有可用技能返回null</returns>
    SkillRuntimeState? SelectSkill(StrategyContext context);
    
    /// <summary>
    /// 判断当前策略是否可以执行
    /// </summary>
    /// <param name="context">策略上下文</param>
    /// <returns>是否可以执行</returns>
    bool CanExecute(StrategyContext context);
}

/// <summary>
/// 策略元数据特性
/// 用于标记策略类的元信息，支持插件式加载
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class StrategyMetadataAttribute : Attribute
{
    /// <summary>
    /// 策略唯一标识符
    /// </summary>
    public string Id { get; }
    
    /// <summary>
    /// 策略显示名称
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// 策略描述
    /// </summary>
    public string Description { get; set; } = "";
    
    /// <summary>
    /// 策略版本
    /// </summary>
    public string Version { get; set; } = "1.0.0";
    
    /// <summary>
    /// 策略作者
    /// </summary>
    public string Author { get; set; } = "";
    
    /// <summary>
    /// 创建策略元数据
    /// </summary>
    /// <param name="id">唯一标识符</param>
    /// <param name="name">显示名称</param>
    public StrategyMetadataAttribute(string id, string name)
    {
        Id = id;
        Name = name;
    }
}
