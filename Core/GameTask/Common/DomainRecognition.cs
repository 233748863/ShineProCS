using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Recognition.OCR;
using ShineProCS.Core.Recognition.YOLO;
using ShineProCS.Core.Services;
using ShineProCS.Models;
using OpenCvSharp;
using Point = OpenCvSharp.Point;

namespace ShineProCS.Core.GameTask.Common;

/// <summary>
/// 秘境识别服务
/// 提供秘境入口、古树位置等识别功能
/// 需求: 19.6 - 支持自动识别秘境入口和古树位置
/// </summary>
public class DomainRecognition
{
    private readonly ICaptureService _captureService;
    private readonly IOcrService? _ocrService;
    private readonly IYoloService? _yoloService;
    private readonly ILogService _logService;
    private readonly ConfigManager _configManager;
    
    /// <summary>
    /// 识别结果
    /// </summary>
    public class RecognitionResult
    {
        /// <summary>
        /// 是否识别成功
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// 识别到的文本
        /// </summary>
        public string Text { get; set; } = "";
        
        /// <summary>
        /// 识别到的位置（屏幕坐标）
        /// </summary>
        public Point? Position { get; set; }
        
        /// <summary>
        /// 边界框
        /// </summary>
        public Rect? BoundingBox { get; set; }
        
        /// <summary>
        /// 置信度
        /// </summary>
        public double Confidence { get; set; }
        
        /// <summary>
        /// 识别类型
        /// </summary>
        public string Type { get; set; } = "";
    }
    
    /// <summary>
    /// 秘境入口信息
    /// </summary>
    public class DomainEntranceInfo
    {
        /// <summary>
        /// 是否检测到入口
        /// </summary>
        public bool Found { get; set; }
        
        /// <summary>
        /// 入口位置
        /// </summary>
        public Point? Position { get; set; }
        
        /// <summary>
        /// 秘境名称
        /// </summary>
        public string DomainName { get; set; } = "";
        
        /// <summary>
        /// 秘境类型
        /// </summary>
        public DomainHelper.DomainType Type { get; set; } = DomainHelper.DomainType.Unknown;
        
        /// <summary>
        /// 是否可以进入（有交互提示）
        /// </summary>
        public bool CanEnter { get; set; }
        
        /// <summary>
        /// 置信度
        /// </summary>
        public double Confidence { get; set; }
    }
    
    /// <summary>
    /// 古树信息
    /// </summary>
    public class TreeInfo
    {
        /// <summary>
        /// 是否检测到古树
        /// </summary>
        public bool Found { get; set; }
        
        /// <summary>
        /// 古树位置
        /// </summary>
        public Point? Position { get; set; }
        
        /// <summary>
        /// 是否可以交互
        /// </summary>
        public bool CanInteract { get; set; }
        
        /// <summary>
        /// 置信度
        /// </summary>
        public double Confidence { get; set; }
    }
    
    /// <summary>
    /// 体力信息
    /// </summary>
    public class ResinInfo
    {
        /// <summary>
        /// 当前体力
        /// </summary>
        public int Current { get; set; }
        
        /// <summary>
        /// 最大体力
        /// </summary>
        public int Max { get; set; } = 160;
        
        /// <summary>
        /// 是否识别成功
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// 原始识别文本
        /// </summary>
        public string RawText { get; set; } = "";
    }
    
    public DomainRecognition(
        ICaptureService captureService,
        ILogService logService,
        ConfigManager configManager,
        IOcrService? ocrService = null,
        IYoloService? yoloService = null)
    {
        _captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _ocrService = ocrService;
        _yoloService = yoloService;
    }
    
    /// <summary>
    /// 识别秘境入口
    /// 需求: 19.6 - 支持自动识别秘境入口
    /// </summary>
    public DomainEntranceInfo DetectDomainEntrance()
    {
        var result = new DomainEntranceInfo();
        var region = _configManager.AppSettings.AutoDomainEntranceRegion;
        
        if (region == null || region.Length < 4 || region.All(v => v == 0))
        {
            Log("秘境入口检测区域未配置", 2);
            return result;
        }
        
        var screenshot = _captureService.GetScreenRegion(region[0], region[1], region[2], region[3]);
        if (screenshot == null)
        {
            return result;
        }
        
        try
        {
            // 使用 OCR 检测入口文字
            if (_ocrService != null)
            {
                var ocrResult = _ocrService.OcrResult(screenshot);
                if (ocrResult != null && ocrResult.Regions.Length > 0)
                {
                    var keywords = _configManager.AppSettings.AutoDomainEntranceKeywords;
                    
                    foreach (var textRegion in ocrResult.Regions)
                    {
                        var text = textRegion.Text;
                        
                        // 检查是否包含秘境相关关键词
                        if (keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
                        {
                            result.Found = true;
                            result.DomainName = ExtractDomainName(text);
                            result.Type = DetermineDomainType(text);
                            result.Confidence = textRegion.Score;
                            
                            // 计算位置（使用 RotatedRect 的 Center 和 Size）
                            var rect = textRegion.Rect;
                            result.Position = new Point(
                                region[0] + (int)rect.Center.X,
                                region[1] + (int)rect.Center.Y);
                            
                            // 检查是否有交互提示（F键）
                            result.CanEnter = text.Contains("F") || 
                                              ocrResult.Text.Contains("进入") ||
                                              ocrResult.Text.Contains("挑战");
                            
                            Log($"检测到秘境入口: {result.DomainName}, 类型: {result.Type}", 1);
                            break;
                        }
                    }
                }
            }
            
            // 使用 YOLO 检测入口图标
            if (!result.Found && _yoloService != null && _yoloService.IsInitialized)
            {
                var detections = _yoloService.Detect(screenshot, new[] { "domain_entrance", "interact_icon", "door" });
                
                if (detections.Detections.Count > 0)
                {
                    var detection = detections.Detections[0];
                    result.Found = true;
                    result.Confidence = detection.Confidence;
                    result.Position = new Point(
                        region[0] + detection.BoundingBox.X + detection.BoundingBox.Width / 2,
                        region[1] + detection.BoundingBox.Y + detection.BoundingBox.Height / 2);
                    result.CanEnter = true;
                    
                    Log($"YOLO 检测到秘境入口，置信度: {detection.Confidence:F2}", 1);
                }
            }
        }
        catch (Exception ex)
        {
            Log($"秘境入口识别异常: {ex.Message}", 2);
        }
        finally
        {
            _captureService.ReturnMat(screenshot);
        }
        
        return result;
    }
    
    /// <summary>
    /// 识别古树位置
    /// 需求: 19.6 - 支持自动识别古树位置
    /// </summary>
    public TreeInfo DetectTree()
    {
        var result = new TreeInfo();
        var region = _configManager.AppSettings.AutoDomainTreeRegion;
        
        if (region == null || region.Length < 4 || region.All(v => v == 0))
        {
            Log("古树检测区域未配置", 2);
            return result;
        }
        
        var screenshot = _captureService.GetScreenRegion(region[0], region[1], region[2], region[3]);
        if (screenshot == null)
        {
            return result;
        }
        
        try
        {
            // 使用 OCR 检测古树文字
            if (_ocrService != null)
            {
                var ocrResult = _ocrService.OcrResult(screenshot);
                if (ocrResult != null && ocrResult.Regions.Length > 0)
                {
                    var treeKeywords = new[] { "古树", "地脉", "花", "领取", "奖励" };
                    
                    foreach (var textRegion in ocrResult.Regions)
                    {
                        var text = textRegion.Text;
                        
                        if (treeKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
                        {
                            result.Found = true;
                            result.Confidence = textRegion.Score;
                            
                            var rect = textRegion.Rect;
                            result.Position = new Point(
                                region[0] + (int)rect.Center.X,
                                region[1] + (int)rect.Center.Y);
                            
                            result.CanInteract = text.Contains("F") || text.Contains("领取");
                            
                            Log($"检测到古树，可交互: {result.CanInteract}", 1);
                            break;
                        }
                    }
                }
            }
            
            // 使用 YOLO 检测古树
            if (!result.Found && _yoloService != null && _yoloService.IsInitialized)
            {
                var detections = _yoloService.Detect(screenshot, new[] { "tree", "ley_line_blossom", "reward_tree" });
                
                if (detections.Detections.Count > 0)
                {
                    var detection = detections.Detections[0];
                    result.Found = true;
                    result.Confidence = detection.Confidence;
                    result.Position = new Point(
                        region[0] + detection.BoundingBox.X + detection.BoundingBox.Width / 2,
                        region[1] + detection.BoundingBox.Y + detection.BoundingBox.Height / 2);
                    result.CanInteract = true;
                    
                    Log($"YOLO 检测到古树，置信度: {detection.Confidence:F2}", 1);
                }
            }
        }
        catch (Exception ex)
        {
            Log($"古树识别异常: {ex.Message}", 2);
        }
        finally
        {
            _captureService.ReturnMat(screenshot);
        }
        
        return result;
    }
    
    /// <summary>
    /// 识别体力
    /// 需求: 19.5 - 体力检测
    /// </summary>
    public ResinInfo DetectResin()
    {
        var result = new ResinInfo();
        var region = _configManager.AppSettings.AutoDomainResinRegion;
        
        if (region == null || region.Length < 4 || region.All(v => v == 0))
        {
            Log("体力检测区域未配置，使用默认值", 0);
            result.Current = 160;
            result.Success = true;
            return result;
        }
        
        var screenshot = _captureService.GetScreenRegion(region[0], region[1], region[2], region[3]);
        if (screenshot == null)
        {
            result.Current = 160;
            result.Success = true;
            return result;
        }
        
        try
        {
            if (_ocrService != null)
            {
                var text = _ocrService.Ocr(screenshot);
                result.RawText = text;
                
                if (!string.IsNullOrEmpty(text))
                {
                    // 尝试解析体力值（格式可能是 "120/160" 或 "120"）
                    var numbers = ExtractNumbers(text);
                    
                    if (numbers.Count >= 2)
                    {
                        // 格式: 当前/最大
                        result.Current = numbers[0];
                        result.Max = numbers[1];
                        result.Success = true;
                    }
                    else if (numbers.Count == 1)
                    {
                        result.Current = numbers[0];
                        result.Success = true;
                    }
                    
                    Log($"体力识别: {result.Current}/{result.Max}", 0);
                }
            }
            
            if (!result.Success)
            {
                // OCR 失败时返回默认值
                result.Current = 160;
                result.Success = true;
            }
        }
        catch (Exception ex)
        {
            Log($"体力识别异常: {ex.Message}", 2);
            result.Current = 160;
            result.Success = true;
        }
        finally
        {
            _captureService.ReturnMat(screenshot);
        }
        
        return result;
    }
    
    /// <summary>
    /// 检测奖励界面
    /// </summary>
    public RecognitionResult DetectRewardScreen()
    {
        var result = new RecognitionResult { Type = "reward" };
        var region = _configManager.AppSettings.AutoDomainRewardRegion;
        
        if (region == null || region.Length < 4 || region.All(v => v == 0))
        {
            return result;
        }
        
        var screenshot = _captureService.GetScreenRegion(region[0], region[1], region[2], region[3]);
        if (screenshot == null)
        {
            return result;
        }
        
        try
        {
            if (_ocrService != null)
            {
                var ocrResult = _ocrService.OcrResult(screenshot);
                if (ocrResult != null)
                {
                    result.Text = ocrResult.Text;
                    
                    var rewardKeywords = new[] { "挑战成功", "奖励", "领取", "完成", "继续挑战", "退出" };
                    
                    if (rewardKeywords.Any(k => result.Text.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    {
                        result.Success = true;
                        result.Confidence = ocrResult.Regions.Length > 0 
                            ? ocrResult.Regions.Max(r => r.Score) 
                            : 0.8;
                        
                        Log("检测到奖励界面", 1);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"奖励界面识别异常: {ex.Message}", 2);
        }
        finally
        {
            _captureService.ReturnMat(screenshot);
        }
        
        return result;
    }
    
    /// <summary>
    /// 检测战斗结束
    /// </summary>
    public bool DetectCombatEnd()
    {
        // 检测奖励界面
        var rewardResult = DetectRewardScreen();
        if (rewardResult.Success)
        {
            return true;
        }
        
        // 检测古树
        var treeResult = DetectTree();
        if (treeResult.Found)
        {
            return true;
        }
        
        return false;
    }
    
    #region 辅助方法
    
    /// <summary>
    /// 从文本中提取秘境名称
    /// </summary>
    private string ExtractDomainName(string text)
    {
        // 简单实现：返回包含"秘境"的部分
        var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.Contains("秘境") || line.Contains("之"))
            {
                return line.Trim();
            }
        }
        return text.Trim();
    }
    
    /// <summary>
    /// 根据文本判断秘境类型
    /// </summary>
    private DomainHelper.DomainType DetermineDomainType(string text)
    {
        if (text.Contains("天赋") || text.Contains("哲学") || text.Contains("指南"))
            return DomainHelper.DomainType.TalentMaterial;
        
        if (text.Contains("武器") || text.Contains("浮世"))
            return DomainHelper.DomainType.WeaponMaterial;
        
        if (text.Contains("圣遗物") || text.Contains("祝圣"))
            return DomainHelper.DomainType.Artifact;
        
        if (text.Contains("周本") || text.Contains("风龙") || text.Contains("公子") || text.Contains("若陀"))
            return DomainHelper.DomainType.WeeklyBoss;
        
        if (text.Contains("深渊") || text.Contains("螺旋"))
            return DomainHelper.DomainType.SpiralAbyss;
        
        return DomainHelper.DomainType.Unknown;
    }
    
    /// <summary>
    /// 从文本中提取数字
    /// </summary>
    private static List<int> ExtractNumbers(string text)
    {
        var numbers = new List<int>();
        var currentNumber = "";
        
        foreach (var c in text)
        {
            if (char.IsDigit(c))
            {
                currentNumber += c;
            }
            else if (currentNumber.Length > 0)
            {
                if (int.TryParse(currentNumber, out var num))
                {
                    numbers.Add(num);
                }
                currentNumber = "";
            }
        }
        
        if (currentNumber.Length > 0 && int.TryParse(currentNumber, out var lastNum))
        {
            numbers.Add(lastNum);
        }
        
        return numbers;
    }
    
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
        
        _logService.Log($"[秘境识别] {message}", logLevel, "DomainRecognition");
    }
    
    #endregion
}
