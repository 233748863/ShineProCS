using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ShineProCS.Utils;

/// <summary>
/// ScrollViewer 辅助类 - 提供滚轮事件穿透功能
/// 
/// 问题背景：
/// WPF UI 库（如 Wpf.Ui）的 NavigationView 内部布局可能导致 ScrollViewer 的 ViewportHeight 不正确，
/// 从而使 ScrollableHeight 为 0，导致滚轮无法滚动。
/// 
/// 解决方案：
/// 1. 主要方案：使用 Grid 包裹 ScrollViewer，确保正确的布局约束
/// 2. 备用方案：使用此附加属性在 PreviewMouseWheel 阶段手动处理滚动
/// 
/// 使用方法：
/// <![CDATA[
/// <!-- 方案1：使用 Grid 包裹（推荐） -->
/// <Grid>
///     <ScrollViewer>
///         <StackPanel>
///             <!-- 内容 -->
///         </StackPanel>
///     </ScrollViewer>
/// </Grid>
/// 
/// <!-- 方案2：使用附加属性（备用） -->
/// <ScrollViewer utils:ScrollViewerHelper.EnableScrolling="True">
///     <StackPanel>
///         <!-- 内容 -->
///     </StackPanel>
/// </ScrollViewer>
/// ]]>
/// </summary>
public static class ScrollViewerHelper
{
    #region EnableScrolling 附加属性

    /// <summary>
    /// 附加属性：启用滚轮穿透
    /// 当设置为 true 时，ScrollViewer 将在 PreviewMouseWheel 阶段处理滚轮事件，
    /// 确保即使子控件拦截了事件，页面仍然可以滚动。
    /// </summary>
    public static readonly DependencyProperty EnableScrollingProperty =
        DependencyProperty.RegisterAttached(
            "EnableScrolling",
            typeof(bool),
            typeof(ScrollViewerHelper),
            new PropertyMetadata(false, OnEnableScrollingChanged));

    /// <summary>
    /// 获取是否启用滚轮穿透
    /// </summary>
    public static bool GetEnableScrolling(DependencyObject obj)
        => (bool)obj.GetValue(EnableScrollingProperty);

    /// <summary>
    /// 设置是否启用滚轮穿透
    /// </summary>
    public static void SetEnableScrolling(DependencyObject obj, bool value)
        => obj.SetValue(EnableScrollingProperty, value);

    /// <summary>
    /// 当 EnableScrolling 属性变化时的处理
    /// </summary>
    private static void OnEnableScrollingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer scrollViewer)
        {
            if ((bool)e.NewValue)
            {
                // 注册 PreviewMouseWheel 事件（隧道阶段，最先触发）
                scrollViewer.PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
            }
            else
            {
                scrollViewer.PreviewMouseWheel -= ScrollViewer_PreviewMouseWheel;
            }
        }
    }

    /// <summary>
    /// 处理 ScrollViewer 的 PreviewMouseWheel 事件
    /// 在隧道阶段拦截滚轮事件，手动执行滚动
    /// </summary>
    private static void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer) return;

        // 检查事件源是否在 ComboBox 的下拉列表中
        // 如果是，则不处理，让 ComboBox 自己处理滚动
        if (IsInComboBoxDropDown(e.OriginalSource as DependencyObject))
        {
            return;
        }

        // 检查事件源是否在 ListBox 中且 ListBox 有自己的滚动条
        // 如果是，则不处理，让 ListBox 自己处理滚动
        if (IsInScrollableListBox(e.OriginalSource as DependencyObject))
        {
            return;
        }

        // 计算滚动量
        // Delta > 0 表示向上滚动，Delta < 0 表示向下滚动
        // 除以 3.0 使滚动更平滑
        double scrollAmount = e.Delta / 3.0;
        
        // 计算新的偏移量
        double newOffset = scrollViewer.VerticalOffset - scrollAmount;
        
        // 确保偏移量在有效范围内
        newOffset = Math.Max(0, Math.Min(newOffset, scrollViewer.ScrollableHeight));
        
        // 执行滚动
        scrollViewer.ScrollToVerticalOffset(newOffset);
        
        // 标记事件已处理，防止子控件再次处理
        e.Handled = true;
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 检查元素是否在 ComboBox 的下拉列表（Popup）中
    /// </summary>
    /// <param name="element">要检查的元素</param>
    /// <returns>如果在下拉列表中返回 true</returns>
    private static bool IsInComboBoxDropDown(DependencyObject? element)
    {
        while (element != null)
        {
            // Popup 是 ComboBox 下拉列表的容器
            if (element is System.Windows.Controls.Primitives.Popup)
            {
                return true;
            }
            
            // 尝试获取逻辑父级或视觉父级
            var parent = LogicalTreeHelper.GetParent(element) 
                         ?? VisualTreeHelper.GetParent(element);
            element = parent;
        }
        return false;
    }

    /// <summary>
    /// 检查元素是否在可滚动的 ListBox 中
    /// </summary>
    /// <param name="element">要检查的元素</param>
    /// <returns>如果在可滚动的 ListBox 中返回 true</returns>
    private static bool IsInScrollableListBox(DependencyObject? element)
    {
        while (element != null)
        {
            if (element is System.Windows.Controls.ListBox listBox)
            {
                // 检查 ListBox 是否有自己的 ScrollViewer
                var scrollViewer = FindVisualChild<ScrollViewer>(listBox);
                if (scrollViewer != null && scrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible)
                {
                    return true;
                }
            }
            
            element = VisualTreeHelper.GetParent(element);
        }
        return false;
    }

    /// <summary>
    /// 在视觉树中查找指定类型的子元素
    /// </summary>
    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
            {
                return result;
            }
            
            var descendant = FindVisualChild<T>(child);
            if (descendant != null)
            {
                return descendant;
            }
        }
        return null;
    }

    #endregion
}
