using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ShineProCS.Models;

namespace ShineProCS.Views;

/// <summary>
/// 悬浮窗口，显示引擎运行状态
/// 支持拖拽移动、透明度调节、位置持久化
/// </summary>
public partial class OverlayWindow : Window
{
    #region 静态画刷资源
    
    private static readonly SolidColorBrush GreenBrush = new(System.Windows.Media.Color.FromRgb(76, 175, 80));
    private static readonly SolidColorBrush OrangeBrush = new(System.Windows.Media.Color.FromRgb(255, 152, 0));
    private static readonly SolidColorBrush GrayBrush = new(System.Windows.Media.Color.FromRgb(136, 136, 136));
    private static readonly SolidColorBrush DarkGrayBrush = new(System.Windows.Media.Color.FromRgb(102, 102, 102));
    private static readonly SolidColorBrush ActiveBgBrush = new(System.Windows.Media.Color.FromRgb(76, 175, 80));
    private static readonly SolidColorBrush InactiveBgBrush = new(System.Windows.Media.Color.FromRgb(51, 51, 51));
    private static readonly SolidColorBrush WhiteBrush = new(System.Windows.Media.Color.FromRgb(255, 255, 255));
    private static readonly SolidColorBrush YellowBrush = new(System.Windows.Media.Color.FromRgb(255, 215, 0));
    
    #endregion

    private bool _isRunning;
    private AppSettings? _settings;
    
    #region 事件定义
    
    /// <summary>
    /// 请求启动/停止引擎
    /// </summary>
    public event Action? OnStartStopRequested;
    
    /// <summary>
    /// 请求暂停/恢复引擎
    /// </summary>
    public event Action? OnPauseRequested;
    
    /// <summary>
    /// 请求隐藏悬浮窗
    /// </summary>
    public event Action? OnHideRequested;
    
    /// <summary>
    /// 位置或透明度变化时触发，用于持久化
    /// </summary>
    public event Action<double, double, double>? OnPositionChanged;
    
    /// <summary>
    /// 请求切换配置方案
    /// </summary>
    public event Action<string>? OnProfileSwitchRequested;
    
    #endregion
    
    private List<string> _profiles = [];

    /// <summary>
    /// 创建悬浮窗实例
    /// </summary>
    public OverlayWindow()
    {
        InitializeComponent();
        
        // 窗口关闭时保存位置
        Closing += (s, e) => SavePosition();
        LocationChanged += (s, e) => SavePositionDebounced();
    }

    /// <summary>
    /// 设置可用的配置方案列表
    /// </summary>
    public void SetProfiles(List<string> profiles)
    {
        _profiles = profiles;
        UpdateProfileMenu();
    }

    /// <summary>
    /// 更新配置方案菜单
    /// </summary>
    private void UpdateProfileMenu()
    {
        ProfileMenu.Items.Clear();
        foreach (var profile in _profiles)
        {
            var item = new System.Windows.Controls.MenuItem
            {
                Header = profile,
                Tag = profile
            };
            item.Click += (s, e) =>
            {
                if (s is System.Windows.Controls.MenuItem mi && mi.Tag is string p)
                    OnProfileSwitchRequested?.Invoke(p);
            };
            ProfileMenu.Items.Add(item);
        }
    }

    /// <summary>
    /// 使用指定设置初始化悬浮窗位置和透明度
    /// </summary>
    /// <param name="settings">应用设置</param>
    public void InitializeFromSettings(AppSettings settings)
    {
        _settings = settings;
        
        // 恢复位置
        Left = settings.OverlayLeft;
        Top = settings.OverlayTop;
        
        // 恢复透明度
        MainBorder.Opacity = settings.OverlayOpacity;
        
        // 确保窗口在屏幕范围内
        EnsureOnScreen();
    }

    /// <summary>
    /// 确保窗口在屏幕可见范围内
    /// </summary>
    private void EnsureOnScreen()
    {
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;
        
        if (Left < 0) Left = 0;
        if (Top < 0) Top = 0;
        if (Left + Width > screenWidth) Left = screenWidth - Width;
        if (Top + Height > screenHeight) Top = screenHeight - Height;
    }

    private System.Threading.Timer? _saveTimer;
    
    /// <summary>
    /// 防抖保存位置（避免拖拽时频繁保存）
    /// </summary>
    private void SavePositionDebounced()
    {
        _saveTimer?.Dispose();
        _saveTimer = new System.Threading.Timer(_ =>
        {
            Dispatcher.Invoke(SavePosition);
        }, null, 500, System.Threading.Timeout.Infinite);
    }

    /// <summary>
    /// 保存当前位置和透明度
    /// </summary>
    private void SavePosition()
    {
        OnPositionChanged?.Invoke(Left, Top, MainBorder.Opacity);
    }

    /// <summary>
    /// 更新悬浮窗显示状态
    /// </summary>
    /// <param name="status">运行状态文本</param>
    /// <param name="count">执行次数</param>
    /// <param name="responseMs">响应时间（毫秒）</param>
    /// <param name="nextSkill">下一个技能名称</param>
    /// <param name="hpPercent">HP百分比</param>
    /// <param name="mpPercent">MP百分比</param>
    public void UpdateStatus(string status, int count, double responseMs, 
        string? nextSkill = null, double hpPercent = 100, double mpPercent = 100)
    {
        Dispatcher.Invoke(() =>
        {
            _isRunning = status == "运行中";
            MenuStartStop.Header = _isRunning ? "停止引擎" : "启动引擎";
            
            StatusText.Text = status;
            CountText.Text = count.ToString();
            ResponseText.Text = $"{responseMs:F1}ms";
            
            StatusText.Foreground = status switch
            {
                "运行中" => GreenBrush,
                "已暂停" => OrangeBrush,
                _ => GrayBrush
            };
            
            // 下一个技能
            NextSkillText.Text = string.IsNullOrEmpty(nextSkill) ? "无" : nextSkill;
            NextSkillText.Foreground = string.IsNullOrEmpty(nextSkill) ? GrayBrush : YellowBrush;
            
            // HP/MP 进度条
            var barMaxWidth = 60.0;
            HpBar.Width = Math.Max(0, Math.Min(barMaxWidth, barMaxWidth * hpPercent / 100.0));
            MpBar.Width = Math.Max(0, Math.Min(barMaxWidth, barMaxWidth * mpPercent / 100.0));
        });
    }

    #region 事件处理

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 右键菜单会自动显示
    }

    private void MenuStartStop_Click(object sender, RoutedEventArgs e)
    {
        OnStartStopRequested?.Invoke();
    }

    private void MenuPause_Click(object sender, RoutedEventArgs e)
    {
        OnPauseRequested?.Invoke();
    }

    private void MenuOpacity_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem menuItem && menuItem.Tag is string opacityStr)
        {
            if (double.TryParse(opacityStr, out var opacity))
            {
                MainBorder.Opacity = opacity;
                SavePosition(); // 保存透明度变化
            }
        }
    }

    private void MenuHide_Click(object sender, RoutedEventArgs e)
    {
        OnHideRequested?.Invoke();
    }
    
    #endregion
}
