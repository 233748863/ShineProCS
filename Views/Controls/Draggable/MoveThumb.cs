using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

using Point = System.Windows.Point;

namespace ShineProCS.Views.Controls;

/// <summary>
/// 移动拖拽控件
/// 移植自 BetterGI
/// </summary>
public class MoveThumb : Thumb
{
    private RotateTransform? _rotateTransform;
    private ContentControl? _designerItem;

    public MoveThumb()
    {
        DragStarted += OnMoveThumbDragStarted;
        DragDelta += OnMoveThumbDragDelta;
    }

    private void OnMoveThumbDragStarted(object sender, DragStartedEventArgs e)
    {
        _designerItem = DataContext as ContentControl;

        if (_designerItem != null)
        {
            _rotateTransform = _designerItem.RenderTransform as RotateTransform;
        }
    }

    private void OnMoveThumbDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_designerItem is not null)
        {
            Point dragDelta = new(e.HorizontalChange, e.VerticalChange);

            if (_rotateTransform is not null)
            {
                dragDelta = _rotateTransform.Transform(dragDelta);
            }

            Canvas.SetLeft(_designerItem, Canvas.GetLeft(_designerItem) + dragDelta.X);
            Canvas.SetTop(_designerItem, Canvas.GetTop(_designerItem) + dragDelta.Y);
        }
    }
}
