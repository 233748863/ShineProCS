using FsCheck;
using FsCheck.Xunit;
using ShineProCS.Core.Services;
using OpenCvSharp;

namespace ShineProCS.Tests;

/// <summary>
/// Property-based tests for LRU Cache Eviction
/// **Feature: business-logic-fixes, Property 13: LRU Cache Eviction**
/// **Validates: Requirements 9.1, 9.2**
/// </summary>
public class LruCacheEvictionTests : IDisposable
{
    private readonly List<LruTemplateCache> _cachesToDispose = new();
    private readonly List<Mat> _matsToDispose = new();
    
    public void Dispose()
    {
        foreach (var cache in _cachesToDispose)
        {
            cache.Dispose();
        }
        foreach (var mat in _matsToDispose)
        {
            if (!mat.IsDisposed)
                mat.Dispose();
        }
    }
    
    /// <summary>
    /// 创建一个测试用的 Mat 对象
    /// </summary>
    private Mat CreateTestMat()
    {
        var mat = new Mat(10, 10, MatType.CV_8UC3, new Scalar(0, 0, 0));
        _matsToDispose.Add(mat);
        return mat;
    }
    
    /// <summary>
    /// 创建一个测试用的 LruTemplateCache
    /// </summary>
    private LruTemplateCache CreateCache(int capacity)
    {
        var cache = new LruTemplateCache(capacity);
        _cachesToDispose.Add(cache);
        return cache;
    }

    /// <summary>
    /// Property 13.1: Cache capacity is respected
    /// WHEN cache reaches capacity, the count SHALL not exceed capacity.
    /// **Validates: Requirements 9.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool CacheCapacityIsRespected(PositiveInt capacityGen, PositiveInt itemCountGen)
    {
        var capacity = (capacityGen.Get % 10) + 1; // 1-10
        var itemCount = (itemCountGen.Get % 20) + 1; // 1-20
        
        var cache = CreateCache(capacity);
        
        // 添加多个项目
        for (int i = 0; i < itemCount; i++)
        {
            var mat = CreateTestMat();
            cache.Set($"path_{i}", mat);
        }
        
        // Assert: 缓存数量不应超过容量
        return cache.Count <= capacity;
    }
    
    /// <summary>
    /// Property 13.2: LRU eviction removes least recently used item
    /// WHEN cache is at capacity and a new item is added, 
    /// THE least recently accessed item SHALL be removed.
    /// **Validates: Requirements 9.1, 9.2**
    /// </summary>
    [Fact]
    public void LruEvictionRemovesLeastRecentlyUsedItem()
    {
        var cache = CreateCache(3);
        
        // 添加3个项目
        var mat1 = CreateTestMat();
        var mat2 = CreateTestMat();
        var mat3 = CreateTestMat();
        
        cache.Set("path_1", mat1);
        Thread.Sleep(10); // 确保时间戳不同
        cache.Set("path_2", mat2);
        Thread.Sleep(10);
        cache.Set("path_3", mat3);
        
        // 访问 path_1，使其成为最近访问的
        Thread.Sleep(10);
        cache.Get("path_1");
        
        // 添加第4个项目，应该驱逐 path_2（最久未访问的）
        Thread.Sleep(10);
        var mat4 = CreateTestMat();
        cache.Set("path_4", mat4);
        
        // Assert: path_2 应该被移除，其他应该存在
        Assert.False(cache.Contains("path_2"), "path_2 应该被驱逐");
        Assert.True(cache.Contains("path_1"), "path_1 应该存在");
        Assert.True(cache.Contains("path_3"), "path_3 应该存在");
        Assert.True(cache.Contains("path_4"), "path_4 应该存在");
    }
    
    /// <summary>
    /// Property 13.3: Get updates last access time
    /// WHEN an item is accessed via Get, its last access time SHALL be updated.
    /// **Validates: Requirements 9.2**
    /// </summary>
    [Fact]
    public void GetUpdatesLastAccessTime()
    {
        var cache = CreateCache(2);
        
        // 添加2个项目
        var mat1 = CreateTestMat();
        var mat2 = CreateTestMat();
        
        cache.Set("path_1", mat1);
        Thread.Sleep(10);
        cache.Set("path_2", mat2);
        
        // 访问 path_1，更新其访问时间
        Thread.Sleep(10);
        cache.Get("path_1");
        
        // 添加第3个项目，应该驱逐 path_2（最久未访问的）
        Thread.Sleep(10);
        var mat3 = CreateTestMat();
        cache.Set("path_3", mat3);
        
        // Assert: path_2 应该被移除，path_1 应该存在
        Assert.False(cache.Contains("path_2"), "path_2 应该被驱逐");
        Assert.True(cache.Contains("path_1"), "path_1 应该存在（因为最近被访问）");
        Assert.True(cache.Contains("path_3"), "path_3 应该存在");
    }
    
    /// <summary>
    /// Property 13.4: Cache hit increments hit counter
    /// WHEN an item is found in cache, the hit counter SHALL increment.
    /// **Validates: Requirements 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool CacheHitIncrementsHitCounter(PositiveInt accessCountGen)
    {
        var accessCount = (accessCountGen.Get % 10) + 1; // 1-10
        var cache = CreateCache(5);
        
        var mat = CreateTestMat();
        cache.Set("test_path", mat);
        
        var initialHits = cache.Hits;
        
        // 多次访问
        for (int i = 0; i < accessCount; i++)
        {
            cache.Get("test_path");
        }
        
        // Assert: 命中次数应该增加
        return cache.Hits == initialHits + accessCount;
    }
    
    /// <summary>
    /// Property 13.5: Cache miss increments miss counter
    /// WHEN an item is not found in cache, the miss counter SHALL increment.
    /// **Validates: Requirements 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool CacheMissIncrementsMissCounter(PositiveInt accessCountGen)
    {
        var accessCount = (accessCountGen.Get % 10) + 1; // 1-10
        var cache = CreateCache(5);
        
        var initialMisses = cache.Misses;
        
        // 多次访问不存在的项目
        for (int i = 0; i < accessCount; i++)
        {
            cache.Get($"nonexistent_path_{i}");
        }
        
        // Assert: 未命中次数应该增加
        return cache.Misses == initialMisses + accessCount;
    }
    
    /// <summary>
    /// Property 13.6: Hit rate is correctly calculated
    /// The hit rate SHALL equal hits / (hits + misses).
    /// **Validates: Requirements 9.2**
    /// </summary>
    [Fact]
    public void HitRateIsCorrectlyCalculated()
    {
        var cache = CreateCache(5);
        
        var mat = CreateTestMat();
        cache.Set("test_path", mat);
        
        // 3次命中
        cache.Get("test_path");
        cache.Get("test_path");
        cache.Get("test_path");
        
        // 2次未命中
        cache.Get("nonexistent_1");
        cache.Get("nonexistent_2");
        
        // Assert: 命中率应该是 3/5 = 0.6
        var expectedHitRate = 3.0 / 5.0;
        Assert.Equal(expectedHitRate, cache.HitRate, 0.001);
    }
    
    /// <summary>
    /// Property 13.7: Empty cache has zero hit rate
    /// WHEN cache has no accesses, hit rate SHALL be 0.
    /// **Validates: Requirements 9.2**
    /// </summary>
    [Fact]
    public void EmptyCacheHasZeroHitRate()
    {
        var cache = CreateCache(5);
        
        // Assert: 没有访问时，命中率应该是0
        Assert.Equal(0.0, cache.HitRate);
    }
    
    /// <summary>
    /// Property 13.8: Set with existing key updates access time only
    /// WHEN Set is called with an existing key, the access time SHALL be updated
    /// but no new entry SHALL be created.
    /// **Validates: Requirements 9.2**
    /// </summary>
    [Fact]
    public void SetWithExistingKeyUpdatesAccessTimeOnly()
    {
        var cache = CreateCache(3);
        
        var mat1 = CreateTestMat();
        var mat2 = CreateTestMat();
        var mat3 = CreateTestMat();
        
        cache.Set("path_1", mat1);
        Thread.Sleep(10);
        cache.Set("path_2", mat2);
        Thread.Sleep(10);
        cache.Set("path_3", mat3);
        
        // 重新设置 path_1（更新访问时间）
        Thread.Sleep(10);
        var mat1New = CreateTestMat();
        cache.Set("path_1", mat1New);
        
        // 添加第4个项目，应该驱逐 path_2（最久未访问的）
        Thread.Sleep(10);
        var mat4 = CreateTestMat();
        cache.Set("path_4", mat4);
        
        // Assert: path_2 应该被移除，path_1 应该存在
        Assert.False(cache.Contains("path_2"), "path_2 应该被驱逐");
        Assert.True(cache.Contains("path_1"), "path_1 应该存在");
        Assert.Equal(3, cache.Count);
    }
    
    /// <summary>
    /// Property 13.9: Clear removes all items
    /// WHEN Clear is called, all items SHALL be removed.
    /// **Validates: Requirements 9.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool ClearRemovesAllItems(PositiveInt itemCountGen)
    {
        var itemCount = (itemCountGen.Get % 10) + 1; // 1-10
        var cache = CreateCache(20);
        
        // 添加多个项目
        for (int i = 0; i < itemCount; i++)
        {
            var mat = CreateTestMat();
            cache.Set($"path_{i}", mat);
        }
        
        // 清空缓存
        cache.Clear();
        
        // Assert: 缓存应该为空
        return cache.Count == 0;
    }
    
    /// <summary>
    /// Property 13.10: Remove returns true for existing items
    /// WHEN Remove is called with an existing key, it SHALL return true.
    /// **Validates: Requirements 9.1**
    /// </summary>
    [Fact]
    public void RemoveReturnsTrueForExistingItems()
    {
        var cache = CreateCache(5);
        
        var mat = CreateTestMat();
        cache.Set("test_path", mat);
        
        // Assert: 移除存在的项目应该返回 true
        Assert.True(cache.Remove("test_path"));
        Assert.False(cache.Contains("test_path"));
    }
    
    /// <summary>
    /// Property 13.11: Remove returns false for non-existing items
    /// WHEN Remove is called with a non-existing key, it SHALL return false.
    /// **Validates: Requirements 9.1**
    /// </summary>
    [Fact]
    public void RemoveReturnsFalseForNonExistingItems()
    {
        var cache = CreateCache(5);
        
        // Assert: 移除不存在的项目应该返回 false
        Assert.False(cache.Remove("nonexistent_path"));
    }
    
    /// <summary>
    /// Property 13.12: Capacity is correctly reported
    /// The Capacity property SHALL return the configured capacity.
    /// **Validates: Requirements 9.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool CapacityIsCorrectlyReported(PositiveInt capacityGen)
    {
        var capacity = (capacityGen.Get % 100) + 1; // 1-100
        var cache = CreateCache(capacity);
        
        // Assert: 容量应该与配置的值相同
        return cache.Capacity == capacity;
    }
    
    /// <summary>
    /// Property 13.13: Invalid capacity throws exception
    /// WHEN capacity is <= 0, constructor SHALL throw ArgumentOutOfRangeException.
    /// **Validates: Requirements 9.1**
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void InvalidCapacityThrowsException(int invalidCapacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LruTemplateCache(invalidCapacity));
    }
    
    /// <summary>
    /// Property 13.14: Get returns null for empty path
    /// WHEN Get is called with null or empty path, it SHALL return null.
    /// **Validates: Requirements 9.1**
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GetReturnsNullForEmptyPath(string? path)
    {
        var cache = CreateCache(5);
        
        // Assert: 空路径应该返回 null
        Assert.Null(cache.Get(path!));
    }
    
    /// <summary>
    /// Property 13.15: Set ignores empty path
    /// WHEN Set is called with null or empty path, it SHALL be ignored.
    /// **Validates: Requirements 9.1**
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SetIgnoresEmptyPath(string? path)
    {
        var cache = CreateCache(5);
        var mat = CreateTestMat();
        
        cache.Set(path!, mat);
        
        // Assert: 缓存应该为空
        Assert.Equal(0, cache.Count);
    }
}
