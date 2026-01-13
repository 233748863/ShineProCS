using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Recognition.OCR;
using ShineProCS.Core.Recognition.YOLO;
using OpenCvSharp;

namespace ShineProCS.Core.GameTask.Common;

/// <summary>
/// 秘境辅助类
/// 提供秘境相关的通用检测和操作方法
/// 需求: 19.2 - 支持自动进入秘境、战斗、领取奖励的完整流程
/// </summary>
public class DomainHelper
{
    private readonly ICaptureService _captureService;
    private readonly IInputService _inputService;
    private readonly IOcrService? _ocrService;
    private readonly IYoloService? _yoloService;
    private readonly ILogService _logService;
    
    // 虚拟键码
    private const int VK_F = 0x46;
    private const int VK_ESCAPE = 0x1B;
    private const int VK_SPACE = 0x20;
    private const int VK_W = 0x57;
    private const int VK_A = 0x41;
    private const int VK_S = 0x53;
    private const int VK_D = 0x44;
    
    /// <summary>
    /// 秘境类型
    /// </summary>
    public enum DomainType
    {
        /// <summary>
        /// 未知类型
        /// </summary>
        Unknown,
        
        /// <summary>
        /// 天赋材料秘境
        /// </summary>
        TalentMaterial,
        
        /// <summary>
        /// 武器突破材料秘境
        /// </summary>
        WeaponMaterial,
        
        /// <summary>
        /// 圣遗物秘境
        /// </summary>
        Artifact,
        
        /// <summary>
        /// 周本
        /// </summary>
        WeeklyBoss,
        
        /// <summary>
        /// 深渊
        /// </summary>
        SpiralAbyss
    }
    
    /// <summary>
    /// 秘境状态信息
    /// </summary>
    public class DomainStatus
    {
        /// <summary>
        /// 是否在秘境内
        /// </summary>
        public bool IsInDomain { get; set; }
        
        /// <summary>
        /// 是否在战斗中
        /// </summary>
        public bool IsInCombat { get; set; }
        
        /// <summary>
        /// 是否显示奖励界面
        /// </summary>
        public bool IsRewardShown { get; set; }
        
        /// <summary>
        /// 是否显示古树
        /// </summary>
        public bool IsTreeVisible { get; set; }
        
        /// <summary>
        /// 是否在加载中
        /// </summary>
        public bool IsLoading { get; set; }
        
        /// <summary>
        /// 当前体力
        /// </summary>
        public int CurrentResin { get; set; }
        
        /// <summary>
        /// 秘境类型
        /// </summary>
        public DomainType Type { get; set; } = DomainType.Unknown;
    }
    
    public DomainHelper(
        ICaptureService captureService,
        IInputService inputService,
        ILogService logService,
        IOcrService? ocrService = null,
        IYoloService? yoloService = null)
    {
        _captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
        _inputService = inputService ?? throw new ArgumentNullException(nameof(inputService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _ocrService = ocrService;
        _yoloService = yoloService;
    }
    
    /// <summary>
    /// 获取当前秘境状态
    /// </summary>
    public DomainStatus GetCurrentStatus(int[] detectionRegion)
    {
        var status = new DomainStatus();
        
        try
        {
            var screenshot = _captureService.GetScreenRegion(
                detectionRegion[0], detectionRegion[1],
                detectionRegion[2], detectionRegion[3]);
            
            if (screenshot == null)
            {
                return status;
            }
            
            try
            {
                // 检测加载状态
                status.IsLoading = IsLoadingScreen(screenshot);
                
                if (!status.IsLoading)
                {
                    // 检测是否在秘境内
                    status.IsInDomain = DetectInDomain(screenshot);
                    
                    // 检测战斗状态
                    status.IsInCombat = DetectCombatState(screenshot);
                    
                    // 检测奖励界面
                    status.IsRewardShown = DetectRewardScreen(screenshot);
                    
                    // 检测古树
                    status.IsTreeVisible = DetectTree(screenshot);
                }
            }
            finally
            {
                _captureService.ReturnMat(screenshot);
            }
        }
        catch (Exception ex)
        {
            Log($"获取秘境状态异常: {ex.Message}", 2);
        }
        
        return status;
    }
    
    /// <summary>
    /// 检测是否为加载画面
    /// </summary>
    private bool IsLoadingScreen(Mat image)
    {
        try
        {
            // 加载画面通常较暗或有特定图案
            var brightness = CalculateAverageBrightness(image);
            return brightness < 20; // 非常暗的画面认为是加载中
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 检测是否在秘境内
    /// </summary>
    private bool DetectInDomain(Mat image)
    {
        if (_ocrService == null) return false;
        
        try
        {
            var text = _ocrService.Ocr(image);
            if (string.IsNullOrEmpty(text)) return false;
            
            // 秘境内通常有特定UI元素
            var domainKeywords = new[] { "挑战", "秘境", "剩余", "时间", "波次" };
            return domainKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 检测战斗状态
    /// </summary>
    private bool DetectCombatState(Mat image)
    {
        try
        {
            // 战斗中通常有敌人血条、技能CD等
            // 这里使用简单的亮度和颜色分析
            var brightness = CalculateAverageBrightness(image);
            
            // 战斗场景通常亮度适中
            return brightness > 30 && brightness < 200;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 检测奖励界面
    /// </summary>
    private bool DetectRewardScreen(Mat image)
    {
        if (_ocrService == null) return false;
        
        try
        {
            var text = _ocrService.Ocr(image);
            if (string.IsNullOrEmpty(text)) return false;
            
            var rewardKeywords = new[] { "挑战成功", "奖励", "领取", "完成", "继续挑战" };
            return rewardKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 检测古树
    /// </summary>
    private bool DetectTree(Mat image)
    {
        if (_ocrService == null) return false;
        
        try
        {
            var text = _ocrService.Ocr(image);
            if (string.IsNullOrEmpty(text)) return false;
            
            var treeKeywords = new[] { "古树", "地脉", "花", "领取" };
            return treeKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 执行进入秘境操作
    /// 需求: 19.2 - 自动进入秘境
    /// </summary>
    public async Task<bool> EnterDomainAsync(CancellationToken ct, int timeoutMs = 30000)
    {
        var startTime = DateTime.Now;
        var timeout = TimeSpan.FromMilliseconds(timeoutMs);
        
        while (!ct.IsCancellationRequested && DateTime.Now - startTime < timeout)
        {
            // 按 F 键尝试进入
            _inputService.Keyboard.PressAndRelease(VK_F);
            await Task.Delay(500, ct);
            
            // 检查是否出现确认界面
            // 如果出现，再按一次确认
            _inputService.Keyboard.PressAndRelease(VK_F);
            await Task.Delay(2000, ct);
            
            // 等待加载完成
            var loadStart = DateTime.Now;
            while (!ct.IsCancellationRequested && DateTime.Now - loadStart < TimeSpan.FromSeconds(20))
            {
                await Task.Delay(500, ct);
                
                // 检测是否加载完成
                var detectionRegion = new[] { 0, 0, 400, 300 };
                var status = GetCurrentStatus(detectionRegion);
                
                if (!status.IsLoading && status.IsInDomain)
                {
                    Log("成功进入秘境", 1);
                    return true;
                }
            }
        }
        
        Log("进入秘境超时", 2);
        return false;
    }
    
    /// <summary>
    /// 执行领取奖励操作
    /// 需求: 19.2 - 自动领取奖励
    /// </summary>
    public async Task<bool> ClaimRewardAsync(CancellationToken ct, int delayMs = 2000)
    {
        try
        {
            // 等待奖励界面稳定
            await Task.Delay(1000, ct);
            
            // 按 F 键领取
            _inputService.Keyboard.PressAndRelease(VK_F);
            await Task.Delay(delayMs, ct);
            
            // 确认领取
            _inputService.Keyboard.PressAndRelease(VK_F);
            await Task.Delay(500, ct);
            
            Log("已领取奖励", 1);
            return true;
        }
        catch (Exception ex)
        {
            Log($"领取奖励异常: {ex.Message}", 2);
            return false;
        }
    }
    
    /// <summary>
    /// 执行退出秘境操作
    /// </summary>
    public async Task ExitDomainAsync(CancellationToken ct)
    {
        try
        {
            // 按 ESC 打开菜单
            _inputService.Keyboard.PressAndRelease(VK_ESCAPE);
            await Task.Delay(500, ct);
            
            // 选择离开选项
            _inputService.Keyboard.PressAndRelease(VK_F);
            await Task.Delay(500, ct);
            
            // 确认离开
            _inputService.Keyboard.PressAndRelease(VK_F);
            await Task.Delay(500, ct);
            
            Log("已退出秘境", 1);
        }
        catch (Exception ex)
        {
            Log($"退出秘境异常: {ex.Message}", 2);
        }
    }
    
    /// <summary>
    /// 移动到指定方向
    /// </summary>
    public async Task MoveToDirectionAsync(string direction, int durationMs, CancellationToken ct)
    {
        var keyCode = direction.ToLower() switch
        {
            "w" or "forward" => VK_W,
            "a" or "left" => VK_A,
            "s" or "backward" => VK_S,
            "d" or "right" => VK_D,
            _ => 0
        };
        
        if (keyCode == 0) return;
        
        _inputService.Keyboard.PressKey(keyCode);
        await Task.Delay(durationMs, ct);
        _inputService.Keyboard.ReleaseKey(keyCode);
    }
    
    /// <summary>
    /// 计算图像平均亮度
    /// </summary>
    private static double CalculateAverageBrightness(Mat image)
    {
        try
        {
            using var gray = new Mat();
            if (image.Channels() == 3)
            {
                Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
            }
            else
            {
                image.CopyTo(gray);
            }
            
            return Cv2.Mean(gray).Val0;
        }
        catch
        {
            return 0;
        }
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
        
        _logService.Log($"[秘境助手] {message}", logLevel, "DomainHelper");
    }
}
