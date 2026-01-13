using OpenCvSharp;

namespace ShineProCS.Core.Services;

/// <summary>
/// LRU（最近最少使用）模板缓存
/// 使用 Dictionary + LinkedList 实现 O(1) 时间复杂度的 LRU 淘汰策略
/// 当缓存达到容量上限时，移除最久未访问的模板
/// </summary>
public class LruTemplateCache : IDisposable
{
    private readonly int _capacity;
    
    /// <summary>
    /// 缓存字典，用于 O(1) 查找
    /// </summary>
    private readonly Dictionary<string, LinkedListNode<CacheEntry>> _cache = new();
    
    /// <summary>
    /// LRU 链表，头部是最近访问的，尾部是最久未访问的
    /// </summary>
    private readonly LinkedList<CacheEntry> _lruList = new();
    
    private readonly object _lock = new();
    private bool _disposed;
    
    // 缓存统计
    private long _hits;
    private long _misses;
    private long _evictions;
    
    /// <summary>
    /// 缓存条目，包含模板路径和模板数据
    /// </summary>
    private class CacheEntry
    {
        /// <summary>
        /// 模板路径（作为缓存键）
        /// </summary>
        public string Path { get; }
        
        /// <summary>
        /// 模板 Mat 对象
        /// </summary>
        public Mat Template { get; }
        
        public CacheEntry(string path, Mat template)
        {
            Path = path;
            Template = template;
        }
    }
    
    /// <summary>
    /// 创建 LRU 模板缓存
    /// </summary>
    /// <param name="capacity">缓存容量，必须大于 0</param>
    public LruTemplateCache(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "缓存容量必须大于 0");
        
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
    /// 缓存淘汰次数
    /// </summary>
    public long Evictions => Interlocked.Read(ref _evictions);
    
    /// <summary>
    /// 缓存命中率（0.0 - 1.0）
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
    /// 如果命中，将该条目移动到链表头部（标记为最近访问）
    /// </summary>
    /// <param name="path">模板路径</param>
    /// <returns>模板 Mat 对象，如果不存在则返回 null</returns>
    public Mat? Get(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        
        lock (_lock)
        {
            if (_cache.TryGetValue(path, out var node))
            {
                // 将节点移动到链表头部（O(1) 操作）
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                
                Interlocked.Increment(ref _hits);
                return node.Value.Template;
            }
            
            Interlocked.Increment(ref _misses);
            return null;
        }
    }
    
    /// <summary>
    /// 将模板添加到缓存
    /// 新添加的条目会被放到链表头部（标记为最近访问）
    /// </summary>
    /// <param name="path">模板路径</param>
    /// <param name="template">模板 Mat 对象</param>
    public void Set(string path, Mat template)
    {
        if (string.IsNullOrEmpty(path) || template == null || template.Empty())
            return;
        
        lock (_lock)
        {
            // 如果已存在，移动到头部
            if (_cache.TryGetValue(path, out var existingNode))
            {
                _lruList.Remove(existingNode);
                _lruList.AddFirst(existingNode);
                return;
            }
            
            // 如果缓存已满，移除最久未访问的条目（链表尾部）
            if (_cache.Count >= _capacity)
            {
                EvictLeastRecentlyUsed();
            }
            
            // 创建新条目并添加到头部
            var entry = new CacheEntry(path, template);
            var newNode = _lruList.AddFirst(entry);
            _cache[path] = newNode;
        }
    }
    
    /// <summary>
    /// 检查缓存中是否包含指定路径的模板
    /// 注意：此方法不会更新访问顺序
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
            if (_cache.TryGetValue(path, out var node))
            {
                // 释放 Mat 资源
                node.Value.Template.Dispose();
                
                // 从链表和字典中移除
                _lruList.Remove(node);
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
            // 释放所有 Mat 资源
            foreach (var node in _lruList)
            {
                node.Template.Dispose();
            }
            
            _lruList.Clear();
            _cache.Clear();
        }
    }
    
    /// <summary>
    /// 重置统计信息
    /// </summary>
    public void ResetStatistics()
    {
        Interlocked.Exchange(ref _hits, 0);
        Interlocked.Exchange(ref _misses, 0);
        Interlocked.Exchange(ref _evictions, 0);
    }
    
    /// <summary>
    /// 移除最久未访问的条目（链表尾部）
    /// O(1) 时间复杂度
    /// </summary>
    private void EvictLeastRecentlyUsed()
    {
        var lastNode = _lruList.Last;
        if (lastNode == null)
            return;
        
        var entry = lastNode.Value;
        
        // 释放 Mat 资源
        entry.Template.Dispose();
        
        // 从链表和字典中移除
        _lruList.RemoveLast();
        _cache.Remove(entry.Path);
        
        Interlocked.Increment(ref _evictions);
        
        System.Diagnostics.Debug.WriteLine($"[LruTemplateCache] 淘汰最久未访问的模板: {entry.Path}");
    }
    
    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    /// <returns>统计信息字符串</returns>
    public string GetStatistics()
    {
        return $"缓存: {Count}/{Capacity}, 命中率: {HitRate:P2} (命中: {Hits}, 未命中: {Misses}, 淘汰: {Evictions})";
    }
    
    /// <summary>
    /// 获取详细的缓存统计信息
    /// </summary>
    /// <returns>缓存统计记录</returns>
    public CacheStatistics GetDetailedStatistics()
    {
        return new CacheStatistics(
            Count,
            Capacity,
            Hits,
            Misses,
            Evictions,
            HitRate
        );
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

/// <summary>
/// 缓存统计信息记录
/// </summary>
/// <param name="Count">当前缓存数量</param>
/// <param name="Capacity">缓存容量</param>
/// <param name="Hits">命中次数</param>
/// <param name="Misses">未命中次数</param>
/// <param name="Evictions">淘汰次数</param>
/// <param name="HitRate">命中率</param>
public record CacheStatistics(
    int Count,
    int Capacity,
    long Hits,
    long Misses,
    long Evictions,
    double HitRate
);
