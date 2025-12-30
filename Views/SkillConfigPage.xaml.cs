using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using OpenCvSharp;
using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Services;
using ShineProCS.Models;
using ShineProCS.ViewModels;
using Window = System.Windows.Window;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Rectangle = System.Windows.Shapes.Rectangle;
using MessageBox = System.Windows.MessageBox;

namespace ShineProCS.Views;

/// <summary>
/// 技能配置页面 - 卡片式布局 + 实时预览
/// </summary>
public partial class SkillConfigPage : System.Windows.Controls.UserControl
{
    private MainViewModel? _viewModel;
    private IImageInterface? _imageInterface;
    private PresetManager? _presetManager;
    private DispatcherTimer? _previewTimer;
    private SkillConfig? _previewSkill;
    private Mat? _previewTemplate;
    private int _frameCount;
    private DateTime _lastFpsUpdate = DateTime.Now;
    private readonly List<SkillCardControl> _skillCards = [];

    public SkillConfigPage()
    {
        InitializeComponent();
        _presetManager = new PresetManager();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 延迟启动预览定时器，确保页面完全加载
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _previewTimer.Tick += PreviewTimer_Tick;
            
            if (AutoPreviewCheck.IsChecked == true && _imageInterface != null)
                _previewTimer.Start();
        });
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _previewTimer?.Stop();
        _previewTemplate?.Dispose();
        _previewTemplate = null;
        _skillCards.Clear();
    }

    public void SetImageInterface(IImageInterface imageInterface)
    {
        _imageInterface = imageInterface;
    }

    private void SkillCard_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not SkillCardControl card) return;
        
        // 避免重复添加
        if (_skillCards.Contains(card)) return;
        
        _skillCards.Add(card);
        
        // 统一的操作处理
        card.OnActionRequested += HandleCardAction;
        card.ConfigChanged += MarkUnsaved;
        
        // 点击卡片时设置预览
        card.MouseLeftButtonUp += (s, args) =>
        {
            if (card.DataContext is SkillConfig skill)
                SetPreviewSkill(skill);
        };
    }

    /// <summary>
    /// 处理卡片操作请求
    /// </summary>
    private void HandleCardAction(SkillConfig skill, string action)
    {
        switch (action)
        {
            case "QuickConfig":
                QuickConfig(skill);
                break;
            case "CaptureKey":
                _viewModel?.CaptureKeyCommand.Execute(skill);
                break;
            case "CapturePreCastKey":
                _viewModel?.CapturePreCastKeyCommand.Execute(skill);
                break;
            case "SelectRegion":
                SelectRegion(skill);
                break;
            case "CaptureTemplate":
                CaptureTemplate(skill);
                break;
            case "TestMatch":
                TestMatch(skill);
                break;
            case "MoveUp":
                MoveSkillUp(skill);
                break;
            case "MoveDown":
                MoveSkillDown(skill);
                break;
            case "Delete":
                DeleteSkill(skill);
                break;
            case "SelectInterruptPoint":
                SelectInterruptPoint(skill);
                break;
            case "PickInterruptColor":
                PickInterruptColor(skill);
                break;
            case "SelectCastEndPoint":
                SelectCastEndPoint(skill);
                break;
            case "PickCastEndColor":
                PickCastEndColor(skill);
                break;
        }
    }

    #region Preview

    private void SetPreviewSkill(SkillConfig? skill)
    {
        _previewSkill = skill;
        _previewTemplate?.Dispose();
        _previewTemplate = null;
        
        if (skill != null && !string.IsNullOrEmpty(skill.TemplatePath) && File.Exists(skill.TemplatePath))
        {
            try { _previewTemplate = Cv2.ImRead(skill.TemplatePath); }
            catch { }
        }
        
        UpdatePreviewInfo();
        RefreshPreview();
    }

    private void UpdatePreviewInfo()
    {
        if (_previewSkill == null)
        {
            PreviewSkillName.Text = "技能: --";
            PreviewRegionInfo.Text = "区域: --";
            PreviewMatchBadge.Visibility = Visibility.Collapsed;
            NoSelectionHint.Visibility = Visibility.Visible;
            return;
        }
        
        NoSelectionHint.Visibility = Visibility.Collapsed;
        PreviewSkillName.Text = $"技能: {_previewSkill.Name}";
        
        var r = _previewSkill.IconRegion;
        PreviewRegionInfo.Text = r.All(v => v == 0) ? "区域: 未配置" : $"区域: ({r[0]}, {r[1]}, {r[2]}x{r[3]})";
    }

    private void PreviewTimer_Tick(object? sender, EventArgs e)
    {
        RefreshPreview();
        UpdateFps();
    }

    private void RefreshPreview()
    {
        if (_imageInterface == null || _previewSkill == null) return;
        
        var region = _previewSkill.IconRegion;
        if (region.All(v => v == 0)) return;
        
        try
        {
            int padding = 30;
            int x = Math.Max(0, region[0] - padding);
            int y = Math.Max(0, region[1] - padding);
            int w = region[2] + padding * 2;
            int h = region[3] + padding * 2;
            
            var frame = _imageInterface.GetScreenRegion(x, y, w, h);
            if (frame == null) return;
            
            try
            {
                PreviewImage.Source = MatToBitmapSource(frame);
                DrawPreviewOverlay(padding, padding, region[2], region[3], w, h);
                
                if (_previewTemplate != null && !_previewTemplate.Empty())
                    PerformPreviewMatch(frame);
                else
                    PreviewMatchBadge.Visibility = Visibility.Collapsed;
                
                _frameCount++;
            }
            finally
            {
                _imageInterface.ReturnMat(frame);
            }
        }
        catch { }
    }

    private void PerformPreviewMatch(Mat frame)
    {
        if (_previewTemplate == null || _previewSkill == null) return;
        
        try
        {
            using var result = new Mat();
            Cv2.MatchTemplate(frame, _previewTemplate, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out _);
            
            PreviewMatchBadge.Visibility = Visibility.Visible;
            PreviewMatchText.Text = $"匹配: {maxVal:P0}";
            
            var threshold = _previewSkill.SimilarityThreshold;
            if (maxVal >= threshold)
                PreviewMatchBadge.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
            else if (maxVal >= threshold * 0.8)
                PreviewMatchBadge.Background = new SolidColorBrush(Color.FromRgb(255, 152, 0));
            else
                PreviewMatchBadge.Background = new SolidColorBrush(Color.FromRgb(244, 67, 54));
        }
        catch
        {
            PreviewMatchBadge.Visibility = Visibility.Collapsed;
        }
    }

    private void DrawPreviewOverlay(int x, int y, int w, int h, int totalW, int totalH)
    {
        PreviewOverlay.Children.Clear();
        if (PreviewImage.ActualWidth <= 0 || PreviewImage.ActualHeight <= 0) return;
        
        double scaleX = PreviewImage.ActualWidth / totalW;
        double scaleY = PreviewImage.ActualHeight / totalH;
        double scale = Math.Min(scaleX, scaleY);
        
        double offsetX = (PreviewImage.ActualWidth - totalW * scale) / 2;
        double offsetY = (PreviewImage.ActualHeight - totalH * scale) / 2;
        
        var rect = new Rectangle
        {
            Width = w * scale,
            Height = h * scale,
            Stroke = Brushes.LimeGreen,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 }
        };
        
        Canvas.SetLeft(rect, offsetX + x * scale);
        Canvas.SetTop(rect, offsetY + y * scale);
        PreviewOverlay.Children.Add(rect);
    }

    private void UpdateFps()
    {
        var now = DateTime.Now;
        var elapsed = (now - _lastFpsUpdate).TotalSeconds;
        
        if (elapsed >= 1.0)
        {
            PreviewFps.Text = $"{_frameCount / elapsed:F0} FPS";
            _frameCount = 0;
            _lastFpsUpdate = now;
        }
    }

    private static BitmapSource MatToBitmapSource(Mat mat)
    {
        using var converted = mat.CvtColor(ColorConversionCodes.BGR2BGRA);
        var bitmap = BitmapSource.Create(
            converted.Width, converted.Height, 96, 96, PixelFormats.Bgra32, null,
            converted.Data, converted.Width * converted.Height * 4, converted.Width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private void AutoPreview_Changed(object sender, RoutedEventArgs e)
    {
        if (_previewTimer == null) return;
        if (AutoPreviewCheck.IsChecked == true && _imageInterface != null) 
            _previewTimer.Start();
        else 
            _previewTimer.Stop();
    }

    #endregion

    #region Skill Operations

    private async void QuickConfig(SkillConfig skill)
    {
        if (_viewModel == null || _imageInterface == null) return;
        
        var selector = new RegionSelectorWindow();
        if (selector.ShowDialog() != true) return;
        
        var r = selector.SelectedRegion;
        skill.IconRegion = [r.X, r.Y, r.Width, r.Height];
        
        var templateCapture = new TemplateCapture(_imageInterface);
        var path = templateCapture.CaptureSkillTemplate(skill.Name, skill.IconRegion);
        
        if (path == null)
        {
            ToastManager.Error("模板截取失败", "一键配置");
            return;
        }
        
        skill.TemplatePath = path;
        await Task.Delay(100);
        
        var similarity = TestMatchSilent(skill);
        
        if (similarity >= skill.SimilarityThreshold)
            ToastManager.Success($"配置成功! 匹配度: {similarity:P0}", "一键配置");
        else if (similarity > 0)
            ToastManager.Warning($"匹配度较低 ({similarity:P0})，建议在技能可用时重新配置", "一键配置");
        else
            ToastManager.Error("匹配测试失败", "一键配置");
        
        UpdateCardStatus(skill);
        SetPreviewSkill(skill);
        MarkUnsaved();
    }

    private void SelectRegion(SkillConfig skill)
    {
        var selector = new RegionSelectorWindow();
        if (selector.ShowDialog() != true) return;
        
        var r = selector.SelectedRegion;
        skill.IconRegion = [r.X, r.Y, r.Width, r.Height];
        
        UpdateCardStatus(skill);
        SetPreviewSkill(skill);
        MarkUnsaved();
    }

    private void CaptureTemplate(SkillConfig skill)
    {
        if (_imageInterface == null) return;
        
        var templateCapture = new TemplateCapture(_imageInterface);
        var path = templateCapture.CaptureSkillTemplate(skill.Name, skill.IconRegion);
        
        if (path != null)
        {
            skill.TemplatePath = path;
            ToastManager.Success("模板截取成功", "截取模板");
            UpdateCardStatus(skill);
            SetPreviewSkill(skill);
            MarkUnsaved();
        }
        else
        {
            ToastManager.Error("模板截取失败，请先配置检测区域", "截取模板");
        }
    }

    private void TestMatch(SkillConfig skill)
    {
        var r = skill.IconRegion;
        
        // 调试：输出当前区域值
        System.Diagnostics.Debug.WriteLine($"TestMatch - IconRegion: [{r[0]}, {r[1]}, {r[2]}, {r[3]}]");
        
        // 检查区域是否有效
        if (r.All(v => v == 0))
        {
            ToastManager.Error("请先配置检测区域", "测试失败");
            return;
        }
        
        var similarity = TestMatchSilent(skill);
        
        // 显示匹配结果和坐标
        var coordInfo = $"({r[0]}, {r[1]}, {r[2]}×{r[3]})";
        
        if (similarity >= 0.95)
            ToastManager.Success($"匹配度: {similarity:P0}\n区域: {coordInfo}", "测试通过");
        else if (similarity >= skill.SimilarityThreshold)
            ToastManager.Info($"匹配度: {similarity:P0}\n区域: {coordInfo}", "测试通过");
        else if (similarity > 0)
            ToastManager.Warning($"匹配度: {similarity:P0}\n区域: {coordInfo}\n建议重新配置", "测试警告");
        else
            ToastManager.Error($"测试失败\n区域: {coordInfo}\n请检查区域和模板配置", "测试失败");
        
        // 高亮显示实际测试的区域
        RegionHighlightWindow.ShowHighlight(r[0], r[1], r[2], r[3], 3);
    }

    private double TestMatchSilent(SkillConfig skill)
    {
        if (_imageInterface == null) return -1;
        
        var region = skill.IconRegion;
        if (region.All(v => v == 0)) return -1;
        if (string.IsNullOrEmpty(skill.TemplatePath) || !File.Exists(skill.TemplatePath)) return -1;
        
        try
        {
            using var template = Cv2.ImRead(skill.TemplatePath);
            if (template.Empty()) return -1;
            
            var frame = _imageInterface.GetScreenRegion(region[0], region[1], region[2], region[3]);
            if (frame == null) return -1;
            
            try
            {
                using var result = new Mat();
                Cv2.MatchTemplate(frame, template, result, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out _);
                return maxVal;
            }
            finally
            {
                _imageInterface.ReturnMat(frame);
            }
        }
        catch { return -1; }
    }

    private void UpdateCardStatus(SkillConfig skill)
    {
        var card = _skillCards.FirstOrDefault(c => c.DataContext == skill);
        card?.UpdateConfigStatus();
        card?.UpdateTemplatePreview();
    }

    private void DeleteSkill(SkillConfig skill)
    {
        if (MessageBox.Show($"确定要删除技能 [{skill.Name}] 吗?", "确认删除", 
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        
        _viewModel?.Skills.Remove(skill);
        _skillCards.RemoveAll(c => c.DataContext == skill);
        MarkUnsaved();
    }

    private void MoveSkillUp(SkillConfig skill)
    {
        if (_viewModel == null) return;
        var index = _viewModel.Skills.IndexOf(skill);
        if (index > 0) { _viewModel.Skills.Move(index, index - 1); MarkUnsaved(); }
    }

    private void MoveSkillDown(SkillConfig skill)
    {
        if (_viewModel == null) return;
        var index = _viewModel.Skills.IndexOf(skill);
        if (index < _viewModel.Skills.Count - 1) { _viewModel.Skills.Move(index, index + 1); MarkUnsaved(); }
    }
    
    /// <summary>
    /// 选择引导打断检测点
    /// </summary>
    private void SelectInterruptPoint(SkillConfig skill)
    {
        var selector = new RegionSelectorWindow(pointMode: true);
        if (selector.ShowDialog() != true) return;
        
        var point = selector.SelectedPoint;
        skill.ChannelInterruptPoint = [(int)point.X, (int)point.Y];
        
        ToastManager.Success($"已设置检测点: ({(int)point.X}, {(int)point.Y})", "取点成功");
        MarkUnsaved();
    }
    
    /// <summary>
    /// 取色 - 从屏幕上选择颜色
    /// </summary>
    private void PickInterruptColor(SkillConfig skill)
    {
        if (_imageInterface == null) return;
        
        // 先选择点位
        var selector = new RegionSelectorWindow(pointMode: true);
        if (selector.ShowDialog() != true) return;
        
        var point = selector.SelectedPoint;
        int x = (int)point.X;
        int y = (int)point.Y;
        
        try
        {
            // 获取该点的颜色
            var pixel = _imageInterface.GetScreenRegion(x, y, 1, 1);
            if (pixel == null)
            {
                ToastManager.Error("获取颜色失败", "取色");
                return;
            }
            
            try
            {
                var indexer = pixel.GetGenericIndexer<OpenCvSharp.Vec3b>();
                var color = indexer[0, 0];
                
                // BGR -> RGB
                skill.ChannelInterruptColor = [color.Item2, color.Item1, color.Item0];
                
                // 同时更新检测点
                skill.ChannelInterruptPoint = [x, y];
                
                ToastManager.Success($"已取色: RGB({color.Item2}, {color.Item1}, {color.Item0})\n位置: ({x}, {y})", "取色成功");
                MarkUnsaved();
                
                // 刷新卡片显示
                UpdateCardStatus(skill);
            }
            finally
            {
                _imageInterface.ReturnMat(pixel);
            }
        }
        catch (Exception ex)
        {
            ToastManager.Error($"取色失败: {ex.Message}", "取色");
        }
    }
    
    /// <summary>
    /// 选择读条结束检测点
    /// </summary>
    private void SelectCastEndPoint(SkillConfig skill)
    {
        var selector = new RegionSelectorWindow(pointMode: true);
        if (selector.ShowDialog() != true) return;
        
        var point = selector.SelectedPoint;
        skill.CastEndDetectionPoint = [(int)point.X, (int)point.Y];
        
        ToastManager.Success($"已设置检测点: ({(int)point.X}, {(int)point.Y})", "取点成功");
        MarkUnsaved();
    }
    
    /// <summary>
    /// 取色 - 读条结束颜色
    /// </summary>
    private void PickCastEndColor(SkillConfig skill)
    {
        if (_imageInterface == null) return;
        
        var selector = new RegionSelectorWindow(pointMode: true);
        if (selector.ShowDialog() != true) return;
        
        var point = selector.SelectedPoint;
        int x = (int)point.X;
        int y = (int)point.Y;
        
        try
        {
            var pixel = _imageInterface.GetScreenRegion(x, y, 1, 1);
            if (pixel == null)
            {
                ToastManager.Error("获取颜色失败", "取色");
                return;
            }
            
            try
            {
                var indexer = pixel.GetGenericIndexer<OpenCvSharp.Vec3b>();
                var color = indexer[0, 0];
                
                // BGR -> RGB
                skill.CastEndColor = [color.Item2, color.Item1, color.Item0];
                skill.CastEndDetectionPoint = [x, y];
                
                ToastManager.Success($"已取色: RGB({color.Item2}, {color.Item1}, {color.Item0})\n位置: ({x}, {y})", "取色成功");
                MarkUnsaved();
                UpdateCardStatus(skill);
            }
            finally
            {
                _imageInterface.ReturnMat(pixel);
            }
        }
        catch (Exception ex)
        {
            ToastManager.Error($"取色失败: {ex.Message}", "取色");
        }
    }

    #endregion

    #region Toolbar

    private void AddSkill_Click(object sender, RoutedEventArgs e) 
    { 
        _viewModel?.AddSkillCommand.Execute(null); 
        MarkUnsaved(); 
    }
    
    private void ExpandAll_Click(object sender, RoutedEventArgs e) 
    { 
        foreach (var card in _skillCards) card.SetExpanded(true); 
    }
    
    private void CollapseAll_Click(object sender, RoutedEventArgs e) 
    { 
        foreach (var card in _skillCards) card.SetExpanded(false); 
    }
    
    private void SaveConfig_Click(object sender, RoutedEventArgs e) 
    { 
        _viewModel?.SaveConfigCommand.Execute(null); 
        UnsavedHint.Visibility = Visibility.Collapsed;
        ToastManager.Success("配置已保存", "保存");
    }
    
    private void CreateProfile_Click(object sender, RoutedEventArgs e) 
    { 
        _viewModel?.CreateProfileCommand.Execute(null); 
    }
    
    private void DeleteProfile_Click(object sender, RoutedEventArgs e) 
    { 
        _viewModel?.DeleteProfileCommand.Execute(null); 
    }

    private void LoadPreset_Click(object sender, RoutedEventArgs e)
    {
        if (_presetManager == null)
        {
            ToastManager.Warning("预设管理器未初始化，请稍后重试", "预设");
            return;
        }
        
        if (_viewModel == null)
        {
            ToastManager.Warning("视图模型未初始化", "预设");
            return;
        }
        
        try
        {
            var presets = _presetManager.GetAvailablePresets();
            var menu = new ContextMenu();
            
            foreach (var preset in presets)
            {
                var item = new MenuItem { Header = preset.Name, ToolTip = preset.Description };
                item.Click += (s, args) =>
                {
                    var skills = _presetManager.LoadPreset(preset);
                    if (skills != null)
                    {
                        _viewModel.Skills.Clear();
                        foreach (var skill in skills) _viewModel.Skills.Add(skill);
                        ToastManager.Success($"已加载预设: {preset.Name}", "预设");
                        MarkUnsaved();
                    }
                };
                menu.Items.Add(item);
            }
            
            menu.PlacementTarget = sender as UIElement;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
        catch (Exception ex)
        {
            ToastManager.Error($"加载预设失败: {ex.Message}", "预设");
        }
    }

    private void MarkUnsaved() 
    { 
        UnsavedHint.Visibility = Visibility.Visible; 
    }

    #endregion
}
