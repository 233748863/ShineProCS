using OpenCvSharp;

namespace ShineProCS.Core.Interfaces;

/// <summary>
/// 捕获内容，包含截图和相关信息
/// </summary>
public class CaptureContent
{
    /// <summary>
    /// 捕获的图像
    /// </summary>
    public Mat Image { get; set; } = null!;
    
    /// <summary>
    /// 捕获区域（x, y, width, height）
    /// </summary>
    public int[] CaptureRect { get; set; } = new int[4];
    
    /// <summary>
    /// 捕获时间戳
    /// </summary>
    public DateTime CaptureTime { get; set; } = DateTime.Now;
}

/// <summary>
/// 任务触发器接口
/// 用于实时监控游戏画面并触发操作
/// 参考 BetterGI 的 ITaskTrigger 设计
/// </summary>
public interface ITaskTrigger
{
    /// <summary>
    /// 触发器名称，用于显示和日志
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 是否启用
    /// </summary>
    bool IsEnabled { get; set; }
    
    /// <summary>
    /// 执行优先级，数值越大越先执行
    /// </summary>
    int Priority { get; }
    
    /// <summary>
    /// 是否处于独占模式
    /// 独占模式下，其他触发器将被暂停
    /// </summary>
    bool IsExclusive { get; }
    
    /// <summary>
    /// 初始化触发器
    /// 在触发器启用时调用
    /// </summary>
    void Init();
    
    /// <summary>
    /// 捕获图像后的处理
    /// 每次截图后调用，触发器在此方法中检测条件并执行操作
    /// </summary>
    /// <param name="content">捕获的内容</param>
    void OnCapture(CaptureContent content);
}
