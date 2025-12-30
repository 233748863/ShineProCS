using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ShineProCS.Utils;

/// <summary>
/// 日志消息模型
/// </summary>
public class LogMessage
{
    public string Text { get; set; } = "";
    public int Level { get; set; } // 0=调试, 1=信息, 2=警告, 3=错误
    public DateTime Time { get; set; } = DateTime.Now;
    
    public string FormattedText => $"[{Time:HH:mm:ss}] {Text}";
}

/// <summary>
/// 日志级别到颜色的转换器
/// </summary>
public class LogLevelColorConverter : IValueConverter
{
    private static readonly SolidColorBrush DebugBrush = new(System.Windows.Media.Color.FromRgb(128, 128, 128));
    private static readonly SolidColorBrush InfoBrush = new(System.Windows.Media.Color.FromRgb(200, 200, 200));
    private static readonly SolidColorBrush WarnBrush = new(System.Windows.Media.Color.FromRgb(255, 193, 7));
    private static readonly SolidColorBrush ErrorBrush = new(System.Windows.Media.Color.FromRgb(244, 67, 54));
    private static readonly SolidColorBrush SuccessBrush = new(System.Windows.Media.Color.FromRgb(76, 175, 80));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int level)
        {
            return level switch
            {
                0 => DebugBrush,
                1 => InfoBrush,
                2 => WarnBrush,
                3 => ErrorBrush,
                _ => InfoBrush
            };
        }
        
        // 如果是字符串，根据内容判断
        if (value is string text)
        {
            if (text.Contains("错误") || text.Contains("失败") || text.Contains("异常"))
                return ErrorBrush;
            if (text.Contains("警告") || text.Contains("CD中"))
                return WarnBrush;
            if (text.Contains("释放:") || text.Contains("成功") || text.Contains("已启动"))
                return SuccessBrush;
            if (text.Contains("检测") || text.Contains("等待"))
                return DebugBrush;
        }
        
        return InfoBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 日志文本到颜色的转换器（根据文本内容判断）
/// </summary>
public class LogTextColorConverter : IValueConverter
{
    private static readonly SolidColorBrush DebugBrush = new(System.Windows.Media.Color.FromRgb(128, 128, 128));
    private static readonly SolidColorBrush InfoBrush = new(System.Windows.Media.Color.FromRgb(180, 180, 180));
    private static readonly SolidColorBrush WarnBrush = new(System.Windows.Media.Color.FromRgb(255, 193, 7));
    private static readonly SolidColorBrush ErrorBrush = new(System.Windows.Media.Color.FromRgb(244, 67, 54));
    private static readonly SolidColorBrush SuccessBrush = new(System.Windows.Media.Color.FromRgb(76, 175, 80));
    private static readonly SolidColorBrush HighlightBrush = new(System.Windows.Media.Color.FromRgb(33, 150, 243));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string text) return InfoBrush;
        
        // 错误类
        if (text.Contains("错误") || text.Contains("失败") || text.Contains("异常") || text.Contains("Error"))
            return ErrorBrush;
        
        // 警告类
        if (text.Contains("警告") || text.Contains("CD中") || text.Contains("冷却") || text.Contains("缺少"))
            return WarnBrush;
        
        // 成功/释放类
        if (text.Contains("释放:") || text.Contains("成功") || text.Contains("已启动") || text.Contains("已保存"))
            return SuccessBrush;
        
        // 联动/高亮类
        if (text.Contains("联动") || text.Contains("Buff"))
            return HighlightBrush;
        
        // 调试类
        if (text.Contains("检测") || text.Contains("等待") || text.Contains("跳过"))
            return DebugBrush;
        
        return InfoBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


/// <summary>
/// 枚举到整数的双向转换器
/// </summary>
public class EnumToIntConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Enum enumValue)
            return System.Convert.ToInt32(enumValue);
        if (value is int intVal)
            return intVal;
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            // 如果目标类型是枚举，转换为枚举
            if (targetType.IsEnum)
                return Enum.ToObject(targetType, intValue);
            return intValue;
        }
        return 0;
    }
}
