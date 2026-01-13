using System.Collections.Concurrent;

namespace ShineProCS.Core.Services;

/// <summary>
/// 通用对象池统计信息记录类
/// 用于追踪对象池的使用情况和性能指标
/// </summary>
/// <param name="Created">创建的对象总数</param>
/// <param name="Reused">复用的对象次数</param>
/// <param name="Rejected">被拒绝归还的对象数量（验证失败）</param>
/// <param name="PoolSize">当前池中对象数量</param>
/// <param name="ReuseRate">复用率（0-1之间的小数）</param>
public record ObjectPoolStatistics(int Created, int Reused, int Rejected, int PoolSize, double ReuseRate);

/// <summary>
/// 通用对象池
/// 减少频繁创建对象带来的GC压力
/// 支持自定义工厂函数、重置函数和对象验证逻辑
/// </summary>
/// <typeparam name="T">池化对象类型，必须是引用类型且有无参构造函数</typeparam>
public class ObjectPool<T> where T : class, new()
{
    private readonly ConcurrentBag<T> _pool = [];
    private readonly int _maxSize;
    private readonly Func<T>? _factory;
    private readonly Action<T>? _reset;
    private readonly Func<T, bool>? _validator;
    private int _created;   // 创建计数
    private int _reused;    // 复用计数
    private int _rejected;  // 拒绝计数（验证失败）

    /// <summary>
    /// 创建对象池
    /// </summary>
    /// <param name="maxSize">最大缓存数量，默认 32</param>
    /// <param name="factory">对象创建工厂（可选），为 null 时使用默认构造函数</param>
    /// <param name="reset">对象重置方法（可选），归还时调用以清理对象状态</param>
    /// <param name="validator">对象验证函数（可选），归还时验证对象是否有效</param>
    public ObjectPool(int maxSize = 32, Func<T>? factory = null, Action<T>? reset = null, Func<T, bool>? validator = null)
    {
        _maxSize = maxSize;
        _factory = factory;
        _reset = reset;
        _validator = validator;
    }

    /// <summary>
    /// 从池中获取对象
    /// 优先从池中复用，池空时创建新对象
    /// </summary>
    /// <returns>可用的对象实例</returns>
    public T Rent()
    {
        // 尝试从池中获取对象
        while (_pool.TryTake(out var item))
        {
            // 如果有验证器，验证对象状态
            if (_validator != null)
            {
                if (_validator(item))
                {
                    // 验证通过，复用对象
                    Interlocked.Increment(ref _reused);
                    return item;
                }
                // 验证失败，继续尝试获取下一个
                continue;
            }
            
            // 无验证器，直接复用
            Interlocked.Increment(ref _reused);
            return item;
        }
        
        // 池空，创建新对象
        Interlocked.Increment(ref _created);
        
        // 优先使用工厂函数创建，否则使用默认构造函数
        return _factory?.Invoke() ?? new T();
    }

    /// <summary>
    /// 归还对象到池中
    /// 会验证对象状态，无效对象将被拒绝
    /// </summary>
    /// <param name="item">要归还的对象</param>
    public void Return(T? item)
    {
        // null 对象不接受
        if (item == null) 
        {
            return;
        }
        
        // 如果有验证器，先验证对象状态
        if (_validator != null && !_validator(item))
        {
            // 验证失败，拒绝归还
            Interlocked.Increment(ref _rejected);
            return;
        }
        
        // 调用重置函数清理对象状态（如果提供）
        try
        {
            _reset?.Invoke(item);
        }
        catch
        {
            // 重置失败，拒绝归还
            Interlocked.Increment(ref _rejected);
            return;
        }
        
        // 检查池容量限制
        if (_pool.Count < _maxSize)
        {
            _pool.Add(item);
        }
        // 池已满，对象被丢弃（由 GC 回收）
    }

    /// <summary>
    /// 已创建的对象总数
    /// </summary>
    public int TotalCreated => _created;

    /// <summary>
    /// 复用的对象次数
    /// </summary>
    public int TotalReused => _reused;

    /// <summary>
    /// 被拒绝归还的对象数量
    /// </summary>
    public int TotalRejected => _rejected;

    /// <summary>
    /// 当前池中对象数量
    /// </summary>
    public int PooledCount => _pool.Count;

    /// <summary>
    /// 最大池容量
    /// </summary>
    public int MaxSize => _maxSize;

    /// <summary>
    /// 获取详细统计信息
    /// </summary>
    /// <returns>对象池统计信息</returns>
    public ObjectPoolStatistics GetDetailedStats()
    {
        int totalOperations = _created + _reused;
        // 计算复用率：复用次数 / 总操作次数
        double reuseRate = totalOperations > 0 ? (double)_reused / totalOperations : 0.0;
        
        return new ObjectPoolStatistics(
            Created: _created,
            Reused: _reused,
            Rejected: _rejected,
            PoolSize: _pool.Count,
            ReuseRate: reuseRate
        );
    }

    /// <summary>
    /// 清空对象池
    /// </summary>
    public void Clear()
    {
        while (_pool.TryTake(out _)) { }
    }
}

/// <summary>
/// 字节数组池统计信息记录类
/// </summary>
/// <param name="Size">数组大小</param>
/// <param name="Created">创建的数组数量</param>
/// <param name="Reused">复用的数组次数</param>
/// <param name="PoolSize">当前池中数组数量</param>
public record ByteArrayPoolStats(int Size, int Created, int Reused, int PoolSize);

/// <summary>
/// 字节数组池（用于帧哈希等）
/// 支持多种固定大小的字节数组池化，减少小数组分配带来的 GC 压力
/// </summary>
public static class ByteArrayPool
{
    // 各种固定大小的池
    private static readonly ConcurrentBag<byte[]> _pool8 = [];
    private static readonly ConcurrentBag<byte[]> _pool16 = [];
    private static readonly ConcurrentBag<byte[]> _pool32 = [];
    
    // 每个池的最大容量
    private const int MaxPoolSize = 16;
    
    // 统计计数器
    private static int _created8;
    private static int _reused8;
    private static int _created16;
    private static int _reused16;
    private static int _created32;
    private static int _reused32;

    /// <summary>
    /// 获取 8 字节数组
    /// </summary>
    /// <returns>8 字节的数组</returns>
    public static byte[] Rent8()
    {
        if (_pool8.TryTake(out var arr))
        {
            Interlocked.Increment(ref _reused8);
            return arr;
        }
        Interlocked.Increment(ref _created8);
        return new byte[8];
    }

    /// <summary>
    /// 获取 16 字节数组
    /// </summary>
    /// <returns>16 字节的数组</returns>
    public static byte[] Rent16()
    {
        if (_pool16.TryTake(out var arr))
        {
            Interlocked.Increment(ref _reused16);
            return arr;
        }
        Interlocked.Increment(ref _created16);
        return new byte[16];
    }

    /// <summary>
    /// 获取 32 字节数组
    /// </summary>
    /// <returns>32 字节的数组</returns>
    public static byte[] Rent32()
    {
        if (_pool32.TryTake(out var arr))
        {
            Interlocked.Increment(ref _reused32);
            return arr;
        }
        Interlocked.Increment(ref _created32);
        return new byte[32];
    }

    /// <summary>
    /// 根据大小获取合适的字节数组
    /// 支持 8、16、32 字节大小
    /// </summary>
    /// <param name="size">需要的数组大小</param>
    /// <returns>对应大小的字节数组，不支持的大小返回 null</returns>
    public static byte[]? Rent(int size)
    {
        return size switch
        {
            8 => Rent8(),
            16 => Rent16(),
            32 => Rent32(),
            _ => null  // 不支持的大小
        };
    }

    /// <summary>
    /// 归还字节数组到池中
    /// 会根据数组大小自动放入对应的池
    /// </summary>
    /// <param name="arr">要归还的字节数组</param>
    public static void Return(byte[]? arr)
    {
        if (arr == null) return;

        switch (arr.Length)
        {
            case 8:
                if (_pool8.Count < MaxPoolSize)
                {
                    // 清空数组内容
                    Array.Clear(arr, 0, arr.Length);
                    _pool8.Add(arr);
                }
                break;
            case 16:
                if (_pool16.Count < MaxPoolSize)
                {
                    Array.Clear(arr, 0, arr.Length);
                    _pool16.Add(arr);
                }
                break;
            case 32:
                if (_pool32.Count < MaxPoolSize)
                {
                    Array.Clear(arr, 0, arr.Length);
                    _pool32.Add(arr);
                }
                break;
            // 不支持的大小，不放入池中
        }
    }

    /// <summary>
    /// 获取所有池的统计信息
    /// </summary>
    /// <returns>各个大小池的统计信息列表</returns>
    public static List<ByteArrayPoolStats> GetAllStats()
    {
        return
        [
            new ByteArrayPoolStats(8, _created8, _reused8, _pool8.Count),
            new ByteArrayPoolStats(16, _created16, _reused16, _pool16.Count),
            new ByteArrayPoolStats(32, _created32, _reused32, _pool32.Count)
        ];
    }

    /// <summary>
    /// 获取指定大小池的统计信息
    /// </summary>
    /// <param name="size">数组大小</param>
    /// <returns>统计信息，不支持的大小返回 null</returns>
    public static ByteArrayPoolStats? GetStats(int size)
    {
        return size switch
        {
            8 => new ByteArrayPoolStats(8, _created8, _reused8, _pool8.Count),
            16 => new ByteArrayPoolStats(16, _created16, _reused16, _pool16.Count),
            32 => new ByteArrayPoolStats(32, _created32, _reused32, _pool32.Count),
            _ => null
        };
    }

    /// <summary>
    /// 获取总体统计摘要
    /// </summary>
    /// <returns>总创建数、总复用数、总池大小的元组</returns>
    public static (int TotalCreated, int TotalReused, int TotalPoolSize) GetSummaryStats()
    {
        return (
            _created8 + _created16 + _created32,
            _reused8 + _reused16 + _reused32,
            _pool8.Count + _pool16.Count + _pool32.Count
        );
    }

    /// <summary>
    /// 清空所有池
    /// </summary>
    public static void ClearAll()
    {
        while (_pool8.TryTake(out _)) { }
        while (_pool16.TryTake(out _)) { }
        while (_pool32.TryTake(out _)) { }
    }
}
