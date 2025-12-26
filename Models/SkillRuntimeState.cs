namespace ShineProCS.Models;

public class SkillRuntimeState
{
    public SkillConfig Config { get; }
    public DateTime LastUsedTime { get; private set; } = DateTime.MinValue;
    public bool IsVisuallyReady { get; set; } = true;
    public int ConsecutiveFailures { get; set; }
    public int ExecutionCount { get; private set; }

    public SkillRuntimeState(SkillConfig config) => Config = config;

    public bool IsAvailable => (DateTime.Now - LastUsedTime).TotalSeconds >= Config.Cooldown;
    public double RemainingCooldown => Math.Max(0, Config.Cooldown - (DateTime.Now - LastUsedTime).TotalSeconds);

    public void MarkAsUsed()
    {
        LastUsedTime = DateTime.Now;
        ExecutionCount++;
        ConsecutiveFailures = 0;
    }
}
