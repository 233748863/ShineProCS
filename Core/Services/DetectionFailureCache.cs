namespace ShineProCS.Core.Services;

/// <summary>
/// 检测失败缓存
/// 需求 3.2: 当 HP/MP 检测连续失败时，返回上次有效的缓存值
/// 需求 3.5: 将连续检测失败次数限制在可配置的最大值，超过后使用缓存值
/// 
/// 设计原理：
/// - 游戏画面可能因为特效、遮挡等原因导致检测失败
/// - 连续失败时返回上次有效值，避免数值跳变
/// - 超过最大失败次数后，持续返回缓存值直到检测成功
/// </summary>
public class DetectionFailureCache
{
    private readonly int _maxFailures;
    private double _lastValidValue;
    private int _consecutiveFailures;
    private readonly object _lock = new();
    
    /// <summary>
    /// 创建检测失败缓存
    /// </summary>
    /// <param name="maxFailures">最大连续失败次数（需求 3.5）</param>
    /// <param name="initialValue">初始缓存值</param>
    public DetectionFailureCache(int maxFailures = 5, double initialValue = 100.0)
    {
        _maxFailures = Math.Max(1, maxFailures);
        _lastValidValue = initialValue;
        _consecutiveFailures = 0;
    }
    
    /// <summary>
    /// 获取当前缓存的有效值
    /// </summary>
    public double LastValidValue
    {
        get
        {
            lock (_lock)
            {
                return _lastValidValue;
            }
        }
    }
    
    /// <summary>
    /// 获取当前连续失败次数
    /// </summary>
    public int ConsecutiveFailures
    {
        get
        {
            lock (_lock)
            {
                return _consecutiveFailures;
            }
        }
    }
    
    /// <summary>
    /// 获取最大连续失败次数
    /// </summary>
    public int MaxFailures => _maxFailures;
    
    /// <summary>
    /// 检查是否已达到最大失败次数
    /// </summary>
    public bool IsAtMaxFailures
    {
        get
        {
            lock (_lock)
            {
                return _consecutiveFailures >= _maxFailures;
            }
        }
    }
    
    /// <summary>
    /// 获取值或返回缓存
    /// 需求 3.2: 检测失败时返回上次有效的缓存值
    /// 需求 3.5: 超过最大失败次数后使用缓存值
    /// </summary>
    /// <param name="detectedValue">检测到的值，null 表示检测失败</param>
    /// <param name="isCached">输出参数，指示返回的是否为缓存值</param>
    /// <returns>有效值或缓存值</returns>
    public double GetValueOrCache(double? detectedValue, out bool isCached)
    {
        lock (_lock)
        {
            if (detectedValue.HasValue)
            {
                // 检测成功，更新缓存并重置失败计数
                _lastValidValue = detectedValue.Value;
                _consecutiveFailures = 0;
                isCached = false;
                return detectedValue.Value;
            }
            else
            {
                // 检测失败，增加失败计数
                _consecutiveFailures++;
                isCached = true;
                return _lastValidValue;
            }
        }
    }
    
    /// <summary>
    /// 记录检测失败
    /// </summary>
    public void RecordFailure()
    {
        lock (_lock)
        {
            _consecutiveFailures++;
        }
    }
    
    /// <summary>
    /// 记录检测成功并更新缓存值
    /// </summary>
    /// <param name="value">检测到的有效值</param>
    public void RecordSuccess(double value)
    {
        lock (_lock)
        {
            _lastValidValue = value;
            _consecutiveFailures = 0;
        }
    }
    
    /// <summary>
    /// 重置缓存状态
    /// </summary>
    /// <param name="initialValue">重置后的初始值</param>
    public void Reset(double initialValue = 100.0)
    {
        lock (_lock)
        {
            _lastValidValue = initialValue;
            _consecutiveFailures = 0;
        }
    }
    
    /// <summary>
    /// 判断是否应该使用缓存值
    /// 需求 3.5: 连续失败次数达到最大值后返回缓存值
    /// </summary>
    /// <param name="detectionSucceeded">本次检测是否成功</param>
    /// <returns>是否应该使用缓存值</returns>
    public bool ShouldUseCachedValue(bool detectionSucceeded)
    {
        lock (_lock)
        {
            if (detectionSucceeded)
            {
                return false;
            }
            
            // 需求 3.5: 超过最大失败次数后使用缓存值
            return _consecutiveFailures >= _maxFailures;
        }
    }
}
