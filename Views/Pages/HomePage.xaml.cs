using System.Windows.Controls;
using ShineProCS.ViewModels;

namespace ShineProCS.Views.Pages;

/// <summary>
/// 启动页 - 显示引擎状态和控制按钮
/// 需求: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6
/// </summary>
public partial class HomePage : Page
{
    public MainViewModel ViewModel { get; }

    public HomePage(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;
        InitializeComponent();
    }
}
