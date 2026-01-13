using System.Diagnostics;
using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Recognition.OCR;
using ShineProCS.Core.Services;
using ShineProCS.Models;
using OpenCvSharp;

namespace ShineProCS.Core.GameTask.Triggers;

/// <summary>
/// 自动拾取触发器
/// 实现 ITaskTrigger 接口，检测游戏中的拾取提示并自动按 F 键拾取
/// 参考 BetterGI 的 AutoPickTrigger 实现
/// 需求: 17.1 - 自动拾取作为 ITaskTrigger 实现
/// 需求: 17.2 - 通过 OCR 或模板匹配检测拾取提示
/// 需求: 17.3 - 支持黑名单和白名单配置
/// 需求: 17.4 - 检测到可拾取内容时自动发送 F 键
/// 需求: 17.5 - 支持配置触发间隔
/// </summary>
public class AutoPickTrigger : ITaskTrigger, IDisposable
{
    #region 依赖组件
    
    private readonly IInputService _inputService;
    private readonly IOcrService? _ocrService;
    private readonly ConfigManager _configManager;
    private readonly ILogService _logService;
    
    #endregion
    
    #region 运行状态
    
    private DateTime _lastPickTime = DateTime.MinValue;
    private int _pickCount;
    private bool _disposed;
    private readonly object _stateLock = new();
    
    // 日志去重相关
    private string _lastLogText = string.Empty;
    private int _lastLogFrameIndex = -1;
    private int _currentFrameIndex;
    private const int LogDedupeFrameThreshold = 5; // 相同文字5帧内只输出一次
    
    #endregion
    
    #region ITaskTrigger 实现
    
    /// <summary>
    /// 触发器名称
    /// </summary>
    public string Name => "自动拾取";
    
    /// <summary>
    /// 触发器优先级（数值越大越先执行）
    /// 自动拾取优先级设置为 50，低于技能循环（100）
    /// </summary>
    public int Priority => 50;
    
    private bool _isEnabled;
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            Log($"自动拾取触发器已{(value ? "启用" : "禁用")}", 1);
        }
    }
    
    /// <summary>
    /// 是否处于独占模式
    /// 自动拾取不需要独占，可以与其他触发器并行
    /// </summary>
    public bool IsExclusive => false;
    
    #endregion
    
    #region 构造函数
    
    /// <summary>
    /// 创建自动拾取触发器
    /// </summary>
    public AutoPickTrigger(
        IInputService inputService,
        IOcrService? ocrService,
        ConfigManager configManager,
        ILogService logService)
    {
        _inputService = inputService ?? throw new ArgumentNullException(nameof(inputService));
        _ocrService = ocrService;
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        
        _isEnabled = _configManager.AppSettings.EnableAutoPick;
    }
    
    #endregion
    
    #region ITaskTrigger 接口方法
    
    /// <summary>
    /// 初始化触发器
    /// </summary>
    public void Init()
    {
        Log("自动拾取触发器已初始化", 1);
        
        if (_ocrService == null)
        {
            Log("警告: OCR 服务不可用，自动拾取功能将受限", 2);
        }
    }
    
    /// <summary>
    /// 捕获图像后的处理
    /// </summary>
    public void OnCapture(CaptureContent content)
    {
        if (!_isEnabled || content.Image == null || content.Image.Empty())
            return;
        
        _currentFrameIndex++;
        var speedTimer = new SpeedTimer("AutoPick");
        
        var settings = _configManager.AppSettings;
        var interval = settings.AutoPickInterval;
        
        lock (_stateLock)
        {
            if ((DateTime.Now - _lastPickTime).TotalMilliseconds < interval)
                return;
        }
        
        try
        {
            var region = settings.AutoPickRegion;
            if (region == null || region.Length < 4)
                return;
            
            // 裁剪检测区域
            using var pickRegion = GetPickRegion(content.Image, region);
            if (pickRegion == null || pickRegion.Empty())
                return;
            
            speedTimer.Record("裁剪区域");

            // 提取文字区域（提高 OCR 准确率）
            using var textRegion = ExtractTextRegion(pickRegion);
            speedTimer.Record("提取文字区域");
            
            // 执行 OCR 识别
            var rawText = DetectPickPrompt(textRegion ?? pickRegion, settings);
            speedTimer.Record("OCR识别");
            
            if (string.IsNullOrWhiteSpace(rawText))
            {
                speedTimer.DebugPrint();
                return;
            }
            
            // OCR 文字后处理
            var processedText = ProcessOcrText(rawText);
            speedTimer.Record("文字后处理");
            
            // 检查是否应该拾取
            if (ShouldPick(processedText, settings))
            {
                LogPickWithDedupe(processedText);
                ExecutePick(settings.AutoPickKeyCode);
                speedTimer.Record("执行拾取");
            }
            
            speedTimer.DebugPrint();
        }
        catch (Exception ex)
        {
            Log($"自动拾取检测异常: {ex.Message}", 2);
        }
    }
    
    #endregion
    
    #region 文字区域提取 (参考 BetterGI TextRectExtractor)
    
    /// <summary>
    /// 从图片中提取文字范围
    /// 参考 BetterGI 的 TextRectExtractor.GetTextBoundingRect
    /// </summary>
    private Mat? ExtractTextRegion(Mat textMat, double minThreshold = 160, double maxThreshold = 255)
    {
        try
        {
            // 转换为灰度图
            Mat gray;
            if (textMat.Channels() == 3)
            {
                gray = new Mat();
                Cv2.CvtColor(textMat, gray, ColorConversionCodes.BGR2GRAY);
            }
            else
            {
                gray = textMat.Clone();
            }
            
            // 二值化处理
            using var bin = new Mat();
            Cv2.Threshold(gray, bin, minThreshold, maxThreshold, ThresholdTypes.Binary);
            
            // 形态学操作：先腐蚀后膨胀，去除噪点并保持文字完整
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
            Cv2.Erode(bin, bin, kernel, iterations: 1);
            Cv2.Dilate(bin, bin, kernel, iterations: 2);
            
            // 投影获取文字边界
            var boundingRect = GetProjectionRect(textMat, bin);
            gray.Dispose();
            
            if (boundingRect.Width <= 5 || boundingRect.Height <= 5)
                return null;
            
            // 裁剪只包含文字的区域
            var safeWidth = Math.Min(boundingRect.Right + 5, textMat.Width) - boundingRect.X;
            if (safeWidth <= 0)
                return null;
            
            return new Mat(textMat, new Rect(boundingRect.X, 0, safeWidth, textMat.Height));
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// 投影获取连续文字的边界矩形
    /// 参考 BetterGI 的 TextRectExtractor.ProjectionRect
    /// </summary>
    private static Rect GetProjectionRect(Mat textMat, Mat bin, int maxGap = 30)
    {
        try
        {
            // 对行做 ReduceSum，得到 1 x width 的列和
            using var projection = new Mat();
            Cv2.Reduce(bin, projection, 0, ReduceTypes.Sum, MatType.CV_32S);
            
            int width = projection.Cols;
            projection.GetArray(out int[] colSums);
            
            int gapCount = 0;
            int lastNonEmpty = -1;
            
            for (int x = 0; x < width; x++)
            {
                bool hasInk = colSums[x] > 0;
                if (hasInk)
                {
                    lastNonEmpty = x;
                    gapCount = 0;
                }
                else
                {
                    gapCount++;
                    if (gapCount > maxGap)
                        break;
                }
            }
            
            if (lastNonEmpty == -1)
                return new Rect();
            
            return new Rect(0, 0, lastNonEmpty, textMat.Height);
        }
        catch
        {
            return new Rect();
        }
    }
    
    #endregion
    
    #region OCR 文字后处理 (参考 BetterGI ProcessOcrText)
    
    /// <summary>
    /// 处理 OCR 识别的文字结果
    /// 参考 BetterGI 的 ProcessOcrText 方法
    /// 1. 替换【、[ 为「，替换】、] 为」
    /// 2. 清理左边非「字符和中文的字符
    /// 3. 清理右边非」字符和中文的字符  
    /// 4. 确保引号配对：有「必有」，有」必有「
    /// </summary>
    public static string ProcessOcrText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        
        // 使用 Span<char> 进行高性能处理
        Span<char> chars = stackalloc char[text.Length];
        text.AsSpan().CopyTo(chars);
        
        int writeIndex = 0;
        bool hasChanges = false;
        
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            
            // 跳过空白字符
            if (char.IsWhiteSpace(c))
            {
                hasChanges = true;
                continue;
            }
            
            // 替换括号字符
            if (c == '【' || c == '[')
            {
                chars[writeIndex++] = '「';
                hasChanges = true;
            }
            else if (c == '】' || c == ']')
            {
                chars[writeIndex++] = '」';
                hasChanges = true;
            }
            else
            {
                chars[writeIndex++] = c;
            }
        }
        
        ReadOnlySpan<char> span = hasChanges ? chars.Slice(0, writeIndex) : text.AsSpan();
        int start = 0;
        int end = span.Length - 1;
        
        // 从左边开始，删除非「字符和中文的字符
        while (start <= end)
        {
            char c = span[start];
            if (c == '「' || (c >= 0x4E00 && c <= 0x9FFF))
                break;
            start++;
        }
        
        // 从右边开始，删除非」字符和中文的字符
        while (end >= start)
        {
            char c = span[end];
            if (c == '」' || c == '！' || (c >= 0x4E00 && c <= 0x9FFF))
                break;
            end--;
        }
        
        if (start > end)
            return string.Empty;
        
        var cleanedSpan = span.Slice(start, end - start + 1);
        
        // 检查并补充引号配对
        bool hasLeftQuote = false;
        bool hasRightQuote = false;
        
        for (int i = 0; i < cleanedSpan.Length; i++)
        {
            if (cleanedSpan[i] == '「')
                hasLeftQuote = true;
            else if (cleanedSpan[i] == '」')
                hasRightQuote = true;
        }
        
        if (hasLeftQuote && !hasRightQuote)
            return string.Concat(cleanedSpan, "」");
        else if (hasRightQuote && !hasLeftQuote)
            return string.Concat("「", cleanedSpan);
        
        return cleanedSpan.ToString();
    }
    
    #endregion

    
    #region 图标排除检测 (参考 BetterGI)
    
    /// <summary>
    /// 检查是否应该排除（如 NPC 对话、设置图标等）
    /// 参考 BetterGI 的图标排除逻辑
    /// </summary>
    /// <param name="text">识别的文本</param>
    /// <returns>是否应该排除</returns>
    public static bool ShouldExclude(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return true;
        
        // 单个字符不拾取
        if (text.Length <= 1)
            return true;
        
        // 特殊内容过滤（参考 BetterGI 的 DoNotPick）
        // 唯一一个动态拾取项，特殊处理，不拾取
        if (text.Contains("长时间"))
            return true;
        
        // 纳塔部落中文名特殊处理，不拾取
        if (text.Contains("我在") && (text.Contains("声望") || text.Contains("回声") || 
            text.Contains("悬木人") || text.Contains("流泉")))
            return true;
        
        // 挪德卡莱聚所中文名特殊处理，不拾取
        if (text.Contains("聚所"))
            return true;
        
        if (text.Contains("霜月") && text.Contains("坊"))
            return true;
        
        if (text.Contains("叮铃") || text.Contains("眶螂") || 
            (text.Contains("蛋卷") && text.Contains("坊")))
            return true;
        
        if (text.Contains("西风成垒") || text.Contains("望崖营壁") || text.Contains("魔女的花园"))
            return true;
        
        return false;
    }
    
    #endregion
    
    #region 核心逻辑
    
    /// <summary>
    /// 获取拾取检测区域
    /// </summary>
    private Mat? GetPickRegion(Mat fullImage, int[] region)
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
    
    /// <summary>
    /// 检测拾取提示
    /// </summary>
    public string DetectPickPrompt(Mat image, AppSettings settings)
    {
        if (_ocrService == null)
            return string.Empty;
        
        try
        {
            var result = _ocrService.OcrResult(image);
            if (result != null && result.Regions.Length > 0)
            {
                var filteredRegions = result.Regions
                    .Where(r => r.Score >= settings.AutoPickConfidenceThreshold)
                    .ToArray();
                
                if (filteredRegions.Length > 0)
                    return new OcrResult(filteredRegions).Text;
            }
            
            // 降级到简单 OCR
            return _ocrService.Ocr(image);
        }
        catch (Exception ex)
        {
            Log($"OCR 识别失败: {ex.Message}", 2);
            return string.Empty;
        }
    }
    
    /// <summary>
    /// 判断是否应该拾取
    /// </summary>
    public bool ShouldPick(string text, AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        
        // 检查排除条件
        if (ShouldExclude(text))
            return false;
        
        // 检查是否包含拾取关键词
        var keywords = settings.AutoPickKeywords;
        var hasKeyword = keywords == null || keywords.Count == 0 ||
                         keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
        
        if (!hasKeyword)
            return false;
        
        // 检查黑名单
        if (IsInBlacklist(text, settings.AutoPickBlacklist))
        {
            LogWithDedupe($"物品在黑名单中，跳过: {text}", 0);
            return false;
        }
        
        // 检查白名单
        if (!IsAllowedByWhitelist(text, settings.AutoPickWhitelist))
        {
            LogWithDedupe($"物品不在白名单中，跳过: {text}", 0);
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// 检查文本是否在黑名单中
    /// </summary>
    public static bool IsInBlacklist(string text, ICollection<string>? blacklist)
    {
        if (blacklist == null || blacklist.Count == 0)
            return false;
        
        return blacklist.Any(item => 
            !string.IsNullOrWhiteSpace(item) && 
            text.Contains(item, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// 检查文本是否被白名单允许
    /// </summary>
    public static bool IsAllowedByWhitelist(string text, ICollection<string>? whitelist)
    {
        if (whitelist == null || whitelist.Count == 0)
            return true;
        
        return whitelist.Any(item => 
            !string.IsNullOrWhiteSpace(item) && 
            text.Contains(item, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// 执行拾取操作
    /// </summary>
    private void ExecutePick(int keyCode)
    {
        try
        {
            var keyboard = _inputService.Keyboard;
            if (keyboard.PressAndRelease(keyCode))
            {
                lock (_stateLock)
                {
                    _lastPickTime = DateTime.Now;
                    _pickCount++;
                }
            }
            else
            {
                Log("拾取按键发送失败", 2);
            }
        }
        catch (Exception ex)
        {
            Log($"拾取执行异常: {ex.Message}", 2);
        }
    }
    
    #endregion
    
    #region 黑白名单管理
    
    public void AddToBlacklist(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName)) return;
        var blacklist = _configManager.AppSettings.AutoPickBlacklist;
        if (!blacklist.Contains(itemName, StringComparer.OrdinalIgnoreCase))
        {
            blacklist.Add(itemName);
            Log($"已添加到黑名单: {itemName}", 1);
        }
    }
    
    public void RemoveFromBlacklist(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName)) return;
        var blacklist = _configManager.AppSettings.AutoPickBlacklist;
        var item = blacklist.FirstOrDefault(i => i.Equals(itemName, StringComparison.OrdinalIgnoreCase));
        if (item != null)
        {
            blacklist.Remove(item);
            Log($"已从黑名单移除: {itemName}", 1);
        }
    }
    
    public void AddToWhitelist(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName)) return;
        var whitelist = _configManager.AppSettings.AutoPickWhitelist;
        if (!whitelist.Contains(itemName, StringComparer.OrdinalIgnoreCase))
        {
            whitelist.Add(itemName);
            Log($"已添加到白名单: {itemName}", 1);
        }
    }
    
    public void RemoveFromWhitelist(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName)) return;
        var whitelist = _configManager.AppSettings.AutoPickWhitelist;
        var item = whitelist.FirstOrDefault(i => i.Equals(itemName, StringComparison.OrdinalIgnoreCase));
        if (item != null)
        {
            whitelist.Remove(item);
            Log($"已从白名单移除: {itemName}", 1);
        }
    }
    
    public void ClearBlacklist()
    {
        _configManager.AppSettings.AutoPickBlacklist.Clear();
        Log("已清空黑名单", 1);
    }
    
    public void ClearWhitelist()
    {
        _configManager.AppSettings.AutoPickWhitelist.Clear();
        Log("已清空白名单", 1);
    }
    
    #endregion
    
    #region 统计方法
    
    public int GetPickCount()
    {
        lock (_stateLock) { return _pickCount; }
    }
    
    public void ResetPickCount()
    {
        lock (_stateLock) { _pickCount = 0; }
    }
    
    public DateTime GetLastPickTime()
    {
        lock (_stateLock) { return _lastPickTime; }
    }
    
    public bool CanExecutePick(int interval)
    {
        lock (_stateLock)
        {
            return (DateTime.Now - _lastPickTime).TotalMilliseconds >= interval;
        }
    }
    
    #endregion

    
    #region 日志方法（带去重）
    
    /// <summary>
    /// 记录拾取日志（带去重）
    /// 相同文字在指定帧数内只输出一次
    /// 参考 BetterGI 的 LogPick 方法
    /// </summary>
    private void LogPickWithDedupe(string text)
    {
        if (_lastLogText != text || 
            (_lastLogText == text && Math.Abs(_currentFrameIndex - _lastLogFrameIndex) >= LogDedupeFrameThreshold))
        {
            Log($"交互或拾取：{text}", 1);
        }
        
        _lastLogText = text;
        _lastLogFrameIndex = _currentFrameIndex;
    }
    
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
        _logService.Log($"[自动拾取] {message}", logLevel, "AutoPickTrigger");
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

#region SpeedTimer 计时器类

/// <summary>
/// 性能计时器
/// 参考 BetterGI 的 SpeedTimer 实现
/// 用于记录各阶段耗时
/// </summary>
public class SpeedTimer
{
    private readonly Stopwatch _stopwatch;
    private readonly Dictionary<string, TimeSpan> _timeRecordDic = [];
    private readonly string _name;
    
    public SpeedTimer(string name = "")
    {
        _name = name;
        _stopwatch = new Stopwatch();
        _stopwatch.Start();
    }
    
    /// <summary>
    /// 记录当前阶段耗时
    /// </summary>
    public void Record(string name)
    {
        _timeRecordDic[name] = _stopwatch.Elapsed;
        _stopwatch.Restart();
    }
    
    /// <summary>
    /// 输出调试信息
    /// </summary>
    public void DebugPrint()
    {
        var msg = _name;
        if (!string.IsNullOrEmpty(msg))
            msg += " : ";
        
        foreach (var pair in _timeRecordDic)
        {
            msg += $"{pair.Key}:{pair.Value.TotalMilliseconds:F2}ms,";
        }
        
        if (msg.Length > 0)
        {
            Debug.WriteLine(msg.TrimEnd(','));
        }
        
        _stopwatch.Stop();
    }
    
    /// <summary>
    /// 获取总耗时
    /// </summary>
    public double GetTotalMilliseconds()
    {
        return _timeRecordDic.Values.Sum(t => t.TotalMilliseconds);
    }
}

#endregion
