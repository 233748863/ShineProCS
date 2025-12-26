using System.Diagnostics;
using ShineProCS.Models;

namespace ShineProCS.Core.Services;

public class PerformanceMonitor : IDisposable
{
    private readonly Stopwatch _timer = new();
    private readonly Queue<double> _recentTimes = new();
    private int _total, _success, _failed;
    private double _totalTime, _minTime = double.MaxValue, _maxTime;
    private DateTime _startTime = DateTime.Now;
    private readonly ReaderWriterLockSlim _rwLock = new();
    private bool _disposed;
    
    // 使用固定大小的循环队列减少内存分配
    private const int MaxRecentSamples = 100;

    public void StartOperation() => _timer.Restart();

    public void EndOperation(bool success)
    {
        _timer.Stop();
        var elapsed = _timer.Elapsed.TotalMilliseconds;
        
        _rwLock.EnterWriteLock();
        try
        {
            _total++;
            if (success) _success++; else _failed++;
            
            _totalTime += elapsed;
            if (elapsed < _minTime) _minTime = elapsed;
            if (elapsed > _maxTime) _maxTime = elapsed;
            
            _recentTimes.Enqueue(elapsed);
            while (_recentTimes.Count > MaxRecentSamples)
                _recentTimes.Dequeue();
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    public PerformanceMetrics GetMetrics()
    {
        _rwLock.EnterReadLock();
        try
        {
            var avgTime = _recentTimes.Count > 0 ? _recentTimes.Average() : 0;
            var runningSeconds = (DateTime.Now - _startTime).TotalSeconds;
            
            return new PerformanceMetrics
            {
                TotalExecutions = _total,
                SuccessCount = _success,
                FailedCount = _failed,
                AverageResponseTime = avgTime,
                MinResponseTime = _minTime == double.MaxValue ? 0 : _minTime,
                MaxResponseTime = _maxTime,
                SuccessRate = _total > 0 ? (double)_success / _total * 100 : 100,
                ExecutionsPerSecond = runningSeconds > 0 ? _total / runningSeconds : 0
            };
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <summary>
    /// 检查是否存在性能问题
    /// </summary>
    public bool HasPerformanceIssue(double maxAvgResponseMs = 100, double minSuccessRate = 90)
    {
        var metrics = GetMetrics();
        return metrics.AverageResponseTime > maxAvgResponseMs || 
               (metrics.SuccessRate < minSuccessRate && metrics.TotalExecutions > 10);
    }

    public void Reset()
    {
        _rwLock.EnterWriteLock();
        try
        {
            _total = _success = _failed = 0;
            _totalTime = _maxTime = 0;
            _minTime = double.MaxValue;
            _recentTimes.Clear();
            _startTime = DateTime.Now;
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rwLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
