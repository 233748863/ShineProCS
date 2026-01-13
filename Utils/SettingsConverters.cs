using System.Globalization;
using System.Windows.Data;

namespace ShineProCS.Utils;

/// <summary>
/// 区域数组 [X, Y, Width, Height] 到字符串的转换器
/// </summary>
public class RegionArrayToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int[] region && region.Length >= 4)
        {
            if (region.All(v => v == 0))
                return "未设置";
            return $"X={region[0]}, Y={region[1]}, W={region[2]}, H={region[3]}";
        }
        return "未设置";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 点数组 [X, Y] 到字符串的转换器
/// </summary>
public class PointArrayToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int[] point && point.Length >= 2)
        {
            if (point.All(v => v == 0))
                return "未设置";
            return $"X={point[0]}, Y={point[1]}";
        }
        return "未设置";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Color 到 SolidColorBrush 的转换器
/// </summary>
public class ColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is System.Windows.Media.Color color)
            return new System.Windows.Media.SolidColorBrush(color);
        return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 数字范围验证转换器
/// 用于验证输入是否在指定范围内
/// </summary>
public class NumericRangeValidationConverter : IValueConverter
{
    public int MinValue { get; set; } = int.MinValue;
    public int MaxValue { get; set; } = int.MaxValue;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str && int.TryParse(str, out int result))
        {
            return Math.Clamp(result, MinValue, MaxValue);
        }
        return MinValue;
    }
}
