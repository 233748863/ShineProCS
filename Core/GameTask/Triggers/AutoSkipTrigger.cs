using System.Diagnostics;
using System.IO;
using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Recognition.OCR;
using ShineProCS.Core.Services;
using ShineProCS.Models;
using OpenCvSharp;

namespace ShineProCS.Core.GameTask.Triggers;

/// <summary>
/// 自动剧情跳过触发器
/// 实现 ITaskTrigger 接口，检测游戏中的对话框和选项界面并自动跳过
/// 需求: 18.1 - 自动剧情跳过作为 ITaskTrigger 实现
/// 需求: 18.2 - 检测对话框和选项界面
/// 需求: 18.3 - 检测到对话时自动点击跳过或继续
/// 需求: 18.4 - 检测到选项时自动选择
/// 需求: 18.5 - 支持启用/禁用切换
/// </summary>
public class AutoSkipTrigger : ITaskTrigger, IDisposable
{
    #region 依赖组件
    
    private readonly IInputService _inputService;
    private readonly IOcrService? _ocrService;
    private readonly ConfigManager _configManager;
    private readonly ILogService _logService;
    
    // 后台消息模拟器
    private PostMessageSimulator? _postMessageSimulator;
    
    #endregion
    
    #region 运行状态
    
    private DateTime _lastSkipTime = DateTime.MinValue;
    private int _skipCount;
    private bool _disposed;
    private readonly object _stateLock = new();
    
    // 日志去重相关
    private string _lastLogText = string.Empty;
    private int _lastLogFrameIndex = -1;
    private int _currentFrameIndex;
    private const int LogDedupeFrameThreshold = 5;
    
    // 检测状态
    private DialogState _lastDialogState = DialogState.None;
    
    // 后台运行状态
    private bool _isBackgroundRunning;
    private bool _useBackgroundOperation;
    
    #endregion
    
    #region ITaskTrigger 实现
    
    /// <summary>
    /// 触发器名称
    /// </summary>
    public string Name => "自动剧情跳过";
    
    /// <summary>
    /// 触发器优先级（数值越大越先执行）
    /// 自动剧情跳过优先级设置为 40，低于自动拾取（50）和技能循环（100）
    /// </summary>
    public int Priority => 40;
    
    private bool _isEnabled;
    
    /// <summary>
    /// 是否启用
    /// 需求: 18.5 - 支持运行时启用/禁用
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            Log($"自动剧情跳过触发器已{(value ? "启用" : "禁用")}", 1);
        }
    }
    
    /// <summary>
    /// 是否处于独占模式
    /// 自动剧情跳过不需要独占，可以与其他触发器并行
    /// </summary>
    public bool IsExclusive => false;
    
    #endregion
    
    #region 构造函数
    
    /// <summary>
    /// 创建自动剧情跳过触发器
    /// </summary>
    public AutoSkipTrigger(
        IInputService inputService,
        IOcrService? ocrService,
        ConfigManager configManager,
        ILogService logService)
    {
        _inputService = inputService ?? throw new ArgumentNullException(nameof(inputService));
        _ocrService = ocrService;
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        
        _isEnabled = _configManager.AppSettings.EnableAutoSkip;
    }
    
    #endregion
    
    #region ITaskTrigger 接口方法
    
    /// <summary>
    /// 初始化触发器
    /// </summary>
    public void Init()
    {
        Log("自动剧情跳过触发器已初始化", 1);
        
        if (_ocrService == null)
        {
            Log("警告: OCR 服务不可用，自动剧情跳过功能将受限", 2);
        }
        
        // 初始化后台运行配置
        _isBackgroundRunning = _configManager.AppSettings.AutoSkipRunBackground;
        
        // 初始化后台消息模拟器（需要游戏窗口句柄）
        InitPostMessageSimulator();
    }
    
    /// <summary>
    /// 初始化后台消息模拟器
    /// </summary>
    private void InitPostMessageSimulator()
    {
        try
        {
            var gameWindowTitle = _configManager.AppSettings.GameWindowTitle;
            if (string.IsNullOrEmpty(gameWindowTitle))
            {
                Log("未配置游戏窗口标题，后台运行功能不可用", 2);
                return;
            }
            
            var hWnd = FindWindowByTitle(gameWindowTitle);
            if (hWnd != IntPtr.Zero)
            {
                _postMessageSimulator = new PostMessageSimulator(hWnd);
                Log("后台消息模拟器已初始化", 1);
            }
            else
            {
                Log($"未找到游戏窗口: {gameWindowTitle}，后台运行功能不可用", 2);
            }
        }
        catch (Exception ex)
        {
            Log($"初始化后台消息模拟器失败: {ex.Message}", 2);
        }
    }
    
    /// <summary>
    /// 通过标题查找窗口句柄
    /// </summary>
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);
    
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    
    private IntPtr FindWindowByTitle(string title)
    {
        return FindWindow(null, title);
    }
    
    /// <summary>
    /// 检查游戏窗口是否在前台
    /// </summary>
    private bool IsGameWindowActive()
    {
        if (_postMessageSimulator == null) return true;
        return _postMessageSimulator.IsWindowForeground;
    }
    
    /// <summary>
    /// 捕获图像后的处理
    /// 需求: 18.2 - 检测对话框和选项界面
    /// </summary>
    public void OnCapture(CaptureContent content)
    {
        if (!_isEnabled || content.Image == null || content.Image.Empty())
            return;
        
        _currentFrameIndex++;
        var speedTimer = new SpeedTimer("AutoSkip");
        
        var settings = _configManager.AppSettings;
        var interval = settings.AutoSkipInterval;
        
        // 更新后台运行状态
        _isBackgroundRunning = settings.AutoSkipRunBackground;
        _useBackgroundOperation = _isBackgroundRunning && !IsGameWindowActive();
        
        lock (_stateLock)
        {
            if ((DateTime.Now - _lastSkipTime).TotalMilliseconds < interval)
                return;
        }
        
        try
        {
            var region = settings.AutoSkipRegion;
            if (region == null || region.Length < 4)
                return;
            
            // 裁剪检测区域
            using var skipRegion = GetSkipRegion(content.Image, region);
            if (skipRegion == null || skipRegion.Empty())
                return;
            
            speedTimer.Record("裁剪区域");
            
            // 检测对话状态
            var dialogState = DetectDialogState(skipRegion, settings);
            speedTimer.Record("检测对话状态");
            
            if (dialogState == DialogState.None)
            {
                _lastDialogState = DialogState.None;
                speedTimer.DebugPrint();
                return;
            }
            
            // 根据对话状态执行相应操作
            switch (dialogState)
            {
                case DialogState.Dialog:
                    // 需求: 18.3 - 检测到对话时自动点击跳过或继续
                    ExecuteDialogSkip(settings);
                    speedTimer.Record("执行对话跳过");
                    break;
                    
                case DialogState.Option:
                    // 需求: 18.4 - 检测到选项时自动选择
                    ExecuteOptionSelect(settings);
                    speedTimer.Record("执行选项选择");
                    break;
            }
            
            _lastDialogState = dialogState;
            speedTimer.DebugPrint();
        }
        catch (Exception ex)
        {
            Log($"自动剧情跳过检测异常: {ex.Message}", 2);
        }
    }
    
    #endregion

    
    #region 对话状态检测
    
    /// <summary>
    /// 对话状态枚举
    /// </summary>
    public enum DialogState
    {
        /// <summary>
        /// 无对话
        /// </summary>
        None,
        
        /// <summary>
        /// 普通对话框
        /// </summary>
        Dialog,
        
        /// <summary>
        /// 选项界面
        /// </summary>
        Option
    }
    
    /// <summary>
    /// 检测当前对话状态
    /// 需求: 18.2 - 检测对话框和选项界面
    /// </summary>
    private DialogState DetectDialogState(Mat image, AppSettings settings)
    {
        // 优先使用模板匹配（如果配置了）
        if (settings.AutoSkipUseTemplateMatch)
        {
            return DetectDialogStateByTemplate(image, settings);
        }
        
        // 使用 OCR 检测
        return DetectDialogStateByOcr(image, settings);
    }
    
    /// <summary>
    /// 使用 OCR 检测对话状态
    /// </summary>
    private DialogState DetectDialogStateByOcr(Mat image, AppSettings settings)
    {
        if (_ocrService == null)
            return DialogState.None;
        
        try
        {
            var result = _ocrService.OcrResult(image);
            if (result == null || result.Regions.Length == 0)
                return DialogState.None;
            
            var filteredRegions = result.Regions
                .Where(r => r.Score >= settings.AutoSkipConfidenceThreshold)
                .ToArray();
            
            if (filteredRegions.Length == 0)
                return DialogState.None;
            
            var text = new OcrResult(filteredRegions).Text;
            
            if (string.IsNullOrWhiteSpace(text))
                return DialogState.None;
            
            // 检查是否是选项界面
            var optionKeywords = settings.AutoSkipOptionKeywords;
            if (optionKeywords != null && optionKeywords.Count > 0)
            {
                if (optionKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
                {
                    LogWithDedupe($"检测到选项界面: {text}", 0);
                    return DialogState.Option;
                }
            }
            
            // 检查是否是对话框
            var dialogKeywords = settings.AutoSkipDialogKeywords;
            if (dialogKeywords != null && dialogKeywords.Count > 0)
            {
                if (dialogKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
                {
                    LogWithDedupe($"检测到对话框: {text}", 0);
                    return DialogState.Dialog;
                }
            }
            
            return DialogState.None;
        }
        catch (Exception ex)
        {
            Log($"OCR 检测失败: {ex.Message}", 2);
            return DialogState.None;
        }
    }
    
    /// <summary>
    /// 使用模板匹配检测对话状态
    /// </summary>
    private DialogState DetectDialogStateByTemplate(Mat image, AppSettings settings)
    {
        try
        {
            // 检测选项界面模板
            if (!string.IsNullOrEmpty(settings.AutoSkipOptionTemplatePath) &&
                File.Exists(settings.AutoSkipOptionTemplatePath))
            {
                using var optionTemplate = Cv2.ImRead(settings.AutoSkipOptionTemplatePath);
                if (!optionTemplate.Empty())
                {
                    var matchResult = MatchTemplate(image, optionTemplate, settings.AutoSkipTemplateThreshold);
                    if (matchResult.HasValue)
                    {
                        LogWithDedupe($"模板匹配检测到选项界面，置信度: {matchResult.Value.confidence:F2}", 0);
                        return DialogState.Option;
                    }
                }
            }
            
            // 检测对话框模板
            if (!string.IsNullOrEmpty(settings.AutoSkipDialogTemplatePath) &&
                File.Exists(settings.AutoSkipDialogTemplatePath))
            {
                using var dialogTemplate = Cv2.ImRead(settings.AutoSkipDialogTemplatePath);
                if (!dialogTemplate.Empty())
                {
                    var matchResult = MatchTemplate(image, dialogTemplate, settings.AutoSkipTemplateThreshold);
                    if (matchResult.HasValue)
                    {
                        LogWithDedupe($"模板匹配检测到对话框，置信度: {matchResult.Value.confidence:F2}", 0);
                        return DialogState.Dialog;
                    }
                }
            }
            
            return DialogState.None;
        }
        catch (Exception ex)
        {
            Log($"模板匹配检测失败: {ex.Message}", 2);
            return DialogState.None;
        }
    }
    
    /// <summary>
    /// 执行模板匹配
    /// </summary>
    private (OpenCvSharp.Point location, double confidence)? MatchTemplate(Mat image, Mat template, double threshold)
    {
        try
        {
            using var result = new Mat();
            Cv2.MatchTemplate(image, template, result, TemplateMatchModes.CCoeffNormed);
            
            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);
            
            if (maxVal >= threshold)
            {
                return (maxLoc, maxVal);
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }
    
    #endregion
    
    #region 跳过执行
    
    /// <summary>
    /// 执行对话跳过
    /// 需求: 18.3 - 检测到对话时自动点击跳过或继续
    /// 支持后台运行模式
    /// </summary>
    private void ExecuteDialogSkip(AppSettings settings)
    {
        try
        {
            var keyCode = settings.AutoSkipKeyCode;
            
            if (keyCode == -1)
            {
                // 使用鼠标点击
                if (_useBackgroundOperation && _postMessageSimulator != null)
                {
                    // 后台模式：使用 PostMessage
                    if (_postMessageSimulator.LeftButtonClickBackground())
                    {
                        RecordSkip("对话跳过（后台鼠标点击）");
                    }
                    else
                    {
                        Log("对话跳过后台鼠标点击失败", 2);
                    }
                }
                else
                {
                    // 前台模式：使用常规输入
                    var mouse = _inputService.Mouse;
                    if (mouse != null && mouse.Click(1))
                    {
                        RecordSkip("对话跳过（鼠标点击）");
                    }
                    else
                    {
                        Log("对话跳过鼠标点击失败", 2);
                    }
                }
            }
            else
            {
                // 使用键盘按键
                if (_useBackgroundOperation && _postMessageSimulator != null)
                {
                    // 后台模式：使用 PostMessage
                    if (_postMessageSimulator.KeyPressBackground(keyCode))
                    {
                        RecordSkip($"对话跳过（后台按键 {keyCode}）");
                    }
                    else
                    {
                        Log("对话跳过后台按键发送失败", 2);
                    }
                }
                else
                {
                    // 前台模式：使用常规输入
                    var keyboard = _inputService.Keyboard;
                    if (keyboard.PressAndRelease(keyCode))
                    {
                        RecordSkip($"对话跳过（按键 {keyCode}）");
                    }
                    else
                    {
                        Log("对话跳过按键发送失败", 2);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"对话跳过执行异常: {ex.Message}", 2);
        }
    }
    
    /// <summary>
    /// 执行选项选择
    /// 需求: 18.4 - 检测到选项时自动选择（可配置）
    /// 支持后台运行模式
    /// </summary>
    private void ExecuteOptionSelect(AppSettings settings)
    {
        try
        {
            var optionMode = settings.AutoSkipOptionMode;
            
            // 根据选项模式执行不同的选择策略
            bool success = false;
            string modeText = optionMode switch
            {
                0 => "第一个选项",
                1 => "最后一个选项",
                2 => "随机选项",
                _ => "默认"
            };
            
            if (_useBackgroundOperation && _postMessageSimulator != null)
            {
                // 后台模式：使用 PostMessage
                success = _postMessageSimulator.LeftButtonClickBackground();
                if (success)
                {
                    RecordSkip($"选项选择（后台 - {modeText}）");
                }
            }
            else
            {
                // 前台模式：使用常规输入
                var mouse = _inputService.Mouse;
                if (mouse == null)
                {
                    Log("鼠标接口不可用", 2);
                    return;
                }
                
                success = mouse.Click(1);
                if (success)
                {
                    RecordSkip($"选项选择（{modeText}）");
                }
            }
            
            if (!success)
            {
                Log($"选项选择失败: {modeText}", 2);
            }
        }
        catch (Exception ex)
        {
            Log($"选项选择执行异常: {ex.Message}", 2);
        }
    }
    
    /// <summary>
    /// 记录跳过操作
    /// </summary>
    private void RecordSkip(string action)
    {
        lock (_stateLock)
        {
            _lastSkipTime = DateTime.Now;
            _skipCount++;
        }
        LogWithDedupe($"执行: {action}", 1);
    }
    
    #endregion
    
    #region 辅助方法
    
    /// <summary>
    /// 获取跳过检测区域
    /// </summary>
    private Mat? GetSkipRegion(Mat fullImage, int[] region)
    {
        try
        {
            var x = Math.Max(0, region[0]);
            var y = Math.Max(0, region[1]);
            var width = Math.Min(region[2], fullImage.Width - x);
            var height = Math.Min(region[3], fullImage.Height - y);
            
            if (width <= 0 || height <= 0)
                return null;
            
            var rect = new Rect(x, y, width, height);
            return new Mat(fullImage, rect);
        }
        catch
        {
            return null;
        }
    }
    
    #endregion
    
    #region 统计方法
    
    /// <summary>
    /// 获取跳过次数
    /// </summary>
    public int GetSkipCount()
    {
        lock (_stateLock) { return _skipCount; }
    }
    
    /// <summary>
    /// 重置跳过次数
    /// </summary>
    public void ResetSkipCount()
    {
        lock (_stateLock) { _skipCount = 0; }
    }
    
    /// <summary>
    /// 获取上次跳过时间
    /// </summary>
    public DateTime GetLastSkipTime()
    {
        lock (_stateLock) { return _lastSkipTime; }
    }
    
    /// <summary>
    /// 获取上次检测到的对话状态
    /// </summary>
    public DialogState GetLastDialogState()
    {
        return _lastDialogState;
    }
    
    #endregion
    
    #region 日志方法（带去重）
    
    /// <summary>
    /// 记录日志（带去重）
    /// </summary>
    private void LogWithDedupe(string message, int level)
    {
        if (_lastLogText != message || 
            (_lastLogText == message && Math.Abs(_currentFrameIndex - _lastLogFrameIndex) >= LogDedupeFrameThreshold))
        {
            Log(message, level);
        }
        
        _lastLogText = message;
        _lastLogFrameIndex = _currentFrameIndex;
    }
    
    /// <summary>
    /// 记录日志
    /// </summary>
    private void Log(string message, int level)
    {
        var logLevel = level switch
        {
            0 => Interfaces.LogLevel.Debug,
            1 => Interfaces.LogLevel.Info,
            2 => Interfaces.LogLevel.Warning,
            3 => Interfaces.LogLevel.Error,
            _ => Interfaces.LogLevel.Info
        };
        _logService.Log($"[自动剧情跳过] {message}", logLevel, "AutoSkipTrigger");
    }
    
    #endregion
    
    #region IDisposable 实现
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
    
    #endregion
}
