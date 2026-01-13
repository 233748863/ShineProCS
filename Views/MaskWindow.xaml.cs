using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using ShineProCS.Core.Config;
using ShineProCS.Core.View.Drawable;
using ShineProCS.Models;
using ShineProCS.ViewModels;

// 使用 WPF 的类型，避免与 System.Drawing 冲突
using Application = System.Windows.Application;
using RichTextBox = System.Windows.Controls.RichTextBox;
using TextChangedEventArgs = System.Windows.Controls.TextChangedEventArgs;
using FontFamily = System.Windows.Media.FontFamily;
using Brushes = System.Windows.Media.Brushes;
using Pen = System.Windows.Media.Pen;
using FlowDirection = System.Windows.FlowDirection;

namespace ShineProCS.Views;

/// <summary>
/// 遮罩窗口 - 覆盖在游戏窗口上，用于显示识别结果、显示日志、设置区域位置等
/// 100% 移植自 BetterGI
/// </summary>
public partial class MaskWindow : Window
{
    #region Win32 API

    private const int GWL_EXSTYLE = -20;
    private const int GWL_STYLE = -16;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_CHILD = 0x40000000;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    [DllImport("user32.dll")]
    private static extern bool SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, 
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    private const uint SWP_SHOWWINDOW = 0x0040;

    #endregion


    #region 静态实例

    private static MaskWindow? _maskWindow;
    private static readonly Typeface _typeface;

    static MaskWindow()
    {
        if (Application.Current.TryFindResource("TextThemeFontFamily") is FontFamily fontFamily)
        {
            _typeface = fontFamily.GetTypefaces().First();
        }
        else
        {
            _typeface = new FontFamily("Microsoft YaHei UI").GetTypefaces().First();
        }
        DefaultStyleKeyProperty.OverrideMetadata(typeof(MaskWindow), 
            new FrameworkPropertyMetadata(typeof(MaskWindow)));
    }

    public static MaskWindow Instance()
    {
        if (_maskWindow == null) throw new Exception("MaskWindow 未初始化");
        return _maskWindow;
    }

    public bool IsExist() => _maskWindow != null && PresentationSource.FromVisual(_maskWindow) != null;

    #endregion

    #region 字段

    private nint _hWnd;
    private nint _gameHandle;
    private double _dpiScale = 1.0;
    private MaskWindowConfig _config = new();
    private MaskWindowViewModel? _viewModel;
    private ILogger<MaskWindow>? _logger;

    #endregion

    #region 属性

    public MaskWindowConfig Config
    {
        get => _config;
        set { _config = value; if (_viewModel != null) _viewModel.Config = value; }
    }

    public nint GameHandle
    {
        get => _gameHandle;
        set { _gameHandle = value; if (_viewModel != null) _viewModel.GameHandle = value; }
    }

    public double DpiScale
    {
        get => _dpiScale;
        set { _dpiScale = value; if (_viewModel != null) _viewModel.DpiScale = value; }
    }

    public RichTextBox LogBox => LogTextBox;

    #endregion

    #region 构造函数

    public MaskWindow()
    {
        _maskWindow = this;
        this.SetResourceReference(StyleProperty, typeof(MaskWindow));
        InitializeComponent();
        InitializeDpiAwareness();

        _viewModel = DataContext as MaskWindowViewModel;
        if (_viewModel != null) _viewModel.Config = _config;

        LogTextBox.TextChanged += LogTextBoxTextChanged;
        Loaded += OnLoaded;
        VisionContext.Instance().DrawContent.RefreshAction = Refresh;
    }

    #endregion

    #region DPI 感知

    private void InitializeDpiAwareness()
    {
        var source = PresentationSource.FromVisual(Application.Current.MainWindow);
        if (source?.CompositionTarget != null)
            _dpiScale = source.CompositionTarget.TransformToDevice.M11;
    }

    #endregion

    #region 初始化

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hWnd = new WindowInteropHelper(this).Handle;

        try { _logger = App.GetLogger<MaskWindow>(); } catch { }

        if (_config.UseSubform && _gameHandle != IntPtr.Zero)
        {
            if (GetParent(_hWnd) != _gameHandle)
                SetParent(_hWnd, _gameHandle);
        }

        RefreshPosition();
        PrintSystemInfo();
    }

    public void InitializeFromSettings(AppSettings settings)
    {
        if (_viewModel != null)
        {
            _viewModel.AppSettings = settings;
            _viewModel.InitializeStatusList();
        }
    }

    private void PrintSystemInfo()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        
        if (_logger != null)
        {
            _logger.LogInformation("ShineProCS {Version}", version);
            _logger.LogInformation("遮罩窗口已启动，窗口大小{Width}x{Height}，DPI缩放{Dpi}",
                (int)Width, (int)Height, _dpiScale.ToString("F2"));
        }
        else
        {
            AppendLog($"ShineProCS {version}", LogLevel.Info);
            AppendLog($"遮罩窗口已启动，窗口大小{(int)Width}x{(int)Height}，DPI缩放{_dpiScale:F2}", LogLevel.Info);
        }

        if ((int)Width * 9 != (int)Height * 16)
        {
            var msg = "当前游戏分辨率不是16:9，部分功能可能无法正常使用！";
            if (_logger != null) _logger.LogError(msg);
            else AppendLog(msg, LogLevel.Error);
        }

        CheckInterferingSoftware();
    }

    private void CheckInterferingSoftware()
    {
        if (Process.GetProcessesByName("MSIAfterburner").Length > 0)
        {
            var msg = "检测到 MSI Afterburner 正在运行，OSD可能干扰图像识别";
            if (_logger != null) _logger.LogWarning(msg);
            else AppendLog(msg, LogLevel.Warning);
        }

        if (Process.GetProcessesByName("RTSS").Length > 0)
        {
            var msg = "检测到 RTSS 正在运行，OSD显示可能干扰图像识别";
            if (_logger != null) _logger.LogWarning(msg);
            else AppendLog(msg, LogLevel.Warning);
        }
    }

    #endregion


    #region 窗口位置

    public void BringToTop() => BringWindowToTop(_hWnd);

    public void RefreshPosition()
    {
        if (_config.UseSubform) RefreshPositionForSubform();
        else RefreshPositionForNormal();
    }

    public void RefreshPositionForNormal()
    {
        if (_gameHandle == IntPtr.Zero) return;
        var currentRect = GetCaptureRect(_gameHandle);
        Invoke(() =>
        {
            Left = currentRect.Left / _dpiScale;
            Top = currentRect.Top / _dpiScale;
            Width = currentRect.Width / _dpiScale;
            Height = currentRect.Height / _dpiScale;
            BringToTop();
        });
    }

    public void RefreshPositionForSubform()
    {
        if (_gameHandle == IntPtr.Zero) return;
        GetClientRect(_gameHandle, out RECT targetRect);
        SetWindowPos(_hWnd, IntPtr.Zero, 0, 0, targetRect.Width, targetRect.Height, SWP_SHOWWINDOW);
    }

    public static System.Drawing.Rectangle GetCaptureRect(nint hWnd)
    {
        GetClientRect(hWnd, out RECT clientRect);
        POINT point = new() { X = 0, Y = 0 };
        ClientToScreen(hWnd, ref point);
        return new System.Drawing.Rectangle(point.X, point.Y, clientRect.Width, clientRect.Height);
    }

    #endregion

    #region 窗口样式

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hWnd = new WindowInteropHelper(this).Handle;
        SetLayeredWindow();
        HideFromAltTab();
        if (_config.UseSubform) SetChildWindow();
    }

    private void SetLayeredWindow(bool isLayered = true)
    {
        int style = GetWindowLong(_hWnd, GWL_EXSTYLE);
        if (isLayered)
        {
            style |= WS_EX_TRANSPARENT;
            style |= WS_EX_LAYERED;
        }
        else
        {
            style &= ~WS_EX_TRANSPARENT;
            style &= ~WS_EX_LAYERED;
        }
        SetWindowLong(_hWnd, GWL_EXSTYLE, style);
    }

    private void HideFromAltTab()
    {
        int style = GetWindowLong(_hWnd, GWL_EXSTYLE);
        style |= WS_EX_TOOLWINDOW;
        SetWindowLong(_hWnd, GWL_EXSTYLE, style);
    }

    private void SetChildWindow()
    {
        int style = GetWindowLong(_hWnd, GWL_STYLE);
        style |= WS_CHILD;
        SetWindowLong(_hWnd, GWL_STYLE, style);
    }

    #endregion

    #region 日志

    public enum LogLevel { Debug, Info, Warning, Error }

    public void AppendLog(string message, LogLevel level = LogLevel.Info)
    {
        Invoke(() =>
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var levelStr = level switch
            {
                LogLevel.Debug => "DBG", LogLevel.Info => "INF",
                LogLevel.Warning => "WRN", LogLevel.Error => "ERR", _ => "INF"
            };
            var color = level switch
            {
                LogLevel.Debug => Colors.Gray, LogLevel.Info => Colors.LightGray,
                LogLevel.Warning => Colors.Orange, LogLevel.Error => Colors.Red, _ => Colors.LightGray
            };

            var paragraph = LogTextBox.Document.Blocks.FirstBlock as Paragraph;
            if (paragraph == null)
            {
                paragraph = new Paragraph();
                LogTextBox.Document.Blocks.Add(paragraph);
            }

            var run = new Run($"[{timestamp} {levelStr}] {message}") { Foreground = new SolidColorBrush(color) };
            paragraph.Inlines.Add(run);
            paragraph.Inlines.Add(new LineBreak());
        });
    }

    private void LogTextBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (LogTextBox.Document.Blocks.FirstBlock is Paragraph p && p.Inlines.Count > 100)
            (p.Inlines as System.Collections.IList)?.RemoveAt(0);

        var textRange = new TextRange(LogTextBox.Document.ContentStart, LogTextBox.Document.ContentEnd);
        if (textRange.Text.Length > 10000) LogTextBox.Document.Blocks.Clear();

        LogTextBox.ScrollToEnd();
    }

    public void ClearLog() => Invoke(() => LogTextBox.Document.Blocks.Clear());

    #endregion

    #region 绘制

    public void Refresh() => Dispatcher.Invoke(InvalidateVisual);
    public void Invoke(Action action) => Dispatcher.Invoke(action);

    protected override void OnRender(DrawingContext drawingContext)
    {
        try
        {
            var drawContent = VisionContext.Instance().DrawContent;
            var cnt = drawContent.RectList.Count + drawContent.LineList.Count + drawContent.TextList.Count;
            if (cnt == 0 || !_config.DisplayRecognitionResultsOnMask) return;

            foreach (var kv in drawContent.RectList)
                foreach (var drawable in kv.Value)
                    if (!drawable.IsEmpty)
                        drawingContext.DrawRectangle(Brushes.Transparent,
                            new Pen(new SolidColorBrush(drawable.Pen.Color.ToWindowsColor()), drawable.Pen.Width),
                            drawable.Rect);

            foreach (var kv in drawContent.LineList)
                foreach (var drawable in kv.Value)
                    drawingContext.DrawLine(
                        new Pen(new SolidColorBrush(drawable.Pen.Color.ToWindowsColor()), drawable.Pen.Width),
                        drawable.P1, drawable.P2);

            foreach (var kv in drawContent.TextList)
                foreach (var drawable in kv.Value)
                    if (!drawable.IsEmpty)
                        drawingContext.DrawText(
                            new FormattedText(drawable.Text, CultureInfo.GetCultureInfo("zh-cn"),
                                FlowDirection.LeftToRight, _typeface, 36, Brushes.Black, 1),
                            drawable.Point);
        }
        catch (Exception ex) { Debug.WriteLine(ex); }
        base.OnRender(drawingContext);
    }

    #endregion

    #region 清理

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewModel?.Cleanup();
        VisionContext.Instance().DrawContent.RefreshAction = null;
        _maskWindow = null;
    }

    #endregion
}
