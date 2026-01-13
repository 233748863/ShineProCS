using ShineProCS.Core.Interfaces;

namespace ShineProCS.Core.Engine;

/// <summary>
/// 取消上下文
/// 管理任务取消令牌的生命周期
/// </summary>
public class CancellationContext
{
    private static readonly Lazy<CancellationContext> _instance = new(() => new CancellationContext());
    
    /// <summary>
    /// 单例实例
    /// </summary>
    public static CancellationContext Instance => _instance.Value;
    
    private CancellationTokenSource? _cts;
    private readonly object _lock = new();
    
    /// <summary>
    /// 当前的取消令牌源
    /// </summary>
    public CancellationTokenSource? Cts
    {
        get
        {
            lock (_lock)
            {
                return _cts;
            }
        }
    }
    
    /// <summary>
    /// 当前的取消令牌
    /// </summary>
    public CancellationToken Token
    {
        get
        {
            lock (_lock)
            {
                return _cts?.Token ?? CancellationToken.None;
            }
        }
    }
    
    /// <summary>
    /// 是否已取消
    /// </summary>
    public bool IsCancellationRequested
    {
        get
        {
            lock (_lock)
            {
                return _cts?.IsCancellationRequested ?? false;
            }
        }
    }
    
    /// <summary>
    /// 设置新的取消令牌源
    /// </summary>
    public void Set()
    {
        lock (_lock)
        {
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
        }
    }
    
    /// <summary>
    /// 取消当前任务
    /// </summary>
    public void Cancel()
    {
        lock (_lock)
        {
            _cts?.Cancel();
        }
    }
    
    /// <summary>
    /// 清理取消令牌源
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _cts?.Dispose();
            _cts = null;
        }
    }
}


/// <summary>
/// 任务运行器
/// 管理独立任务的执行生命周期
/// 参考 BetterGI 的 TaskRunner 设计
/// 需求: 8.3, 8.4, 8.5
/// </summary>
public class TaskRunner : IDisposable
{
    /// <summary>
    /// 任务信号量，用于并发控制
    /// 确保同一时间只有一个任务在执行
    /// </summary>
    private static readonly SemaphoreSlim TaskSemaphore = new(1, 1);
    
    private readonly ILogService _logger;
    private readonly INotificationService _notification;
    private bool _disposed;
    
    /// <summary>
    /// 当前正在运行的任务名称
    /// </summary>
    public string? CurrentTaskName { get; private set; }
    
    /// <summary>
    /// 是否有任务正在运行
    /// </summary>
    public bool IsRunning => CurrentTaskName != null;
    
    /// <summary>
    /// 任务开始事件
    /// </summary>
    public event Action<string>? TaskStarted;
    
    /// <summary>
    /// 任务结束事件
    /// </summary>
    public event Action<string, bool>? TaskEnded;
    
    public TaskRunner(ILogService logger, INotificationService notification)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _notification = notification ?? throw new ArgumentNullException(nameof(notification));
    }
    
    /// <summary>
    /// 运行独立任务
    /// </summary>
    /// <param name="task">要运行的任务</param>
    /// <returns>任务是否成功完成</returns>
    public async Task<bool> RunSoloTaskAsync(ISoloTask task)
    {
        if (task == null)
        {
            throw new ArgumentNullException(nameof(task));
        }
        
        // 尝试获取信号量，不等待
        var hasLock = await TaskSemaphore.WaitAsync(0);
        if (!hasLock)
        {
            _logger.Warning($"任务启动失败：当前存在正在运行中的任务 [{CurrentTaskName}]", "TaskRunner");
            _notification.ShowWarning($"无法启动任务 [{task.Name}]，当前有任务正在运行");
            return false;
        }
        
        var success = false;
        
        try
        {
            CurrentTaskName = task.Name;
            _logger.Info($"→ {task.Name} 任务启动", "TaskRunner");
            
            // 设置新的取消令牌
            CancellationContext.Instance.Set();
            
            // 触发任务开始事件
            TaskStarted?.Invoke(task.Name);
            
            // 执行任务
            await task.Start(CancellationContext.Instance.Token);
            
            success = true;
            _logger.Info($"✓ {task.Name} 任务完成", "TaskRunner");
        }
        catch (OperationCanceledException)
        {
            _logger.Info($"○ {task.Name} 任务已取消", "TaskRunner");
            _notification.ShowInfo($"任务 [{task.Name}] 已取消");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"任务 [{task.Name}] 执行异常", "TaskRunner");
            _notification.ShowError($"任务执行异常: {ex.Message}");
        }
        finally
        {
            // 清理资源
            CleanupResources();
            
            var taskName = CurrentTaskName ?? task.Name;
            CurrentTaskName = null;
            
            // 释放信号量
            TaskSemaphore.Release();
            
            _logger.Info($"← {taskName} 任务结束", "TaskRunner");
            
            // 触发任务结束事件
            TaskEnded?.Invoke(taskName, success);
        }
        
        return success;
    }
    
    /// <summary>
    /// 取消当前运行的任务
    /// </summary>
    public void CancelCurrentTask()
    {
        if (CurrentTaskName == null)
        {
            _logger.Debug("没有正在运行的任务需要取消", "TaskRunner");
            return;
        }
        
        _logger.Info($"正在取消任务 [{CurrentTaskName}]...", "TaskRunner");
        CancellationContext.Instance.Cancel();
    }
    
    /// <summary>
    /// 清理资源
    /// </summary>
    private void CleanupResources()
    {
        // 清理取消令牌
        CancellationContext.Instance.Clear();
        
        // 这里可以添加其他资源清理逻辑
        // 例如：恢复系统状态、释放截图资源等
    }
    
    /// <summary>
    /// 检查是否可以启动新任务
    /// </summary>
    /// <returns>是否可以启动</returns>
    public bool CanStartTask()
    {
        return CurrentTaskName == null;
    }
    
    /// <summary>
    /// 等待当前任务完成
    /// </summary>
    /// <param name="timeout">超时时间</param>
    /// <returns>是否在超时前完成</returns>
    public async Task<bool> WaitForCompletionAsync(TimeSpan timeout)
    {
        var hasLock = await TaskSemaphore.WaitAsync(timeout);
        if (hasLock)
        {
            TaskSemaphore.Release();
            return true;
        }
        return false;
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        // 取消当前任务
        CancelCurrentTask();
        
        // 等待任务完成
        TaskSemaphore.Wait(TimeSpan.FromSeconds(5));
        TaskSemaphore.Release();
        
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
