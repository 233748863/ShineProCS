using System.Globalization;
using System.Windows.Data;

namespace ShineProCS.Utils;

/// <summary>
/// 按键码到按键名称的转换器
/// </summary>
public class KeyCodeToNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int keyCode)
            return KeyCodeHelper.GetKeyName(keyCode);
        return "无";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
