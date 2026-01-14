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
    private readonly InputDriverManager _inputDriverManager;
    
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
    
    /// <summary>
    /// 当前页面类型
    /// 需求: 2.1, 2.4 - 导航一致性和页面状态保持
    /// </summary>
    [ObservableProperty] private Type? _currentPageType;
    
    #region 窗口选择器相关属性
    
    private readonly IWindowEnumerationService _windowEnumerationService;
    
    /// <summary>
    /// 可用窗口列表
    /// </summary>
    [ObservableProperty] 
    private ObservableCollection<WindowInfo> _windowList = [];
    
    /// <summary>
    /// 当前选中的窗口
    /// </summary>
    [ObservableProperty] 
    private WindowInfo? _selectedWindow;
    
    /// <summary>
    /// 是否正在刷新窗口列表
    /// </summary>
    [ObservableProperty] 
    private bool _isRefreshingWindows;
    
    /// <summary>
    /// 窗口选择变化时更新配置
    /// </summary>
    partial void OnSelectedWindowChanged(WindowInfo? value)
    {
        if (value != null)
        {
            AppSettings.GameWindowTitle = value.Title;
            OnLog($"已选择窗口: {value.DisplayText}", 1);
        }
    }
    
    /// <summary>
    /// 初始化窗口列表并尝试匹配保存的窗口
    /// </summary>
    private void InitializeWindowList()
    {
        try
        {
            var windows = _windowEnumerationService.GetVisibleWindows();
            WindowList.Clear();
            foreach (var window in windows)
            {
                WindowList.Add(window);
            }
            
            // 尝试匹配保存的窗口标题
            var savedTitle = AppSettings.GameWindowTitle;
            if (!string.IsNullOrEmpty(savedTitle))
            {
                var matchedWindow = _windowEnumerationService.FindWindowByTitle(savedTitle);
                if (matchedWindow != null)
                {
                    // 在列表中找到对应的窗口并选中
                    SelectedWindow = WindowList.FirstOrDefault(w => w.Handle == matchedWindow.Handle);
                    if (SelectedWindow != null)
                    {
                        OnLog($"已自动匹配窗口: {SelectedWindow.DisplayText}", 1);
                    }
                }
                else
                {
                    OnLog($"未找到保存的窗口: {savedTitle}", 2);
                }
            }
        }
        catch (Exception ex)
        {
            OnLog($"初始化窗口列表失败: {ex.Message}", 2);
        }
    }
    
    /// <summary>
    /// 刷新窗口列表命令
    /// </summary>
    [RelayCommand]
    private async Task RefreshWindowListAsync()
    {
        IsRefreshingWindows = true;
        try
        {
            // 保存当前选择的窗口标题
            var currentSelection = SelectedWindow?.Title;
            
            // 异步获取窗口列表
            var windows = await Task.Run(() => _windowEnumerationService.GetVisibleWindows());
            
            // 更新列表
            WindowList.Clear();
            foreach (var window in windows)
            {
                WindowList.Add(window);
            }
            
            // 尝试恢复之前的选择
            if (!string.IsNullOrEmpty(currentSelection))
            {
                SelectedWindow = WindowList.FirstOrDefault(w => w.Title == currentSelection);
                if (SelectedWindow != null)
                {
                    OnLog($"已恢复窗口选择: {SelectedWindow.DisplayText}", 1);
                }
            }
            
            OnLog($"窗口列表已刷新，共 {WindowList.Count} 个窗口", 1);
        }
        catch (Exception ex)
        {
            OnLog($"刷新窗口列表失败: {ex.Message}", 2);
            ToastManager.Error($"刷新失败: {ex.Message}", "窗口列表");
        }
        finally
        {
            IsRefreshingWindows = false;
        }
    }
    
    #endregion
    
    #region 输入驱动相关属性
    
    /// <summary>
    /// 当前选择的驱动索引 (0=Win32, 1=GhostBox)
    /// </summary>
    public int SelectedDriverIndex
    {
        get => (int)_inputDriverManager.CurrentDriverType;
        set
        {
            if (value != (int)_inputDriverManager.CurrentDriverType)
            {
                SwitchInputDriver((InputDriverType)value);
            }
        }
    }
    
    /// <summary>
    /// GhostBox 是否可用
    /// </summary>
    public bool IsGhostBoxAvailable => _inputDriverManager.IsGhostBoxAvailable;
    
    /// <summary>
    /// 是否选择了 GhostBox 驱动
    /// </summary>
    public bool IsGhostBoxSelected => _inputDriverManager.CurrentDriverType == InputDriverType.GhostBox;
    
    /// <summary>
    /// GhostBox 是否已连接
    /// </summary>
    public bool IsGhostBoxConnected => _inputDriverManager.IsGhostBoxConnected;
    
    /// <summary>
    /// GhostBox 连接状态文本
    /// </summary>
    public string GhostBoxConnectionStatus => _inputDriverManager.GhostBoxStatus;
    
    /// <summary>
    /// GhostBox 连接状态颜色
    /// </summary>
    [ObservableProperty]
    private string _ghostBoxConnectionStatusColor = "Gray";
    
    /// <summary>
    /// GhostBox 设备信息
    /// </summary>
    public string GhostBoxDeviceInfo
    {
        get
        {
            if (!_inputDriverManager.IsGhostBoxConnected) return "";
            var model = _inputDriverManager.GhostBoxDeviceModel;
            var serial = _inputDriverManager.GhostBoxSerialNumber;
            if (!string.IsNullOrEmpty(model) || !string.IsNullOrEmpty(serial))
                return $"型号: {model}, 序列号: {serial}";
            return "设备已连接";
        }
    }
    
    #endregion
    
    #region GPU/CPU 切换相关属性（需求 5.1）
    
    /// <summary>
    /// 推理设备索引 (0=CPU, 1=DirectML GPU)
    /// 需求 5.1: 支持两种推理设备类型
    /// </summary>
    public int InferenceDeviceIndex
    {
        get => AppSettings.InferenceDeviceType;
        set
        {
            if (AppSettings.InferenceDeviceType != value)
            {
                AppSettings.InferenceDeviceType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsGpuModeEnabled));
                OnLog($"推理设备已切换为: {(value == 0 ? "CPU" : "DirectML GPU")}", 1);
            }
        }
    }
    
    /// <summary>
    /// 是否启用了 GPU 模式
    /// 用于控制 GPU 设备 ID 输入框的启用状态
    /// </summary>
    public bool IsGpuModeEnabled => AppSettings.InferenceDeviceType == 1;
    
    #endregion
    
    #region GhostBox 错误信息属性
    
    /// <summary>
    /// GhostBox 错误信息
    /// </summary>
    public string GhostBoxErrorMessage => _inputDriverManager.GhostBoxLastError;
    
    /// <summary>
    /// 是否有 GhostBox 错误
    /// </summary>
    public bool HasGhostBoxError => !string.IsNullOrEmpty(_inputDriverManager.GhostBoxLastError);
    
    #endregion

    public MainViewModel()
    {
        _config = new ConfigManager();
        _config.LoadConfigs();
        AppSettings = _config.AppSettings;

        // 初始化输入驱动管理器，使用配置中保存的驱动类型
        _inputDriverManager = new InputDriverManager(AppSettings.InputDriverType);
        _inputDriverManager.DriverChanged += OnDriverChanged;
        _inputDriverManager.ConnectionStatusChanged += OnConnectionStatusChanged;
        _inputDriverManager.DeviceConnectionChanged += OnDeviceConnectionChanged;
        
        // 初始化窗口枚举服务
        _windowEnumerationService = new WindowEnumerationService();
        
        // 使用驱动管理器提供的键盘接口
        IKeyboardInterface keyboard = _inputDriverManager.KeyboardInterface;
        _imageInterface = new OpenCvImageInterface();
        _engine = new SkillLoopEngine(keyboard, _imageInterface, _config);
        _engine.StatusChanged += OnStatusChanged;
        _engine.LogMessage += OnLog;
        
        _templateCapture = new TemplateCapture(_imageInterface);
        _tempTemplateCache = new Dictionary<string, OpenCvSharp.Mat>();
        
        _hotkeyService = new GlobalHotkeyService();
        _hotkeyService.HotkeyTriggered += OnHotkeyTriggered;
        
        // 订阅配置变更事件，确保技能状态列表同步更新
        _config.ConfigChanged += OnConfigChangedHandler;

        LoadSkills();
        RefreshProfiles();
        InitializeSkillStatusList();
        StartMemoryMonitor();
        StartCooldownTimer();
        CheckFirstTimeGuide();
        
        // 初始化窗口列表并尝试匹配保存的窗口
        InitializeWindowList();
        
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
        var failed = new List<string>();
        
        // 检查启动/停止热键冲突
        var startStopConflict = _hotkeyService.CheckConflict(
            AppSettings.HotkeyStartStopModifier, 
            AppSettings.HotkeyStartStopKey, 
            "StartStop");
        
        if (startStopConflict != null && startStopConflict.IsSystemConflict)
        {
            OnLog($"启动/停止热键与系统热键冲突: {startStopConflict.Description}", 2);
            failed.Add("启动/停止");
        }
        else if (_hotkeyService.RegisterHotkey("StartStop", 
            AppSettings.HotkeyStartStopModifier, 
            AppSettings.HotkeyStartStopKey, 
            () => { if (IsRunning) StopEngine(); else StartEngine(); }))
        {
            registered.Add(GlobalHotkeyService.GetHotkeyDisplayTextStatic(
                AppSettings.HotkeyStartStopModifier, AppSettings.HotkeyStartStopKey));
        }
        else
        {
            failed.Add("启动/停止");
        }
        
        // 检查暂停热键冲突
        var pauseConflict = _hotkeyService.CheckConflict(
            AppSettings.HotkeyPauseModifier, 
            AppSettings.HotkeyPauseKey, 
            "Pause");
        
        if (pauseConflict != null && pauseConflict.IsSystemConflict)
        {
            OnLog($"暂停热键与系统热键冲突: {pauseConflict.Description}", 2);
            failed.Add("暂停");
        }
        else if (_hotkeyService.RegisterHotkey("Pause",
            AppSettings.HotkeyPauseModifier,
            AppSettings.HotkeyPauseKey,
            () => PauseEngine()))
        {
            registered.Add(GlobalHotkeyService.GetHotkeyDisplayTextStatic(
                AppSettings.HotkeyPauseModifier, AppSettings.HotkeyPauseKey));
        }
        else
        {
            failed.Add("暂停");
        }
        
        if (registered.Count > 0)
        {
            HotkeyStatus = $"快捷键: {string.Join(", ", registered)}";
            OnLog($"已注册全局快捷键: {string.Join(", ", registered)}", 1);
        }
        
        if (failed.Count > 0)
        {
            OnLog($"以下热键注册失败: {string.Join(", ", failed)}，可能被其他程序占用", 2);
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

    /// <summary>
    /// 配置变更处理，确保技能状态列表与配置同步
    /// </summary>
    private void OnConfigChangedHandler(string filePath)
    {
        // 只处理技能配置文件的变更
        if (!filePath.Contains("skills.json")) return;
        
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            // 重新加载技能配置到UI
            Skills = new ObservableCollection<SkillConfig>(_config.Skills);
            if (Skills.Count > 0 && SelectedSkill == null) 
                SelectedSkill = Skills[0];
            
            // 重新初始化技能状态列表
            InitializeSkillStatusList();
            
            OnLog("技能状态列表已同步更新", 0);
        });
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
            
            // 更新悬浮窗（AvgResponseTime 已经是毫秒单位，无需转换）
            _overlay?.UpdateStatus(s.Mode, s.ExecutionCount, s.AvgResponseTime, 
                _nextSkillName, _currentHpPercent, _currentMpPercent);
            
            // 更新技能状态列表
            UpdateSkillStatusFromEngine(s);
            
            // 需求 12.5: 更新托盘图标提示
            UpdateTrayTooltip();
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
        
        // 关闭遮罩窗口
        CloseMaskWindow();
        
        // 取消独立任务
        _soloTaskCts?.Cancel();
        _soloTaskCts?.Dispose();
        _soloTaskCts = null;
        
        // 取消订阅配置变更事件
        _config.ConfigChanged -= OnConfigChangedHandler;
        
        // 释放输入驱动管理器
        _inputDriverManager.DriverChanged -= OnDriverChanged;
        _inputDriverManager.ConnectionStatusChanged -= OnConnectionStatusChanged;
        _inputDriverManager.DeviceConnectionChanged -= OnDeviceConnectionChanged;
        _inputDriverManager.Dispose();
        
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 启动引擎，启动前验证选中窗口是否存在
    /// </summary>
    [RelayCommand] 
    private void StartEngine()
    {
        // 验证选中窗口是否存在
        if (!ValidateSelectedWindow())
        {
            return;
        }
        
        _engine.Start();
    }
    
    /// <summary>
    /// 验证选中的窗口是否仍然存在
    /// </summary>
    /// <returns>窗口有效返回true，无效返回false</returns>
    private bool ValidateSelectedWindow()
    {
        // 如果没有选中窗口，检查是否有保存的窗口标题
        if (SelectedWindow == null)
        {
            if (string.IsNullOrEmpty(AppSettings.GameWindowTitle))
            {
                ToastManager.Warning("请先选择目标窗口", "启动失败");
                OnLog("引擎启动失败: 未选择目标窗口", 2);
                return false;
            }
            
            // 尝试根据保存的标题查找窗口
            var window = _windowEnumerationService.FindWindowByTitle(AppSettings.GameWindowTitle);
            if (window == null)
            {
                ToastManager.Warning($"目标窗口 \"{AppSettings.GameWindowTitle}\" 不存在，请重新选择", "启动失败");
                OnLog($"引擎启动失败: 目标窗口 \"{AppSettings.GameWindowTitle}\" 不存在", 2);
                return false;
            }
            
            // 找到窗口，更新选择
            SelectedWindow = WindowList.FirstOrDefault(w => w.Handle == window.Handle);
            if (SelectedWindow == null)
            {
                // 窗口不在列表中，刷新列表
                _ = RefreshWindowListAsync();
                SelectedWindow = WindowList.FirstOrDefault(w => w.Handle == window.Handle);
            }
        }
        
        // 验证选中窗口是否仍然有效
        if (SelectedWindow != null && !_windowEnumerationService.IsWindowValid(SelectedWindow.Handle))
        {
            ToastManager.Warning($"目标窗口 \"{SelectedWindow.Title}\" 已关闭，请重新选择", "启动失败");
            OnLog($"引擎启动失败: 目标窗口 \"{SelectedWindow.Title}\" 已关闭", 2);
            SelectedWindow = null;
            return false;
        }
        
        return true;
    }
    
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
    /// 取公共CD颜色
    /// </summary>
    [RelayCommand]
    private void PickGlobalCdColor()
    {
        var point = AppSettings.GlobalCdPoint;
        if (point.All(v => v == 0))
        {
            ToastManager.Warning("请先设置公共CD检测点", "取色失败");
            return;
        }
        
        var color = _imageInterface.GetPixelColor(point[0], point[1]);
        if (color == null)
        {
            ToastManager.Error("无法获取颜色", "取色失败");
            return;
        }
        
        AppSettings.GlobalCdColor = [color.Value.r, color.Value.g, color.Value.b];
        OnPropertyChanged(nameof(GlobalCdPreviewColor));
        OnLog($"公共CD颜色: R={color.Value.r}, G={color.Value.g}, B={color.Value.b}", 1);
        ToastManager.Success($"R={color.Value.r}, G={color.Value.g}, B={color.Value.b}", "取色成功");
    }
    
    /// <summary>
    /// 公共CD颜色预览
    /// </summary>
    public System.Windows.Media.Color GlobalCdPreviewColor
    {
        get
        {
            var c = AppSettings.GlobalCdColor;
            if (c.Length >= 3)
                return System.Windows.Media.Color.FromRgb((byte)c[0], (byte)c[1], (byte)c[2]);
            return System.Windows.Media.Colors.White;
        }
    }
    
    /// <summary>
    /// 测试公共CD检测
    /// </summary>
    [RelayCommand]
    private void TestGlobalCd()
    {
        var point = AppSettings.GlobalCdPoint;
        if (point.All(v => v == 0))
        {
            ToastManager.Warning("请先设置公共CD检测点", "测试失败");
            return;
        }
        
        var color = _imageInterface.GetPixelColor(point[0], point[1]);
        if (color == null)
        {
            ToastManager.Error("无法获取颜色", "测试失败");
            return;
        }
        
        var targetColor = AppSettings.GlobalCdColor;
        var tolerance = AppSettings.GlobalCdColorTolerance;
        
        var isInCd = Math.Abs(color.Value.r - targetColor[0]) <= tolerance &&
                     Math.Abs(color.Value.g - targetColor[1]) <= tolerance &&
                     Math.Abs(color.Value.b - targetColor[2]) <= tolerance;
        
        var status = isInCd ? "正在公共CD中" : "公共CD已结束";
        OnLog($"公共CD测试: {status} (当前颜色: R={color.Value.r}, G={color.Value.g}, B={color.Value.b})", 1);
        ToastManager.Info($"{status}\n当前: R={color.Value.r}, G={color.Value.g}, B={color.Value.b}\n目标: R={targetColor[0]}, G={targetColor[1]}, B={targetColor[2]}", "公共CD检测");
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
        
        // 重新初始化技能状态列表，确保与技能配置同步
        InitializeSkillStatusList();
        
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
        
        // 获取当前修饰键
        uint modifiers = hotkeyType switch
        {
            "StartStop" => AppSettings.HotkeyStartStopModifier,
            "Pause" => AppSettings.HotkeyPauseModifier,
            _ => 0
        };
        
        // 检测热键冲突
        var conflict = _hotkeyService.CheckConflict(modifiers, keyCode, hotkeyType);
        if (conflict != null)
        {
            ToastManager.Warning($"热键冲突: {conflict.Description}", "热键冲突");
            OnLog($"热键冲突: {conflict.Description}", 2);
            
            // 如果是系统热键冲突，建议用户更换
            if (conflict.IsSystemConflict)
            {
                ToastManager.Info("建议使用其他按键组合避免与系统热键冲突", "提示");
            }
            return;
        }
        
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

    /// <summary>
    /// 检查热键冲突（供外部调用）
    /// </summary>
    public HotkeyConflict? CheckHotkeyConflict(uint modifiers, uint key, string? excludeName = null)
    {
        return _hotkeyService.CheckConflict(modifiers, key, excludeName);
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

    #region 独立任务相关 (需求 19.1, 20.1)
    
    /// <summary>
    /// 是否有独立任务正在运行
    /// </summary>
    [ObservableProperty] private bool _isSoloTaskRunning;
    
    private CancellationTokenSource? _soloTaskCts;
    
    #endregion
    
    #region 遮罩窗口相关 (需求 21.1)
    
    private MaskWindow? _maskWindow;
    
    /// <summary>
    /// 遮罩窗口是否已显示
    /// </summary>
    [ObservableProperty] private bool _isMaskWindowVisible;
    
    /// <summary>
    /// 遮罩窗口按钮文本
    /// </summary>
    public string MaskWindowButtonText => IsMaskWindowVisible ? "隐藏遮罩窗口" : "显示遮罩窗口";
    
    partial void OnIsMaskWindowVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(MaskWindowButtonText));
    }
    
    /// <summary>
    /// 切换遮罩窗口显示状态
    /// 需求: 21.1
    /// </summary>
    [RelayCommand]
    private void ToggleMaskWindow()
    {
        if (!AppSettings.EnableMaskWindow)
        {
            ToastManager.Warning("请先启用遮罩窗口", "遮罩窗口");
            return;
        }
        
        // 使用 IsMaskWindowVisible 状态来判断，而不是检查窗口是否存在
        // 因为 Hide() 后窗口仍然存在但不可见
        if (IsMaskWindowVisible)
        {
            HideMaskWindow();
        }
        else
        {
            ShowMaskWindow();
        }
    }
    
    /// <summary>
    /// 显示遮罩窗口
    /// </summary>
    private void ShowMaskWindow()
    {
        try
        {
            if (_maskWindow == null)
            {
                _maskWindow = new MaskWindow();
                
                // 配置遮罩窗口
                _maskWindow.Config.ShowLogBox = AppSettings.MaskShowLogBox;
                _maskWindow.Config.ShowStatus = AppSettings.MaskShowStatus;
                _maskWindow.Config.DirectionsEnabled = AppSettings.MaskDirectionsEnabled;
                _maskWindow.Config.UidCoverEnabled = AppSettings.MaskUidCoverEnabled;
                _maskWindow.Config.ShowFps = AppSettings.MaskShowFps;
                _maskWindow.Config.UseSubform = AppSettings.MaskUseSubform;
                _maskWindow.Config.TextOpacity = AppSettings.MaskTextOpacity;
                _maskWindow.Config.LogBoxLeft = AppSettings.MaskLogBoxLeft;
                _maskWindow.Config.LogBoxTop = AppSettings.MaskLogBoxTop;
                _maskWindow.Config.LogBoxWidth = AppSettings.MaskLogBoxWidth;
                _maskWindow.Config.LogBoxHeight = AppSettings.MaskLogBoxHeight;
            }
            
            _maskWindow.Show();
            IsMaskWindowVisible = true;
            OnLog("遮罩窗口已显示", 1);
        }
        catch (Exception ex)
        {
            OnLog($"显示遮罩窗口失败: {ex.Message}", 2);
            ToastManager.Error($"显示失败: {ex.Message}", "遮罩窗口");
        }
    }
    
    /// <summary>
    /// 隐藏遮罩窗口
    /// </summary>
    private void HideMaskWindow()
    {
        try
        {
            if (_maskWindow != null)
            {
                // 保存遮罩窗口配置
                AppSettings.MaskLogBoxLeft = _maskWindow.Config.LogBoxLeft;
                AppSettings.MaskLogBoxTop = _maskWindow.Config.LogBoxTop;
                AppSettings.MaskLogBoxWidth = _maskWindow.Config.LogBoxWidth;
                AppSettings.MaskLogBoxHeight = _maskWindow.Config.LogBoxHeight;
                
                _maskWindow.Hide();
            }
            IsMaskWindowVisible = false;
            OnLog("遮罩窗口已隐藏", 1);
        }
        catch (Exception ex)
        {
            OnLog($"隐藏遮罩窗口失败: {ex.Message}", 2);
        }
    }
    
    /// <summary>
    /// 关闭遮罩窗口（释放资源）
    /// </summary>
    private void CloseMaskWindow()
    {
        try
        {
            if (_maskWindow != null)
            {
                // 保存遮罩窗口配置
                AppSettings.MaskLogBoxLeft = _maskWindow.Config.LogBoxLeft;
                AppSettings.MaskLogBoxTop = _maskWindow.Config.LogBoxTop;
                AppSettings.MaskLogBoxWidth = _maskWindow.Config.LogBoxWidth;
                AppSettings.MaskLogBoxHeight = _maskWindow.Config.LogBoxHeight;
                
                _maskWindow.Close();
                _maskWindow = null;
            }
            IsMaskWindowVisible = false;
        }
        catch (Exception ex)
        {
            OnLog($"关闭遮罩窗口失败: {ex.Message}", 2);
        }
    }
    
    #endregion
    
    /// <summary>
    /// 停止当前独立任务
    /// </summary>
    [RelayCommand]
    private void StopSoloTask()
    {
        if (_soloTaskCts != null && !_soloTaskCts.IsCancellationRequested)
        {
            _soloTaskCts.Cancel();
            OnLog("正在停止独立任务...", 1);
            ToastManager.Info("正在停止任务...", "独立任务");
        }
    }

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
    
    /// <summary>
    /// 添加技能组
    /// </summary>
    [RelayCommand]
    private void AddSkillGroup()
    {
        var newGroup = new SkillGroupConfig
        {
            Name = $"技能组{AppSettings.SkillGroups.Count + 1}",
            ConditionBuff = "",
            Enabled = true
        };
        AppSettings.SkillGroups.Add(newGroup);
        OnLog($"已添加技能组: {newGroup.Name}", 1);
        ToastManager.Success($"已添加技能组: {newGroup.Name}", "技能组");
    }
    
    /// <summary>
    /// 删除技能组
    /// </summary>
    [RelayCommand]
    private void RemoveSkillGroup(SkillGroupConfig? group)
    {
        if (group == null) return;
        
        var groupName = group.Name;
        AppSettings.SkillGroups.Remove(group);
        OnLog($"已删除技能组: {groupName}", 1);
        ToastManager.Info($"已删除技能组: {groupName}", "技能组");
    }
    
    #region 输入驱动相关方法
    
    /// <summary>
    /// 切换输入驱动
    /// </summary>
    private void SwitchInputDriver(InputDriverType driverType)
    {
        var oldType = _inputDriverManager.CurrentDriverType;
        bool success = _inputDriverManager.SwitchDriver(driverType);
        
        if (success)
        {
            // 更新配置
            AppSettings.InputDriverType = driverType;
            _config.SaveAppSettings();
            
            // 更新引擎使用的键盘接口
            _engine.UpdateKeyboardInterface(_inputDriverManager.KeyboardInterface);
            
            OnLog($"输入驱动已切换: {oldType} -> {driverType}", 1);
            ToastManager.Success($"已切换到 {GetDriverDisplayName(driverType)}", "驱动切换");
        }
        else
        {
            OnLog($"输入驱动切换失败: {_inputDriverManager.GhostBoxLastError}", 2);
            ToastManager.Error($"切换失败: {_inputDriverManager.GhostBoxLastError}", "驱动切换");
        }
        
        // 通知 UI 更新
        NotifyDriverPropertiesChanged();
    }
    
    /// <summary>
    /// 重新连接 GhostBox 设备
    /// </summary>
    [RelayCommand]
    private void ReconnectGhostBox()
    {
        OnLog("正在重新连接 GhostBox 设备...", 1);
        
        bool success = _inputDriverManager.ReconnectGhostBox();
        
        if (success)
        {
            // 如果当前选择的是 GhostBox 驱动，更新引擎的键盘接口
            if (_inputDriverManager.CurrentDriverType == InputDriverType.GhostBox)
            {
                _engine.UpdateKeyboardInterface(_inputDriverManager.KeyboardInterface);
            }
            
            OnLog("GhostBox 设备重新连接成功", 1);
            ToastManager.Success("设备已连接", "GhostBox");
        }
        else
        {
            OnLog($"GhostBox 设备重新连接失败: {_inputDriverManager.GhostBoxLastError}", 2);
            ToastManager.Error($"连接失败: {_inputDriverManager.GhostBoxLastError}", "GhostBox");
        }
        
        NotifyDriverPropertiesChanged();
    }
    
    /// <summary>
    /// 驱动切换事件处理
    /// </summary>
    private void OnDriverChanged(object? sender, DriverChangedEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            NotifyDriverPropertiesChanged();
        });
    }
    
    /// <summary>
    /// 连接状态变化事件处理
    /// </summary>
    private void OnConnectionStatusChanged(object? sender, string status)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            OnLog($"GhostBox 状态: {status}", 1);
            NotifyDriverPropertiesChanged();
        });
    }
    
    /// <summary>
    /// 设备连接状态变化事件处理（来自 ConnectionMonitor）
    /// </summary>
    private void OnDeviceConnectionChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            // 更新 UI 绑定属性
            OnPropertyChanged(nameof(IsGhostBoxConnected));
            OnPropertyChanged(nameof(GhostBoxConnectionStatus));
            OnPropertyChanged(nameof(GhostBoxDeviceInfo));
            
            // 更新状态颜色
            GhostBoxConnectionStatusColor = e.IsConnected ? "Green" : "Red";
            
            // 通知引擎设备状态变化
            if (e.IsConnected)
            {
                _engine.OnDeviceReconnected();
                ToastManager.Success("GhostBox 设备已连接", "设备状态");
            }
            else
            {
                _engine.OnDeviceDisconnected();
                ToastManager.Warning("GhostBox 设备已断开，正在尝试重连...", "设备状态");
            }
            
            OnLog(e.Message, e.IsConnected ? 1 : 2);
        });
    }
    
    /// <summary>
    /// 通知驱动相关属性变化
    /// </summary>
    private void NotifyDriverPropertiesChanged()
    {
        OnPropertyChanged(nameof(SelectedDriverIndex));
        OnPropertyChanged(nameof(IsGhostBoxAvailable));
        OnPropertyChanged(nameof(IsGhostBoxSelected));
        OnPropertyChanged(nameof(IsGhostBoxConnected));
        OnPropertyChanged(nameof(GhostBoxConnectionStatus));
        OnPropertyChanged(nameof(GhostBoxDeviceInfo));
        OnPropertyChanged(nameof(GhostBoxErrorMessage));
        OnPropertyChanged(nameof(HasGhostBoxError));
    }
    
    /// <summary>
    /// 获取驱动显示名称
    /// </summary>
    private static string GetDriverDisplayName(InputDriverType driverType)
    {
        return driverType switch
        {
            InputDriverType.Win32 => "Win32 (软件模拟)",
            InputDriverType.GhostBox => "GhostBox (硬件驱动)",
            _ => driverType.ToString()
        };
    }
    
    #endregion
    
    #region 主窗口 UI 重构相关属性和命令 (需求 7)
    
    /// <summary>
    /// 托盘图标工具提示
    /// 需求: 12.4, 12.5 - 显示引擎状态工具提示，状态变化时更新
    /// </summary>
    public string TrayTooltip
    {
        get
        {
            var status = IsRunning 
                ? (IsPaused ? "已暂停" : "运行中") 
                : "已停止";
            
            var tooltip = $"ShineProCS - {status}";
            
            // 如果正在运行，显示更多信息
            if (IsRunning && !IsPaused)
            {
                tooltip += $"\n执行次数: {ExecutionCount}";
                if (AvgResponseTime > 0)
                    tooltip += $"\n平均响应: {AvgResponseTime:F1}ms";
            }
            
            return tooltip;
        }
    }
    
    /// <summary>
    /// 托盘图标状态（用于可能的图标切换）
    /// 需求: 12.5
    /// </summary>
    public string TrayIconStatus => IsRunning 
        ? (IsPaused ? "Paused" : "Running") 
        : "Stopped";
    
    /// <summary>
    /// 截图方式索引 (0=WGC, 1=BitBlt)
    /// 需求: 3.6
    /// </summary>
    public int CaptureModeIndex
    {
        get => AppSettings.EnableWgcCapture ? 0 : 1;
        set
        {
            AppSettings.EnableWgcCapture = value == 0;
            OnPropertyChanged();
        }
    }
    

    
    /// <summary>
    /// 切换主题命令
    /// 需求: 1.6
    /// </summary>
    [RelayCommand]
    private void SwitchTheme()
    {
        // 切换深浅主题
        var currentTheme = Wpf.Ui.Appearance.ApplicationThemeManager.GetAppTheme();
        var newTheme = currentTheme == Wpf.Ui.Appearance.ApplicationTheme.Dark 
            ? Wpf.Ui.Appearance.ApplicationTheme.Light 
            : Wpf.Ui.Appearance.ApplicationTheme.Dark;
        
        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(newTheme);
        OnLog($"主题已切换为: {newTheme}", 1);
    }
    
    /// <summary>
    /// 最小化到托盘命令
    /// 需求: 1.6
    /// </summary>
    [RelayCommand]
    private void MinimizeToTray()
    {
        System.Windows.Application.Current?.MainWindow?.Hide();
    }
    
    /// <summary>
    /// 显示窗口命令
    /// 需求: 12.2
    /// </summary>
    [RelayCommand]
    private void ShowWindow()
    {
        var mainWindow = System.Windows.Application.Current?.MainWindow;
        if (mainWindow != null)
        {
            mainWindow.Show();
            mainWindow.WindowState = System.Windows.WindowState.Normal;
            mainWindow.Activate();
        }
    }
    
    /// <summary>
    /// 切换引擎状态命令（用于托盘菜单）
    /// 需求: 12.3
    /// </summary>
    [RelayCommand]
    private void ToggleEngine()
    {
        if (IsRunning)
            StopEngine();
        else
            StartEngine();
    }
    
    /// <summary>
    /// 退出应用程序命令
    /// 需求: 12.3
    /// </summary>
    [RelayCommand]
    private void Exit()
    {
        // 停止引擎
        if (IsRunning)
            StopEngine();
        
        // 释放资源
        Dispose();
        
        // 退出应用程序
        System.Windows.Application.Current?.Shutdown();
    }
    
    /// <summary>
    /// 检查更新命令
    /// 需求: 12.3
    /// </summary>
    [RelayCommand]
    private void CheckUpdate()
    {
        try
        {
            // 打开 GitHub 发布页面（简单实现）
            var url = "https://github.com/ShineProCS/releases";
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            OnLog("已打开更新页面", 1);
            ToastManager.Info("已打开更新页面，请在浏览器中查看最新版本", "检查更新");
        }
        catch (Exception ex)
        {
            OnLog($"打开更新页面失败: {ex.Message}", 2);
            ToastManager.Error($"打开更新页面失败: {ex.Message}", "检查更新");
        }
    }
    
    /// <summary>
    /// 保存设置命令
    /// 需求: 6.4
    /// </summary>
    [RelayCommand]
    private void SaveSettings()
    {
        _config.SaveAppSettings();
        OnLog("设置已保存", 1);
        ToastManager.Success("设置已保存", "保存");
    }
    
    /// <summary>
    /// 更新托盘提示和状态
    /// 需求: 12.4, 12.5
    /// </summary>
    private void UpdateTrayTooltip()
    {
        OnPropertyChanged(nameof(TrayTooltip));
        OnPropertyChanged(nameof(TrayIconStatus));
    }
    
    #endregion
}
