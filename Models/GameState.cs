namespace ShineProCS.Models;

public class GameState
{
    public double HpPercentage { get; set; } = 1.0;
    public double MpPercentage { get; set; } = 1.0;
    public double CurrentHpPercent { get; set; } = 100.0;
    public double CurrentMpPercent { get; set; } = 100.0;
    
    /// <summary>
    /// 目标HP百分比 (0-100)
    /// </summary>
    public double TargetHpPercent { get; set; } = 100.0;
    
    /// <summary>
    /// HP值是否为缓存值（检测失败时）
    /// </summary>
    public bool IsHpCached { get; set; }
    
    /// <summary>
    /// MP值是否为缓存值（检测失败时）
    /// </summary>
    public bool IsMpCached { get; set; }
    
    public bool HasTarget { get; set; }
    public bool InCombat { get; set; }
    public bool IsCasting { get; set; }
    public bool IsGlobalCdActive { get; set; }
    public List<string> ActiveBuffs { get; set; } = [];
    public DateTime UpdateTime { get; set; } = DateTime.Now;
}
