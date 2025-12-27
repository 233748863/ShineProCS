using System.Collections.Concurrent;
using OpenCvSharp;

namespace ShineProCS.Core.Services;

/// <summary>
/// Mat 对象池，复用 OpenCV Mat 对象减少 GC 压力
/// </summary>
public class MatPool : IDisposable
{
    private readonly ConcurrentBag<Mat> _pool = [];
    private readonly int _maxSize;
    private int _created;
    private int _reused;
    private bool _disposed;

    public MatPool(int maxSize = 20)
    {
        _maxSize = maxSize;
    }

    /// <summary>
    /// 获取或创建指定大小的 Mat
    /// </summary>
    public Mat Rent(int rows, int cols, MatType type)
    {
        if (_pool.TryTake(out var mat))
        {
            // 检查大小是否匹配
            if (mat.Rows == rows && mat.Cols == cols && mat.Type() == type)
            {
                Interlocked.Increment(ref _reused);
                return mat;
            }
            // 大小不匹配，释放并创建新的
            mat.Dispose();
        }

        Interlocked.Increment(ref _created);
        return new Mat(rows, cols, type);
    }

    /// <summary>
    /// 获取空 Mat（用于输出参数）
    /// </summary>
    public Mat RentEmpty()
    {
        if (_pool.TryTake(out var mat))
        {
            Interlocked.Increment(ref _reused);
            return mat;
        }

        Interlocked.Increment(ref _created);
        return new Mat();
    }

    /// <summary>
    /// 归还 Mat 到池中
    /// </summary>
    public void Return(Mat? mat)
    {
        if (mat == null || mat.IsDisposed) return;

        // 池已满则直接释放
        if (_pool.Count >= _maxSize)
        {
            mat.Dispose();
            return;
        }

        _pool.Add(mat);
    }

    /// <summary>
    /// 获取统计信息
    /// </summary>
    public (int Created, int Reused, int PoolSize) GetStats() 
        => (_created, _reused, _pool.Count);

    /// <summary>
    /// 清空池
    /// </summary>
    public void Clear()
    {
        while (_pool.TryTake(out var mat))
            mat.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
