using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Services;
using ShineProCS.Models;
using ShineProCS.ViewModels;
using WinUserControl = System.Windows.Controls.UserControl;
using WinApp = System.Windows.Application;

namespace ShineProCS.Views;

public partial class BuffLibraryPage : WinUserControl
{
    private MainViewModel? _viewModel;
    private IImageInterface? _imageInterface;
    private BuffConfig? _selectedBuff;
    
    public BuffLibraryPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }
    
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 从MainWindow获取ViewModel
        if (WinApp.Current.MainWindow is MainWindow mainWindow && 
            mainWindow.DataContext is MainViewModel vm)
        {
            _viewModel = vm;
            _imageInterface = vm.ImageInterface;
            
            // 绑定到ViewModel的AppSettings.BuffLibrary
            BuffList.ItemsSource = vm.AppSettings.BuffLibrary;
        }
    }
    
    private void AddBuff_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;
        
        var newBuff = new BuffConfig
        {
            Name = $"buff_{DateTime.Now:HHmmss}",
            DisplayName = "新Buff",
            Description = "",
            Enabled = true
        };
        
        _viewModel.AppSettings.BuffLibrary.Add(newBuff);
        BuffList.SelectedItem = newBuff;
        
        ToastManager.Info("已添加新Buff，请编辑后点击保存", "Buff库");
    }
    
    private void BuffList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (BuffList.SelectedItem is BuffConfig buff)
        {
            SelectBuff(buff);
        }
    }
    
    private void SelectBuff(BuffConfig buff)
    {
        _selectedBuff = buff;
        
        // 填充编辑表单
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
        
        // 显示模板预览
        UpdateTemplatePreview(buff.TemplatePath);
        
        // 显示编辑面板
        EditPanel.Visibility = Visibility.Visible;
        EmptyHint.Visibility = Visibility.Collapsed;
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
            var result = System.Windows.MessageBox.Show(
                $"确定要删除 \"{buff.DisplayName}\" 吗？\n\n注意：引用此Buff的技能配置将失效。",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                _viewModel.AppSettings.BuffLibrary.Remove(buff);
                
                if (_selectedBuff == buff)
                {
                    _selectedBuff = null;
                    EditPanel.Visibility = Visibility.Collapsed;
                    EmptyHint.Visibility = Visibility.Visible;
                }
                
                // 保存配置到文件
                _viewModel.ConfigManager.SaveAll();
                
                ToastManager.Success($"已删除 {buff.DisplayName}", "Buff库");
            }
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
        
        // 解析当前区域
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
            // 截取当前区域并匹配
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
                
                // 显示高亮
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
        
        // 验证名称
        var name = BuffNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            ToastManager.Warning("名称标识不能为空", "保存失败");
            return;
        }
        
        // 检查名称是否重复
        var duplicate = _viewModel.AppSettings.BuffLibrary
            .FirstOrDefault(b => b != _selectedBuff && b.Name == name);
        if (duplicate != null)
        {
            ToastManager.Warning($"名称 \"{name}\" 已存在", "保存失败");
            return;
        }
        
        // 更新数据
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
        
        // 保存配置
        _viewModel.ConfigManager.SaveAll();
        
        ToastManager.Success($"已保存 {_selectedBuff.DisplayName}", "Buff库");
    }
}
