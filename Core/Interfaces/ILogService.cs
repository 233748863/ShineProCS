namespace ShineProCS.Core.Interfaces;

/// <summary>
/// 日志级别
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// 调试
    /// </summary>
    Debug = 0,
    
    /// <summary>
    /// 信息
    /// </summary>
    Info = 1,
    
    /// <summary>
    /// 警告
    /// </summary>
    Warning = 2,
    
    /// <summary>
    /// 错误
    /// </summary>
    Error = 3
}

/// <summary>
/// 日志条目
/// </summary>
public class LogEntry
{
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;
    
    /// <summary>
    /// 日志级别
    /// </summary>
    public LogLevel Level { get; set; }
    
    /// <summary>
    /// 日志消息
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// 来源（可选）
    /// </summary>
    public string? Source { get; set; }
}

/// <summary>
/// 日志服务接口
/// 管理应用程序日志的记录和显示
/// 参考 BetterGI 的日志服务设计
/// </summary>
public interface ILogService
{
    /// <summary>
    /// 当前日志级别过滤器
    /// 只有等于或高于此级别的日志才会被记录
    /// </summary>
    LogLevel MinimumLevel { get; set; }
    
    /// <summary>
    /// 日志条目添加事件
    /// 用于 UI 显示日志
    /// </summary>
    event Action<LogEntry>? LogAdded;
    
    /// <summary>
    /// 记录日志
    /// </summary>
    /// <param name="message">日志消息</param>
    /// <param name="level">日志级别</param>
    /// <param name="source">来源</param>
    void Log(string message, LogLevel level = LogLevel.Info, string? source = null);
    
    /// <summary>
    /// 记录调试日志
    /// </summary>
    void Debug(string message, string? source = null);
    
    /// <summary>
    /// 记录信息日志
    /// </summary>
    void Info(string message, string? source = null);
    
    /// <summary>
    /// 记录警告日志
    /// </summary>
    void Warning(string message, string? source = null);
    
    /// <summary>
    /// 记录错误日志
    /// </summary>
    void Error(string message, string? source = null);
    
    /// <summary>
    /// 记录异常
    /// </summary>
    void Error(Exception ex, string? message = null, string? source = null);
    
    /// <summary>
    /// 获取最近的日志条目
    /// </summary>
    /// <param name="count">数量</param>
    IEnumerable<LogEntry> GetRecentLogs(int count = 100);
    
    /// <summary>
    /// 清除日志
    /// </summary>
    void Clear();
}
