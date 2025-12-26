using System.Diagnostics;
using System.Runtime;

namespace ShineProCS.Utils;

/// <summary>
/// 内存监控器
/// 监控应用程序内存使用情况，必要时触发清理
/// </summary>
public class MemoryMonitor
{
    private readonly Process _currentProcess;

    public MemoryMonitor()
    {
        _currentProcess = Process.GetCurrentProcess();
    }

    /// <summary>
    /// 获取当前内存统计信息（MB）
    /// </summary>
    public (double WorkingSet, double PrivateMemory, double ManagedMemory) GetMemoryStats()
    {
        _currentProcess.Refresh();
        
        double workingSet = _currentProcess.WorkingSet64 / 1024.0 / 1024.0;
        double privateMemory = _currentProcess.PrivateMemorySize64 / 1024.0 / 1024.0;
        double managedMemory = GC.GetTotalMemory(false) / 1024.0 / 1024.0;

        return (workingSet, privateMemory, managedMemory);
    }

    /// <summary>
    /// 强制执行内存清理
    /// </summary>
    public void ForceCleanup()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
    }

    /// <summary>
    /// 检查内存是否超过安全阈值
    /// </summary>
    public bool IsMemoryHigh(double thresholdMb = 200)
    {
        var stats = GetMemoryStats();
        return stats.PrivateMemory > thresholdMb;
    }

    /// <summary>
    /// 自动清理（如果内存过高）
    /// </summary>
    public bool AutoCleanupIfNeeded(double thresholdMb = 200)
    {
        if (IsMemoryHigh(thresholdMb))
        {
            ForceCleanup();
            return true;
        }
        return false;
    }
}
