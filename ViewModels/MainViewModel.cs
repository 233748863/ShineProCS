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
    private readonly TemplateCapture _templateCapture;
    private OverlayWindow? _overlay;
    private string _nextSkillName = "";
    private double _currentHpPercent = 100;
    private double _currentMpPercent = 100;
    private DispatcherTimer? _memoryTimer;
    private DispatcherTimer? _cooldownTimer; // CD更新定时器
    private bool _disposed;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private bool _isQiQingMode;
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
    [ObservableProperty] private string _hotkeyStatus = ""; // 快捷键状态显示

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
        
        // 初始化模板截取服务
        _templateCapture = new TemplateCapture(_imageInterface);
        
        // 初始化全局快捷键服务
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

    /// <summary>
    /// 初始化全局快捷键（需要在窗口加载后调用）
    /// </summary>
    public void InitializeHotkeys(Window window)
    {
        _hotkeyService.Initialize(window);
        RegisterHotkeys();
    }

    /// <summary>
    /// 注册所有快捷键
    /// </summary>
    private void RegisterHotkeys()
    {
        if (!AppSettings.EnableGlobalHotkeys) return;
        
        var registered = new List<string>();
        
        // 启动/停止快捷键
        if (_hotkeyService.RegisterHotkey("StartStop", 
            AppSettings.HotkeyStartStopModifier, 
            AppSettings.HotkeyStartStopKey, 
            () => { if (IsRunning) StopEngine(); else StartEngine(); }))
        {
            registered.Add(GlobalHotkeyService.GetHotkeyDisplayText(
                AppSettings.HotkeyStartStopModifier, AppSettings.HotkeyStartStopKey));
        }
        
        // 暂停快捷键
        if (_hotkeyService.RegisterHotkey("Pause",
            AppSettings.HotkeyPauseModifier,
            AppSettings.HotkeyPauseKey,
            () => PauseEngine()))
        {
            registered.Add(GlobalHotkeyService.GetHotkeyDisplayText(
                AppSettings.HotkeyPauseModifier, AppSettings.HotkeyPauseKey));
        }
        
        // 七情模式快捷键
        if (_hotkeyService.RegisterHotkey("QiQing",
            AppSettings.HotkeyQiQingModifier,
            AppSettings.HotkeyQiQingKey,
            () => ToggleQiQingMode()))
        {
            registered.Add(GlobalHotkeyService.GetHotkeyDisplayText(
                AppSettings.HotkeyQiQingModifier, AppSettings.HotkeyQiQingKey));
        }
        
        if (registered.Count > 0)
        {
            HotkeyStatus = $"快捷键: {string.Join(", ", registered)}";
            OnLog($"已注册全局快捷键: {string.Join(", ", registered)}", 1);
        }
    }

    /// <summary>
    /// 快捷键触发回调
    /// </summary>
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
            _overlay.OnQiQingToggleRequested += () => ToggleQiQingMode();
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
            _overlay.OnQiQingToggleRequested -= () => { };
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
            IsQiQingMode = s.IsQiQingInLoop;
            
            // 获取下一个技能名称
            _nextSkillName = s.NextSkillName ?? "";
            _currentHpPercent = s.HpPercent;
            _currentMpPercent = s.MpPercent;
            
            // 更新悬浮窗
            _overlay?.UpdateStatus(s.Mode, s.ExecutionCount, s.AvgResponseTime * 1000, 
                s.IsQianZhiActive, s.IsQiQingInLoop, _nextSkillName, _currentHpPercent, _currentMpPercent);
            
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
        
        _hotkeyService.Dispose();
        _engine.Stop();
        HideOverlay();
        
        GC.SuppressFinalize(this);
    }

    [RelayCommand] private void StartEngine() => _engine.Start();
    [RelayCommand] private void StopEngine() => _engine.Stop();
    [RelayCommand] private void PauseEngine() => _engine.TogglePause();
    
    [RelayCommand] 
    private void ToggleQiQingMode() 
    { 
        if (_engine.IsQiQingInLoop)
        {
            _engine.DisableQiQingLoop();
            IsQiQingMode = false;
        }
        else
        {
            _engine.EnableQiQingLoop();
            IsQiQingMode = true;
        }
    }
    
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
    /// 一键截取Buff模板
    /// </summary>
    [RelayCommand]
    private void CaptureBuffTemplate(BuffRequirement? buff)
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
            MinHp = SelectedSkill.MinHp,
            MinMp = SelectedSkill.MinMp,
            RequireTarget = SelectedSkill.RequireTarget,
            Cooldown = SelectedSkill.Cooldown,
            PreCastKeyCode = SelectedSkill.PreCastKeyCode,
            PreCastConditionBuff = SelectedSkill.PreCastConditionBuff,
            ComboDelay = SelectedSkill.ComboDelay
        };
        
        // 复制 Buff 依赖
        foreach (var buff in SelectedSkill.BuffRequirements)
        {
            copy.BuffRequirements.Add(new BuffRequirement
            {
                Name = buff.Name,
                IconRegion = (int[])buff.IconRegion.Clone(),
                TemplatePath = buff.TemplatePath,
                SimilarityThreshold = buff.SimilarityThreshold,
                IsDebuff = buff.IsDebuff,
                IsRequired = buff.IsRequired
            });
        }
        
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
    
    [RelayCommand] private void AddBuffRequirement() { if (SelectedSkill == null) return; SelectedSkill.BuffRequirements.Add(new BuffRequirement { Name = "新Buff", IsRequired = true }); _hasUnsavedChanges = true; }
    [RelayCommand] private void DeleteBuffRequirement(BuffRequirement? b) { if (SelectedSkill != null && b != null) { SelectedSkill.BuffRequirements.Remove(b); _hasUnsavedChanges = true; } }

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
            sk.IconRegion = arr; 
            OnLog($"设置技能[{sk.Name}]区域: {r.X},{r.Y},{r.Width},{r.Height}", 1);
            ShowRegionPreview(arr, $"技能[{sk.Name}]区域预览", region => sk.IconRegion = region);
        }
        else if (p is BuffRequirement bf) 
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
                    ShowRegionPreview(arr, "血条区域预览", region => AppSettings.HealthBarRegion = region);
                    break;
                case "ManaBar": 
                    AppSettings.ManaBarRegion = arr;
                    ShowRegionPreview(arr, "蓝条区域预览", region => AppSettings.ManaBarRegion = region);
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
    private void PreviewBuffRegion(BuffRequirement? buff)
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
            _ => [0, 0, 0, 0]
        };
        
        ShowRegionPreview(region, $"{regionType}区域预览", r =>
        {
            switch (regionType)
            {
                case "Detection": AppSettings.DetectionRegion = r; break;
                case "HealthBar": AppSettings.HealthBarRegion = r; break;
                case "ManaBar": AppSettings.ManaBarRegion = r; break;
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
        else if (p is BuffRequirement bf) { bf.TemplatePath = dlg.FileName; OnLog($"设置Buff[{bf.Name}]模板", 1); }
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

    /// <summary>
    /// 七情模式快捷键修饰键索引
    /// </summary>
    public int QiQingModifierIndex
    {
        get => ModifierToIndex(AppSettings.HotkeyQiQingModifier);
        set { AppSettings.HotkeyQiQingModifier = IndexToModifier(value); OnPropertyChanged(); }
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
            case "QiQing":
                AppSettings.HotkeyQiQingKey = keyCode;
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
            
            if (skill.MinHp < 0 || skill.MinHp > 100)
                errors.Add($"技能[{skill.Name}]最低HP应在0-100之间");
            
            if (skill.MinMp < 0 || skill.MinMp > 100)
                errors.Add($"技能[{skill.Name}]最低MP应在0-100之间");
            
            if (skill.PreCastKeyCode < 0 || skill.PreCastKeyCode > 255)
                errors.Add($"技能[{skill.Name}]前置技能按键码无效");
            
            if (skill.ComboDelay < 0)
                errors.Add($"技能[{skill.Name}]连招延迟不能为负数");
            
            foreach (var buff in skill.BuffRequirements)
            {
                if (string.IsNullOrWhiteSpace(buff.Name))
                    errors.Add($"技能[{skill.Name}]的Buff名称不能为空");
                
                if (buff.IconRegion.Any(v => v < 0))
                    errors.Add($"技能[{skill.Name}]的Buff[{buff.Name}]区域坐标不能为负数");
            }
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
