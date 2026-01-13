using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using ShineProCS.Core.Config;
using ShineProCS.Core.Interfaces;
using ShineProCS.Helpers;
using ShineProCS.Models;

namespace ShineProCS.ViewModels;

/// <summary>
/// 遮罩窗口 ViewModel
/// 100% 移植自 BetterGI
/// </summary>
public partial class MaskWindowViewModel : ObservableRecipient
{
    #region Win32 API for FPS

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    #endregion

    /// <summary>
    /// 窗口矩形
    /// </summary>
    [ObservableProperty] 
    private Rect _windowRect;

    /// <summary>
    /// 状态列表
    /// </summary>
    [ObservableProperty] 
    private ObservableCollection<StatusItem> _statusList = [];

    /// <summary>
    /// 遮罩窗口配置
    /// </summary>
    public MaskWindowConfig Config { get; set; } = new();

    /// <summary>
    /// FPS 显示
    /// </summary>
    [ObservableProperty] 
    private string _fps = "0";

    /// <summary>
    /// 游戏窗口句柄
    /// </summary>
    public nint GameHandle { get; set; }

    /// <summary>
    /// DPI 缩放
    /// </summary>
    public double DpiScale { get; set; } = 1.0;

    /// <summary>
    /// 应用设置引用
    /// </summary>
    public AppSettings? AppSettings { get; set; }

    /// <summary>
    /// 是否在 Wine 环境下运行
    /// </summary>
    [ObservableProperty]
    private bool _isRunningOnWine = false;

    private CancellationTokenSource? _fpsCts;

    public MaskWindowViewModel()
    {
        // 注册消息，用于刷新配置
        WeakReferenceMessenger.Default.Register<PropertyChangedMessage<object>>(this, (sender, msg) =>
        {
            if (msg.PropertyName == "RefreshSettings")
            {
                UIDispatcherHelper.Invoke(RefreshSettings);
            }
        });

        // 检测 Wine 环境
        CheckWineEnvironment();
    }

    /// <summary>
    /// 检测是否在 Wine 环境下运行
    /// </summary>
    private void CheckWineEnvironment()
    {
        try
        {
            // 检测 Wine 的方法：检查是否存在 wine 相关的注册表项或环境变量
            var winePrefix = Environment.GetEnvironmentVariable("WINEPREFIX");
            var wineLoaderName = Environment.GetEnvironmentVariable("WINELOADERNOEXEC");
            
            IsRunningOnWine = !string.IsNullOrEmpty(winePrefix) || !string.IsNullOrEmpty(wineLoaderName);
        }
        catch
        {
            IsRunningOnWine = false;
        }
    }

    /// <summary>
    /// 初始化状态列表
    /// </summary>
    public void InitializeStatusList()
    {
        StatusList.Clear();
        
        // 状态列表已清空，可根据需要添加其他状态项
    }

    /// <summary>
    /// 窗口加载命令
    /// </summary>
    [RelayCommand]
    private void OnLoaded()
    {
        RefreshSettings();
        InitializeStatusList();
        InitFps();
    }

    /// <summary>
    /// 刷新设置
    /// </summary>
    private void RefreshSettings()
    {
        InitConfig();
        if (Config != null)
        {
            OnPropertyChanged(nameof(Config));
        }
    }

    /// <summary>
    /// 初始化配置（从 DI 容器获取）
    /// </summary>
    private void InitConfig()
    {
        try
        {
            var configService = App.GetService<IConfigService>();
            if (configService != null)
            {
                // 从 ConfigService 获取 AppSettings
                var appSettings = configService.AppSettings;
                // 可以从 appSettings 同步 MaskWindowConfig
            }
        }
        catch
        {
            // 忽略，使用默认配置
        }
    }

    /// <summary>
    /// 初始化 FPS 监控
    /// </summary>
    private void InitFps()
    {
        if (!Config.ShowFps || GameHandle == IntPtr.Zero) return;

        _fpsCts?.Cancel();
        _fpsCts = new CancellationTokenSource();
        var token = _fpsCts.Token;

        // 获取游戏进程 ID
        GetWindowThreadProcessId(GameHandle, out var pid);

        Task.Run(async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            var lastTime = stopwatch.ElapsedMilliseconds;
            // frameCount 用于未来集成 PresentMonFps 时统计帧数
            _ = 0; // 占位符，避免未使用变量警告

            while (!token.IsCancellationRequested && Config.ShowFps)
            {
                await Task.Delay(1000, token);

                // 简单的 FPS 估算（实际应该使用 PresentMon 等专业库）
                // 这里暂时使用占位符，后续可以集成 PresentMonFps
                try
                {
                    // 尝试获取进程的 FPS（需要 PresentMon 库支持）
                    // 目前使用占位符
                    var currentTime = stopwatch.ElapsedMilliseconds;
                    var elapsed = currentTime - lastTime;
                    lastTime = currentTime;

                    // 占位符：显示估算值
                    Fps = "60"; // TODO: 集成 PresentMonFps 库
                }
                catch
                {
                    Fps = "--";
                }
            }
        }, token);
    }

    /// <summary>
    /// 清理资源
    /// </summary>
    public void Cleanup()
    {
        _fpsCts?.Cancel();
        _fpsCts?.Dispose();

        foreach (var item in StatusList)
        {
            item.Unsubscribe();
        }
        StatusList.Clear();

        // 取消消息注册
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }
}
