using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ShineProCS.Views;

public partial class RegionHighlightWindow : Window
{
    private readonly DispatcherTimer _closeTimer;
    private readonly Storyboard _blinkStoryboard;

    public RegionHighlightWindow(int x, int y, int width, int height, int durationSeconds = 5)
    {
        InitializeComponent();
        
        // 设置位置和大小
        Left = x - 3;
        Top = y - 3;
        Width = width + 6;
        Height = height + 6;
        
        // 创建闪烁动画
        _blinkStoryboard = new Storyboard();
        var animation = new ColorAnimation
        {
            From = Colors.Red,
            To = Colors.Yellow,
            Duration = TimeSpan.FromMilliseconds(300),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        Storyboard.SetTarget(animation, HighlightBorder);
        Storyboard.SetTargetProperty(animation, new PropertyPath("BorderBrush.Color"));
        _blinkStoryboard.Children.Add(animation);
        
        // 自动关闭定时器
        _closeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(durationSeconds)
        };
        _closeTimer.Tick += (s, e) =>
        {
            _closeTimer.Stop();
            _blinkStoryboard.Stop();
            Close();
        };
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        _blinkStoryboard.Begin();
        _closeTimer.Start();
    }

    /// <summary>
    /// 显示高亮框
    /// </summary>
    public static void ShowHighlight(int x, int y, int width, int height, int durationSeconds = 5)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var window = new RegionHighlightWindow(x, y, width, height, durationSeconds);
            window.Show();
        });
    }
}
