using System.Windows;
using System.Windows.Controls;

using Control = System.Windows.Controls.Control;

namespace ShineProCS.Views.Controls.Adorners;

/// <summary>
/// 大小装饰器外观
/// 移植自 BetterGI
/// </summary>
public class SizeChrome : Control
{
    static SizeChrome()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SizeChrome), new FrameworkPropertyMetadata(typeof(SizeChrome)));
    }
}
