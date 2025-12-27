using System.Collections.Concurrent;

namespace ShineProCS.Core.Services;

/// <summary>
/// 通用对象池
/// 减少频繁创建对象带来的GC压力
/// </summary>
public class ObjectPool<T> where T : class, new()
{
    private readonly ConcurrentBag<T> _pool = [];
    private readonly int _maxSize;
    private readonly Func<T>? _factory;
    private readonly Action<T>? _reset;
    private int _created;

    /// <summary>
    /// 创建对象池
    /// </summary>
    /// <param name="maxSize">最大缓存数量</param>
    /// <param name="factory">对象创建工厂（可选）</param>
    /// <param name="reset">对象重置方法（可选）</param>
    public ObjectPool(int maxSize = 32, Func<T>? factory = null, Action<T>? reset = null)
    {
        _maxSize = maxSize;
        _factory = factory;
        _reset = reset;
    }

    /// <summary>
    /// 从池中获取对象
    /// </summary>
    public T Rent()
    {
        if (_pool.TryTake(out var item))
            return item;
        
        Interlocked.Increment(ref _created);
        return _factory?.Invoke() ?? new T();
    }

    /// <summary>
    /// 归还对象到池中
    /// </summary>
    public void Return(T item)
    {
        if (item == null) return;
        
        _reset?.Invoke(item);
        
        if (_pool.Count < _maxSize)
            _pool.Add(item);
    }

    /// <summary>
    /// 已创建的对象总数
    /// </summary>
    public int TotalCreated => _created;

    /// <summary>
    /// 当前池中对象数量
    /// </summary>
    public int PooledCount => _pool.Count;

    /// <summary>
    /// 清空对象池
    /// </summary>
    public void Clear()
    {
        while (_pool.TryTake(out _)) { }
    }
}

/// <summary>
/// 游戏状态对象池
/// </summary>
public static class GameStatePool
{
    private static readonly ObjectPool<Models.GameState> _pool = new(
        maxSize: 16,
        factory: () => new Models.GameState(),
        reset: state =>
        {
            state.CurrentHpPercent = 100;
            state.CurrentMpPercent = 100;
            state.HpPercentage = 1.0;
            state.MpPercentage = 1.0;
            state.IsGlobalCdActive = false;
            state.IsCasting = false;
            state.HasTarget = false;
            state.UpdateTime = default;
        }
    );

    public static Models.GameState Rent() => _pool.Rent();
    public static void Return(Models.GameState state) => _pool.Return(state);
}

/// <summary>
/// 字节数组池（用于帧哈希等）
/// </summary>
public static class ByteArrayPool
{
    private static readonly System.Collections.Concurrent.ConcurrentBag<byte[]> _pool8 = [];
    private const int MaxSize = 8;

    public static byte[] Rent8()
    {
        if (_pool8.TryTake(out var arr))
            return arr;
        return new byte[8];
    }

    public static void Return(byte[] arr)
    {
        if (arr.Length == 8 && _pool8.Count < MaxSize)
        {
            Array.Clear(arr, 0, arr.Length);
            _pool8.Add(arr);
        }
    }
}
