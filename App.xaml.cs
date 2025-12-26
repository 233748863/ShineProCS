using System.IO;
using System.Windows;

namespace ShineProCS;

public partial class App : System.Windows.Application
{
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
    
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // 确保日志目录存在
        if (!Directory.Exists(LogPath))
            Directory.CreateDirectory(LogPath);
        
        // UI 线程异常处理
        DispatcherUnhandledException += (s, args) =>
        {
            LogException("UI线程异常", args.Exception);
            System.Windows.MessageBox.Show($"发生异常：{args.Exception.Message}\n\n详细信息已记录到日志文件", "错误", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        
        // 非 UI 线程异常处理
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            LogException("非UI线程异常", ex);
            if (args.IsTerminating)
            {
                System.Windows.MessageBox.Show($"发生严重异常，程序即将退出：{ex?.Message}\n\n详细信息已记录到日志文件", "严重错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        
        // Task 异常处理
        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            LogException("Task异常", args.Exception);
            args.SetObserved(); // 防止进程崩溃
        };
    }
    
    private static void LogException(string source, Exception? ex)
    {
        if (ex == null) return;
        
        try
        {
            var logFile = Path.Combine(LogPath, $"crash_{DateTime.Now:yyyyMMdd}.log");
            var logContent = $"""
                ================== {source} ==================
                时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                类型: {ex.GetType().FullName}
                消息: {ex.Message}
                堆栈:
                {ex.StackTrace}
                
                内部异常: {ex.InnerException?.Message}
                内部堆栈:
                {ex.InnerException?.StackTrace}
                ================================================

                """;
            File.AppendAllText(logFile, logContent);
        }
        catch { /* 日志写入失败时忽略 */ }
    }
}
