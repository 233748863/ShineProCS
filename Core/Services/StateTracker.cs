using System.Collections.Concurrent;

namespace ShineProCS.Core.Services;

/// <summary>
/// 状态追踪器
/// 用于跨周期追踪命名的布尔状态，支持技能循环中的复杂状态管理
/// </summary>
public class StateTracker
{
    private readonly ConcurrentDictionary<string, bool> _states = new();
    private readonly object _lock = new();

    /// <summary>
    /// 状态变化事件
    /// </summary>
    public event Action<string, bool>? StateChanged;

    /// <summary>
    /// 获取指定状态的值
    /// </summary>
    /// <param name="name">状态名称</param>
    /// <returns>状态值，如果状态不存在则返回false</returns>
    public bool GetState(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        
        return _states.TryGetValue(name, out var value) && value;
    }

    /// <summary>
    /// 设置指定状态的值
    /// </summary>
    /// <param name="name">状态名称</param>
    /// <param name="value">状态值</param>
    public void SetState(string name, bool value)
    {
        if (string.IsNullOrEmpty(name))
            return;
        
        lock (_lock)
        {
            var oldValue = GetState(name);
            _states[name] = value;
            
            if (oldValue != value)
            {
                StateChanged?.Invoke(name, value);
            }
        }
    }

    /// <summary>
    /// 清除指定状态（将其设为false并从字典中移除）
    /// </summary>
    /// <param name="name">状态名称</param>
    public void ClearState(string name)
    {
        if (string.IsNullOrEmpty(name))
            return;
        
        lock (_lock)
        {
            if (_states.TryRemove(name, out var oldValue) && oldValue)
            {
                StateChanged?.Invoke(name, false);
            }
        }
    }

    /// <summary>
    /// 清除所有状态
    /// </summary>
    public void ClearAll()
    {
        lock (_lock)
        {
            var stateNames = _states.Keys.ToList();
            _states.Clear();
            
            // 触发所有状态变为false的事件
            foreach (var name in stateNames)
            {
                StateChanged?.Invoke(name, false);
            }
        }
    }

    /// <summary>
    /// 获取所有状态的只读副本
    /// </summary>
    /// <returns>状态字典的只读副本</returns>
    public IReadOnlyDictionary<string, bool> GetAllStates()
    {
        return new Dictionary<string, bool>(_states);
    }

    /// <summary>
    /// 检查状态是否存在（无论值为true还是false）
    /// </summary>
    /// <param name="name">状态名称</param>
    /// <returns>状态是否存在</returns>
    public bool HasState(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        
        return _states.ContainsKey(name);
    }

    /// <summary>
    /// 获取当前追踪的状态数量
    /// </summary>
    public int Count => _states.Count;
}
