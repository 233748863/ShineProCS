using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ShineProCS.ViewModels;

namespace ShineProCS.Views.Pages;

/// <summary>
/// 设置页 - 全局配置
/// 需求: 6.1, 6.2, 6.3, 6.4, 6.5
/// </summary>
public partial class SettingsPage : Page
{
    public MainViewModel ViewModel { get; }

    public SettingsPage(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;
        InitializeComponent();
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

    #endregion
}
