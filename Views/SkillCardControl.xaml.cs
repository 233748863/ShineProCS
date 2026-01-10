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
            RefreshConditionBuffComboBox();
            RefreshExcludeConditionBuffComboBox();
            RefreshPriorityOverrideConditionComboBox();
            RefreshSkillGroupComboBox();
            RefreshPreCastSkillNameComboBox();
            ValidateReferences(); // 加载后验证引用
        }
        
        // 确保施法类型UI正确显示
        UpdateCastTypeUI();
    }
    
    #region 引用验证方法
    
    /// <summary>
    /// 检查技能组引用是否有效
    /// </summary>
    private bool IsValidSkillGroup(string groupName)
    {
        if (string.IsNullOrEmpty(groupName)) return true;
        if (_configManager == null) return false;
        return _configManager.AppSettings.SkillGroups
            .Any(g => g.Enabled && g.Name == groupName);
    }
    
    /// <summary>
    /// 检查Buff引用是否有效
    /// </summary>
    private bool IsValidBuff(string buffName)
    {
        if (string.IsNullOrEmpty(buffName)) return true;
        if (_configManager == null) return false;
        return _configManager.AppSettings.BuffLibrary
            .Any(b => b.Enabled && b.Name == buffName);
    }
    
    /// <summary>
    /// 检查前置技能引用是否有效
    /// </summary>
    private bool IsValidPreCastSkill(string skillName)
    {
        if (string.IsNullOrEmpty(skillName)) return true;
        if (_configManager == null) return false;
        return _configManager.Skills
            .Any(s => s.Enabled && s.Name == skillName && s != Skill);
    }
    
    /// <summary>
    /// 验证当前技能的所有引用，更新警告图标
    /// </summary>
    private void ValidateReferences()
    {
        if (Skill == null) return;
        
        // 验证技能组
        var skillGroupValid = IsValidSkillGroup(Skill.SkillGroup);
        SkillGroupWarning.Visibility = skillGroupValid ? Visibility.Collapsed : Visibility.Visible;
        if (!skillGroupValid)
            SkillGroupWarning.ToolTip = $"技能组 \"{Skill.SkillGroup}\" 不存在或已禁用";
        
        // 验证条件Buff
        var conditionBuffValid = IsValidBuff(Skill.ConditionBuff);
        ConditionBuffWarning.Visibility = conditionBuffValid ? Visibility.Collapsed : Visibility.Visible;
        if (!conditionBuffValid)
            ConditionBuffWarning.ToolTip = $"Buff \"{Skill.ConditionBuff}\" 不存在或已禁用";
        
        // 验证排除条件Buff
        var excludeConditionBuffValid = IsValidBuff(Skill.ExcludeConditionBuff);
        ExcludeConditionBuffWarning.Visibility = excludeConditionBuffValid ? Visibility.Collapsed : Visibility.Visible;
        if (!excludeConditionBuffValid)
            ExcludeConditionBuffWarning.ToolTip = $"Buff \"{Skill.ExcludeConditionBuff}\" 不存在或已禁用";
        
        // 验证优先级覆盖条件Buff
        var priorityOverrideValid = IsValidBuff(Skill.PriorityOverrideCondition);
        PriorityOverrideConditionWarning.Visibility = priorityOverrideValid ? Visibility.Collapsed : Visibility.Visible;
        if (!priorityOverrideValid)
            PriorityOverrideConditionWarning.ToolTip = $"Buff \"{Skill.PriorityOverrideCondition}\" 不存在或已禁用";
        
        // 验证前置技能
        var preCastSkillValid = IsValidPreCastSkill(Skill.PreCastSkillName);
        PreCastSkillNameWarning.Visibility = preCastSkillValid ? Visibility.Collapsed : Visibility.Visible;
        if (!preCastSkillValid)
            PreCastSkillNameWarning.ToolTip = $"技能 \"{Skill.PreCastSkillName}\" 不存在或已禁用";
        
        // 验证触发条件Buff
        var preCastConditionBuffValid = IsValidBuff(Skill.PreCastConditionBuff);
        BuffWarning.Visibility = preCastConditionBuffValid ? Visibility.Collapsed : Visibility.Visible;
        if (!preCastConditionBuffValid)
            BuffWarning.ToolTip = $"Buff \"{Skill.PreCastConditionBuff}\" 不存在或已禁用";
    }
    
    #endregion
    
    /// <summary>
    /// 刷新Buff下拉列表
    /// </summary>
    public void RefreshBuffComboBox()
    {
        if (_configManager == null || Skill == null) return;
        
        var currentValue = Skill.PreCastConditionBuff;
        BuffComboBox.Items.Clear();
        
        // 添加空选项
        BuffComboBox.Items.Add("");
        
        // 从Buff库加载
        foreach (var buff in _configManager.AppSettings.BuffLibrary.Where(b => b.Enabled))
        {
            BuffComboBox.Items.Add(buff.Name);
        }
        
        // 如果当前值无效但非空，添加到列表中（带警告标记）
        if (!string.IsNullOrEmpty(currentValue) && !IsValidBuff(currentValue))
        {
            BuffComboBox.Items.Add($"⚠ {currentValue}");
        }
        
        // 设置选中项
        if (string.IsNullOrEmpty(currentValue))
        {
            BuffComboBox.SelectedIndex = 0;
        }
        else if (IsValidBuff(currentValue))
        {
            BuffComboBox.SelectedItem = currentValue;
        }
        else
        {
            BuffComboBox.SelectedItem = $"⚠ {currentValue}";
        }
    }
    
    /// <summary>
    /// 刷新条件Buff下拉列表
    /// </summary>
    public void RefreshConditionBuffComboBox()
    {
        if (_configManager == null || Skill == null) return;
        
        var currentValue = Skill.ConditionBuff;
        ConditionBuffComboBox.Items.Clear();
        
        // 添加空选项
        ConditionBuffComboBox.Items.Add("");
        
        // 从Buff库加载
        foreach (var buff in _configManager.AppSettings.BuffLibrary.Where(b => b.Enabled))
        {
            ConditionBuffComboBox.Items.Add(buff.Name);
        }
        
        // 如果当前值无效但非空，添加到列表中
        if (!string.IsNullOrEmpty(currentValue) && !IsValidBuff(currentValue))
        {
            ConditionBuffComboBox.Items.Add($"⚠ {currentValue}");
        }
        
        // 设置选中项
        if (string.IsNullOrEmpty(currentValue))
        {
            ConditionBuffComboBox.SelectedIndex = 0;
        }
        else if (IsValidBuff(currentValue))
        {
            ConditionBuffComboBox.SelectedItem = currentValue;
        }
        else
        {
            ConditionBuffComboBox.SelectedItem = $"⚠ {currentValue}";
        }
    }
    
    /// <summary>
    /// 刷新排除条件Buff下拉列表
    /// </summary>
    public void RefreshExcludeConditionBuffComboBox()
    {
        if (_configManager == null || Skill == null) return;
        
        var currentValue = Skill.ExcludeConditionBuff;
        ExcludeConditionBuffComboBox.Items.Clear();
        
        // 添加空选项
        ExcludeConditionBuffComboBox.Items.Add("");
        
        // 从Buff库加载
        foreach (var buff in _configManager.AppSettings.BuffLibrary.Where(b => b.Enabled))
        {
            ExcludeConditionBuffComboBox.Items.Add(buff.Name);
        }
        
        // 如果当前值无效但非空，添加到列表中
        if (!string.IsNullOrEmpty(currentValue) && !IsValidBuff(currentValue))
        {
            ExcludeConditionBuffComboBox.Items.Add($"⚠ {currentValue}");
        }
        
        // 设置选中项
        if (string.IsNullOrEmpty(currentValue))
        {
            ExcludeConditionBuffComboBox.SelectedIndex = 0;
        }
        else if (IsValidBuff(currentValue))
        {
            ExcludeConditionBuffComboBox.SelectedItem = currentValue;
        }
        else
        {
            ExcludeConditionBuffComboBox.SelectedItem = $"⚠ {currentValue}";
        }
    }
    
    /// <summary>
    /// 刷新优先级覆盖条件Buff下拉列表
    /// </summary>
    public void RefreshPriorityOverrideConditionComboBox()
    {
        if (_configManager == null || Skill == null) return;
        
        var currentValue = Skill.PriorityOverrideCondition;
        PriorityOverrideConditionComboBox.Items.Clear();
        
        // 添加空选项
        PriorityOverrideConditionComboBox.Items.Add("");
        
        // 从Buff库加载
        foreach (var buff in _configManager.AppSettings.BuffLibrary.Where(b => b.Enabled))
        {
            PriorityOverrideConditionComboBox.Items.Add(buff.Name);
        }
        
        // 如果当前值无效但非空，添加到列表中
        if (!string.IsNullOrEmpty(currentValue) && !IsValidBuff(currentValue))
        {
            PriorityOverrideConditionComboBox.Items.Add($"⚠ {currentValue}");
        }
        
        // 设置选中项
        if (string.IsNullOrEmpty(currentValue))
        {
            PriorityOverrideConditionComboBox.SelectedIndex = 0;
        }
        else if (IsValidBuff(currentValue))
        {
            PriorityOverrideConditionComboBox.SelectedItem = currentValue;
        }
        else
        {
            PriorityOverrideConditionComboBox.SelectedItem = $"⚠ {currentValue}";
        }
    }
    
    /// <summary>
    /// 刷新技能组下拉列表
    /// </summary>
    public void RefreshSkillGroupComboBox()
    {
        if (_configManager == null || Skill == null) return;
        
        var currentValue = Skill.SkillGroup;
        SkillGroupComboBox.Items.Clear();
        
        // 添加空选项
        SkillGroupComboBox.Items.Add("");
        
        // 从技能组配置加载
        foreach (var group in _configManager.AppSettings.SkillGroups.Where(g => g.Enabled))
        {
            SkillGroupComboBox.Items.Add(group.Name);
        }
        
        // 如果当前值无效但非空，添加到列表中
        if (!string.IsNullOrEmpty(currentValue) && !IsValidSkillGroup(currentValue))
        {
            SkillGroupComboBox.Items.Add($"⚠ {currentValue}");
        }
        
        // 设置选中项
        if (string.IsNullOrEmpty(currentValue))
        {
            SkillGroupComboBox.SelectedIndex = 0;
        }
        else if (IsValidSkillGroup(currentValue))
        {
            SkillGroupComboBox.SelectedItem = currentValue;
        }
        else
        {
            SkillGroupComboBox.SelectedItem = $"⚠ {currentValue}";
        }
    }
    
    /// <summary>
    /// 刷新前置技能名称下拉列表
    /// </summary>
    public void RefreshPreCastSkillNameComboBox()
    {
        if (_configManager == null || Skill == null) return;
        
        var currentValue = Skill.PreCastSkillName;
        PreCastSkillNameComboBox.Items.Clear();
        
        // 添加空选项
        PreCastSkillNameComboBox.Items.Add("");
        
        // 从技能列表加载（排除当前技能）
        foreach (var skill in _configManager.Skills.Where(s => s.Enabled && s != Skill))
        {
            PreCastSkillNameComboBox.Items.Add(skill.Name);
        }
        
        // 如果当前值无效但非空，添加到列表中
        if (!string.IsNullOrEmpty(currentValue) && !IsValidPreCastSkill(currentValue))
        {
            PreCastSkillNameComboBox.Items.Add($"⚠ {currentValue}");
        }
        
        // 设置选中项
        if (string.IsNullOrEmpty(currentValue))
        {
            PreCastSkillNameComboBox.SelectedIndex = 0;
        }
        else if (IsValidPreCastSkill(currentValue))
        {
            PreCastSkillNameComboBox.SelectedItem = currentValue;
        }
        else
        {
            PreCastSkillNameComboBox.SelectedItem = $"⚠ {currentValue}";
        }
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
    /// 通用ComboBox选择变更（触发配置变更）
    /// </summary>
    private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ConfigChanged?.Invoke();
    }
    
    /// <summary>
    /// Buff下拉框选择变更
    /// </summary>
    private void BuffComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Skill == null) return;
        
        var selectedItem = BuffComboBox.SelectedItem?.ToString() ?? "";
        // 移除警告前缀
        var selectedBuff = selectedItem.StartsWith("⚠ ") ? selectedItem[2..] : selectedItem;
        
        if (Skill.PreCastConditionBuff != selectedBuff)
        {
            Skill.PreCastConditionBuff = selectedBuff;
            ConfigChanged?.Invoke();
            ValidateReferences();
        }
    }
    
    /// <summary>
    /// 条件Buff下拉框选择变更
    /// </summary>
    private void ConditionBuffComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Skill == null) return;
        
        var selectedItem = ConditionBuffComboBox.SelectedItem?.ToString() ?? "";
        var selectedBuff = selectedItem.StartsWith("⚠ ") ? selectedItem[2..] : selectedItem;
        
        if (Skill.ConditionBuff != selectedBuff)
        {
            Skill.ConditionBuff = selectedBuff;
            ConfigChanged?.Invoke();
            ValidateReferences();
        }
    }
    
    /// <summary>
    /// 排除条件Buff下拉框选择变更
    /// </summary>
    private void ExcludeConditionBuffComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Skill == null) return;
        
        var selectedItem = ExcludeConditionBuffComboBox.SelectedItem?.ToString() ?? "";
        var selectedBuff = selectedItem.StartsWith("⚠ ") ? selectedItem[2..] : selectedItem;
        
        if (Skill.ExcludeConditionBuff != selectedBuff)
        {
            Skill.ExcludeConditionBuff = selectedBuff;
            ConfigChanged?.Invoke();
            ValidateReferences();
        }
    }
    
    /// <summary>
    /// 优先级覆盖条件Buff下拉框选择变更
    /// </summary>
    private void PriorityOverrideConditionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Skill == null) return;
        
        var selectedItem = PriorityOverrideConditionComboBox.SelectedItem?.ToString() ?? "";
        var selectedBuff = selectedItem.StartsWith("⚠ ") ? selectedItem[2..] : selectedItem;
        
        if (Skill.PriorityOverrideCondition != selectedBuff)
        {
            Skill.PriorityOverrideCondition = selectedBuff;
            ConfigChanged?.Invoke();
            ValidateReferences();
        }
    }
    
    /// <summary>
    /// 技能组下拉框选择变更
    /// </summary>
    private void SkillGroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Skill == null) return;
        
        var selectedItem = SkillGroupComboBox.SelectedItem?.ToString() ?? "";
        var selectedGroup = selectedItem.StartsWith("⚠ ") ? selectedItem[2..] : selectedItem;
        
        if (Skill.SkillGroup != selectedGroup)
        {
            Skill.SkillGroup = selectedGroup;
            ConfigChanged?.Invoke();
            ValidateReferences();
        }
    }
    
    /// <summary>
    /// 前置技能名称下拉框选择变更
    /// </summary>
    private void PreCastSkillNameComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Skill == null) return;
        
        var selectedItem = PreCastSkillNameComboBox.SelectedItem?.ToString() ?? "";
        var selectedSkill = selectedItem.StartsWith("⚠ ") ? selectedItem[2..] : selectedItem;
        
        if (Skill.PreCastSkillName != selectedSkill)
        {
            Skill.PreCastSkillName = selectedSkill;
            ConfigChanged?.Invoke();
            ValidateReferences();
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
        // 正读条：显示读条时间和结束检测
        // 引导：显示引导时间、结束检测和打断配置
        
        switch (castType)
        {
            case SkillCastType.Instant:
                CastDurationPanel.Visibility = Visibility.Collapsed;
                CastEndDetectionPanel.Visibility = Visibility.Collapsed;
                ChannelInterruptPanel.Visibility = Visibility.Collapsed;
                break;
                
            case SkillCastType.CastTime:
                CastDurationPanel.Visibility = Visibility.Visible;
                CastDurationLabel.Text = "最大读条时间(毫秒)";
                CastEndDetectionPanel.Visibility = Visibility.Visible;
                ChannelInterruptPanel.Visibility = Visibility.Collapsed;
                UpdateCastEndDetectionUI();
                break;
                
            case SkillCastType.Channeled:
                CastDurationPanel.Visibility = Visibility.Visible;
                CastDurationLabel.Text = "最大引导时间(毫秒)";
                CastEndDetectionPanel.Visibility = Visibility.Visible;
                ChannelInterruptPanel.Visibility = Visibility.Visible;
                UpdateCastEndDetectionUI();
                UpdateInterruptModeUI();
                break;
        }
    }
    
    /// <summary>
    /// 读条结束检测开关变更
    /// </summary>
    private void UseCastEndDetection_Changed(object sender, RoutedEventArgs e)
    {
        UpdateCastEndDetectionUI();
        ConfigChanged?.Invoke();
    }
    
    /// <summary>
    /// 读条结束检测模式变更
    /// </summary>
    private void CastEndModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Skill == null) return;
        
        var newMode = CastEndModeComboBox.SelectedIndex;
        if (Skill.CastEndDetectionMode != newMode)
        {
            Skill.CastEndDetectionMode = newMode;
            ConfigChanged?.Invoke();
        }
        
        UpdateCastEndDetectionModeUI();
    }
    
    /// <summary>
    /// 更新读条结束检测UI
    /// </summary>
    private void UpdateCastEndDetectionUI()
    {
        if (Skill == null) return;
        
        // 同步复选框
        if (UseCastEndDetectionCheck.IsChecked != Skill.UseCastEndDetection)
        {
            UseCastEndDetectionCheck.IsChecked = Skill.UseCastEndDetection;
        }
        
        // 显示/隐藏详细配置
        CastEndDetectionConfig.Visibility = Skill.UseCastEndDetection ? Visibility.Visible : Visibility.Collapsed;
        
        if (Skill.UseCastEndDetection)
        {
            // 同步检测模式
            if (CastEndModeComboBox.SelectedIndex != Skill.CastEndDetectionMode)
            {
                CastEndModeComboBox.SelectedIndex = Skill.CastEndDetectionMode;
            }
            
            UpdateCastEndDetectionModeUI();
        }
    }
    
    /// <summary>
    /// 更新读条结束检测模式UI
    /// </summary>
    private void UpdateCastEndDetectionModeUI()
    {
        if (Skill == null) return;
        
        if (Skill.CastEndDetectionMode == 0)
        {
            // 点色检测
            CastEndColorPanel.Visibility = Visibility.Visible;
            CastEndTemplateHint.Visibility = Visibility.Collapsed;
            UpdateCastEndColorPreview();
        }
        else
        {
            // 模板匹配
            CastEndColorPanel.Visibility = Visibility.Collapsed;
            CastEndTemplateHint.Visibility = Visibility.Visible;
        }
    }
    
    /// <summary>
    /// 更新读条结束颜色预览
    /// </summary>
    private void UpdateCastEndColorPreview()
    {
        if (Skill == null) return;
        
        try
        {
            var color = Skill.CastEndColor;
            if (color.Length >= 3)
            {
                var r = (byte)Math.Clamp(color[0], 0, 255);
                var g = (byte)Math.Clamp(color[1], 0, 255);
                var b = (byte)Math.Clamp(color[2], 0, 255);
                CastEndColorPreview.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(r, g, b));
            }
        }
        catch { }
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
