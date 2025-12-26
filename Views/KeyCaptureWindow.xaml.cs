using System.Windows;
using System.Windows.Input;
using ShineProCS.Utils;

namespace ShineProCS.Views;

public partial class KeyCaptureWindow : Window
{
    public int CapturedKeyCode { get; private set; }
    public string CapturedKeyName { get; private set; } = "";

    public KeyCaptureWindow()
    {
        InitializeComponent();
        Loaded += (s, e) => Focus();
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // 忽略修饰键单独按下
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
            e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
            e.Key == Key.LeftShift || e.Key == Key.RightShift ||
            e.Key == Key.LWin || e.Key == Key.RWin ||
            e.Key == Key.System)
        {
            return;
        }

        // ESC 取消
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
            return;
        }

        // 获取实际按键（处理System键）
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        CapturedKeyCode = KeyCodeHelper.KeyToVirtualKey(key);
        CapturedKeyName = KeyCodeHelper.GetKeyName(CapturedKeyCode);

        KeyDisplay.Text = CapturedKeyName;
        ConfirmBtn.IsEnabled = true;

        e.Handled = true;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (CapturedKeyCode > 0)
        {
            DialogResult = true;
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
