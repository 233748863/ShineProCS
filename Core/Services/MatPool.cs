using System.Collections.Concurrent;
using OpenCvSharp;

namespace ShineProCS.Core.Services;

/// <summary>
/// 对象池统计信息记录类
/// 用于追踪对象池的使用情况和性能指标
/// </summary>
/// <param name="Created">创建的对象总数</param>
/// <param name="Reused">复用的对象次数</param>
/// <param name="Disposed">已释放的对象数量</param>
/// <param name="PoolSize">当前池中对象数量</param>
/// <param name="ReuseRate">复用率（0-1之间的小数）</param>
public record PoolStatistics(int Created, int Reused, int Disposed, int PoolSize, double ReuseRate);

/// <summary>
/// Mat 对象池，复用 OpenCV Mat 对象减少 GC 压力
/// 支持尺寸匹配的智能复用、对象状态验证和详细统计
/// </summary>
public class MatPool : IDisposable
{
    // 使用字典按尺寸分组存储 Mat 对象，实现智能复用
    private readonly ConcurrentDictionary<(int rows, int cols, int type), ConcurrentBag<Mat>> _sizedPools = new();
    
    // 通用池，用于存储不常用尺寸的 Mat
    private readonly ConcurrentBag<Mat> _generalPool = [];
    
    private readonly int _maxSize;           // 每个尺寸池的最大容量
    private readonly int _maxTotalSize;      // 总池容量限制
    private int _created;                    // 创建计数
    private int _reused;                     // 复用计数
    private int _disposed;                   // 释放计数
    private bool _isDisposed;                // 池是否已释放

    /// <summary>
    /// 创建 Mat 对象池
    /// </summary>
    /// <param name="maxSize">每个尺寸池的最大容量，默认 10</param>
    /// <param name="maxTotalSize">总池容量限制，默认 50</param>
    public MatPool(int maxSize = 10, int maxTotalSize = 50)
    {
        _maxSize = maxSize;
        _maxTotalSize = maxTotalSize;
    }

    /// <summary>
    /// 获取当前池中所有对象的总数
    /// </summary>
    private int TotalPooledCount
    {
        get
        {
            int count = _generalPool.Count;
            foreach (var pool in _sizedPools.Values)
            {
                count += pool.Count;
            }
            return count;
        }
    }

    /// <summary>
    /// 获取或创建指定大小的 Mat（智能尺寸匹配复用）
    /// </summary>
    /// <param name="rows">行数</param>
    /// <param name="cols">列数</param>
    /// <param name="type">Mat 类型</param>
    /// <returns>可用的 Mat 对象</returns>
    public Mat Rent(int rows, int cols, MatType type)
    {
        // 创建尺寸键用于查找匹配的池
        var sizeKey = (rows, cols, (int)type);
        
        // 尝试从对应尺寸的池中获取
        if (_sizedPools.TryGetValue(sizeKey, out var sizedPool))
        {
            if (sizedPool.TryTake(out var mat))
            {
                // 验证对象状态：检查是否已释放
                if (!mat.IsDisposed && mat.Rows == rows && mat.Cols == cols && mat.Type() == type)
                {
                    Interlocked.Increment(ref _reused);
                    return mat;
                }
                // 对象状态无效，释放并继续
                if (!mat.IsDisposed)
                {
                    mat.Dispose();
                }
                Interlocked.Increment(ref _disposed);
            }
        }

        // 尝试从通用池获取并调整大小
        if (_generalPool.TryTake(out var generalMat))
        {
            // 验证对象状态
            if (!generalMat.IsDisposed)
            {
                // 如果尺寸匹配，直接复用
                if (generalMat.Rows == rows && generalMat.Cols == cols && generalMat.Type() == type)
                {
                    Interlocked.Increment(ref _reused);
                    return generalMat;
                }
                // 尺寸不匹配，释放
                generalMat.Dispose();
            }
            Interlocked.Increment(ref _disposed);
        }

        // 创建新的 Mat 对象
        Interlocked.Increment(ref _created);
        return new Mat(rows, cols, type);
    }

    /// <summary>
    /// 获取空 Mat（用于输出参数）
    /// </summary>
    /// <returns>空的 Mat 对象</returns>
    public Mat RentEmpty()
    {
        // 尝试从通用池获取
        if (_generalPool.TryTake(out var mat))
        {
            // 验证对象状态
            if (!mat.IsDisposed)
            {
                Interlocked.Increment(ref _reused);
                return mat;
            }
            Interlocked.Increment(ref _disposed);
        }

        Interlocked.Increment(ref _created);
        return new Mat();
    }

    /// <summary>
    /// 归还 Mat 到池中
    /// 会验证对象状态，无效对象将被拒绝
    /// </summary>
    /// <param name="mat">要归还的 Mat 对象</param>
    public void Return(Mat? mat)
    {
        // 验证对象状态：null 或已释放的对象不接受
        if (mat == null || mat.IsDisposed) 
        {
            return;
        }

        // 检查总池容量限制
        if (TotalPooledCount >= _maxTotalSize)
        {
            mat.Dispose();
            Interlocked.Increment(ref _disposed);
            return;
        }

        // 如果 Mat 有有效尺寸，放入对应尺寸的池
        if (mat.Rows > 0 && mat.Cols > 0)
        {
            var sizeKey = (mat.Rows, mat.Cols, (int)mat.Type());
            var sizedPool = _sizedPools.GetOrAdd(sizeKey, _ => new ConcurrentBag<Mat>());
            
            // 检查该尺寸池的容量
            if (sizedPool.Count < _maxSize)
            {
                sizedPool.Add(mat);
                return;
            }
        }

        // 放入通用池
        if (_generalPool.Count < _maxSize)
        {
            _generalPool.Add(mat);
            return;
        }

        // 池已满，释放对象
        mat.Dispose();
        Interlocked.Increment(ref _disposed);
    }

    /// <summary>
    /// 获取简单统计信息（向后兼容）
    /// </summary>
    /// <returns>创建数、复用数、池大小的元组</returns>
    public (int Created, int Reused, int PoolSize) GetStats() 
        => (_created, _reused, TotalPooledCount);

    /// <summary>
    /// 获取详细统计信息
    /// 包含创建数、复用数、释放数、池大小和复用率
    /// </summary>
    /// <returns>详细的池统计信息</returns>
    public PoolStatistics GetDetailedStats()
    {
        int totalOperations = _created + _reused;
        // 计算复用率：复用次数 / 总操作次数
        double reuseRate = totalOperations > 0 ? (double)_reused / totalOperations : 0.0;
        
        return new PoolStatistics(
            Created: _created,
            Reused: _reused,
            Disposed: _disposed,
            PoolSize: TotalPooledCount,
            ReuseRate: reuseRate
        );
    }

    /// <summary>
    /// 清空所有池
    /// </summary>
    public void Clear()
    {
        // 清空通用池
        while (_generalPool.TryTake(out var mat))
        {
            if (!mat.IsDisposed)
            {
                mat.Dispose();
            }
            Interlocked.Increment(ref _disposed);
        }

        // 清空所有尺寸池
        foreach (var pool in _sizedPools.Values)
        {
            while (pool.TryTake(out var mat))
            {
                if (!mat.IsDisposed)
                {
                    mat.Dispose();
                }
                Interlocked.Increment(ref _disposed);
            }
        }
        _sizedPools.Clear();
    }

    /// <summary>
    /// 释放对象池资源
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        Clear();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
