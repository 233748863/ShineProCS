using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

using Size = System.Windows.Size;

namespace ShineProCS.Views.Controls.Adorners;

/// <summary>
/// 大小装饰器
/// 移植自 BetterGI
/// </summary>
public class SizeAdorner : Adorner
{
    private readonly SizeChrome _chrome;
    private readonly VisualCollection _visuals;
    private readonly ContentControl _designerItem;

    protected override int VisualChildrenCount => _visuals.Count;

    public SizeAdorner(ContentControl designerItem)
        : base(designerItem)
    {
        SnapsToDevicePixels = true;
        _designerItem = designerItem;
        _chrome = new SizeChrome
        {
            DataContext = designerItem
        };
        _visuals = new VisualCollection(this)
        {
            _chrome
        };
    }

    protected override Visual GetVisualChild(int index)
    {
        return _visuals[index];
    }

    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        _chrome.Arrange(new Rect(default, arrangeBounds));
        return arrangeBounds;
    }
}
