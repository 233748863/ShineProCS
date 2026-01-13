namespace ShineProCS.Core.Interfaces;

/// <summary>
/// 通知级别
/// </summary>
public enum NotificationLevel
{
    /// <summary>
    /// 信息
    /// </summary>
    Info,
    
    /// <summary>
    /// 成功
    /// </summary>
    Success,
    
    /// <summary>
    /// 警告
    /// </summary>
    Warning,
    
    /// <summary>
    /// 错误
    /// </summary>
    Error
}

/// <summary>
/// 通知服务接口
/// 管理应用程序通知的显示
/// 参考 BetterGI 的通知服务设计
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// 显示通知
    /// </summary>
    /// <param name="message">通知消息</param>
    /// <param name="title">通知标题</param>
    /// <param name="level">通知级别</param>
    void Show(string message, string? title = null, NotificationLevel level = NotificationLevel.Info);
    
    /// <summary>
    /// 显示信息通知
    /// </summary>
    /// <param name="message">通知消息</param>
    /// <param name="title">通知标题</param>
    void ShowInfo(string message, string? title = null);
    
    /// <summary>
    /// 显示成功通知
    /// </summary>
    /// <param name="message">通知消息</param>
    /// <param name="title">通知标题</param>
    void ShowSuccess(string message, string? title = null);
    
    /// <summary>
    /// 显示警告通知
    /// </summary>
    /// <param name="message">通知消息</param>
    /// <param name="title">通知标题</param>
    void ShowWarning(string message, string? title = null);
    
    /// <summary>
    /// 显示错误通知
    /// </summary>
    /// <param name="message">通知消息</param>
    /// <param name="title">通知标题</param>
    void ShowError(string message, string? title = null);
}
