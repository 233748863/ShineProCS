using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

using Size = System.Windows.Size;

namespace ShineProCS.Views.Controls.Adorners;

/// <summary>
/// 调整大小和旋转装饰器
/// 移植自 BetterGI
/// </summary>
public class ResizeRotateAdorner : Adorner
{
    private readonly VisualCollection _visuals;
    private readonly ResizeRotateChrome _chrome;

    protected override int VisualChildrenCount => _visuals.Count;

    public ResizeRotateAdorner(ContentControl? designerItem)
        : base(designerItem)
    {
        SnapsToDevicePixels = true;
        _chrome = new ResizeRotateChrome
        {
            DataContext = designerItem
        };
        _visuals = new VisualCollection(this)
        {
            _chrome
        };
    }

    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        _chrome.Arrange(new Rect(arrangeBounds));
        return arrangeBounds;
    }

    protected override Visual GetVisualChild(int index)
    {
        return _visuals[index];
    }
}
