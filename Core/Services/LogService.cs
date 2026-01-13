using System.Collections.Concurrent;
using System.IO;
using ShineProCS.Core.Interfaces;
using Serilog;
using Serilog.Events;

namespace ShineProCS.Core.Services;

/// <summary>
/// 日志服务实现
/// 使用 Serilog 进行多目标输出（UI + 文件）
/// 参考 BetterGI 的日志服务设计
/// </summary>
public class LogService : ILogService, IDisposable
{
    private readonly ConcurrentQueue<LogEntry> _logBuffer;
    private readonly int _maxBufferSize;
    private readonly Serilog.ILogger _fileLogger;
    private readonly object _bufferLock = new();
    private bool _disposed;
    
    /// <summary>
    /// 当前日志级别过滤器
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Info;
    
    /// <summary>
    /// 日志条目添加事件
    /// </summary>
    public event Action<LogEntry>? LogAdded;
    
    public LogService(int maxBufferSize = 1000)
    {
        _maxBufferSize = maxBufferSize;
        _logBuffer = new ConcurrentQueue<LogEntry>();
        
        // 配置 Serilog 文件日志
        var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "app-.log");
        
        _fileLogger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                encoding: System.Text.Encoding.UTF8
            )
            .CreateLogger();
    }
    
    /// <summary>
    /// 记录日志
    /// </summary>
    public void Log(string message, LogLevel level = LogLevel.Info, string? source = null)
    {
        // 检查日志级别过滤
        if (level < MinimumLevel)
        {
            return;
        }
        
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            Source = source
        };
        
        // 添加到缓冲区
        AddToBuffer(entry);
        
        // 写入文件
        WriteToFile(entry);
        
        // 触发事件（用于 UI 显示）
        LogAdded?.Invoke(entry);
    }
    
    /// <summary>
    /// 添加到缓冲区
    /// </summary>
    private void AddToBuffer(LogEntry entry)
    {
        _logBuffer.Enqueue(entry);
        
        // 限制缓冲区大小
        while (_logBuffer.Count > _maxBufferSize)
        {
            _logBuffer.TryDequeue(out _);
        }
    }
    
    /// <summary>
    /// 写入文件
    /// </summary>
    private void WriteToFile(LogEntry entry)
    {
        try
        {
            var serilogLevel = ConvertToSerilogLevel(entry.Level);
            var message = string.IsNullOrEmpty(entry.Source) 
                ? entry.Message 
                : $"[{entry.Source}] {entry.Message}";
            
            _fileLogger.Write(serilogLevel, message);
        }
        catch
        {
            // 忽略文件写入错误
        }
    }
    
    /// <summary>
    /// 转换为 Serilog 日志级别
    /// </summary>
    private static LogEventLevel ConvertToSerilogLevel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Info => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            _ => LogEventLevel.Information
        };
    }
    
    /// <summary>
    /// 记录调试日志
    /// </summary>
    public void Debug(string message, string? source = null)
    {
        Log(message, LogLevel.Debug, source);
    }
    
    /// <summary>
    /// 记录信息日志
    /// </summary>
    public void Info(string message, string? source = null)
    {
        Log(message, LogLevel.Info, source);
    }
    
    /// <summary>
    /// 记录警告日志
    /// </summary>
    public void Warning(string message, string? source = null)
    {
        Log(message, LogLevel.Warning, source);
    }
    
    /// <summary>
    /// 记录错误日志
    /// </summary>
    public void Error(string message, string? source = null)
    {
        Log(message, LogLevel.Error, source);
    }
    
    /// <summary>
    /// 记录异常
    /// </summary>
    public void Error(Exception ex, string? message = null, string? source = null)
    {
        var fullMessage = string.IsNullOrEmpty(message) 
            ? $"{ex.GetType().Name}: {ex.Message}" 
            : $"{message} - {ex.GetType().Name}: {ex.Message}";
        
        Log(fullMessage, LogLevel.Error, source);
        
        // 同时记录堆栈跟踪到文件
        try
        {
            _fileLogger.Error(ex, message ?? "发生异常");
        }
        catch
        {
            // 忽略文件写入错误
        }
    }
    
    /// <summary>
    /// 获取最近的日志条目
    /// </summary>
    public IEnumerable<LogEntry> GetRecentLogs(int count = 100)
    {
        return _logBuffer.TakeLast(count).ToList();
    }
    
    /// <summary>
    /// 清除日志
    /// </summary>
    public void Clear()
    {
        while (_logBuffer.TryDequeue(out _)) { }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        // 释放 Serilog 日志器
        (_fileLogger as IDisposable)?.Dispose();
        
        GC.SuppressFinalize(this);
    }
}
