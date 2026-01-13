using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using ShineProCS.Views.Controls.Adorners;

using Point = System.Windows.Point;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ShineProCS.Views.Controls;

/// <summary>
/// 调整大小拖拽控件
/// 移植自 BetterGI
/// </summary>
public class ResizeThumb : Thumb
{
    private RotateTransform? _rotateTransform;
    private double _angle;
    private Adorner? _adorner;
    private Point _transformOrigin;
    private ContentControl? _designerItem;
    private Canvas? _canvas;

    public ResizeThumb()
    {
        DragStarted += OnResizeThumbDragStarted;
        DragDelta += OnResizeThumbDragDelta;
        DragCompleted += OnResizeThumbDragCompleted;
    }

    private void OnResizeThumbDragStarted(object sender, DragStartedEventArgs e)
    {
        _designerItem = DataContext as ContentControl;

        if (_designerItem is not null)
        {
            _canvas = VisualTreeHelper.GetParent(_designerItem) as Canvas;

            if (_canvas is not null)
            {
                _transformOrigin = _designerItem.RenderTransformOrigin;

                _rotateTransform = _designerItem.RenderTransform as RotateTransform;
                if (_rotateTransform is not null)
                {
                    _angle = _rotateTransform.Angle * Math.PI / 180.0;
                }
                else
                {
                    _angle = 0.0d;
                }

                AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(_canvas);
                if (adornerLayer is not null)
                {
                    _adorner = new SizeAdorner(_designerItem);
                    adornerLayer.Add(_adorner);
                }
            }
        }
    }


    private void OnResizeThumbDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_designerItem is not null)
        {
            double deltaVertical, deltaHorizontal;

            switch (VerticalAlignment)
            {
                case VerticalAlignment.Bottom:
                    deltaVertical = Math.Min(-e.VerticalChange, _designerItem.ActualHeight - _designerItem.MinHeight);
                    Canvas.SetTop(_designerItem, Canvas.GetTop(_designerItem) + (_transformOrigin.Y * deltaVertical * (1 - Math.Cos(-_angle))));
                    Canvas.SetLeft(_designerItem, Canvas.GetLeft(_designerItem) - deltaVertical * _transformOrigin.Y * Math.Sin(-_angle));
                    _designerItem.Height -= deltaVertical;
                    break;
                case VerticalAlignment.Top:
                    deltaVertical = Math.Min(e.VerticalChange, _designerItem.ActualHeight - _designerItem.MinHeight);
                    Canvas.SetTop(_designerItem, Canvas.GetTop(_designerItem) + deltaVertical * Math.Cos(-_angle) + (_transformOrigin.Y * deltaVertical * (1 - Math.Cos(-_angle))));
                    Canvas.SetLeft(_designerItem, Canvas.GetLeft(_designerItem) + deltaVertical * Math.Sin(-_angle) - (_transformOrigin.Y * deltaVertical * Math.Sin(-_angle)));
                    _designerItem.Height -= deltaVertical;
                    break;
                default:
                    break;
            }

            switch (HorizontalAlignment)
            {
                case HorizontalAlignment.Left:
                    deltaHorizontal = Math.Min(e.HorizontalChange, _designerItem.ActualWidth - _designerItem.MinWidth);
                    Canvas.SetTop(_designerItem, Canvas.GetTop(_designerItem) + deltaHorizontal * Math.Sin(_angle) - _transformOrigin.X * deltaHorizontal * Math.Sin(_angle));
                    Canvas.SetLeft(_designerItem, Canvas.GetLeft(_designerItem) + deltaHorizontal * Math.Cos(_angle) + (_transformOrigin.X * deltaHorizontal * (1 - Math.Cos(_angle))));
                    _designerItem.Width -= deltaHorizontal;
                    break;
                case HorizontalAlignment.Right:
                    deltaHorizontal = Math.Min(-e.HorizontalChange, _designerItem.ActualWidth - _designerItem.MinWidth);
                    Canvas.SetTop(_designerItem, Canvas.GetTop(_designerItem) - _transformOrigin.X * deltaHorizontal * Math.Sin(_angle));
                    Canvas.SetLeft(_designerItem, Canvas.GetLeft(_designerItem) + (deltaHorizontal * _transformOrigin.X * (1 - Math.Cos(_angle))));
                    _designerItem.Width -= deltaHorizontal;
                    break;
                default:
                    break;
            }
        }

        e.Handled = true;
    }

    private void OnResizeThumbDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (_adorner is not null)
        {
            AdornerLayer.GetAdornerLayer(_canvas)?.Remove(_adorner);
            _adorner = null;
        }
    }
}
