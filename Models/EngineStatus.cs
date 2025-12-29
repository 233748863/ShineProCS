namespace ShineProCS.Models;

public class EngineStatus
{
    public bool IsRunning { get; set; }
    public bool IsPaused { get; set; }
    public string Mode { get; set; } = "已停止";
    public int ExecutionCount { get; set; }
    public double AvgResponseTime { get; set; }
    public double SuccessRate { get; set; } = 100.0;
    
    public string? NextSkillName { get; set; }
    public double HpPercent { get; set; } = 100;
    public double MpPercent { get; set; } = 100;
}
