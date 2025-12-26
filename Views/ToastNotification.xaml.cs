using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ShineProCS.Views;

public enum ToastType
{
    Info,
    Success,
    Warning,
    Error
}

public partial class ToastNotification : Window
{
    private readonly System.Timers.Timer _autoCloseTimer;
    private static readonly SolidColorBrush InfoBrush = new(System.Windows.Media.Color.FromArgb(230, 51, 51, 51));
    private static readonly SolidColorBrush SuccessBrush = new(System.Windows.Media.Color.FromArgb(230, 46, 125, 50));
    private static readonly SolidColorBrush WarningBrush = new(System.Windows.Media.Color.FromArgb(230, 245, 124, 0));
    private static readonly SolidColorBrush ErrorBrush = new(System.Windows.Media.Color.FromArgb(230, 198, 40, 40));

    public ToastNotification(string title, string message, ToastType type = ToastType.Info, int durationMs = 3000)
    {
        InitializeComponent();
        
        TitleText.Text = title;
        MessageText.Text = message;
        
        // 设置图标和背景色
        IconText.Text = type switch
        {
            ToastType.Success => "✅",
            ToastType.Warning => "⚠️",
            ToastType.Error => "❌",
            _ => "ℹ️"
        };
        
        ToastBorder.Background = type switch
        {
            ToastType.Success => SuccessBrush,
            ToastType.Warning => WarningBrush,
            ToastType.Error => ErrorBrush,
            _ => InfoBrush
        };
        
        // 自动关闭定时器
        _autoCloseTimer = new System.Timers.Timer(durationMs);
        _autoCloseTimer.Elapsed += (s, e) =>
        {
            _autoCloseTimer.Stop();
            Dispatcher.Invoke(CloseWithAnimation);
        };
        
        // 点击关闭
        MouseLeftButtonDown += (s, e) => CloseWithAnimation();
        
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 定位到屏幕右下角
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - ActualWidth - 20;
        Top = workArea.Bottom - ActualHeight - 20;
        
        // 播放进入动画
        PlayEnterAnimation();
        
        // 启动自动关闭定时器
        _autoCloseTimer.Start();
    }

    private void PlayEnterAnimation()
    {
        // 初始状态：透明且偏右
        Opacity = 0;
        SlideTransform.X = 50;
        
        // 淡入动画
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        
        // 滑入动画
        var slideIn = new DoubleAnimation(50, 0, TimeSpan.FromMilliseconds(250))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        
        BeginAnimation(OpacityProperty, fadeIn);
        SlideTransform.BeginAnimation(TranslateTransform.XProperty, slideIn);
    }

    private void CloseWithAnimation()
    {
        // 淡出动画
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
        fadeOut.Completed += (s, e) => Close();
        
        // 滑出动画
        var slideOut = new DoubleAnimation(0, 30, TimeSpan.FromMilliseconds(200));
        
        BeginAnimation(OpacityProperty, fadeOut);
        SlideTransform.BeginAnimation(TranslateTransform.XProperty, slideOut);
    }

    protected override void OnClosed(EventArgs e)
    {
        _autoCloseTimer?.Stop();
        _autoCloseTimer?.Dispose();
        base.OnClosed(e);
    }
}

/// <summary>
/// Toast 通知管理器
/// </summary>
public static class ToastManager
{
    private static readonly Queue<ToastNotification> _toastQueue = new();
    private static ToastNotification? _currentToast;
    private static readonly object _lock = new();

    public static void Show(string title, string message, ToastType type = ToastType.Info, int durationMs = 3000)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            lock (_lock)
            {
                var toast = new ToastNotification(title, message, type, durationMs);
                toast.Closed += OnToastClosed;
                
                if (_currentToast == null)
                {
                    _currentToast = toast;
                    toast.Show();
                }
                else
                {
                    _toastQueue.Enqueue(toast);
                }
            }
        });
    }

    private static void OnToastClosed(object? sender, EventArgs e)
    {
        lock (_lock)
        {
            _currentToast = null;
            
            if (_toastQueue.Count > 0)
            {
                _currentToast = _toastQueue.Dequeue();
                _currentToast.Show();
            }
        }
    }

    public static void Success(string message, string title = "成功") 
        => Show(title, message, ToastType.Success);

    public static void Warning(string message, string title = "警告") 
        => Show(title, message, ToastType.Warning);

    public static void Error(string message, string title = "错误") 
        => Show(title, message, ToastType.Error);

    public static void Info(string message, string title = "提示") 
        => Show(title, message, ToastType.Info);
}
