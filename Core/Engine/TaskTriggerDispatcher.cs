using ShineProCS.Core.Interfaces;

namespace ShineProCS.Core.Engine;

/// <summary>
/// 触发器调度器
/// 管理多个触发器的执行，按优先级排序
/// 参考 BetterGI 的 TaskTriggerDispatcher 设计
/// 需求: 8.6
/// </summary>
public class TaskTriggerDispatcher : IDisposable
{
    private readonly ILogService _logger;
    private readonly List<ITaskTrigger> _triggers = new();
    private readonly object _triggersLock = new();
    private bool _disposed;
    
    /// <summary>
    /// 是否正在运行
    /// </summary>
    public bool IsRunning { get; private set; }
    
    /// <summary>
    /// 当前独占的触发器
    /// </summary>
    public ITaskTrigger? ExclusiveTrigger { get; private set; }
    
    /// <summary>
    /// 触发器列表（按优先级排序，只读）
    /// </summary>
    public IReadOnlyList<ITaskTrigger> Triggers
    {
        get
        {
            lock (_triggersLock)
            {
                return _triggers.OrderByDescending(t => t.Priority).ToList().AsReadOnly();
            }
        }
    }
    
    /// <summary>
    /// 触发器数量
    /// </summary>
    public int TriggerCount
    {
        get
        {
            lock (_triggersLock)
            {
                return _triggers.Count;
            }
        }
    }
    
    public TaskTriggerDispatcher(ILogService logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// 注册触发器
    /// </summary>
    /// <param name="trigger">要注册的触发器</param>
    public void RegisterTrigger(ITaskTrigger trigger)
    {
        if (trigger == null)
        {
            throw new ArgumentNullException(nameof(trigger));
        }
        
        lock (_triggersLock)
        {
            if (_triggers.Any(t => t.Name == trigger.Name))
            {
                _logger.Warning($"触发器 [{trigger.Name}] 已存在，跳过注册", "Dispatcher");
                return;
            }
            
            _triggers.Add(trigger);
            _logger.Debug($"注册触发器: {trigger.Name} (优先级: {trigger.Priority})", "Dispatcher");
        }
    }
    
    /// <summary>
    /// 注销触发器
    /// </summary>
    /// <param name="triggerName">触发器名称</param>
    public void UnregisterTrigger(string triggerName)
    {
        lock (_triggersLock)
        {
            var trigger = _triggers.FirstOrDefault(t => t.Name == triggerName);
            if (trigger != null)
            {
                _triggers.Remove(trigger);
                _logger.Debug($"注销触发器: {triggerName}", "Dispatcher");
                
                if (ExclusiveTrigger == trigger)
                {
                    ExclusiveTrigger = null;
                }
            }
        }
    }
    
    /// <summary>
    /// 启动调度器
    /// </summary>
    public void Start()
    {
        if (IsRunning)
        {
            _logger.Warning("调度器已在运行中", "Dispatcher");
            return;
        }
        
        IsRunning = true;
        
        // 初始化所有启用的触发器
        lock (_triggersLock)
        {
            foreach (var trigger in _triggers.Where(t => t.IsEnabled))
            {
                try
                {
                    trigger.Init();
                    _logger.Debug($"初始化触发器: {trigger.Name}", "Dispatcher");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"初始化触发器 [{trigger.Name}] 失败", "Dispatcher");
                }
            }
        }
        
        _logger.Info("触发器调度器已启动", "Dispatcher");
    }
    
    /// <summary>
    /// 停止调度器
    /// </summary>
    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }
        
        IsRunning = false;
        ExclusiveTrigger = null;
        
        _logger.Info("触发器调度器已停止", "Dispatcher");
    }

    
    /// <summary>
    /// 分发捕获内容到所有触发器
    /// 按优先级从高到低执行
    /// </summary>
    /// <param name="content">捕获的内容</param>
    public void Dispatch(CaptureContent content)
    {
        if (!IsRunning)
        {
            return;
        }
        
        if (content == null)
        {
            return;
        }
        
        List<ITaskTrigger> triggersToExecute;
        
        lock (_triggersLock)
        {
            // 如果有独占触发器，只执行独占触发器
            if (ExclusiveTrigger != null)
            {
                if (ExclusiveTrigger.IsEnabled)
                {
                    ExecuteTrigger(ExclusiveTrigger, content);
                }
                return;
            }
            
            // 按优先级排序获取启用的触发器
            triggersToExecute = _triggers
                .Where(t => t.IsEnabled)
                .OrderByDescending(t => t.Priority)
                .ToList();
        }
        
        // 按优先级顺序执行触发器
        foreach (var trigger in triggersToExecute)
        {
            ExecuteTrigger(trigger, content);
            
            // 如果触发器进入独占模式，停止执行其他触发器
            if (trigger.IsExclusive)
            {
                lock (_triggersLock)
                {
                    ExclusiveTrigger = trigger;
                }
                _logger.Debug($"触发器 [{trigger.Name}] 进入独占模式", "Dispatcher");
                break;
            }
        }
    }
    
    /// <summary>
    /// 执行单个触发器
    /// </summary>
    private void ExecuteTrigger(ITaskTrigger trigger, CaptureContent content)
    {
        try
        {
            trigger.OnCapture(content);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"触发器 [{trigger.Name}] 执行异常", "Dispatcher");
        }
    }
    
    /// <summary>
    /// 清除独占模式
    /// </summary>
    public void ClearExclusiveMode()
    {
        lock (_triggersLock)
        {
            if (ExclusiveTrigger != null)
            {
                _logger.Debug($"触发器 [{ExclusiveTrigger.Name}] 退出独占模式", "Dispatcher");
                ExclusiveTrigger = null;
            }
        }
    }
    
    /// <summary>
    /// 设置触发器启用状态
    /// </summary>
    /// <param name="triggerName">触发器名称</param>
    /// <param name="enabled">是否启用</param>
    public void SetTriggerEnabled(string triggerName, bool enabled)
    {
        lock (_triggersLock)
        {
            var trigger = _triggers.FirstOrDefault(t => t.Name == triggerName);
            if (trigger != null)
            {
                trigger.IsEnabled = enabled;
                _logger.Debug($"触发器 [{triggerName}] 已{(enabled ? "启用" : "禁用")}", "Dispatcher");
                
                // 如果禁用的是独占触发器，清除独占模式
                if (!enabled && ExclusiveTrigger == trigger)
                {
                    ExclusiveTrigger = null;
                }
            }
        }
    }
    
    /// <summary>
    /// 获取触发器
    /// </summary>
    /// <param name="triggerName">触发器名称</param>
    /// <returns>触发器实例，如果不存在则返回 null</returns>
    public ITaskTrigger? GetTrigger(string triggerName)
    {
        lock (_triggersLock)
        {
            return _triggers.FirstOrDefault(t => t.Name == triggerName);
        }
    }
    
    /// <summary>
    /// 获取按优先级排序的触发器名称列表
    /// </summary>
    /// <returns>触发器名称列表</returns>
    public IReadOnlyList<string> GetTriggerNamesByPriority()
    {
        lock (_triggersLock)
        {
            return _triggers
                .OrderByDescending(t => t.Priority)
                .Select(t => t.Name)
                .ToList()
                .AsReadOnly();
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        Stop();
        
        lock (_triggersLock)
        {
            _triggers.Clear();
        }
        
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
