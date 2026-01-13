using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ShineProCS.ViewModels;

namespace ShineProCS.Views.Pages;

/// <summary>
/// 设置页 - 全局配置
/// 需求: 6.1, 6.2, 6.3, 6.4, 6.5
/// 
/// 注意：ScrollViewer 滚轮问题的解决方案
/// 问题：WPF UI 库的 NavigationView 内部布局导致 ScrollViewer 的 ViewportHeight 不正确
/// 解决：
/// 1. 使用 Grid 包裹 ScrollViewer，确保正确的布局约束
/// 2. 在 Loaded 事件中设置 MaxHeight，限制 ScrollViewer 的高度
/// 3. 强制 WPF 重新计算布局，使 ViewportHeight 正确更新
/// </summary>
public partial class SettingsPage : Page
{
    public MainViewModel ViewModel { get; }

    public SettingsPage(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;
        InitializeComponent();
        
        // 注册 ScrollViewer 的 Loaded 事件，用于设置 MaxHeight
        MainScrollViewer.Loaded += MainScrollViewer_Loaded;
    }

    /// <summary>
    /// ScrollViewer 加载完成后设置 MaxHeight
    /// 这是解决滚轮问题的关键：限制 ScrollViewer 的高度，使 ScrollableHeight > 0
    /// </summary>
    private void MainScrollViewer_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            // 获取主窗口高度
            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow != null && mainWindow.ActualHeight > 0)
            {
                // 计算允许的最大高度（窗口高度减去标题栏和导航栏等）
                double maxAllowedHeight = mainWindow.ActualHeight - 150;
                sv.MaxHeight = maxAllowedHeight;
                
                // 强制 WPF 重新计算布局，使 ViewportHeight 正确更新
                sv.InvalidateMeasure();
                sv.UpdateLayout();
            }
            
            // 监听窗口大小变化，动态调整 MaxHeight
            if (mainWindow != null)
            {
                mainWindow.SizeChanged += (s, args) =>
                {
                    if (args.HeightChanged && mainWindow.ActualHeight > 0)
                    {
                        double newMaxHeight = mainWindow.ActualHeight - 150;
                        sv.MaxHeight = newMaxHeight;
                        sv.InvalidateMeasure();
                        sv.UpdateLayout();
                    }
                };
            }
        }
    }

    #region 需求 6.3: 输入验证

    /// <summary>
    /// 数字输入预处理 - 只允许输入数字
    /// </summary>
    private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // 只允许数字输入
        e.Handled = !Regex.IsMatch(e.Text, @"^[0-9]+$");
    }

    /// <summary>
    /// 循环间隔验证 (10-5000ms)
    /// </summary>
    private void LoopIntervalTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not Wpf.Ui.Controls.TextBox textBox) return;
        
        var (isValid, errorMessage, correctedValue) = ValidateNumericInput(
            textBox.Text, 
            minValue: 10, 
            maxValue: 5000, 
            fieldName: "循环间隔");
        
        if (!isValid)
        {
            ShowValidationError(LoopIntervalError, errorMessage);
            if (correctedValue.HasValue)
            {
                ViewModel.AppSettings.LoopInterval = correctedValue.Value;
            }
        }
        else
        {
            HideValidationError(LoopIntervalError);
        }
    }

    /// <summary>
    /// 图像队列容量验证 (2-10)
    /// </summary>
    private void ImageQueueCapacityTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not Wpf.Ui.Controls.TextBox textBox) return;
        
        var (isValid, errorMessage, correctedValue) = ValidateNumericInput(
            textBox.Text, 
            minValue: 2, 
            maxValue: 10, 
            fieldName: "图像队列容量");
        
        if (!isValid)
        {
            ShowValidationError(ImageQueueCapacityError, errorMessage);
            if (correctedValue.HasValue)
            {
                ViewModel.AppSettings.ImageQueueCapacity = correctedValue.Value;
            }
        }
        else
        {
            HideValidationError(ImageQueueCapacityError);
        }
    }

    /// <summary>
    /// 帧变化检测阈值验证 (0-255)
    /// </summary>
    private void FrameChangeThresholdTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not Wpf.Ui.Controls.TextBox textBox) return;
        
        var (isValid, errorMessage, correctedValue) = ValidateNumericInput(
            textBox.Text, 
            minValue: 0, 
            maxValue: 255, 
            fieldName: "帧变化检测阈值");
        
        if (!isValid)
        {
            ShowValidationError(FrameChangeThresholdError, errorMessage);
            if (correctedValue.HasValue)
            {
                ViewModel.AppSettings.FrameChangeThreshold = correctedValue.Value;
            }
        }
        else
        {
            HideValidationError(FrameChangeThresholdError);
        }
    }

    /// <summary>
    /// 模板缓存大小验证 (10-500)
    /// </summary>
    private void TemplateCacheSizeTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not Wpf.Ui.Controls.TextBox textBox) return;
        
        var (isValid, errorMessage, correctedValue) = ValidateNumericInput(
            textBox.Text, 
            minValue: 10, 
            maxValue: 500, 
            fieldName: "模板缓存大小");
        
        if (!isValid)
        {
            ShowValidationError(TemplateCacheSizeError, errorMessage);
            if (correctedValue.HasValue)
            {
                ViewModel.AppSettings.TemplateCacheSize = correctedValue.Value;
            }
        }
        else
        {
            HideValidationError(TemplateCacheSizeError);
        }
    }

    /// <summary>
    /// 验证数字输入
    /// </summary>
    /// <param name="input">输入字符串</param>
    /// <param name="minValue">最小值</param>
    /// <param name="maxValue">最大值</param>
    /// <param name="fieldName">字段名称（用于错误消息）</param>
    /// <returns>(是否有效, 错误消息, 修正后的值)</returns>
    public static (bool IsValid, string ErrorMessage, int? CorrectedValue) ValidateNumericInput(
        string input, 
        int minValue, 
        int maxValue, 
        string fieldName)
    {
        // 空值检查
        if (string.IsNullOrWhiteSpace(input))
        {
            return (false, $"{fieldName}不能为空", minValue);
        }

        // 数字格式检查
        if (!int.TryParse(input, out int value))
        {
            return (false, $"{fieldName}必须是有效的整数", minValue);
        }

        // 范围检查
        if (value < minValue)
        {
            return (false, $"{fieldName}不能小于 {minValue}，已自动修正", minValue);
        }

        if (value > maxValue)
        {
            return (false, $"{fieldName}不能大于 {maxValue}，已自动修正", maxValue);
        }

        return (true, string.Empty, null);
    }

    /// <summary>
    /// 显示验证错误
    /// </summary>
    private static void ShowValidationError(TextBlock errorTextBlock, string message)
    {
        errorTextBlock.Text = message;
        errorTextBlock.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// 隐藏验证错误
    /// </summary>
    private static void HideValidationError(TextBlock errorTextBlock)
    {
        errorTextBlock.Visibility = Visibility.Collapsed;
    }


    /// <summary>
    /// 按键最小延迟验证 (0-500ms)
    /// </summary>
    private void KeyPressMinDelayTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not Wpf.Ui.Controls.TextBox textBox) return;
        
        var (isValid, errorMessage, correctedValue) = ValidateNumericInput(
            textBox.Text, 
            minValue: 0, 
            maxValue: 500, 
            fieldName: "按键最小延迟");
        
        if (!isValid)
        {
            ShowValidationError(KeyPressMinDelayError, errorMessage);
            if (correctedValue.HasValue)
            {
                ViewModel.AppSettings.KeyPressMinDelayMs = correctedValue.Value;
            }
        }
        else
        {
            HideValidationError(KeyPressMinDelayError);
        }
    }

    /// <summary>
    /// 按键最大延迟验证 (0-1000ms)
    /// </summary>
    private void KeyPressMaxDelayTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not Wpf.Ui.Controls.TextBox textBox) return;
        
        var (isValid, errorMessage, correctedValue) = ValidateNumericInput(
            textBox.Text, 
            minValue: 0, 
            maxValue: 1000, 
            fieldName: "按键最大延迟");
        
        if (!isValid)
        {
            ShowValidationError(KeyPressMaxDelayError, errorMessage);
            if (correctedValue.HasValue)
            {
                ViewModel.AppSettings.KeyPressMaxDelayMs = correctedValue.Value;
            }
        }
        else
        {
            HideValidationError(KeyPressMaxDelayError);
            
            // 确保最大延迟 >= 最小延迟
            if (ViewModel.AppSettings.KeyPressMaxDelayMs < ViewModel.AppSettings.KeyPressMinDelayMs)
            {
                ViewModel.AppSettings.KeyPressMaxDelayMs = ViewModel.AppSettings.KeyPressMinDelayMs;
                ShowValidationError(KeyPressMaxDelayError, "最大延迟不能小于最小延迟，已自动修正");
            }
        }
    }

    /// <summary>
    /// 最小按键间隔验证 (0-200ms)
    /// </summary>
    private void MinInterKeyDelayTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not Wpf.Ui.Controls.TextBox textBox) return;
        
        var (isValid, errorMessage, correctedValue) = ValidateNumericInput(
            textBox.Text, 
            minValue: 0, 
            maxValue: 200, 
            fieldName: "最小按键间隔");
        
        if (!isValid)
        {
            ShowValidationError(MinInterKeyDelayError, errorMessage);
            if (correctedValue.HasValue)
            {
                ViewModel.AppSettings.MinInterKeyDelayMs = correctedValue.Value;
            }
        }
        else
        {
            HideValidationError(MinInterKeyDelayError);
        }
    }

    /// <summary>
    /// 贝塞尔曲线步数验证 (5-100)
    /// </summary>
    private void BezierMouseStepsTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not Wpf.Ui.Controls.TextBox textBox) return;
        
        var (isValid, errorMessage, correctedValue) = ValidateNumericInput(
            textBox.Text, 
            minValue: 5, 
            maxValue: 100, 
            fieldName: "鼠标移动路径点数");
        
        if (!isValid)
        {
            ShowValidationError(BezierMouseStepsError, errorMessage);
            if (correctedValue.HasValue)
            {
                ViewModel.AppSettings.BezierMouseSteps = correctedValue.Value;
            }
        }
        else
        {
            HideValidationError(BezierMouseStepsError);
        }
    }

    /// <summary>
    /// 重连间隔验证 (500-30000ms)
    /// </summary>
    private void ReconnectRetryIntervalTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not Wpf.Ui.Controls.TextBox textBox) return;
        
        var (isValid, errorMessage, correctedValue) = ValidateNumericInput(
            textBox.Text, 
            minValue: 500, 
            maxValue: 30000, 
            fieldName: "重连间隔");
        
        if (!isValid)
        {
            ShowValidationError(ReconnectRetryIntervalError, errorMessage);
            if (correctedValue.HasValue)
            {
                ViewModel.AppSettings.ReconnectRetryIntervalMs = correctedValue.Value;
            }
        }
        else
        {
            HideValidationError(ReconnectRetryIntervalError);
        }
    }

    /// <summary>
    /// 最大重试次数验证 (0-100, 0=无限)
    /// </summary>
    private void ReconnectMaxRetriesTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not Wpf.Ui.Controls.TextBox textBox) return;
        
        var (isValid, errorMessage, correctedValue) = ValidateNumericInput(
            textBox.Text, 
            minValue: 0, 
            maxValue: 100, 
            fieldName: "最大重试次数");
        
        if (!isValid)
        {
            ShowValidationError(ReconnectMaxRetriesError, errorMessage);
            if (correctedValue.HasValue)
            {
                ViewModel.AppSettings.ReconnectMaxRetries = correctedValue.Value;
            }
        }
        else
        {
            HideValidationError(ReconnectMaxRetriesError);
        }
    }

    #endregion
}
