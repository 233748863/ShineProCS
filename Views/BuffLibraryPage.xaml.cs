using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Services;
using ShineProCS.Models;
using ShineProCS.ViewModels;
using WinUserControl = System.Windows.Controls.UserControl;
using WinApp = System.Windows.Application;
using WinMessageBox = System.Windows.MessageBox;

namespace ShineProCS.Views;

/// <summary>
/// Buff库页面
/// 需求 5.1: 可搜索的Buff列表
/// 需求 5.2: Buff CRUD操作
/// 需求 5.3: 按类别和状态筛选
/// 需求 5.4: 引用计数显示
/// </summary>
public partial class BuffLibraryPage : WinUserControl
{
    private MainViewModel? _viewModel;
    private IImageInterface? _imageInterface;
    private BuffConfig? _selectedBuff;
    private BuffConfig? _pendingNewBuff;
    
    // 筛选后的Buff列表
    private ObservableCollection<BuffConfig> _filteredBuffs = [];
    
    public BuffLibraryPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }
    
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (WinApp.Current.MainWindow is MainWindow mainWindow && 
            mainWindow.DataContext is MainViewModel vm)
        {
            _viewModel = vm;
            _imageInterface = vm.ImageInterface;
            
            // 初始化筛选列表
            RefreshFilteredList();
            BuffList.ItemsSource = _filteredBuffs;
            
            // 计算引用计数
            UpdateAllReferenceCount();
        }
    }
    
    #region 搜索和筛选 - 需求 5.1, 5.3
    
    /// <summary>
    /// 搜索框文本变化
    /// </summary>
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshFilteredList();
    }
    
    /// <summary>
    /// 类别筛选变化
    /// </summary>
    private void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshFilteredList();
    }
    
    /// <summary>
    /// 状态筛选变化
    /// </summary>
    private void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshFilteredList();
    }
    
    /// <summary>
    /// 刷新筛选后的列表
    /// </summary>
    private void RefreshFilteredList()
    {
        if (_viewModel == null) return;
        
        var searchText = SearchBox?.Text?.Trim().ToLower() ?? "";
        var categoryTag = (CategoryFilter?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "All";
        var statusTag = (StatusFilter?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "All";
        
        var filtered = FilterBuffs(
            _viewModel.AppSettings.BuffLibrary, 
            searchText, 
            categoryTag, 
            statusTag).ToList();
        
        _filteredBuffs.Clear();
        foreach (var buff in filtered)
        {
            _filteredBuffs.Add(buff);
        }
        
        // 更新统计文本
        var totalCount = _viewModel.AppSettings.BuffLibrary.Count;
        var filteredCount = _filteredBuffs.Count;
        FilterResultText.Text = totalCount == filteredCount 
            ? $"共 {totalCount} 个 Buff" 
            : $"显示 {filteredCount} / {totalCount} 个 Buff";
    }
    
    /// <summary>
    /// 筛选Buff列表（供外部调用和测试）
    /// </summary>
    public static IEnumerable<BuffConfig> FilterBuffs(
        IEnumerable<BuffConfig> buffs, 
        string? searchText, 
        string? categoryFilter, 
        string? statusFilter)
    {
        return buffs.Where(buff =>
        {
            // 搜索过滤
            if (!string.IsNullOrEmpty(searchText))
            {
                var search = searchText.ToLower();
                var matchName = buff.Name?.ToLower().Contains(search) ?? false;
                var matchDisplayName = buff.DisplayName?.ToLower().Contains(search) ?? false;
                var matchDescription = buff.Description?.ToLower().Contains(search) ?? false;
                if (!matchName && !matchDisplayName && !matchDescription)
                    return false;
            }
            
            // 类别过滤
            if (categoryFilter == "Buff" && buff.IsDebuff) return false;
            if (categoryFilter == "Debuff" && !buff.IsDebuff) return false;
            
            // 状态过滤
            if (statusFilter == "Enabled" && !buff.Enabled) return false;
            if (statusFilter == "Disabled" && buff.Enabled) return false;
            if (statusFilter == "Configured" && !buff.IsConfigured) return false;
            if (statusFilter == "NotConfigured" && buff.IsConfigured) return false;
            
            return true;
        });
    }
    
    #endregion
    
    #region 引用计数 - 需求 5.4
    
    /// <summary>
    /// 更新所有Buff的引用计数
    /// </summary>
    private void UpdateAllReferenceCount()
    {
        if (_viewModel == null) return;
        
        // 使用 Skills 属性获取技能列表
        var skills = _viewModel.Skills;
        
        foreach (var buff in _viewModel.AppSettings.BuffLibrary)
        {
            UpdateBuffReferenceCount(buff, skills);
        }
    }
    
    /// <summary>
    /// 更新单个Buff的引用计数
    /// </summary>
    private static void UpdateBuffReferenceCount(BuffConfig buff, IEnumerable<SkillConfig> skills)
    {
        var referencingSkills = new List<string>();
        
        foreach (var skill in skills)
        {
            // 检查技能的条件Buff
            if (!string.IsNullOrEmpty(skill.ConditionBuff) && skill.ConditionBuff == buff.Name)
            {
                referencingSkills.Add(skill.Name);
            }
            
            // 检查技能的排除条件Buff
            if (!string.IsNullOrEmpty(skill.ExcludeConditionBuff) && skill.ExcludeConditionBuff == buff.Name)
            {
                referencingSkills.Add($"{skill.Name} (排除)");
            }
            
            // 检查前置技能条件Buff
            if (!string.IsNullOrEmpty(skill.PreCastConditionBuff) && skill.PreCastConditionBuff == buff.Name)
            {
                referencingSkills.Add($"{skill.Name} (前置)");
            }
        }
        
        buff.ReferenceCount = referencingSkills.Count;
        buff.ReferencingSkills = referencingSkills;
    }
    
    /// <summary>
    /// 计算Buff引用计数（静态方法，供测试使用）
    /// </summary>
    public static int CalculateReferenceCount(BuffConfig buff, IEnumerable<SkillConfig> skills)
    {
        int count = 0;
        
        foreach (var skill in skills)
        {
            if (!string.IsNullOrEmpty(skill.ConditionBuff) && skill.ConditionBuff == buff.Name)
            {
                count++;
            }
            
            if (!string.IsNullOrEmpty(skill.ExcludeConditionBuff) && skill.ExcludeConditionBuff == buff.Name)
            {
                count++;
            }
            
            if (!string.IsNullOrEmpty(skill.PreCastConditionBuff) && skill.PreCastConditionBuff == buff.Name)
            {
                count++;
            }
        }
        
        return count;
    }
    
    #endregion
    
    #region CRUD 操作 - 需求 5.2
    
    private void AddBuff_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        
        if (_pendingNewBuff != null)
        {
            ToastManager.Warning("请先保存当前新增的Buff", "提示");
            BuffList.SelectedItem = _pendingNewBuff;
            return;
        }
        
        var newBuff = new BuffConfig
        {
            Name = $"buff_{DateTime.Now:HHmmss}",
            DisplayName = "新Buff",
            Description = "",
            Enabled = true
        };
        
        _viewModel.AppSettings.BuffLibrary.Add(newBuff);
        _pendingNewBuff = newBuff;
        
        AddBuffButton.IsEnabled = false;
        
        RefreshFilteredList();
        BuffList.SelectedItem = newBuff;
        
        ToastManager.Info("请编辑后点击保存", "已添加");
    }
    
    private void BuffList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BuffList.SelectedItem is BuffConfig buff)
        {
            SelectBuff(buff);
        }
    }
    
    private void SelectBuff(BuffConfig buff)
    {
        _selectedBuff = buff;
        
        BuffNameBox.Text = buff.Name;
        BuffDisplayNameBox.Text = buff.DisplayName;
        BuffDescriptionBox.Text = buff.Description;
        IsDebuffCheck.IsChecked = buff.IsDebuff;
        
        RegionX.Text = buff.IconRegion[0].ToString();
        RegionY.Text = buff.IconRegion[1].ToString();
        RegionW.Text = buff.IconRegion[2].ToString();
        RegionH.Text = buff.IconRegion[3].ToString();
        
        TemplatePathBox.Text = buff.TemplatePath;
        ThresholdBox.Text = buff.SimilarityThreshold.ToString("F2");
        
        UpdateTemplatePreview(buff.TemplatePath);
        UpdateReferenceInfo(buff);
        
        EditPanel.Visibility = Visibility.Visible;
        EmptyHint.Visibility = Visibility.Collapsed;
    }
    
    private void UpdateReferenceInfo(BuffConfig buff)
    {
        if (buff.ReferenceCount > 0)
        {
            ReferenceInfoPanel.Visibility = Visibility.Visible;
            ReferenceSkillsText.Text = string.Join(", ", buff.ReferencingSkills);
        }
        else
        {
            ReferenceInfoPanel.Visibility = Visibility.Collapsed;
        }
    }
    
    private void UpdateTemplatePreview(string templatePath)
    {
        if (!string.IsNullOrEmpty(templatePath) && File.Exists(templatePath))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(templatePath, UriKind.Absolute);
                bitmap.EndInit();
                
                TemplatePreviewImage.Source = bitmap;
                TemplateInfoText.Text = $"{bitmap.PixelWidth}x{bitmap.PixelHeight}";
                TemplatePreviewBorder.Visibility = Visibility.Visible;
            }
            catch
            {
                TemplatePreviewBorder.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            TemplatePreviewBorder.Visibility = Visibility.Collapsed;
        }
    }
    
    private void DeleteBuff_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is BuffConfig buff && _viewModel != null)
        {
            var warningMessage = $"确定要删除 \"{buff.DisplayName}\" 吗？";
            if (buff.ReferenceCount > 0)
            {
                warningMessage += $"\n\n⚠ 警告：此Buff被 {buff.ReferenceCount} 个技能引用，删除后这些技能的配置将失效。";
            }
            
            var result = WinMessageBox.Show(
                warningMessage,
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                if (buff == _pendingNewBuff)
                {
                    _pendingNewBuff = null;
                    AddBuffButton.IsEnabled = true;
                }
                
                _viewModel.AppSettings.BuffLibrary.Remove(buff);
                
                if (_selectedBuff == buff)
                {
                    _selectedBuff = null;
                    EditPanel.Visibility = Visibility.Collapsed;
                    EmptyHint.Visibility = Visibility.Visible;
                }
                
                _viewModel.ConfigManager.SaveAppSettings(_viewModel.AppSettings);
                
                RefreshFilteredList();
                ToastManager.Success($"已删除 {buff.DisplayName}", "删除成功");
            }
        }
    }
    
    private void BuffEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.ConfigManager.SaveAppSettings(_viewModel.AppSettings);
        }
    }
    
    private void SelectRegion_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedBuff == null) return;
        
        var selector = new RegionSelectorWindow();
        if (selector.ShowDialog() == true)
        {
            var r = selector.SelectedRegion;
            if (r.Width > 0 && r.Height > 0)
            {
                RegionX.Text = r.X.ToString();
                RegionY.Text = r.Y.ToString();
                RegionW.Text = r.Width.ToString();
                RegionH.Text = r.Height.ToString();
                
                ToastManager.Success($"区域: {r.X},{r.Y} {r.Width}x{r.Height}", "已选择");
            }
        }
    }
    
    private void CaptureTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedBuff == null || _imageInterface == null) return;
        
        if (!int.TryParse(RegionX.Text, out int x) ||
            !int.TryParse(RegionY.Text, out int y) ||
            !int.TryParse(RegionW.Text, out int w) ||
            !int.TryParse(RegionH.Text, out int h) ||
            w <= 0 || h <= 0)
        {
            ToastManager.Warning("请先设置有效的检测区域", "截取失败");
            return;
        }
        
        try
        {
            var templateCapture = new TemplateCapture(_imageInterface);
            var templatePath = templateCapture.CaptureBuffTemplate(_selectedBuff.Name, [x, y, w, h]);
            
            if (!string.IsNullOrEmpty(templatePath))
            {
                TemplatePathBox.Text = templatePath;
                UpdateTemplatePreview(templatePath);
                ToastManager.Success("模板已保存", "截取成功");
            }
        }
        catch (Exception ex)
        {
            ToastManager.Error($"截取失败: {ex.Message}", "错误");
        }
    }
    
    private void TestMatch_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedBuff == null || _imageInterface == null) return;
        
        var templatePath = TemplatePathBox.Text;
        if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
        {
            ToastManager.Warning("请先截取模板图片", "测试失败");
            return;
        }
        
        if (!int.TryParse(RegionX.Text, out int x) ||
            !int.TryParse(RegionY.Text, out int y) ||
            !int.TryParse(RegionW.Text, out int w) ||
            !int.TryParse(RegionH.Text, out int h) ||
            w <= 0 || h <= 0)
        {
            ToastManager.Warning("请先设置有效的检测区域", "测试失败");
            return;
        }
        
        try
        {
            var currentFrame = _imageInterface.GetScreenRegion(x, y, w, h);
            if (currentFrame == null)
            {
                ToastManager.Error("无法截取屏幕区域", "测试失败");
                return;
            }
            
            try
            {
                var template = OpenCvSharp.Cv2.ImRead(templatePath, OpenCvSharp.ImreadModes.Color);
                var similarity = _imageInterface.MatchTemplate(currentFrame, template);
                template.Dispose();
                
                RegionHighlightWindow.ShowHighlight(x, y, w, h, 2);
                
                if (!double.TryParse(ThresholdBox.Text, out double threshold))
                    threshold = 0.8;
                
                var status = similarity >= threshold ? "匹配" : "不匹配";
                ToastManager.Info($"相似度: {similarity:P1} {status}\n坐标: ({x},{y}) {w}x{h}", "测试结果");
            }
            finally
            {
                _imageInterface.ReturnMat(currentFrame);
            }
        }
        catch (Exception ex)
        {
            ToastManager.Error($"测试失败: {ex.Message}", "错误");
        }
    }
    
    private void SaveBuff_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedBuff == null || _viewModel == null) return;
        
        var name = BuffNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            ToastManager.Warning("名称标识不能为空", "保存失败");
            return;
        }
        
        var duplicate = _viewModel.AppSettings.BuffLibrary
            .FirstOrDefault(b => b != _selectedBuff && b.Name == name);
        if (duplicate != null)
        {
            ToastManager.Warning($"名称 \"{name}\" 已存在", "保存失败");
            return;
        }
        
        _selectedBuff.Name = name;
        _selectedBuff.DisplayName = BuffDisplayNameBox.Text.Trim();
        _selectedBuff.Description = BuffDescriptionBox.Text.Trim();
        _selectedBuff.IsDebuff = IsDebuffCheck.IsChecked == true;
        
        if (int.TryParse(RegionX.Text, out int rx)) _selectedBuff.IconRegion[0] = rx;
        if (int.TryParse(RegionY.Text, out int ry)) _selectedBuff.IconRegion[1] = ry;
        if (int.TryParse(RegionW.Text, out int rw)) _selectedBuff.IconRegion[2] = rw;
        if (int.TryParse(RegionH.Text, out int rh)) _selectedBuff.IconRegion[3] = rh;
        
        _selectedBuff.TemplatePath = TemplatePathBox.Text;
        
        if (double.TryParse(ThresholdBox.Text, out double threshold))
            _selectedBuff.SimilarityThreshold = Math.Clamp(threshold, 0.0, 1.0);
        
        _viewModel.ConfigManager.SaveAppSettings(_viewModel.AppSettings);
        
        if (_selectedBuff == _pendingNewBuff)
        {
            _pendingNewBuff = null;
            AddBuffButton.IsEnabled = true;
        }
        
        RefreshFilteredList();
        ToastManager.Success($"已保存 {_selectedBuff.DisplayName}", "保存成功");
    }
    
    #endregion
}
