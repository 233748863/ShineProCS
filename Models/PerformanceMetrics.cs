namespace ShineProCS.Models;

public class PerformanceMetrics
{
    public int TotalExecutions { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public double AverageResponseTime { get; set; }
    public double MinResponseTime { get; set; }
    public double MaxResponseTime { get; set; }
    public double SuccessRate { get; set; } = 100.0;
    public double ExecutionsPerSecond { get; set; }
}
