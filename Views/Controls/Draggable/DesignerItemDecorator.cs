using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using ShineProCS.Views.Controls.Adorners;

using Control = System.Windows.Controls.Control;

namespace ShineProCS.Views.Controls;

/// <summary>
/// 设计器项装饰器
/// 移植自 BetterGI
/// </summary>
public class DesignerItemDecorator : Control
{
    private Adorner? _adorner;

    public bool ShowDecorator
    {
        get { return (bool)GetValue(ShowDecoratorProperty); }
        set { SetValue(ShowDecoratorProperty, value); }
    }

    public static readonly DependencyProperty ShowDecoratorProperty =
        DependencyProperty.Register(nameof(ShowDecorator), typeof(bool), typeof(DesignerItemDecorator),
        new FrameworkPropertyMetadata(false, new PropertyChangedCallback(OnShowDecoratorChanged)));

    public DesignerItemDecorator()
    {
        Unloaded += OnDesignerItemDecoratorUnloaded;
    }

    private void HideAdorner()
    {
        if (_adorner is not null)
        {
            _adorner.Visibility = Visibility.Hidden;
        }
    }

    private void ShowAdorner()
    {
        if (_adorner is null)
        {
            AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(this);

            if (adornerLayer is not null)
            {
                ContentControl? designerItem = DataContext as ContentControl;
                _adorner = new ResizeRotateAdorner(designerItem);
                adornerLayer.Add(_adorner);

                _adorner.Visibility = ShowDecorator ? Visibility.Visible : Visibility.Hidden;
            }
        }
        else
        {
            _adorner.Visibility = Visibility.Visible;
        }
    }

    private void OnDesignerItemDecoratorUnloaded(object sender, RoutedEventArgs e)
    {
        if (_adorner is not null)
        {
            AdornerLayer.GetAdornerLayer(this)?.Remove(_adorner);
            _adorner = null;
        }
    }

    private static void OnShowDecoratorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        DesignerItemDecorator decorator = (DesignerItemDecorator)d;
        bool showDecorator = (bool)e.NewValue;

        if (showDecorator)
        {
            decorator.ShowAdorner();
        }
        else
        {
            decorator.HideAdorner();
        }
    }
}
