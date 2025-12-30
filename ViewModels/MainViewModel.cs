using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShineProCS.Core.Engine;
using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Services;
using ShineProCS.Infrastructure;
using ShineProCS.Models;
using ShineProCS.Utils;
using ShineProCS.Views;

namespace ShineProCS.ViewModels;

/// <summary>
/// 技能状态显示模型
/// </summary>
public partial class SkillStatusItem : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _status = "Disabled"; // Ready, Cooldown, NoBuff, Disabled
    [ObservableProperty] private string _statusText = "已禁用";
    [ObservableProperty] private string _statusIcon = "⬜";
    [ObservableProperty] private double _cooldownRemaining; // CD剩余时间
    [ObservableProperty] private int _useCount; // 使用次数
}

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ConfigManager _config;
    private readonly SkillLoopEngine _engine;
    private readonly IImageInterface _imageInterface;
    private readonly GlobalHotkeyService _hotkeyService;
    
    /// <summary>
    /// 公开图像接口供其他组件使用（如SkillConfigPage）
    /// </summary>
    public IImageInterface ImageInterface => _imageInterface;
    
    /// <summary>
    /// 公开ConfigManager供其他组件使用（如BuffLibraryPage）
    /// </summary>
    public ConfigManager ConfigManager => _config;
    private readonly TemplateCapture _templateCapture;
    private Dictionary<string, OpenCvSharp.Mat> _tempTemplateCache; // 临时模板缓存
    private OverlayWindow? _overlay;
    private string _nextSkillName = "";
    private double _currentHpPercent = 100;
    private double _currentMpPercent = 100;
    private DispatcherTimer? _memoryTimer;
    private DispatcherTimer? _cooldownTimer; // CD更新定时器
    private bool _disposed;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private string _statusText = "已停止";
    [ObservableProperty] private int _executionCount;
    [ObservableProperty] private double _avgResponseTime;
    [ObservableProperty] private double _successRate = 100.0;
    [ObservableProperty] private string _memoryStats = "内存: 0 MB";
    [ObservableProperty] private ObservableCollection<SkillConfig> _skills = [];
    [ObservableProperty] private SkillConfig? _selectedSkill;
    [ObservableProperty] private ObservableCollection<string> _availableProfiles = [];
    [ObservableProperty] private string _selectedProfile = "默认";
    [ObservableProperty] private ObservableCollection<string> _logMessages = [];
    [ObservableProperty] private AppSettings _appSettings;
    [ObservableProperty] private ObservableCollection<SkillStatusItem> _skillStatusList = [];
    [ObservableProperty] private bool _showGuideTip;
    [ObservableProperty] private string _hotkeyStatus = "";

    public MainViewModel()
    {
        _config = new ConfigManager();
        _config.LoadConfigs();
        AppSettings = _config.AppSettings;

        IKeyboardInterface keyboard = new Win32KeyboardInterface();
        _imageInterface = new OpenCvImageInterface();
        _engine = new SkillLoopEngine(keyboard, _imageInterface, _config);
        _engine.StatusChanged += OnStatusChanged;
        _engine.LogMessage += OnLog;
        
        _templateCapture = new TemplateCapture(_imageInterface);
        _tempTemplateCache = new Dictionary<string, OpenCvSharp.Mat>();
        
        _hotkeyService = new GlobalHotkeyService();
        _hotkeyService.HotkeyTriggered += OnHotkeyTriggered;

        LoadSkills();
        RefreshProfiles();
        InitializeSkillStatusList();
        StartMemoryMonitor();
        StartCooldownTimer();
        CheckFirstTimeGuide();
        
        if (AppSettings.EnableOverlay)
            ShowOverlay();
    }

    public void InitializeHotkeys(Window window)
    {
        _hotkeyService.Initialize(window);
        RegisterHotkeys();
    }

    private void RegisterHotkeys()
    {
        if (!AppSettings.EnableGlobalHotkeys) return;
        
        var registered = new List<string>();
        
        if (_hotkeyService.RegisterHotkey("StartStop", 
            AppSettings.HotkeyStartStopModifier, 
            AppSettings.HotkeyStartStopKey, 
            () => { if (IsRunning) StopEngine(); else StartEngine(); }))
        {
            registered.Add(GlobalHotkeyService.GetHotkeyDisplayText(
                AppSettings.HotkeyStartStopModifier, AppSettings.HotkeyStartStopKey));
        }
        
        if (_hotkeyService.RegisterHotkey("Pause",
            AppSettings.HotkeyPauseModifier,
            AppSettings.HotkeyPauseKey,
            () => PauseEngine()))
        {
            registered.Add(GlobalHotkeyService.GetHotkeyDisplayText(
                AppSettings.HotkeyPauseModifier, AppSettings.HotkeyPauseKey));
        }
        
        if (registered.Count > 0)
        {
            HotkeyStatus = $"快捷键: {string.Join(", ", registered)}";
            OnLog($"已注册全局快捷键: {string.Join(", ", registered)}", 1);
        }
    }

    private void OnHotkeyTriggered(string name)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            ToastManager.Info($"快捷键触发: {name}", "快捷键");
        });
    }

    /// <summary>
    /// 启动CD更新定时器
    /// </summary>
    private void StartCooldownTimer()
    {
        _cooldownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _cooldownTimer.Tick += (s, e) => UpdateCooldownDisplay();
        _cooldownTimer.Start();
    }

    /// <summary>
    /// 更新CD显示
    /// </summary>
    private void UpdateCooldownDisplay()
    {
        for (int i = 0; i < SkillStatusList.Count && i < Skills.Count; i++)
        {
            var skill = Skills[i];
            var statusItem = SkillStatusList[i];
            
            var record = _engine.CooldownTracker.GetRecord(skill.Name);
            if (record != null)
            {
                statusItem.CooldownRemaining = record.RemainingCooldown;
                statusItem.UseCount = record.TotalUseCount;
            }
        }
    }

    /// <summary>
    /// 检查是否需要显示首次使用引导
    /// </summary>
    private void CheckFirstTimeGuide()
    {
        var region = AppSettings.DetectionRegion;
        if (region.All(v => v == 0))
        {
            ShowGuideTip = true;
        }
    }

    [RelayCommand]
    private void DismissGuideTip()
    {
        ShowGuideTip = false;
    }

    private void ShowOverlay()
    {
        if (_overlay == null)
        {
            _overlay = new OverlayWindow();
            
            // 从设置恢复位置和透明度
            _overlay.InitializeFromSettings(AppSettings);
            
            // 设置配置方案列表
            _overlay.SetProfiles(_config.GetAvailableProfiles());
            
            // 绑定悬浮窗事件
            _overlay.OnStartStopRequested += () =>
            {
                if (IsRunning) StopEngine();
                else StartEngine();
            };
            _overlay.OnPauseRequested += () => PauseEngine();
            _overlay.OnHideRequested += () => HideOverlay();
            
            // 配置方案切换
            _overlay.OnProfileSwitchRequested += (profile) =>
            {
                _config.SwitchProfile(profile);
                LoadSkills();
                SelectedProfile = profile;
                ToastManager.Success($"已切换到方案: {profile}", "方案切换");
            };
            
            // 位置变化时保存
            _overlay.OnPositionChanged += (left, top, opacity) =>
            {
                AppSettings.OverlayLeft = left;
                AppSettings.OverlayTop = top;
                AppSettings.OverlayOpacity = opacity;
                _config.SaveAppSettings();
            };
            
            _overlay.Show();
        }
    }

    private void HideOverlay()
    {
        if (_overlay != null)
        {
            _overlay.OnStartStopRequested -= () => { };
            _overlay.OnPauseRequested -= () => { };
            _overlay.OnHideRequested -= () => { };
            _overlay.Close();
            _overlay = null;
        }
    }

    private void LoadSkills()
    {
        Skills = new ObservableCollection<SkillConfig>(_config.Skills);
        if (Skills.Count > 0) SelectedSkill = Skills[0];
        InitializeSkillStatusList();
    }

    private void InitializeSkillStatusList()
    {
        SkillStatusList.Clear();
        foreach (var skill in Skills)
        {
            SkillStatusList.Add(new SkillStatusItem
            {
                Name = skill.Name,
                Status = skill.Enabled ? "Ready" : "Disabled",
                StatusText = skill.Enabled ? "就绪" : "已禁用",
                StatusIcon = skill.Enabled ? "✅" : "⬜"
            });
        }
    }

    private void RefreshProfiles() => AvailableProfiles = new ObservableCollection<string>(_config.GetAvailableProfiles());

    private void OnStatusChanged(EngineStatus s)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            IsRunning = s.IsRunning; IsPaused = s.IsPaused; StatusText = s.Mode;
            ExecutionCount = s.ExecutionCount; AvgResponseTime = s.AvgResponseTime; SuccessRate = s.SuccessRate;
            
            // 获取下一个技能名称
            _nextSkillName = s.NextSkillName ?? "";
            _currentHpPercent = s.HpPercent;
            _currentMpPercent = s.MpPercent;
            
            // 更新悬浮窗
            _overlay?.UpdateStatus(s.Mode, s.ExecutionCount, s.AvgResponseTime * 1000, 
                _nextSkillName, _currentHpPercent, _currentMpPercent);
            
            // 更新技能状态列表
            UpdateSkillStatusFromEngine(s);
        });
    }

    private void UpdateSkillStatusFromEngine(EngineStatus s)
    {
        for (int i = 0; i < SkillStatusList.Count && i < Skills.Count; i++)
        {
            var skill = Skills[i];
            var statusItem = SkillStatusList[i];
            
            if (!skill.Enabled)
            {
                statusItem.Status = "Disabled";
                statusItem.StatusText = "已禁用";
                statusItem.StatusIcon = "⬜";
            }
            else if (s.IsRunning && !s.IsPaused)
            {
                // 检查是否是下一个要释放的技能
                if (skill.Name == _nextSkillName)
                {
                    statusItem.Status = "Ready";
                    statusItem.StatusText = "下一个";
                    statusItem.StatusIcon = "🎯";
                }
                else
                {
                    statusItem.Status = "Ready";
                    statusItem.StatusText = "就绪";
                    statusItem.StatusIcon = "✅";
                }
            }
            else
            {
                statusItem.Status = "Ready";
                statusItem.StatusText = "待命";
                statusItem.StatusIcon = "⏸️";
            }
        }
    }

    private void OnLog(string msg, int level)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            LogMessages.Insert(0, msg);
            while (LogMessages.Count > 500) LogMessages.RemoveAt(LogMessages.Count - 1);
        });
    }

    private void StartMemoryMonitor()
    {
        _memoryTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _memoryTimer.Tick += (s, e) =>
        {
            try
            {
                var mem = Process.GetCurrentProcess().WorkingSet64 / 1024.0 / 1024.0;
                MemoryStats = $"内存: {mem:F1} MB";
            }
            catch { /* 忽略内存读取错误 */ }
        };
        _memoryTimer.Start();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _memoryTimer?.Stop();
        _memoryTimer = null;
        
        _cooldownTimer?.Stop();
        _cooldownTimer = null;
        
        // 清理临时模板缓存
        foreach (var mat in _tempTemplateCache.Values)
        {
            if (!mat.IsDisposed) mat.Dispose();
        }
        _tempTemplateCache.Clear();
        
        _hotkeyService.Dispose();
        _engine.Stop();
        HideOverlay();
        
        GC.SuppressFinalize(this);
    }

    [RelayCommand] private void StartEngine() => _engine.Start();
    [RelayCommand] private void StopEngine() => _engine.Stop();
    [RelayCommand] private void PauseEngine() => _engine.TogglePause();
    
    [RelayCommand] 
    private void ToggleOverlay() 
    { 
        if (_overlay == null) 
            ShowOverlay(); 
        else 
            HideOverlay(); 
    }

    /// <summary>
    /// 一键截取技能模板
    /// </summary>
    [RelayCommand]
    private void CaptureSkillTemplate(SkillConfig? skill)
    {
        if (skill == null) return;
        
        if (skill.IconRegion.All(v => v == 0))
        {
            ToastManager.Warning("请先设置技能图标区域", "截取失败");
            return;
        }
        
        var path = _templateCapture.CaptureSkillTemplate(skill.Name, skill.IconRegion);
        if (path != null)
        {
            skill.TemplatePath = path;
            OnLog($"已截取技能[{skill.Name}]模板: {path}", 1);
            ToastManager.Success($"模板已保存", "截取成功");
        }
        else
        {
            ToastManager.Error("截取失败，请检查区域设置", "截取失败");
        }
    }

    /// <summary>
    /// 测试模板匹配（全屏搜索模板位置）
    /// 首次点击：从框选区域截取临时模板并缓存，然后全屏搜索
    /// 后续点击：使用缓存的临时模板在当前画面中搜索新位置
    /// </summary>
    [RelayCommand]
    private void TestTemplateMatch(SkillConfig? skill)
    {
        if (skill == null) return;
        
        if (skill.IconRegion.All(v => v == 0))
        {
            ToastManager.Warning("请先设置技能图标区域", "测试失败");
            return;
        }
        
        try
        {
            var region = skill.IconRegion;
            int x = region[0], y = region[1], w = region[2], h = region[3];
            string cacheKey = $"{skill.Name}_{x}_{y}_{w}_{h}";
            
            OpenCvSharp.Mat? template = null;
            bool isNewTemplate = false;
            
            // 检查是否有缓存的临时模板
            if (_tempTemplateCache.TryGetValue(cacheKey, out var cachedTemplate) && !cachedTemplate.IsDisposed)
            {
                template = cachedTemplate;
                OnLog($"使用缓存的临时模板进行搜索", 1);
            }
            else
            {
                // 首次测试：截取模板区域作为临时模板
                template = _imageInterface.GetScreenRegion(x, y, w, h);
                if (template == null)
                {
                    ToastManager.Error("截图失败，请检查区域设置", "测试失败");
                    return;
                }
                
                // 清除旧的缓存（如果有）
                if (_tempTemplateCache.TryGetValue(cacheKey, out var oldTemplate))
                {
                    if (!oldTemplate.IsDisposed) oldTemplate.Dispose();
                    _tempTemplateCache.Remove(cacheKey);
                }
                
                // 缓存新模板（克隆一份，因为原始的可能被回收）
                _tempTemplateCache[cacheKey] = template.Clone();
                isNewTemplate = true;
                OnLog($"已截取临时模板并缓存，开始全屏搜索", 1);
            }
            
            // 等待一小段时间（仅首次截取时）
            if (isNewTemplate)
                System.Threading.Thread.Sleep(100);
            
            // 全屏截图
            int screenW = (int)SystemParameters.PrimaryScreenWidth;
            int screenH = (int)SystemParameters.PrimaryScreenHeight;
            var fullScreen = _imageInterface.GetScreenRegion(0, 0, screenW, screenH);
            if (fullScreen == null)
            {
                if (isNewTemplate) _imageInterface.ReturnMat(template);
                ToastManager.Error("全屏截图失败", "测试失败");
                return;
            }
            
            // 在全屏中查找模板
            using var result = new OpenCvSharp.Mat();
            OpenCvSharp.Cv2.MatchTemplate(fullScreen, template, result, OpenCvSharp.TemplateMatchModes.CCoeffNormed);
            OpenCvSharp.Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);
            
            // 找到的位置就是屏幕坐标
            int foundX = maxLoc.X;
            int foundY = maxLoc.Y;
            
            // 释放资源（仅释放首次截取的原始模板，缓存的不释放）
            if (isNewTemplate) _imageInterface.ReturnMat(template);
            _imageInterface.ReturnMat(fullScreen);
            
            // 显示红色闪烁框标记找到的位置
            RegionHighlightWindow.ShowHighlight(foundX, foundY, w, h, 5);
            
            // 显示结果
            var coordInfo = $"找到位置: X={foundX}, Y={foundY}, W={w}, H={h}";
            var offset = Math.Abs(foundX - x) + Math.Abs(foundY - y);
            
            OnLog($"模板测试 [{skill.Name}]: 相似度={maxVal:P1}, {coordInfo}, 偏移={offset}px", 1);
            
            if (maxVal >= 0.95)
                ToastManager.Success($"相似度: {maxVal:P1}\n{coordInfo}\n匹配成功，可以截取保存", "测试通过");
            else if (maxVal >= 0.8)
                ToastManager.Info($"相似度: {maxVal:P1}\n{coordInfo}", "测试通过");
            else
                ToastManager.Warning($"相似度: {maxVal:P1}\n{coordInfo}\n匹配度较低，建议重新框选", "测试警告");
        }
        catch (Exception ex)
        {
            OnLog($"模板测试异常: {ex.Message}", 2);
            ToastManager.Error(ex.Message, "测试异常");
        }
    }
    
    /// <summary>
    /// 清除指定技能的临时模板缓存（框选新区域时调用）
    /// </summary>
    private void ClearTempTemplateCache(SkillConfig skill)
    {
        var keysToRemove = _tempTemplateCache.Keys
            .Where(k => k.StartsWith($"{skill.Name}_"))
            .ToList();
        
        foreach (var key in keysToRemove)
        {
            if (_tempTemplateCache.TryGetValue(key, out var mat) && !mat.IsDisposed)
                mat.Dispose();
            _tempTemplateCache.Remove(key);
        }
    }

    /// <summary>
    /// 测试HP检测（智能分析颜色）
    /// </summary>
    [RelayCommand]
    private void TestHpDetection()
    {
        var region = AppSettings.HealthBarRegion;
        if (region.All(v => v == 0))
        {
            ToastManager.Warning("请先设置HP条区域", "测试失败");
            return;
        }
        
        try
        {
            // 分析主色调并自动配置
            var (hueMin, hueMax, satMin, valMin) = AnalyzeBarColor(region);
            
            if (hueMin >= 0)
            {
                // 判断是红色还是绿色血条
                if (hueMin <= 20 || hueMin >= 160) // 红色范围
                {
                    AppSettings.HealthHueMin = hueMin >= 160 ? 0 : hueMin;
                    AppSettings.HealthHueMax = hueMin >= 160 ? 180 : Math.Min(hueMax, 20);
                }
                else // 其他颜色（绿色等）
                {
                    AppSettings.HealthGreenHueMin = hueMin;
                    AppSettings.HealthGreenHueMax = hueMax;
                }
                AppSettings.HealthSatMin = satMin;
                AppSettings.HealthValMin = valMin;
                
                OnLog($"HP颜色自动配置: H={hueMin}-{hueMax}, S>={satMin}, V>={valMin}", 1);
            }
            
            // 使用新配置检测
            var percent = DetectBarPercent(region, isHealth: true);
            
            // 显示高亮框
            RegionHighlightWindow.ShowHighlight(region[0], region[1], region[2], region[3], 3);
            
            OnLog($"HP检测测试: {percent:F1}%", 1);
            ToastManager.Success($"当前HP: {percent:F1}%\n已自动配置颜色范围\nH={hueMin}-{hueMax}, S>={satMin}, V>={valMin}", "HP检测");
        }
        catch (Exception ex)
        {
            OnLog($"HP检测异常: {ex.Message}", 2);
            ToastManager.Error(ex.Message, "检测失败");
        }
    }
    
    /// <summary>
    /// 测试MP检测（智能分析颜色）
    /// </summary>
    [RelayCommand]
    private void TestMpDetection()
    {
        var region = AppSettings.ManaBarRegion;
        if (region.All(v => v == 0))
        {
            ToastManager.Warning("请先设置MP条区域", "测试失败");
            return;
        }
        
        try
        {
            // 分析主色调并自动配置
            var (hueMin, hueMax, satMin, valMin) = AnalyzeBarColor(region);
            
            if (hueMin >= 0)
            {
                AppSettings.ManaHueMin = hueMin;
                AppSettings.ManaHueMax = hueMax;
                AppSettings.ManaSatMin = satMin;
                AppSettings.ManaValMin = valMin;
                
                OnLog($"MP颜色自动配置: H={hueMin}-{hueMax}, S>={satMin}, V>={valMin}", 1);
            }
            
            // 使用新配置检测
            var percent = DetectBarPercent(region, isHealth: false);
            
            // 显示高亮框
            RegionHighlightWindow.ShowHighlight(region[0], region[1], region[2], region[3], 3);
            
            OnLog($"MP检测测试: {percent:F1}%", 1);
            ToastManager.Success($"当前MP: {percent:F1}%\n已自动配置颜色范围\nH={hueMin}-{hueMax}, S>={satMin}, V>={valMin}", "MP检测");
        }
        catch (Exception ex)
        {
            OnLog($"MP检测异常: {ex.Message}", 2);
            ToastManager.Error(ex.Message, "检测失败");
        }
    }
    
    /// <summary>
    /// 测试目标HP检测（智能分析颜色）
    /// </summary>
    [RelayCommand]
    private void TestTargetHpDetection()
    {
        var region = AppSettings.TargetHealthBarRegion;
        if (region.All(v => v == 0))
        {
            ToastManager.Warning("请先设置目标HP条区域", "测试失败");
            return;
        }
        
        try
        {
            // 使用与自身HP相同的颜色配置检测
            var percent = DetectBarPercent(region, isHealth: true);
            
            // 显示高亮框
            RegionHighlightWindow.ShowHighlight(region[0], region[1], region[2], region[3], 3);
            
            OnLog($"目标HP检测测试: {percent:F1}%", 1);
            ToastManager.Success($"目标HP: {percent:F1}%", "目标HP检测");
        }
        catch (Exception ex)
        {
            OnLog($"目标HP检测异常: {ex.Message}", 2);
            ToastManager.Error(ex.Message, "检测失败");
        }
    }
    
    /// <summary>
    /// 分析区域主色调，返回推荐的HSV范围
    /// </summary>
    private (int hueMin, int hueMax, int satMin, int valMin) AnalyzeBarColor(int[] region)
    {
        if (region.Length < 4 || region[2] <= 0 || region[3] <= 0)
            return (-1, -1, -1, -1);
        
        var frame = _imageInterface.GetScreenRegion(region[0], region[1], region[2], region[3]);
        if (frame == null) return (-1, -1, -1, -1);
        
        try
        {
            using var hsv = new OpenCvSharp.Mat();
            OpenCvSharp.Cv2.CvtColor(frame, hsv, OpenCvSharp.ColorConversionCodes.BGR2HSV);
            
            // 收集所有像素的HSV值
            var hues = new List<int>();
            var sats = new List<int>();
            var vals = new List<int>();
            
            var indexer = hsv.GetGenericIndexer<OpenCvSharp.Vec3b>();
            for (int y = 0; y < hsv.Height; y++)
            {
                for (int x = 0; x < hsv.Width; x++)
                {
                    var pixel = indexer[y, x];
                    int h = pixel.Item0; // 0-180
                    int s = pixel.Item1; // 0-255
                    int v = pixel.Item2; // 0-255
                    
                    // 只统计有颜色的像素（排除灰色/黑色背景）
                    if (s > 50 && v > 50)
                    {
                        hues.Add(h);
                        sats.Add(s);
                        vals.Add(v);
                    }
                }
            }
            
            if (hues.Count < 10)
            {
                OnLog("颜色分析: 有效像素太少，可能框选区域不正确", 2);
                return (-1, -1, -1, -1);
            }
            
            // 计算色相的众数（主色调）
            var hueGroups = hues.GroupBy(h => h / 5 * 5) // 按5度分组
                               .OrderByDescending(g => g.Count())
                               .First();
            int dominantHue = hueGroups.Key;
            
            // 计算色相范围（主色调 ± 15度）
            int hueMin = Math.Max(0, dominantHue - 15);
            int hueMax = Math.Min(180, dominantHue + 20);
            
            // 处理红色跨越0度的情况
            if (dominantHue < 15)
            {
                hueMin = 0;
                hueMax = dominantHue + 15;
            }
            else if (dominantHue > 165)
            {
                hueMin = dominantHue - 15;
                hueMax = 180;
            }
            
            // 计算饱和度和明度的下限（取较低的百分位数）
            sats.Sort();
            vals.Sort();
            int satMin = Math.Max(30, sats[(int)(sats.Count * 0.1)]); // 10%分位数
            int valMin = Math.Max(30, vals[(int)(vals.Count * 0.1)]);
            
            OnLog($"颜色分析完成: 主色调H={dominantHue}, 有效像素={hues.Count}", 1);
            
            return (hueMin, hueMax, satMin, valMin);
        }
        finally
        {
            _imageInterface.ReturnMat(frame);
        }
    }
    
    /// <summary>
    /// 检测血条/蓝条百分比（复用StateDetector的逻辑）
    /// </summary>
    private double DetectBarPercent(int[] region, bool isHealth)
    {
        if (region.Length < 4 || region[2] <= 0 || region[3] <= 0)
            return 100.0;
        
        var frame = _imageInterface.GetScreenRegion(region[0], region[1], region[2], region[3]);
        if (frame == null) return 100.0;
        
        try
        {
            using var hsv = new OpenCvSharp.Mat();
            OpenCvSharp.Cv2.CvtColor(frame, hsv, OpenCvSharp.ColorConversionCodes.BGR2HSV);
            
            using var mask = new OpenCvSharp.Mat();
            if (isHealth)
            {
                using var maskRed = new OpenCvSharp.Mat();
                using var maskGreen = new OpenCvSharp.Mat();
                
                // 红色血条
                OpenCvSharp.Cv2.InRange(hsv, 
                    new OpenCvSharp.Scalar(AppSettings.HealthHueMin, AppSettings.HealthSatMin, AppSettings.HealthValMin), 
                    new OpenCvSharp.Scalar(AppSettings.HealthHueMax, 255, 255), 
                    maskRed);
                // 绿色血条
                OpenCvSharp.Cv2.InRange(hsv, 
                    new OpenCvSharp.Scalar(AppSettings.HealthGreenHueMin, AppSettings.HealthSatMin, AppSettings.HealthValMin), 
                    new OpenCvSharp.Scalar(AppSettings.HealthGreenHueMax, 255, 255), 
                    maskGreen);
                OpenCvSharp.Cv2.BitwiseOr(maskRed, maskGreen, mask);
            }
            else
            {
                // 蓝色蓝条
                OpenCvSharp.Cv2.InRange(hsv, 
                    new OpenCvSharp.Scalar(AppSettings.ManaHueMin, AppSettings.ManaSatMin, AppSettings.ManaValMin), 
                    new OpenCvSharp.Scalar(AppSettings.ManaHueMax, 255, 255), 
                    mask);
            }
            
            var nonZero = OpenCvSharp.Cv2.CountNonZero(mask);
            var total = frame.Width * frame.Height;
            var percent = (double)nonZero / total * 100.0;
            
            return Math.Min(100.0, Math.Max(0.0, percent));
        }
        finally
        {
            _imageInterface.ReturnMat(frame);
        }
    }

    /// <summary>
    /// 一键截取Buff模板（用于Buff库）
    /// </summary>
    [RelayCommand]
    private void CaptureBuffTemplate(BuffConfig? buff)
    {
        if (buff == null) return;
        
        if (buff.IconRegion.All(v => v == 0))
        {
            ToastManager.Warning("请先设置Buff图标区域", "截取失败");
            return;
        }
        
        var path = _templateCapture.CaptureBuffTemplate(buff.Name, buff.IconRegion);
        if (path != null)
        {
            buff.TemplatePath = path;
            OnLog($"已截取Buff[{buff.Name}]模板: {path}", 1);
            ToastManager.Success($"模板已保存", "截取成功");
        }
        else
        {
            ToastManager.Error("截取失败，请检查区域设置", "截取失败");
        }
    }

    /// <summary>
    /// 打开模板目录
    /// </summary>
    [RelayCommand]
    private void OpenTemplateFolder()
    {
        try
        {
            Process.Start("explorer.exe", _templateCapture.TemplateDirectory);
        }
        catch (Exception ex)
        {
            OnLog($"打开目录失败: {ex.Message}", 2);
        }
    }

    /// <summary>
    /// 重新注册快捷键
    /// </summary>
    [RelayCommand]
    private void ReregisterHotkeys()
    {
        _hotkeyService.UnregisterAll();
        RegisterHotkeys();
        ToastManager.Success("快捷键已重新注册", "快捷键");
    }

    /// <summary>
    /// 获取技能统计信息
    /// </summary>
    [RelayCommand]
    private void ShowSkillStatistics(SkillConfig? skill)
    {
        if (skill == null) return;
        
        var stats = _engine.GetSkillStatistics(skill.Name);
        var message = $"技能: {stats.SkillName}\n" +
                      $"使用次数: {stats.TotalUseCount}\n" +
                      $"平均CD: {stats.AverageCooldown:F1}秒\n" +
                      $"最短CD: {stats.MinCooldown:F1}秒\n" +
                      $"最长CD: {stats.MaxCooldown:F1}秒";
        
        System.Windows.MessageBox.Show(message, "技能统计", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand] private void AddSkill() 
    { 
        var s = new SkillConfig { Name = $"新技能{Skills.Count + 1}", KeyCode = 48 + Skills.Count + 1, Enabled = true }; 
        Skills.Add(s); 
        SelectedSkill = s;
        SkillStatusList.Add(new SkillStatusItem { Name = s.Name, Status = "Ready", StatusText = "就绪", StatusIcon = "✅" });
        _hasUnsavedChanges = true;
    }

    /// <summary>
    /// 复制当前选中的技能
    /// </summary>
    [RelayCommand]
    private void DuplicateSkill()
    {
        if (SelectedSkill == null) return;
        
        var copy = new SkillConfig
        {
            Name = $"{SelectedSkill.Name}_副本",
            KeyCode = SelectedSkill.KeyCode,
            Priority = SelectedSkill.Priority - 1,
            Enabled = SelectedSkill.Enabled,
            IconRegion = (int[])SelectedSkill.IconRegion.Clone(),
            TemplatePath = SelectedSkill.TemplatePath,
            SimilarityThreshold = SelectedSkill.SimilarityThreshold,
            MinMp = SelectedSkill.MinMp,
            HpCheckTarget = SelectedSkill.HpCheckTarget,
            HpThreshold = SelectedSkill.HpThreshold,
            RequireTarget = SelectedSkill.RequireTarget,
            Cooldown = SelectedSkill.Cooldown,
            PreCastKeyCode = SelectedSkill.PreCastKeyCode,
            PreCastConditionBuff = SelectedSkill.PreCastConditionBuff,
            ComboDelay = SelectedSkill.ComboDelay
        };
        
        var index = Skills.IndexOf(SelectedSkill);
        Skills.Insert(index + 1, copy);
        SkillStatusList.Insert(index + 1, new SkillStatusItem { Name = copy.Name, Status = "Ready", StatusText = "就绪", StatusIcon = "✅" });
        SelectedSkill = copy;
        _hasUnsavedChanges = true;
        
        ToastManager.Success($"已复制技能: {copy.Name}", "复制成功");
    }

    /// <summary>
    /// 批量启用所有技能
    /// </summary>
    [RelayCommand]
    private void EnableAllSkills()
    {
        foreach (var skill in Skills)
            skill.Enabled = true;
        
        for (int i = 0; i < SkillStatusList.Count; i++)
        {
            SkillStatusList[i].Status = "Ready";
            SkillStatusList[i].StatusText = "就绪";
            SkillStatusList[i].StatusIcon = "✅";
        }
        
        _hasUnsavedChanges = true;
        ToastManager.Info($"已启用全部 {Skills.Count} 个技能", "批量操作");
    }

    /// <summary>
    /// 批量禁用所有技能
    /// </summary>
    [RelayCommand]
    private void DisableAllSkills()
    {
        foreach (var skill in Skills)
            skill.Enabled = false;
        
        for (int i = 0; i < SkillStatusList.Count; i++)
        {
            SkillStatusList[i].Status = "Disabled";
            SkillStatusList[i].StatusText = "已禁用";
            SkillStatusList[i].StatusIcon = "⬜";
        }
        
        _hasUnsavedChanges = true;
        ToastManager.Info($"已禁用全部 {Skills.Count} 个技能", "批量操作");
    }

    /// <summary>
    /// 一键框选并截取技能模板
    /// </summary>
    [RelayCommand]
    private void QuickCaptureSkillTemplate(SkillConfig? skill)
    {
        if (skill == null) return;
        
        var sel = new RegionSelectorWindow();
        if (sel.ShowDialog() != true) return;
        
        var r = sel.SelectedRegion;
        skill.IconRegion = [r.X, r.Y, r.Width, r.Height];
        
        // 自动截取模板
        var path = _templateCapture.CaptureSkillTemplate(skill.Name, skill.IconRegion);
        if (path != null)
        {
            skill.TemplatePath = path;
            OnLog($"已框选并截取技能[{skill.Name}]模板", 1);
            ToastManager.Success($"区域已设置，模板已截取", "一键配置");
            _hasUnsavedChanges = true;
        }
    }

    // 未保存变更标记
    private bool _hasUnsavedChanges;
    
    /// <summary>
    /// 检查是否有未保存的变更
    /// </summary>
    public bool HasUnsavedChanges => _hasUnsavedChanges;

    /// <summary>
    /// 提示保存变更
    /// </summary>
    public bool PromptSaveChanges()
    {
        if (!_hasUnsavedChanges) return true;
        
        var result = System.Windows.MessageBox.Show(
            "技能配置已修改但未保存，是否保存？",
            "保存确认",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            SaveConfig();
            return true;
        }
        else if (result == MessageBoxResult.No)
        {
            _hasUnsavedChanges = false;
            return true;
        }
        
        return false; // Cancel
    }
    
    [RelayCommand] private void DeleteSkill() 
    { 
        if (SelectedSkill == null) return; 
        var i = Skills.IndexOf(SelectedSkill); 
        Skills.Remove(SelectedSkill);
        if (i < SkillStatusList.Count) SkillStatusList.RemoveAt(i);
        if (Skills.Count > 0) SelectedSkill = Skills[Math.Min(i, Skills.Count - 1)];
        _hasUnsavedChanges = true;
    }
    
    [RelayCommand] private void MoveSkillUp() 
    { 
        if (SelectedSkill == null) return; 
        var i = Skills.IndexOf(SelectedSkill); 
        if (i > 0) 
        {
            Skills.Move(i, i - 1);
            SkillStatusList.Move(i, i - 1);
            _hasUnsavedChanges = true;
        }
    }
    
    [RelayCommand] private void MoveSkillDown() 
    { 
        if (SelectedSkill == null) return; 
        var i = Skills.IndexOf(SelectedSkill); 
        if (i < Skills.Count - 1) 
        {
            Skills.Move(i, i + 1);
            SkillStatusList.Move(i, i + 1);
            _hasUnsavedChanges = true;
        }
    }
    
    /// <summary>
    /// 添加释放条件
    /// </summary>
    [RelayCommand]
    private void AddReleaseCondition(SkillConfig? skill)
    {
        if (skill == null) return;
        skill.ShowReleaseCondition = true;
        _hasUnsavedChanges = true;
    }
    
    /// <summary>
    /// 移除释放条件
    /// </summary>
    [RelayCommand]
    private void RemoveReleaseCondition(SkillConfig? skill)
    {
        if (skill == null) return;
        skill.ShowReleaseCondition = false;
        skill.MinMp = 0;
        skill.HpCheckTarget = 0;
        skill.HpThreshold = 0;
        _hasUnsavedChanges = true;
    }
    
    /// <summary>
    /// 添加联动配置
    /// </summary>
    [RelayCommand]
    private void AddComboConfig(SkillConfig? skill)
    {
        if (skill == null) return;
        skill.ShowComboConfig = true;
        _hasUnsavedChanges = true;
    }
    
    /// <summary>
    /// 移除联动配置
    /// </summary>
    [RelayCommand]
    private void RemoveComboConfig(SkillConfig? skill)
    {
        if (skill == null) return;
        skill.ShowComboConfig = false;
        skill.PreCastKeyCode = 0;
        skill.PreCastConditionBuff = "";
        skill.ComboDelay = 100;
        _hasUnsavedChanges = true;
    }

    [RelayCommand]
    private void CaptureKey(SkillConfig? skill)
    {
        if (skill == null) return;
        var window = new KeyCaptureWindow { Owner = System.Windows.Application.Current.MainWindow };
        if (window.ShowDialog() == true)
        {
            skill.KeyCode = window.CapturedKeyCode;
            OnLog($"设置技能[{skill.Name}]按键: {window.CapturedKeyName} (VK={window.CapturedKeyCode})", 1);
        }
    }

    [RelayCommand]
    private void CapturePreCastKey(SkillConfig? skill)
    {
        if (skill == null) return;
        var window = new KeyCaptureWindow { Owner = System.Windows.Application.Current.MainWindow };
        if (window.ShowDialog() == true)
        {
            skill.PreCastKeyCode = window.CapturedKeyCode;
            OnLog($"设置技能[{skill.Name}]前置按键: {window.CapturedKeyName} (VK={window.CapturedKeyCode})", 1);
        }
    }

    [RelayCommand]
    private void SelectRegion(object? p)
    {
        var sel = new RegionSelectorWindow();
        if (sel.ShowDialog() != true) return;
        var r = sel.SelectedRegion;
        var arr = new int[] { r.X, r.Y, r.Width, r.Height };
        
        if (p is SkillConfig sk) 
        { 
            // 框选新区域时清除临时模板缓存
            ClearTempTemplateCache(sk);
            sk.IconRegion = arr; 
            OnLog($"设置技能[{sk.Name}]区域: {r.X},{r.Y},{r.Width},{r.Height}", 1);
            ShowRegionPreview(arr, $"技能[{sk.Name}]区域预览", region => sk.IconRegion = region);
        }
        else if (p is BuffConfig bf) 
        { 
            bf.IconRegion = arr; 
            OnLog($"设置Buff[{bf.Name}]区域: {r.X},{r.Y},{r.Width},{r.Height}", 1);
            ShowRegionPreview(arr, $"Buff[{bf.Name}]区域预览", region => bf.IconRegion = region);
        }
        else if (p is string t)
        {
            switch (t)
            {
                case "Detection": 
                    AppSettings.DetectionRegion = arr;
                    ShowRegionPreview(arr, "检测区域预览", region => AppSettings.DetectionRegion = region);
                    // 配置了检测区域后隐藏引导提示
                    ShowGuideTip = false;
                    break;
                case "HealthBar": 
                    AppSettings.HealthBarRegion = arr;
                    ShowRegionPreview(arr, "自身血条区域预览", region => AppSettings.HealthBarRegion = region);
                    break;
                case "ManaBar": 
                    AppSettings.ManaBarRegion = arr;
                    ShowRegionPreview(arr, "自身蓝条区域预览", region => AppSettings.ManaBarRegion = region);
                    break;
                case "TargetHealthBar": 
                    AppSettings.TargetHealthBarRegion = arr;
                    ShowRegionPreview(arr, "目标血条区域预览", region => AppSettings.TargetHealthBarRegion = region);
                    break;
            }
            OnLog($"设置{t}区域: {r.X},{r.Y},{r.Width},{r.Height}", 1);
        }
    }

    private void ShowRegionPreview(int[] region, string title, Action<int[]>? onConfirm = null)
    {
        if (region.All(v => v == 0)) return;
        
        var preview = new RegionPreviewWindow(region)
        {
            Title = title,
            Owner = System.Windows.Application.Current.MainWindow
        };
        
        if (preview.ShowDialog() == true && onConfirm != null)
        {
            onConfirm(preview.Region);
            OnLog($"区域已更新: X={preview.Region[0]}, Y={preview.Region[1]}, W={preview.Region[2]}, H={preview.Region[3]}", 1);
        }
    }

    [RelayCommand]
    private void PreviewRegion(SkillConfig? skill)
    {
        if (skill == null) return;
        ShowRegionPreview(skill.IconRegion, $"技能[{skill.Name}]区域预览", region => skill.IconRegion = region);
    }

    [RelayCommand]
    private void PreviewBuffRegion(BuffConfig? buff)
    {
        if (buff == null) return;
        ShowRegionPreview(buff.IconRegion, $"Buff[{buff.Name}]区域预览", region => buff.IconRegion = region);
    }

    [RelayCommand]
    private void PreviewSettingsRegion(string? regionType)
    {
        if (string.IsNullOrEmpty(regionType)) return;
        
        int[] region = regionType switch
        {
            "Detection" => AppSettings.DetectionRegion,
            "HealthBar" => AppSettings.HealthBarRegion,
            "ManaBar" => AppSettings.ManaBarRegion,
            "TargetHealthBar" => AppSettings.TargetHealthBarRegion,
            _ => [0, 0, 0, 0]
        };
        
        ShowRegionPreview(region, $"{regionType}区域预览", r =>
        {
            switch (regionType)
            {
                case "Detection": AppSettings.DetectionRegion = r; break;
                case "HealthBar": AppSettings.HealthBarRegion = r; break;
                case "ManaBar": AppSettings.ManaBarRegion = r; break;
                case "TargetHealthBar": AppSettings.TargetHealthBarRegion = r; break;
            }
        });
    }

    [RelayCommand]
    private void SelectPoint(string? t)
    {
        var sel = new RegionSelectorWindow(pointMode: true);
        if (sel.ShowDialog() != true) return;
        var p = sel.SelectedPoint;
        var x = (int)p.X; var y = (int)p.Y;
        if (t == "GlobalCd") { AppSettings.GlobalCdPoint = [x, y]; OnLog($"设置公共CD点: {x},{y}", 1); }
    }

    [RelayCommand]
    private void SelectTemplateFile(object? p)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "图片|*.png;*.jpg;*.bmp|所有|*.*", Title = "选择模板" };
        if (dlg.ShowDialog() != true) return;
        if (p is SkillConfig sk) { sk.TemplatePath = dlg.FileName; OnLog($"设置技能[{sk.Name}]模板", 1); }
        else if (p is BuffConfig bf) { bf.TemplatePath = dlg.FileName; OnLog($"设置Buff[{bf.Name}]模板", 1); }
    }

    [RelayCommand] 
    private void SaveConfig() 
    { 
        var errors = ValidateConfig();
        if (errors.Count > 0)
        {
            foreach (var err in errors) OnLog($"配置警告: {err}", 2);
            Views.ToastManager.Warning($"配置已保存，但有 {errors.Count} 个警告", "配置保存");
        }
        else
        {
            Views.ToastManager.Success("所有配置已保存", "配置保存");
        }
        
        _config.Skills.Clear(); 
        _config.Skills.AddRange(Skills); 
        _config.SaveAll(); 
        OnLog("配置已保存", 1);
        
        for (int i = 0; i < Skills.Count && i < SkillStatusList.Count; i++)
        {
            SkillStatusList[i].Name = Skills[i].Name;
        }
        
        _hasUnsavedChanges = false;
    }

    #region 配置导入导出

    [RelayCommand]
    private void ExportConfig()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "配置包|*.zip",
            FileName = $"ShineProCS_Config_{DateTime.Now:yyyyMMdd}",
            Title = "导出配置"
        };
        
        if (dlg.ShowDialog() != true) return;
        
        try
        {
            _config.ExportConfig(dlg.FileName, includeTemplates: true);
            ToastManager.Success("配置已导出", "导出成功");
            OnLog($"配置已导出到: {dlg.FileName}", 1);
        }
        catch (Exception ex)
        {
            ToastManager.Error($"导出失败: {ex.Message}", "导出错误");
            OnLog($"导出失败: {ex.Message}", 3);
        }
    }

    [RelayCommand]
    private void ImportConfig()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "配置包|*.zip",
            Title = "导入配置"
        };
        
        if (dlg.ShowDialog() != true) return;
        
        var result = System.Windows.MessageBox.Show(
            "导入配置将覆盖现有配置，是否继续？\n（现有配置会自动备份）",
            "确认导入",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        
        if (result != MessageBoxResult.Yes) return;
        
        try
        {
            var msg = _config.ImportConfig(dlg.FileName, overwrite: true);
            LoadSkills();
            RefreshProfiles();
            ToastManager.Success(msg, "导入成功");
            OnLog(msg, 1);
        }
        catch (Exception ex)
        {
            ToastManager.Error($"导入失败: {ex.Message}", "导入错误");
            OnLog($"导入失败: {ex.Message}", 3);
        }
    }

    [RelayCommand]
    private void ExportProfile()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "方案配置|*.zip",
            FileName = $"ShineProCS_Profile_{SelectedProfile}_{DateTime.Now:yyyyMMdd}",
            Title = "导出当前方案"
        };
        
        if (dlg.ShowDialog() != true) return;
        
        try
        {
            _config.ExportProfile(SelectedProfile, dlg.FileName);
            ToastManager.Success($"方案 [{SelectedProfile}] 已导出", "导出成功");
            OnLog($"方案已导出到: {dlg.FileName}", 1);
        }
        catch (Exception ex)
        {
            ToastManager.Error($"导出失败: {ex.Message}", "导出错误");
        }
    }

    #endregion

    #region 快捷键配置

    /// <summary>
    /// 启动/停止快捷键修饰键索引（用于ComboBox绑定）
    /// </summary>
    public int StartStopModifierIndex
    {
        get => ModifierToIndex(AppSettings.HotkeyStartStopModifier);
        set { AppSettings.HotkeyStartStopModifier = IndexToModifier(value); OnPropertyChanged(); }
    }

    /// <summary>
    /// 暂停快捷键修饰键索引
    /// </summary>
    public int PauseModifierIndex
    {
        get => ModifierToIndex(AppSettings.HotkeyPauseModifier);
        set { AppSettings.HotkeyPauseModifier = IndexToModifier(value); OnPropertyChanged(); }
    }

    private static int ModifierToIndex(uint modifier) => modifier switch
    {
        0 => 0,  // 无
        2 => 1,  // Ctrl
        1 => 2,  // Alt
        4 => 3,  // Shift
        _ => 0
    };

    private static uint IndexToModifier(int index) => index switch
    {
        0 => 0,  // 无
        1 => 2,  // Ctrl
        2 => 1,  // Alt
        3 => 4,  // Shift
        _ => 0
    };

    [RelayCommand]
    private void CaptureHotkey(string? hotkeyType)
    {
        if (string.IsNullOrEmpty(hotkeyType)) return;
        
        var window = new KeyCaptureWindow { Owner = System.Windows.Application.Current.MainWindow };
        if (window.ShowDialog() != true) return;
        
        var keyCode = (uint)window.CapturedKeyCode;
        
        switch (hotkeyType)
        {
            case "StartStop":
                AppSettings.HotkeyStartStopKey = keyCode;
                break;
            case "Pause":
                AppSettings.HotkeyPauseKey = keyCode;
                break;
        }
        
        OnLog($"设置快捷键 [{hotkeyType}]: {window.CapturedKeyName}", 1);
        OnPropertyChanged(nameof(AppSettings));
    }

    #endregion
    
    private List<string> ValidateConfig()
    {
        var errors = new List<string>();
        
        foreach (var skill in Skills)
        {
            if (skill.KeyCode <= 0 || skill.KeyCode > 255)
                errors.Add($"技能[{skill.Name}]按键码无效: {skill.KeyCode}");
            
            if (skill.Priority < 0)
                errors.Add($"技能[{skill.Name}]优先级不能为负数");
            
            if (skill.IconRegion.Any(v => v < 0))
                errors.Add($"技能[{skill.Name}]检测区域坐标不能为负数");
            
            if (skill.SimilarityThreshold < 0 || skill.SimilarityThreshold > 1)
                errors.Add($"技能[{skill.Name}]相似度阈值应在0-1之间");
            
            if (skill.MinMp < 0 || skill.MinMp > 100)
                errors.Add($"技能[{skill.Name}]自身MP条件应在0-100之间");
            
            if (skill.HpThreshold < 0 || skill.HpThreshold > 100)
                errors.Add($"技能[{skill.Name}]HP条件应在0-100之间");
            
            if (skill.PreCastKeyCode < 0 || skill.PreCastKeyCode > 255)
                errors.Add($"技能[{skill.Name}]前置技能按键码无效");
            
            if (skill.ComboDelay < 0)
                errors.Add($"技能[{skill.Name}]连招延迟不能为负数");
        }
        
        if (AppSettings.LoopInterval < 10)
            errors.Add("循环间隔不能小于10ms");
        
        if (AppSettings.ImageQueueCapacity < 2 || AppSettings.ImageQueueCapacity > 10)
            errors.Add("图像队列容量应在2-10之间");
        
        return errors;
    }

    [RelayCommand] private void ReloadConfig() { _config.LoadConfigs(); LoadSkills(); OnLog("配置已重载", 1); }
    [RelayCommand] private void SwitchProfile(string? p) { if (string.IsNullOrEmpty(p)) return; _config.SwitchProfile(p); LoadSkills(); OnLog($"切换方案: {p}", 1); }
    [RelayCommand] private void RefreshProfilesCommand() => RefreshProfiles();
    [RelayCommand] private void ClearLogs() => LogMessages.Clear();
    [RelayCommand] private void ForceCleanup() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); OnLog("内存已清理", 1); }

    [RelayCommand]
    private void CreateProfile()
    {
        var name = Microsoft.VisualBasic.Interaction.InputBox("请输入新方案名称:", "新建方案", $"方案{AvailableProfiles.Count}");
        if (string.IsNullOrWhiteSpace(name)) return;
        
        // 检查名称是否已存在
        if (AvailableProfiles.Contains(name))
        {
            ToastManager.Warning($"方案 '{name}' 已存在");
            return;
        }
        
        // 创建新方案文件（复制当前方案）
        _config.CreateProfile(name);
        RefreshProfiles();
        SelectedProfile = name;
        OnLog($"已创建方案: {name}", 1);
        ToastManager.Success($"方案 '{name}' 创建成功");
    }

    [RelayCommand]
    private void DeleteProfile()
    {
        if (SelectedProfile == "默认")
        {
            ToastManager.Warning("默认方案不能删除");
            return;
        }
        
        var result = System.Windows.MessageBox.Show($"确定要删除方案 '{SelectedProfile}' 吗？\n此操作不可恢复！", "删除方案", 
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        
        var deletedName = SelectedProfile;
        _config.DeleteProfile(SelectedProfile);
        RefreshProfiles();
        SelectedProfile = "默认";
        OnLog($"已删除方案: {deletedName}", 1);
        ToastManager.Info($"方案 '{deletedName}' 已删除");
    }
}
