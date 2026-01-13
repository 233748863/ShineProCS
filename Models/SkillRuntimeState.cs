using ShineProCS.Core.Services;

namespace ShineProCS.Models;

public class SkillRuntimeState
{
    // 可选的冷却追踪器引用，用于统一冷却信息来源
    private readonly SkillCooldownTracker? _tracker;
    
    public SkillConfig Config { get; }
    public DateTime LastUsedTime { get; private set; } = DateTime.MinValue;
    public bool IsVisuallyReady { get; set; } = true;
    public int ConsecutiveFailures { get; set; }
    public int ExecutionCount { get; private set; }
    
    /// <summary>
    /// 上一次的视觉就绪状态，用于检测状态变化
    /// Requirements 5.1: 用于检测 IsVisuallyReady 从 false 变为 true
    /// </summary>
    public bool WasVisuallyReady { get; set; } = true;
    
    /// <summary>
    /// 是否因为 CD 而跳过视觉检测
    /// 需求 1.3: 当技能处于冷却中（剩余 CD > 0.5秒）时，跳过该技能的视觉检测
    /// </summary>
    public bool SkippedByCD { get; set; } = false;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="config">技能配置</param>
    /// <param name="tracker">可选的冷却追踪器，用于统一冷却信息来源</param>
    public SkillRuntimeState(SkillConfig config, SkillCooldownTracker? tracker = null)
    {
        Config = config;
        _tracker = tracker;
    }

    /// <summary>
    /// 技能是否可用
    /// Requirements 5.2, 5.3: CooldownTracker 作为冷却信息的唯一来源
    /// 如果有关联的 CooldownTracker，优先使用其数据
    /// </summary>
    public bool IsAvailable
    {
        get
        {
            // 优先使用 CooldownTracker 作为冷却信息来源
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
    
    public double RemainingCooldown => Math.Max(0, Config.Cooldown - (DateTime.Now - LastUsedTime).TotalSeconds);

    public void MarkAsUsed()
    {
        LastUsedTime = DateTime.Now;
        ExecutionCount++;
        ConsecutiveFailures = 0;
    }
}
