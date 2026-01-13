using System.Windows;
using System.Drawing;
using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Services;
using ShineProCS.ViewModels;
using ShineProCS.Views.Pages;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;
using TrayNotifyIcon = Wpf.Ui.Tray.Controls.NotifyIcon;

namespace ShineProCS;

/// <summary>
/// 主窗口 - 使用 NavigationView 实现导航式布局
/// 需求: 1.1, 1.2, 1.4, 1.5, 1.6, 1.7, 2.1, 2.4, 2.5
/// </summary>
public partial class MainWindow : FluentWindow, INavigationWindow
{
    private readonly MainViewModel _viewModel;
    private readonly IPageService _pageService;
    private readonly INavigationService _navigationService;

    /// <summary>
    /// 默认构造函数（用于 XAML 设计器）
    /// </summary>
    public MainWindow() : this(
        App.GetService<MainViewModel>(),
        App.GetService<INavigationService>(),
        App.GetService<ISnackbarService>(),
        App.GetService<IPageService>())
    {
    }
    
    /// <summary>
    /// DI 构造函数
    /// 需求: 7.3 - 使用构造函数注入
    /// </summary>
    public MainWindow(
        MainViewModel viewModel,
        INavigationService navigationService,
        ISnackbarService snackbarService,
        IPageService pageService)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _pageService = pageService ?? throw new ArgumentNullException(nameof(pageService));
        DataContext = _viewModel;
        
        InitializeComponent();
        
        // 配置 Snackbar 服务
        snackbarService.SetSnackbarPresenter(SnackbarPresenter);
        
        // 配置导航服务
        navigationService.SetNavigationControl(RootNavigation);
        
        // 需求 2.1: 注册页面类型与导航项的映射
        RegisterPageMappings();
        
        // 需求 2.1: 订阅导航事件
        RootNavigation.Navigated += OnNavigated;
        
        // 设置当前窗口为主窗口
        System.Windows.Application.Current.MainWindow = this;
        
        // 需求 12.1: 初始化托盘图标
        InitializeTrayIcon();
        
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    /// <summary>
    /// 注册页面类型与导航项的映射
    /// 需求: 2.1, 2.5 - 导航一致性和嵌套菜单支持
    /// </summary>
    private void RegisterPageMappings()
    {
        _pageService.RegisterPageMapping(typeof(HomePage), "启动");
        _pageService.RegisterPageMapping(typeof(SkillsPage), "技能配置");
        _pageService.RegisterPageMapping(typeof(BuffsPage), "Buff库");
        _pageService.RegisterPageMapping(typeof(SettingsPage), "设置");
        
        // 嵌套菜单的父级映射（需求 2.5）
        // 注意：嵌套菜单项的子页面已在上面注册
    }

    /// <summary>
    /// 导航完成事件处理
    /// 需求: 2.1, 2.4 - 页面导航和状态保持
    /// </summary>
    private void OnNavigated(NavigationView sender, NavigatedEventArgs args)
    {
        // 更新 ViewModel 中的当前页面信息
        if (args.Page != null)
        {
            var pageType = args.Page.GetType();
            _viewModel.CurrentPageType = pageType;
            
            // 记录导航日志
            System.Diagnostics.Debug.WriteLine($"导航到页面: {pageType.Name}");
        }
    }

    /// <summary>
    /// 初始化托盘图标
    /// 需求: 12.1 - 配置 NotifyIcon 组件
    /// </summary>
    private void InitializeTrayIcon()
    {
        try
        {
            // 尝试从资源文件加载图标，如果不存在则使用系统默认图标
            var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
            if (System.IO.File.Exists(iconPath))
            {
                TrayIcon.Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath, UriKind.Absolute));
            }
            else
            {
                // 使用应用程序图标或创建默认图标
                var appIcon = System.Drawing.Icon.ExtractAssociatedIcon(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (appIcon != null)
                {
                    // 将 System.Drawing.Icon 转换为 ImageSource
                    using var bitmap = appIcon.ToBitmap();
                    var hBitmap = bitmap.GetHbitmap();
                    try
                    {
                        var imageSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                            hBitmap,
                            IntPtr.Zero,
                            System.Windows.Int32Rect.Empty,
                            System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                        TrayIcon.Icon = imageSource;
                    }
                    finally
                    {
                        DeleteObject(hBitmap);
                    }
                }
            }
            
            // 绑定托盘菜单命令（ContextMenu 不在可视化树中，需要手动绑定）
            TrayMenuShowWindow.Command = _viewModel.ShowWindowCommand;
            TrayMenuToggleEngine.Command = _viewModel.ToggleEngineCommand;
            TrayMenuPause.Command = _viewModel.PauseEngineCommand;
            TrayMenuCheckUpdate.Command = _viewModel.CheckUpdateCommand;
            TrayMenuExit.Command = _viewModel.ExitCommand;
            
            // 注册托盘图标
            TrayIcon.Register();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"初始化托盘图标失败: {ex.Message}");
        }
    }
    
    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 初始化全局快捷键
        _viewModel.InitializeHotkeys(this);
        
        // 激活窗口
        Activate();
        
        // 需求 1.3: 默认显示启动页
        RootNavigation.Navigate(typeof(HomePage));
    }

    /// <summary>
    /// 托盘图标双击事件 - 显示窗口
    /// 需求: 12.2
    /// </summary>
    private void OnNotifyIconLeftDoubleClick(TrayNotifyIcon sender, RoutedEventArgs e)
    {
        ShowWindow();
    }

    /// <summary>
    /// 显示窗口
    /// </summary>
    public void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    /// <summary>
    /// 窗口关闭事件 - 最小化到托盘
    /// 需求: 1.6
    /// </summary>
    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // 检查是否有未保存的变更
        if (_viewModel.HasUnsavedChanges)
        {
            if (!_viewModel.PromptSaveChanges())
            {
                e.Cancel = true;
                return;
            }
        }
        
        // 最小化到托盘而不是关闭
        e.Cancel = true;
        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        // 取消订阅导航事件
        RootNavigation.Navigated -= OnNavigated;
        
        // 注销托盘图标
        TrayIcon.Unregister();
        
        _viewModel.Dispose();
        base.OnClosed(e);
    }

    #region INavigationWindow 实现

    public INavigationView GetNavigation() => RootNavigation;

    public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

    public void SetServiceProvider(IServiceProvider serviceProvider)
    {
        // 由 App.xaml.cs 中的 PageService 处理
    }

    public void SetPageService(INavigationViewPageProvider navigationViewPageProvider)
    {
        RootNavigation.SetPageProviderService(navigationViewPageProvider);
    }

    public void CloseWindow() => Close();

    #endregion

    #region 向后兼容方法

    /// <summary>
    /// 获取 ConfigManager 实例（向后兼容）
    /// 供 SkillCardControl 和 SkillConfigPage 等控件使用
    /// </summary>
    public ConfigManager GetConfigManager() => _viewModel.ConfigManager;

    #endregion
}
