using System.Windows;
using System.Windows.Controls;

using Control = System.Windows.Controls.Control;

namespace ShineProCS.Views.Controls.Adorners;

/// <summary>
/// 调整大小和旋转装饰器外观
/// 移植自 BetterGI
/// </summary>
public class ResizeRotateChrome : Control
{
    static ResizeRotateChrome()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ResizeRotateChrome), new FrameworkPropertyMetadata(typeof(ResizeRotateChrome)));
    }
}
