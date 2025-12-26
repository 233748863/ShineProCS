using System.Collections;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using ListBox = System.Windows.Controls.ListBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace ShineProCS.Utils;

/// <summary>
/// ListBox 拖拽排序行为 - 简化版
/// 不使用 WPF DragDrop，直接通过鼠标事件实现
/// </summary>
public static class DragDropBehavior
{
    #region 附加属性

    public static readonly DependencyProperty EnableDragDropProperty =
        DependencyProperty.RegisterAttached(
            "EnableDragDrop",
            typeof(bool),
            typeof(DragDropBehavior),
            new PropertyMetadata(false, OnEnableDragDropChanged));

    public static bool GetEnableDragDrop(DependencyObject obj) =>
        (bool)obj.GetValue(EnableDragDropProperty);

    public static void SetEnableDragDrop(DependencyObject obj, bool value) =>
        obj.SetValue(EnableDragDropProperty, value);

    #endregion

    // 拖拽状态
    private static Point _startPoint;
    private static bool _isDragging;
    private static object? _draggedItem;
    private static ListBox? _sourceListBox;
    private static ListBoxItem? _draggedContainer;
    private static int _draggedIndex = -1;
    
    // 视觉反馈
    private static Window? _dragWindow;
    private static Border? _insertMarker;
    private static AdornerLayer? _adornerLayer;
    private static InsertMarkerAdorner? _markerAdorner;

    private static void OnEnableDragDropChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox listBox) return;

        if ((bool)e.NewValue)
        {
            listBox.PreviewMouseLeftButtonDown += ListBox_PreviewMouseLeftButtonDown;
            listBox.PreviewMouseMove += ListBox_PreviewMouseMove;
            listBox.PreviewMouseLeftButtonUp += ListBox_PreviewMouseLeftButtonUp;
            listBox.MouseLeave += ListBox_MouseLeave;
        }
        else
        {
            listBox.PreviewMouseLeftButtonDown -= ListBox_PreviewMouseLeftButtonDown;
            listBox.PreviewMouseMove -= ListBox_PreviewMouseMove;
            listBox.PreviewMouseLeftButtonUp -= ListBox_PreviewMouseLeftButtonUp;
            listBox.MouseLeave -= ListBox_MouseLeave;
        }
    }

    private static void ListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox) return;

        _startPoint = e.GetPosition(listBox);
        _sourceListBox = listBox;

        // 获取点击的 ListBoxItem
        _draggedContainer = GetListBoxItemAtPoint(listBox, _startPoint);
        if (_draggedContainer != null)
        {
            _draggedItem = _draggedContainer.DataContext;
            _draggedIndex = listBox.ItemContainerGenerator.IndexFromContainer(_draggedContainer);
        }
        else
        {
            _draggedItem = null;
            _draggedIndex = -1;
        }
    }

    private static void ListBox_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedItem == null || _draggedContainer == null)
            return;

        if (sender is not ListBox listBox) return;

        var currentPoint = e.GetPosition(listBox);
        var diff = _startPoint - currentPoint;

        // 检查是否移动了足够的距离来开始拖拽
        if (!_isDragging && 
            (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
             Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
        {
            _isDragging = true;
            
            // 创建跟随鼠标的拖拽窗口
            CreateDragWindow(_draggedContainer);
            
            // 降低原项的透明度
            _draggedContainer.Opacity = 0.3;
            
            // 捕获鼠标
            listBox.CaptureMouse();
        }

        if (_isDragging)
        {
            // 更新拖拽窗口位置
            UpdateDragWindowPosition();
            
            // 更新插入标记
            UpdateInsertMarker(listBox, currentPoint);
        }
    }

    private static void ListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging || sender is not ListBox listBox)
        {
            CleanupDragDrop();
            return;
        }

        // 释放鼠标捕获
        listBox.ReleaseMouseCapture();

        // 计算目标位置
        var currentPoint = e.GetPosition(listBox);
        var targetIndex = CalculateInsertIndex(listBox, currentPoint);

        // 执行移动
        if (_draggedItem != null && _draggedIndex >= 0 && targetIndex >= 0)
        {
            var oldIndex = _draggedIndex;
            var newIndex = targetIndex;

            // 调整索引
            if (oldIndex < newIndex) newIndex--;
            if (newIndex < 0) newIndex = 0;

            var itemsSource = listBox.ItemsSource;
            if (itemsSource != null && oldIndex != newIndex)
            {
                // 尝试使用 Move 方法
                var moved = TryMoveItem(itemsSource, oldIndex, newIndex);
                
                if (moved)
                {
                    // 保持选中状态
                    listBox.SelectedItem = _draggedItem;
                    
                    // 触发事件
                    RaiseDragDropCompleted(listBox, oldIndex, newIndex);
                }
            }
        }

        CleanupDragDrop();
    }

    private static void ListBox_MouseLeave(object sender, MouseEventArgs e)
    {
        // 如果鼠标离开 ListBox 且正在拖拽，不要立即取消
        // 只有在鼠标释放时才处理
    }

    /// <summary>
    /// 尝试移动集合中的项
    /// </summary>
    private static bool TryMoveItem(object itemsSource, int oldIndex, int newIndex)
    {
        // 尝试通过反射调用 Move 方法（ObservableCollection 有此方法）
        var moveMethod = itemsSource.GetType().GetMethod("Move", [typeof(int), typeof(int)]);
        if (moveMethod != null)
        {
            try
            {
                moveMethod.Invoke(itemsSource, [oldIndex, newIndex]);
                return true;
            }
            catch { }
        }

        // 回退到 RemoveAt/Insert
        if (itemsSource is IList list)
        {
            try
            {
                var item = list[oldIndex];
                list.RemoveAt(oldIndex);
                list.Insert(newIndex, item);
                return true;
            }
            catch { }
        }

        return false;
    }

    /// <summary>
    /// 创建跟随鼠标的拖拽窗口
    /// </summary>
    private static void CreateDragWindow(ListBoxItem item)
    {
        // 创建项的视觉副本
        var visual = new VisualBrush(item)
        {
            Opacity = 0.9,
            Stretch = Stretch.None
        };

        var border = new Border
        {
            Width = item.ActualWidth,
            Height = item.ActualHeight,
            Background = visual,
            CornerRadius = new CornerRadius(4),
            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 15,
                ShadowDepth = 5,
                Opacity = 0.6
            }
        };

        // 创建无边框透明窗口
        _dragWindow = new Window
        {
            Content = border,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            Topmost = true,
            IsHitTestVisible = false,
            SizeToContent = SizeToContent.WidthAndHeight,
        };

        _dragWindow.Show();
        UpdateDragWindowPosition();
    }

    /// <summary>
    /// 更新拖拽窗口位置
    /// </summary>
    private static void UpdateDragWindowPosition()
    {
        if (_dragWindow == null) return;

        var screenPos = GetMouseScreenPosition();
        _dragWindow.Left = screenPos.X - _dragWindow.ActualWidth / 2;
        _dragWindow.Top = screenPos.Y - _dragWindow.ActualHeight / 2;
    }

    /// <summary>
    /// 获取鼠标屏幕坐标
    /// </summary>
    private static Point GetMouseScreenPosition()
    {
        GetCursorPos(out var point);
        return new Point(point.X, point.Y);
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT pt);

    /// <summary>
    /// 计算插入索引
    /// </summary>
    private static int CalculateInsertIndex(ListBox listBox, Point position)
    {
        for (int i = 0; i < listBox.Items.Count; i++)
        {
            if (listBox.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem item)
            {
                var itemPos = item.TranslatePoint(new Point(0, 0), listBox);
                var itemCenter = itemPos.Y + item.ActualHeight / 2;

                if (position.Y < itemCenter)
                {
                    return i;
                }
            }
        }
        return listBox.Items.Count;
    }

    /// <summary>
    /// 更新插入标记位置
    /// </summary>
    private static void UpdateInsertMarker(ListBox listBox, Point position)
    {
        EnsureInsertMarker(listBox);
        if (_insertMarker == null || _markerAdorner == null) return;

        var insertIndex = CalculateInsertIndex(listBox, position);
        
        // 计算标记 Y 位置
        double markerY = 0;
        
        if (insertIndex < listBox.Items.Count)
        {
            if (listBox.ItemContainerGenerator.ContainerFromIndex(insertIndex) is ListBoxItem item)
            {
                var itemPos = item.TranslatePoint(new Point(0, 0), listBox);
                markerY = itemPos.Y - 1.5;
            }
        }
        else if (listBox.Items.Count > 0)
        {
            // 插入到末尾
            if (listBox.ItemContainerGenerator.ContainerFromIndex(listBox.Items.Count - 1) is ListBoxItem lastItem)
            {
                var itemPos = lastItem.TranslatePoint(new Point(0, 0), listBox);
                markerY = itemPos.Y + lastItem.ActualHeight - 1.5;
            }
        }

        _markerAdorner.UpdatePosition(4, markerY, listBox.ActualWidth - 24);
        _insertMarker.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// 确保插入标记存在
    /// </summary>
    private static void EnsureInsertMarker(ListBox listBox)
    {
        if (_markerAdorner != null) return;

        _adornerLayer = AdornerLayer.GetAdornerLayer(listBox);
        if (_adornerLayer == null) return;

        _insertMarker = new Border
        {
            Height = 3,
            Background = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
            CornerRadius = new CornerRadius(1.5),
            IsHitTestVisible = false,
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 120, 215),
                BlurRadius = 8,
                ShadowDepth = 0,
                Opacity = 0.8
            }
        };

        _markerAdorner = new InsertMarkerAdorner(listBox, _insertMarker);
        _adornerLayer.Add(_markerAdorner);
    }

    /// <summary>
    /// 清理拖拽状态
    /// </summary>
    private static void CleanupDragDrop()
    {
        // 恢复原项透明度
        if (_draggedContainer != null)
        {
            _draggedContainer.Opacity = 1.0;
        }

        // 关闭拖拽窗口
        if (_dragWindow != null)
        {
            _dragWindow.Close();
            _dragWindow = null;
        }

        // 移除插入标记
        if (_markerAdorner != null && _adornerLayer != null)
        {
            _adornerLayer.Remove(_markerAdorner);
            _markerAdorner = null;
            _adornerLayer = null;
        }
        _insertMarker = null;

        // 释放鼠标捕获
        _sourceListBox?.ReleaseMouseCapture();

        _isDragging = false;
        _draggedItem = null;
        _draggedContainer = null;
        _draggedIndex = -1;
        _sourceListBox = null;
    }

    /// <summary>
    /// 获取指定位置的 ListBoxItem
    /// </summary>
    private static ListBoxItem? GetListBoxItemAtPoint(ListBox listBox, Point point)
    {
        var element = listBox.InputHitTest(point) as DependencyObject;
        while (element != null)
        {
            if (element is ListBoxItem item)
                return item;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    #region 拖拽完成事件

    public static readonly RoutedEvent DragDropCompletedEvent =
        EventManager.RegisterRoutedEvent(
            "DragDropCompleted",
            RoutingStrategy.Bubble,
            typeof(DragDropCompletedEventHandler),
            typeof(DragDropBehavior));

    public static void AddDragDropCompletedHandler(DependencyObject d, DragDropCompletedEventHandler handler)
    {
        if (d is UIElement element)
            element.AddHandler(DragDropCompletedEvent, handler);
    }

    public static void RemoveDragDropCompletedHandler(DependencyObject d, DragDropCompletedEventHandler handler)
    {
        if (d is UIElement element)
            element.RemoveHandler(DragDropCompletedEvent, handler);
    }

    private static void RaiseDragDropCompleted(ListBox listBox, int oldIndex, int newIndex)
    {
        var args = new DragDropCompletedEventArgs(oldIndex, newIndex)
        {
            RoutedEvent = DragDropCompletedEvent
        };
        listBox.RaiseEvent(args);
    }

    #endregion
}

/// <summary>
/// 插入标记装饰器
/// </summary>
public class InsertMarkerAdorner : Adorner
{
    private readonly Border _marker;
    private double _left, _top, _width;

    public InsertMarkerAdorner(UIElement adornedElement, Border marker) : base(adornedElement)
    {
        _marker = marker;
        AddVisualChild(_marker);
        IsHitTestVisible = false;
    }

    public void UpdatePosition(double left, double top, double width)
    {
        _left = left;
        _top = top;
        _width = width;
        InvalidateArrange();
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _marker;

    protected override Size MeasureOverride(Size constraint)
    {
        _marker.Measure(new Size(_width > 0 ? _width : constraint.Width, _marker.Height));
        return constraint;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _marker.Arrange(new Rect(_left, _top, _width > 0 ? _width : finalSize.Width - 8, 3));
        return finalSize;
    }
}

/// <summary>
/// 拖拽完成事件参数
/// </summary>
public class DragDropCompletedEventArgs : RoutedEventArgs
{
    public int OldIndex { get; }
    public int NewIndex { get; }

    public DragDropCompletedEventArgs(int oldIndex, int newIndex)
    {
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }
}

/// <summary>
/// 拖拽完成事件处理委托
/// </summary>
public delegate void DragDropCompletedEventHandler(object sender, DragDropCompletedEventArgs e);
