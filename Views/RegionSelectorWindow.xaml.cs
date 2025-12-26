using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ShineProCS.Views;

public partial class RegionSelectorWindow : Window
{
    private System.Windows.Point _start;
    private bool _selecting;
    public Int32Rect SelectedRegion { get; private set; }
    public System.Windows.Point SelectedPoint { get; private set; }
    public bool PointSelectMode { get; set; }

    public RegionSelectorWindow(bool pointMode = false)
    {
        InitializeComponent();
        PointSelectMode = pointMode;
        Left = Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
    }

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

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Escape) { DialogResult = false; Close(); } base.OnKeyDown(e); }
}
