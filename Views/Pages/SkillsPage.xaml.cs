using System.Windows.Controls;
using ShineProCS.ViewModels;

namespace ShineProCS.Views.Pages;

/// <summary>
/// 技能配置页 - 包装现有的 SkillConfigPage 控件
/// 需求: 4.1, 4.2, 4.3, 4.4, 4.5
/// </summary>
public partial class SkillsPage : Page
{
    public MainViewModel ViewModel { get; }

    public SkillsPage(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;
        InitializeComponent();
        
        // 初始化技能配置页的图像接口
        SkillConfigPageControl.SetImageInterface(ViewModel.ImageInterface);
    }
}
