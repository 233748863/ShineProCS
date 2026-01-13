using System;

namespace ShineProCS.Core.Services;

/// <summary>
/// 随机延迟生成器 - 生成人类化的随机延迟
/// 需求 6.1, 6.2: 支持可配置的输入延迟随机化
/// </summary>
public class RandomDelayGenerator
{
    // 使用线程安全的随机数生成器
    private static readonly Random _random = new();
    private static readonly object _lockObject = new();
    
    /// <summary>
    /// 生成指定范围内的随机延迟
    /// </summary>
    /// <param name="minMs">最小延迟（毫秒）</param>
    /// <param name="maxMs">最大延迟（毫秒）</param>
    /// <returns>随机延迟值（毫秒）</returns>
    /// <remarks>
    /// 属性 8: 随机延迟在范围内
    /// 对于任意延迟生成请求，生成的延迟值应该在 [minMs, maxMs] 范围内
    /// </remarks>
    public int Generate(int minMs, int maxMs)
    {
        // 参数验证：确保 minMs <= maxMs
        if (minMs > maxMs)
        {
            // 交换值以确保范围有效
            (minMs, maxMs) = (maxMs, minMs);
        }
        
        // 确保非负值
        minMs = Math.Max(0, minMs);
        maxMs = Math.Max(0, maxMs);
        
        // 如果 min == max，直接返回该值
        if (minMs == maxMs)
        {
            return minMs;
        }
        
        // 线程安全地生成随机数
        // Random.Next(minValue, maxValue) 返回 [minValue, maxValue) 范围内的值
        // 所以需要 maxMs + 1 来包含 maxMs
        lock (_lockObject)
        {
            return _random.Next(minMs, maxMs + 1);
        }
    }
    
    /// <summary>
    /// 异步等待随机延迟
    /// </summary>
    /// <param name="minMs">最小延迟（毫秒）</param>
    /// <param name="maxMs">最大延迟（毫秒）</param>
    /// <returns>实际等待的延迟时间（毫秒）</returns>
    public async Task<int> DelayAsync(int minMs, int maxMs)
    {
        int delay = Generate(minMs, maxMs);
        if (delay > 0)
        {
            await Task.Delay(delay);
        }
        return delay;
    }
    
    /// <summary>
    /// 同步等待随机延迟
    /// </summary>
    /// <param name="minMs">最小延迟（毫秒）</param>
    /// <param name="maxMs">最大延迟（毫秒）</param>
    /// <returns>实际等待的延迟时间（毫秒）</returns>
    public int Delay(int minMs, int maxMs)
    {
        int delay = Generate(minMs, maxMs);
        if (delay > 0)
        {
            Thread.Sleep(delay);
        }
        return delay;
    }
}
