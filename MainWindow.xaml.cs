using System.Windows;
using ShineProCS.Utils;
using ShineProCS.ViewModels;
using Wpf.Ui.Controls;

namespace ShineProCS;

public partial class MainWindow : FluentWindow
{
    private System.Windows.Forms.NotifyIcon? _trayIcon;

    public MainWindow()
    {
        InitializeComponent();
        InitializeTrayIcon();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void InitializeTrayIcon()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "ShineProCS - 游戏自动化引擎",
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true
        };
        
        // 双击托盘图标显示窗口
        _trayIcon.DoubleClick += (s, e) => ShowWindow();
        
        // 创建右键菜单
        var menu = new System.Windows.Forms.ContextMenuStrip();
        
        var showItem = new System.Windows.Forms.ToolStripMenuItem("显示主窗口");
        showItem.Click += (s, e) => ShowWindow();
        menu.Items.Add(showItem);
        
        var startItem = new System.Windows.Forms.ToolStripMenuItem("启动/停止引擎");
        startItem.Click += (s, e) => Dispatcher.Invoke(() =>
        {
            var vm = DataContext as MainViewModel;
            if (vm?.IsRunning == true)
                vm.StopEngineCommand.Execute(null);
            else
                vm?.StartEngineCommand.Execute(null);
        });
        menu.Items.Add(startItem);
        
        var pauseItem = new System.Windows.Forms.ToolStripMenuItem("暂停/恢复");
        pauseItem.Click += (s, e) => Dispatcher.Invoke(() => (DataContext as MainViewModel)?.PauseEngineCommand.Execute(null));
        menu.Items.Add(pauseItem);
        
        var qiqingItem = new System.Windows.Forms.ToolStripMenuItem("七情模式");
        qiqingItem.Click += (s, e) => Dispatcher.Invoke(() => (DataContext as MainViewModel)?.ToggleQiQingModeCommand.Execute(null));
        menu.Items.Add(qiqingItem);
        
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        
        var exitItem = new System.Windows.Forms.ToolStripMenuItem("退出");
        exitItem.Click += (s, e) => 
        { 
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            // 正确释放资源
            (DataContext as MainViewModel)?.Dispose();
            System.Windows.Application.Current.Shutdown(); 
        };
        menu.Items.Add(exitItem);
        
        _trayIcon.ContextMenuStrip = menu;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 初始化全局快捷键（使用 ViewModel 中的服务）
        var vm = DataContext as MainViewModel;
        vm?.InitializeHotkeys(this);
    }

    /// <summary>
    /// 技能列表拖拽排序完成事件处理
    /// </summary>
    private void SkillListBox_DragDropCompleted(object sender, DragDropCompletedEventArgs e)
    {
        var vm = DataContext as MainViewModel;
        if (vm == null) return;

        // 同步更新技能状态列表的顺序
        if (e.OldIndex >= 0 && e.NewIndex >= 0 && 
            e.OldIndex < vm.SkillStatusList.Count && 
            e.NewIndex <= vm.SkillStatusList.Count)
        {
            vm.SkillStatusList.Move(e.OldIndex, e.NewIndex);
        }

        // 显示提示
        Views.ToastManager.Info($"技能顺序已调整", "拖拽排序");
    }

    private void ShowWindow()
    {
        Dispatcher.Invoke(() =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        });
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // 检查是否有未保存的变更
        var vm = DataContext as MainViewModel;
        if (vm?.HasUnsavedChanges == true)
        {
            if (!vm.PromptSaveChanges())
            {
                e.Cancel = true;
                return;
            }
        }
        
        // 最小化到托盘而不是关闭
        e.Cancel = true;
        Hide();
        _trayIcon?.ShowBalloonTip(2000, "ShineProCS", "程序已最小化到系统托盘，双击图标可恢复窗口", System.Windows.Forms.ToolTipIcon.Info);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        (DataContext as MainViewModel)?.Dispose();
        base.OnClosed(e);
    }
}
