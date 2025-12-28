using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ShineProCS.Views;

public partial class RegionSelectorWindow : Window
{
    private System.Windows.Point _start;
    private bool _selecting;
    private Window? _ownerWindow;
    
    public Int32Rect SelectedRegion { get; private set; }
    public System.Windows.Point SelectedPoint { get; private set; }
    public bool PointSelectMode { get; set; }

    public RegionSelectorWindow(bool pointMode = false)
    {
        // 先截取全屏（在初始化UI之前）
        var screenshot = CaptureScreen();
        
        InitializeComponent();
        PointSelectMode = pointMode;
        Left = Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
        
        // 设置截图为背景
        if (screenshot != null)
        {
            ScreenshotBackground.Source = screenshot;
        }
    }

    /// <summary>
    /// 显示选择器（自动隐藏主窗口）
    /// </summary>
    public new bool? ShowDialog()
    {
        // 隐藏主窗口
        _ownerWindow = System.Windows.Application.Current.MainWindow;
        if (_ownerWindow != null && _ownerWindow.IsVisible)
        {
            _ownerWindow.Hide();
        }
        
        // 短暂延迟确保窗口完全隐藏
        System.Threading.Thread.Sleep(100);
        
        // 重新截取屏幕（主窗口已隐藏）
        var screenshot = CaptureScreen();
        if (screenshot != null)
        {
            ScreenshotBackground.Source = screenshot;
        }
        
        var result = base.ShowDialog();
        
        // 恢复主窗口
        if (_ownerWindow != null)
        {
            _ownerWindow.Show();
            _ownerWindow.Activate();
        }
        
        return result;
    }

    /// <summary>
    /// 截取全屏
    /// </summary>
    private BitmapSource? CaptureScreen()
    {
        try
        {
            int width = (int)SystemParameters.PrimaryScreenWidth;
            int height = (int)SystemParameters.PrimaryScreenHeight;
            
            using var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(width, height));
            
            var hBitmap = bitmap.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }
        catch
        {
            return null;
        }
    }
    
    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        
        if (PointSelectMode)
        {
            var p = e.GetPosition(SelectionCanvas);
            SelectedPoint = p;
            SelectedRegion = new Int32Rect((int)p.X, (int)p.Y, 0, 0);
            DialogResult = true;
            Close();
            return;
        }
        
        _selecting = true;
        _start = e.GetPosition(SelectionCanvas);
        Canvas.SetLeft(SelectionBorder, _start.X);
        Canvas.SetTop(SelectionBorder, _start.Y);
        SelectionBorder.Width = SelectionBorder.Height = 0;
        SelectionBorder.Visibility = Visibility.Visible;
        SelectionCanvas.CaptureMouse();
    }

    private void Canvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        var p = e.GetPosition(SelectionCanvas);
        if (PointSelectMode)
        {
            CoordText.Text = $"点击选择坐标: ({(int)p.X}, {(int)p.Y})";
            return;
        }
        CoordText.Text = $"({(int)p.X}, {(int)p.Y})";
        if (!_selecting) return;
        var x = Math.Min(_start.X, p.X); var y = Math.Min(_start.Y, p.Y);
        var w = Math.Abs(_start.X - p.X); var h = Math.Abs(_start.Y - p.Y);
        Canvas.SetLeft(SelectionBorder, x); Canvas.SetTop(SelectionBorder, y);
        SelectionBorder.Width = w; SelectionBorder.Height = h;
        CoordText.Text = $"X={x:F0} Y={y:F0} W={w:F0} H={h:F0}";
    }

    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (PointSelectMode || !_selecting) return;
        _selecting = false;
        SelectionCanvas.ReleaseMouseCapture();
        var x = (int)Canvas.GetLeft(SelectionBorder); var y = (int)Canvas.GetTop(SelectionBorder);
        var w = (int)SelectionBorder.Width; var h = (int)SelectionBorder.Height;
        if (w > 5 && h > 5) { SelectedRegion = new Int32Rect(x, y, w, h); DialogResult = true; Close(); }
        else SelectionBorder.Visibility = Visibility.Collapsed;
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e) 
    { 
        if (e.Key == Key.Escape) 
        { 
            DialogResult = false; 
            Close(); 
        } 
        base.OnKeyDown(e); 
    }
}
