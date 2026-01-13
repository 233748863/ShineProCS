namespace ShineProCS.Core.Interfaces;

/// <summary>
/// 独立任务接口
/// 用于有明确开始和结束的自动化任务
/// 参考 BetterGI 的 ISoloTask 设计
/// </summary>
public interface ISoloTask
{
    /// <summary>
    /// 任务名称，用于显示和日志
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 任务描述
    /// </summary>
    string Description => "";
    
    /// <summary>
    /// 启动任务
    /// </summary>
    /// <param name="ct">取消令牌，用于支持任务取消</param>
    /// <returns>任务完成的异步操作</returns>
    Task Start(CancellationToken ct);
}
