using System.Windows.Controls;
using ShineProCS.ViewModels;

namespace ShineProCS.Views.Pages;

/// <summary>
/// Buff库页面 - 包装现有的 BuffLibraryPage 控件
/// 需求: 5.1, 5.2, 5.3, 5.4
/// </summary>
public partial class BuffsPage : Page
{
    public MainViewModel ViewModel { get; }

    public BuffsPage(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;
        InitializeComponent();
    }
}
