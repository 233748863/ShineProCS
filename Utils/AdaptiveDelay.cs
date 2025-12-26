namespace ShineProCS.Utils;

/// <summary>
/// 自适应延迟计算器
/// 根据系统性能和响应时间动态调整循环延迟
/// </summary>
public class AdaptiveDelay
{
    private readonly int _minDelay;
    private readonly int _maxDelay;
    private int _currentDelay;

    /// <summary>
    /// 是否处于战斗模式（战斗模式下响应更激进）
    /// </summary>
    public bool IsCombatMode { get; set; }

    /// <summary>
    /// 获取当前应使用的延迟时间
    /// </summary>
    public int CurrentDelay => _currentDelay;

    public AdaptiveDelay(int baseDelay, int minDelay = 50, int maxDelay = 500)
    {
        _currentDelay = baseDelay;
        _minDelay = minDelay;
        _maxDelay = maxDelay;
    }

    /// <summary>
    /// 根据最近的响应时间调整延迟
    /// </summary>
    /// <param name="avgResponseTimeMs">平均响应时间（毫秒）</param>
    /// <param name="targetResponseTimeMs">目标响应时间（毫秒，默认50ms）</param>
    public void Adjust(double avgResponseTimeMs, double targetResponseTimeMs = 50)
    {
        // 战斗模式下，目标响应时间减半
        double effectiveTarget = IsCombatMode ? targetResponseTimeMs * 0.5 : targetResponseTimeMs;
        int effectiveMin = IsCombatMode ? Math.Max(20, _minDelay / 2) : _minDelay;

        // 响应时间超过目标的1.5倍，增加10%延迟
        if (avgResponseTimeMs > effectiveTarget * 1.5)
        {
            _currentDelay = (int)Math.Min(_maxDelay, _currentDelay * 1.1);
        }
        // 响应时间低于目标的0.5倍，减小5%延迟
        else if (avgResponseTimeMs < effectiveTarget * 0.5)
        {
            _currentDelay = (int)Math.Max(effectiveMin, _currentDelay * 0.95);
        }
    }

    /// <summary>
    /// 重置为基础延迟
    /// </summary>
    public void Reset(int baseDelay)
    {
        _currentDelay = Math.Clamp(baseDelay, _minDelay, _maxDelay);
    }
}
