using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using ShineProCS.Core.Services;
using ShineProCS.Models;
using WinApp = System.Windows.Application;

namespace ShineProCS.Views;

/// <summary>
/// 技能卡片控件 - 简化版
/// 使用Tag路由统一处理按钮点击事件
/// </summary>
public partial class SkillCardControl : System.Windows.Controls.UserControl
{
    private bool _isExpanded;
    private bool _isAnimating;
    private ConfigManager? _configManager;
    
    // 事件：请求执行操作
    public event Action<SkillConfig, string>? OnActionRequested;
    public event Action? ConfigChanged;

    public SkillCardControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }
    
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 获取ConfigManager并加载Buff库
        if (WinApp.Current.MainWindow is MainWindow mainWindow)
        {
            _configManager = mainWindow.GetConfigManager();
            RefreshBuffComboBox();
        }
        
        // 确保施法类型UI正确显示
        UpdateCastTypeUI();
    }
    
    /// <summary>
    /// 刷新Buff下拉列表
    /// </summary>
    public void RefreshBuffComboBox()
    {
        if (_configManager == null) return;
        
        var currentValue = BuffComboBox.Text;
        BuffComboBox.Items.Clear();
        
        // 添加空选项
        BuffComboBox.Items.Add("");
        
        // 从Buff库加载
        foreach (var buff in _configManager.AppSettings.BuffLibrary.Where(b => b.Enabled))
        {
            BuffComboBox.Items.Add(buff.Name);
        }
        
        // 恢复当前值
        BuffComboBox.Text = currentValue;
    }

    private SkillConfig? Skill => DataContext as SkillConfig;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UpdateConfigStatus();
        UpdateTemplatePreview();
        UpdateCastTypeUI();
    }

    /// <summary>
    /// 更新配置状态指示器
    /// </summary>
    public void UpdateConfigStatus()
    {
        if (Skill == null) return;
        
        var hasRegion = Skill.IconRegion.Any(v => v != 0);
        var hasTemplate = !string.IsNullOrEmpty(Skill.TemplatePath) && File.Exists(Skill.TemplatePath);
        
        // 更新状态图标
        ConfigStatusIcon.Visibility = (hasRegion && hasTemplate) ? Visibility.Visible : Visibility.Collapsed;
        ConfigWarningIcon.Visibility = (hasRegion && hasTemplate) ? Visibility.Collapsed : Visibility.Visible;
        
        // 更新模板预览区域可见性
        TemplatePreviewBorder.Visibility = (hasRegion || hasTemplate) ? Visibility.Visible : Visibility.Collapsed;
        
        // 更新区域信息
        if (hasRegion)
        {
            var r = Skill.IconRegion;
            RegionInfoText.Text = $"区域: ({r[0]}, {r[1]}, {r[2]}×{r[3]})";
        }
        else
        {
            RegionInfoText.Text = "区域: 未配置";
        }
        
        // 更新模板信息
        if (hasTemplate)
        {
            TemplateInfoText.Text = $"模板: {Path.GetFileName(Skill.TemplatePath)}";
        }
        else
        {
            TemplateInfoText.Text = "模板: 未配置";
        }
    }

    /// <summary>
    /// 更新模板预览图
    /// </summary>
    public void UpdateTemplatePreview()
    {
        if (Skill == null) return;
        
        try
        {
            if (!string.IsNullOrEmpty(Skill.TemplatePath) && File.Exists(Skill.TemplatePath))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(Skill.TemplatePath, UriKind.Absolute);
                bitmap.EndInit();
                TemplatePreviewImage.Source = bitmap;
            }
            else
            {
                TemplatePreviewImage.Source = null;
            }
        }
        catch
        {
            TemplatePreviewImage.Source = null;
        }
    }

    /// <summary>
    /// 展开/折叠卡片（带动画）
    /// </summary>
    public void SetExpanded(bool expanded)
    {
        if (_isExpanded == expanded || _isAnimating) return;
        
        _isExpanded = expanded;
        _isAnimating = true;
        
        var storyboard = (Storyboard)FindResource(expanded ? "ExpandStoryboard" : "CollapseStoryboard");
        storyboard = storyboard.Clone(); // 克隆以避免冻结问题
        
        storyboard.Completed += (s, e) => _isAnimating = false;
        storyboard.Begin(this);
    }

    public bool IsExpanded => _isExpanded;

    private void Header_Click(object sender, MouseButtonEventArgs e)
    {
        SetExpanded(!_isExpanded);
    }

    private void EnabledCheckBox_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true; // 阻止事件冒泡到Header
        ConfigChanged?.Invoke();
    }

    /// <summary>
    /// 统一的按钮点击处理 - 通过Tag路由到不同操作
    /// </summary>
    private void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (Skill == null) return;
        if (sender is not FrameworkElement fe) return;
        
        var action = fe.Tag?.ToString();
        if (string.IsNullOrEmpty(action)) return;
        
        OnActionRequested?.Invoke(Skill, action);
    }

    /// <summary>
    /// 配置变更处理
    /// </summary>
    private void OnConfigChanged(object sender, TextChangedEventArgs e)
    {
        ConfigChanged?.Invoke();
    }
    
    /// <summary>
    /// Buff下拉框选择变更
    /// </summary>
    private void BuffComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Skill == null) return;
        
        var selectedBuff = BuffComboBox.SelectedItem?.ToString() ?? BuffComboBox.Text;
        if (Skill.PreCastConditionBuff != selectedBuff)
        {
            Skill.PreCastConditionBuff = selectedBuff;
            ConfigChanged?.Invoke();
        }
    }
    
    /// <summary>
    /// 施法类型变更处理
    /// </summary>
    private void CastTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Skill == null) return;
        
        // 更新模型
        var newCastType = (SkillCastType)CastTypeComboBox.SelectedIndex;
        if (Skill.CastType != newCastType)
        {
            Skill.CastType = newCastType;
            ConfigChanged?.Invoke();
        }
        
        UpdateCastTypeUI();
    }
    
    /// <summary>
    /// 更新施法类型相关UI显示
    /// </summary>
    private void UpdateCastTypeUI()
    {
        if (Skill == null) return;
        
        // 同步 ComboBox 选中项
        if (CastTypeComboBox.SelectedIndex != (int)Skill.CastType)
        {
            CastTypeComboBox.SelectedIndex = (int)Skill.CastType;
        }
        
        var castType = Skill.CastType;
        
        // 瞬发：隐藏所有额外配置
        // 正读条：显示读条时间
        // 引导：显示引导时间和打断时间
        
        switch (castType)
        {
            case SkillCastType.Instant:
                CastDurationPanel.Visibility = Visibility.Collapsed;
                ChannelInterruptPanel.Visibility = Visibility.Collapsed;
                break;
                
            case SkillCastType.CastTime:
                CastDurationPanel.Visibility = Visibility.Visible;
                CastDurationLabel.Text = "读条时间(毫秒)";
                ChannelInterruptPanel.Visibility = Visibility.Collapsed;
                break;
                
            case SkillCastType.Channeled:
                CastDurationPanel.Visibility = Visibility.Visible;
                CastDurationLabel.Text = "引导时间(毫秒)";
                ChannelInterruptPanel.Visibility = Visibility.Visible;
                UpdateInterruptModeUI();
                break;
        }
    }
    
    /// <summary>
    /// 打断模式变更处理
    /// </summary>
    private void InterruptModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Skill == null) return;
        
        var newMode = InterruptModeComboBox.SelectedIndex;
        if (Skill.ChannelInterruptMode != newMode)
        {
            Skill.ChannelInterruptMode = newMode;
            ConfigChanged?.Invoke();
        }
        
        UpdateInterruptModeUI();
    }
    
    /// <summary>
    /// 更新打断模式相关UI
    /// </summary>
    private void UpdateInterruptModeUI()
    {
        if (Skill == null) return;
        
        // 同步 ComboBox
        if (InterruptModeComboBox.SelectedIndex != Skill.ChannelInterruptMode)
        {
            InterruptModeComboBox.SelectedIndex = Skill.ChannelInterruptMode;
        }
        
        // 根据模式显示不同配置面板
        if (Skill.ChannelInterruptMode == 0)
        {
            // 固定时间模式
            FixedTimeInterruptPanel.Visibility = Visibility.Visible;
            ColorDetectInterruptPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            // 点色检测模式
            FixedTimeInterruptPanel.Visibility = Visibility.Collapsed;
            ColorDetectInterruptPanel.Visibility = Visibility.Visible;
            UpdateColorPreview();
        }
    }
    
    /// <summary>
    /// 更新颜色预览
    /// </summary>
    private void UpdateColorPreview()
    {
        if (Skill == null) return;
        
        try
        {
            var color = Skill.ChannelInterruptColor;
            if (color.Length >= 3)
            {
                var r = (byte)Math.Clamp(color[0], 0, 255);
                var g = (byte)Math.Clamp(color[1], 0, 255);
                var b = (byte)Math.Clamp(color[2], 0, 255);
                ColorPreviewBorder.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(r, g, b));
            }
        }
        catch { }
    }
}
