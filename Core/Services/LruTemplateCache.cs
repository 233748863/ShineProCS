using OpenCvSharp;

namespace ShineProCS.Core.Services;

/// <summary>
/// LRU（最近最少使用）模板缓存
/// 当缓存达到容量上限时，移除最久未访问的模板
/// </summary>
public class LruTemplateCache : IDisposable
{
    private readonly int _capacity;
    private readonly Dictionary<string, CacheEntry> _cache = new();
    private readonly object _lock = new();
    private bool _disposed;
    
    // 缓存统计
    private long _hits;
    private long _misses;
    
    /// <summary>
    /// 缓存条目，包含模板和最后访问时间
    /// </summary>
    private class CacheEntry
    {
        public Mat Template { get; }
        public DateTime LastAccess { get; set; }
        
        public CacheEntry(Mat template)
        {
            Template = template;
            LastAccess = DateTime.Now;
        }
    }
    
    /// <summary>
    /// 创建LRU模板缓存
    /// </summary>
    /// <param name="capacity">缓存容量，必须大于0</param>
    public LruTemplateCache(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "缓存容量必须大于0");
        
        _capacity = capacity;
    }
    
    /// <summary>
    /// 当前缓存中的模板数量
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _cache.Count;
            }
        }
    }
    
    /// <summary>
    /// 缓存容量
    /// </summary>
    public int Capacity => _capacity;
    
    /// <summary>
    /// 缓存命中次数
    /// </summary>
    public long Hits => Interlocked.Read(ref _hits);
    
    /// <summary>
    /// 缓存未命中次数
    /// </summary>
    public long Misses => Interlocked.Read(ref _misses);
    
    /// <summary>
    /// 缓存命中率
    /// </summary>
    public double HitRate
    {
        get
        {
            var total = Hits + Misses;
            return total > 0 ? (double)Hits / total : 0.0;
        }
    }
    
    /// <summary>
    /// 从缓存获取模板
    /// </summary>
    /// <param name="path">模板路径</param>
    /// <returns>模板Mat对象，如果不存在则返回null</returns>
    public Mat? Get(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        
        lock (_lock)
        {
            if (_cache.TryGetValue(path, out var entry))
            {
                // 更新最后访问时间
                entry.LastAccess = DateTime.Now;
                Interlocked.Increment(ref _hits);
                return entry.Template;
            }
            
            Interlocked.Increment(ref _misses);
            return null;
        }
    }
    
    /// <summary>
    /// 将模板添加到缓存
    /// </summary>
    /// <param name="path">模板路径</param>
    /// <param name="template">模板Mat对象</param>
    public void Set(string path, Mat template)
    {
        if (string.IsNullOrEmpty(path) || template == null || template.Empty())
            return;
        
        lock (_lock)
        {
            // 如果已存在，更新访问时间
            if (_cache.TryGetValue(path, out var existing))
            {
                existing.LastAccess = DateTime.Now;
                return;
            }
            
            // 如果缓存已满，移除最久未访问的条目
            if (_cache.Count >= _capacity)
            {
                EvictLeastRecentlyUsed();
            }
            
            _cache[path] = new CacheEntry(template);
        }
    }
    
    /// <summary>
    /// 检查缓存中是否包含指定路径的模板
    /// </summary>
    /// <param name="path">模板路径</param>
    /// <returns>是否存在</returns>
    public bool Contains(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        
        lock (_lock)
        {
            return _cache.ContainsKey(path);
        }
    }
    
    /// <summary>
    /// 从缓存中移除指定模板
    /// </summary>
    /// <param name="path">模板路径</param>
    /// <returns>是否成功移除</returns>
    public bool Remove(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        
        lock (_lock)
        {
            if (_cache.TryGetValue(path, out var entry))
            {
                entry.Template.Dispose();
                _cache.Remove(path);
                return true;
            }
            return false;
        }
    }
    
    /// <summary>
    /// 清空缓存
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            foreach (var entry in _cache.Values)
            {
                entry.Template.Dispose();
            }
            _cache.Clear();
        }
    }
    
    /// <summary>
    /// 移除最久未访问的条目
    /// </summary>
    private void EvictLeastRecentlyUsed()
    {
        if (_cache.Count == 0)
            return;
        
        // 找到最久未访问的条目
        string? oldestKey = null;
        DateTime oldestTime = DateTime.MaxValue;
        
        foreach (var kvp in _cache)
        {
            if (kvp.Value.LastAccess < oldestTime)
            {
                oldestTime = kvp.Value.LastAccess;
                oldestKey = kvp.Key;
            }
        }
        
        if (oldestKey != null && _cache.TryGetValue(oldestKey, out var entry))
        {
            entry.Template.Dispose();
            _cache.Remove(oldestKey);
            System.Diagnostics.Debug.WriteLine($"[LruTemplateCache] 移除最久未访问的模板: {oldestKey}");
        }
    }
    
    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    /// <returns>统计信息字符串</returns>
    public string GetStatistics()
    {
        return $"缓存: {Count}/{Capacity}, 命中率: {HitRate:P2} (命中: {Hits}, 未命中: {Misses})";
    }
    
    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        
        _disposed = true;
        Clear();
        GC.SuppressFinalize(this);
    }
}
