namespace ShineProCS.Core.View.Drawable;

/// <summary>
/// Vision 上下文 - 用于管理遮罩窗口上的绘制内容
/// 移植自 BetterGI
/// </summary>
public class VisionContext
{
    private static VisionContext? _uniqueInstance;
    private static readonly object Locker = new();

    private VisionContext()
    {
    }

    public static VisionContext Instance()
    {
        if (_uniqueInstance == null)
        {
            lock (Locker)
            {
                _uniqueInstance ??= new VisionContext();
            }
        }

        return _uniqueInstance;
    }

    /// <summary>
    /// 是否启用绘制
    /// </summary>
    public bool Drawable { get; set; }

    /// <summary>
    /// 绘制内容
    /// </summary>
    public DrawContent DrawContent { get; set; } = new();
}
