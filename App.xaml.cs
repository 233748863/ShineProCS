using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using ShineProCS.Core.Config;
using ShineProCS.Core.Engine;

using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Recognition.OCR;
using ShineProCS.Core.Recognition.OCR.Paddle;
using ShineProCS.Core.Recognition.ONNX;
using ShineProCS.Core.Recognition.YOLO;
using ShineProCS.Core.Services;
using ShineProCS.ViewModels;
using ShineProCS.Views;
using ShineProCS.Views.Pages;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;

namespace ShineProCS;

/// <summary>
/// 应用程序入口
/// 使用 Microsoft.Extensions.Hosting 进行应用程序生命周期管理
/// 使用依赖注入管理所有服务、ViewModel 和页面
/// 需求: 7.1, 7.2, 7.3, 7.4, 7.5
/// </summary>
public partial class App : System.Windows.Application
{
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
    
    /// <summary>
    /// 应用程序主机
    /// </summary>
    private static IHost? _host;
    
    /// <summary>
    /// 获取服务提供者
    /// </summary>
    public static IServiceProvider Services => _host?.Services 
        ?? throw new InvalidOperationException("应用程序主机未初始化");
    
    /// <summary>
    /// 获取指定类型的服务
    /// </summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <returns>服务实例</returns>
    public static T GetService<T>() where T : class
    {
        return Services.GetRequiredService<T>();
    }
    
    /// <summary>
    /// 尝试获取指定类型的服务
    /// </summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <returns>服务实例，如果未注册则返回 null</returns>
    public static T? GetServiceOrDefault<T>() where T : class
    {
        return Services.GetService<T>();
    }
    
    /// <summary>
    /// 获取指定类型的 Logger
    /// </summary>
    /// <typeparam name="T">日志类型</typeparam>
    /// <returns>Logger 实例</returns>
    public static Microsoft.Extensions.Logging.ILogger<T> GetLogger<T>()
    {
        return Services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<T>>();
    }
    
    /// <summary>
    /// 应用程序启动
    /// </summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // 确保日志目录存在
        if (!Directory.Exists(LogPath))
            Directory.CreateDirectory(LogPath);
        
        // 配置全局异常处理
        ConfigureExceptionHandling();
        
        // 构建并启动主机
        _host = CreateHostBuilder().Build();
        
        // 初始化页面服务的服务提供者
        var pageService = _host.Services.GetRequiredService<IPageService>();
        pageService.SetServiceProvider(_host.Services);
        
        // 配置导航页面提供者
        var navigationWindow = _host.Services.GetRequiredService<MainWindow>();
        navigationWindow.SetPageService(_host.Services.GetRequiredService<Wpf.Ui.Abstractions.INavigationViewPageProvider>());
        
        await _host.StartAsync();
        
        // 显示主窗口
        navigationWindow.Show();
    }
    
    /// <summary>
    /// 应用程序退出
    /// </summary>
    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
            _host = null;
        }
        
        base.OnExit(e);
    }
    
    /// <summary>
    /// 创建主机构建器
    /// </summary>
    private static IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .UseSerilog((context, services, configuration) =>
            {
                configuration
                    .MinimumLevel.Debug()
                    .WriteTo.File(
                        Path.Combine(LogPath, "app-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7,
                        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                        encoding: System.Text.Encoding.UTF8
                    );
            })
            .ConfigureServices((context, services) =>
            {
                // ========== 核心服务（单例） ==========
                // 需求 7.4: 单例服务
                services.AddSingleton<IConfigService, ConfigService>();
                services.AddSingleton<ILogService, LogService>();
                services.AddSingleton<INotificationService, NotificationService>();
                services.AddSingleton<IPageService, PageService>();
                services.AddSingleton<IWindowEnumerationService, WindowEnumerationService>();
                services.AddSingleton<ConfigManager>();
                
                // ========== 迁移服务（单例） ==========
                // 需求 18.2: 截图服务
                services.AddSingleton<ICaptureService, CaptureService>();
                // 需求 18.3: 输入服务
                services.AddSingleton<IInputService, InputService>();
                
                // ========== WPF-UI 服务 ==========
                // 需求 1.2, 2.1: 导航服务
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<ISnackbarService, SnackbarService>();
                services.AddNavigationViewPageProvider();
                
                // ========== 任务系统（单例） ==========
                // 需求 8.3, 8.4, 8.5, 8.6
                services.AddSingleton<TaskRunner>();
                services.AddSingleton<TaskTriggerDispatcher>();
                
                // ========== 识别服务（单例） ==========
                // 需求 15.1: OCR 服务
                // 需求 16.1: YOLO 服务
                services.AddSingleton<HardwareAccelerationConfig>();
                services.AddSingleton<BgiOnnxFactory>();
                services.AddSingleton<IOcrService>(sp =>
                {
                    var onnxFactory = sp.GetRequiredService<BgiOnnxFactory>();
                    // 使用 V4 模型（中英文识别）
                    return new PaddleOcrService(onnxFactory, PaddleOcrService.PaddleOcrModelType.V4);
                });
                services.AddSingleton<IYoloService, YoloService>();
                
                // ========== 触发器（单例） ==========
                // 需求 14.6: 技能循环触发器
                services.AddSingleton<ShineProCS.Core.GameTask.Triggers.SkillLoopTrigger>(sp =>
                {
                    var inputService = sp.GetRequiredService<IInputService>();
                    var captureService = sp.GetRequiredService<ICaptureService>();
                    var configManager = sp.GetRequiredService<ConfigManager>();
                    return new ShineProCS.Core.GameTask.Triggers.SkillLoopTrigger(
                        inputService.Keyboard,
                        captureService.GetImageInterface(),
                        configManager);
                });
                
                // ========== 遮罩窗口 ==========
                // 需求 21.1: 遮罩窗口
                services.AddSingleton<MaskWindowViewModel>();
                services.AddSingleton<MaskWindow>();
                
                // ========== ViewModel（单例/瞬态） ==========
                // 需求 7.2, 7.3: 注册 ViewModel
                // MainViewModel 作为单例，因为主窗口只有一个
                services.AddSingleton<MainViewModel>();
                
                // ========== 导航页面（瞬态） ==========
                // 需求 7.5, 2.2: 页面实例作为瞬态，支持 NavigationCacheMode
                services.AddTransient<HomePage>();
                services.AddTransient<SkillsPage>();
                services.AddTransient<BuffsPage>();
                services.AddTransient<SettingsPage>();
                
                // ========== 旧页面控件（瞬态） ==========
                services.AddTransient<SkillConfigPage>();
                services.AddTransient<BuffLibraryPage>();
                
                // ========== 窗口 ==========
                // 主窗口作为单例
                services.AddSingleton<MainWindow>();
            });
    }
    
    /// <summary>
    /// 配置全局异常处理
    /// </summary>
    private void ConfigureExceptionHandling()
    {
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
    
    /// <summary>
    /// 记录异常到日志文件
    /// </summary>
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
            
            // 同时使用 Serilog 记录
            Log.Error(ex, "{Source}: {Message}", source, ex.Message);
        }
        catch { /* 日志写入失败时忽略 */ }
    }
}
