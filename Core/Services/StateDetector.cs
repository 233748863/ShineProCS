using System.IO;
using ShineProCS.Core.Interfaces;
using ShineProCS.Models;
using OpenCvSharp;

namespace ShineProCS.Core.Services;

public class StateDetector
{
    private readonly IImageInterface _image;
    private readonly ConfigManager _config;
    private readonly Dictionary<string, Mat> _templateCache = [];
    private readonly object _cacheLock = new();
    private const int MaxCacheSize = 50; // 最大缓存数量

    public StateDetector(IImageInterface image, ConfigManager config)
    {
        _image = image;
        _config = config;
    }

    /// <summary>
    /// 检测游戏状态（HP/MP/公共CD/读条）
    /// </summary>
    public GameState DetectGameState()
    {
        var state = new GameState { UpdateTime = DateTime.Now };
        var settings = _config.AppSettings;
        
        // 检测HP百分比
        if (settings.HealthBarRegion.Any(v => v > 0))
        {
            state.CurrentHpPercent = DetectBarPercent(settings.HealthBarRegion, isHealth: true);
            state.HpPercentage = state.CurrentHpPercent / 100.0;
        }
        
        // 检测MP百分比
        if (settings.ManaBarRegion.Any(v => v > 0))
        {
            state.CurrentMpPercent = DetectBarPercent(settings.ManaBarRegion, isHealth: false);
            state.MpPercentage = state.CurrentMpPercent / 100.0;
        }
        
        // 检测公共CD（读条状态）
        // 公共CD激活 = 正在读条，此时不应打断当前技能
        if (settings.GlobalCdPoint.Any(v => v > 0))
        {
            state.IsGlobalCdActive = DetectGlobalCd(settings.GlobalCdPoint);
            state.IsCasting = state.IsGlobalCdActive; // 公共CD激活时视为正在读条
        }
        
        return state;
    }

    /// <summary>
    /// 检测血条/蓝条百分比（通过颜色检测，使用可配置阈值）
    /// </summary>
    private double DetectBarPercent(int[] region, bool isHealth)
    {
        if (region.Length < 4 || region[2] <= 0 || region[3] <= 0)
            return 100.0;
        
        var frame = _image.GetScreenRegion(region[0], region[1], region[2], region[3]);
        if (frame == null) return 100.0;
        
        try
        {
            var settings = _config.AppSettings;
            
            // 转换到HSV颜色空间
            using var hsv = new Mat();
            Cv2.CvtColor(frame, hsv, ColorConversionCodes.BGR2HSV);
            
            using var mask = new Mat();
            if (isHealth)
            {
                // 检测红色或绿色（血条可能是红色或绿色）
                using var maskRed = new Mat();
                using var maskGreen = new Mat();
                
                // 使用可配置的阈值
                Cv2.InRange(hsv, 
                    new Scalar(settings.HealthHueMin, settings.HealthSatMin, settings.HealthValMin), 
                    new Scalar(settings.HealthHueMax, 255, 255), 
                    maskRed);
                Cv2.InRange(hsv, 
                    new Scalar(settings.HealthGreenHueMin, settings.HealthSatMin, settings.HealthValMin), 
                    new Scalar(settings.HealthGreenHueMax, 255, 255), 
                    maskGreen);
                Cv2.BitwiseOr(maskRed, maskGreen, mask);
            }
            else
            {
                // 检测蓝色，使用可配置的阈值
                Cv2.InRange(hsv, 
                    new Scalar(settings.ManaHueMin, settings.ManaSatMin, settings.ManaValMin), 
                    new Scalar(settings.ManaHueMax, 255, 255), 
                    mask);
            }
            
            // 计算非零像素占比
            var nonZero = Cv2.CountNonZero(mask);
            var total = frame.Width * frame.Height;
            var percent = (double)nonZero / total * 100.0;
            
            return Math.Min(100.0, Math.Max(0.0, percent));
        }
        finally
        {
            _image.ReturnMat(frame);
        }
    }

    /// <summary>
    /// 检测公共CD是否激活（通过像素颜色判断进度条状态，使用可配置阈值）
    /// </summary>
    private bool DetectGlobalCd(int[] point)
    {
        if (point.Length < 2) return false;
        
        var color = _image.GetPixelColor(point[0], point[1]);
        if (color == null) return false;
        
        var settings = _config.AppSettings;
        var r = color.Value.r;
        var g = color.Value.g;
        var b = color.Value.b;
        var brightness = (r + g + b) / 3.0;
        
        // 使用可配置的亮度阈值
        if (brightness > settings.GlobalCdBrightnessThreshold)
        {
            return true; // 亮度高，正在读条
        }
        
        // 也可以检测特定颜色（如黄色进度条）
        // 黄色特征：R和G较高，B较低
        if (r > 150 && g > 150 && b < 100)
        {
            return true; // 黄色进度条，正在读条
        }
        
        return false; // 无进度条，可以释放技能
    }

    /// <summary>
    /// 更新技能视觉状态
    /// </summary>
    public void UpdateSkillState(SkillRuntimeState state, Mat? frame = null)
    {
        var region = state.Config.IconRegion;
        
        // 如果没有配置区域，默认可用
        if (region.All(v => v == 0))
        {
            state.IsVisuallyReady = true;
            return;
        }
        
        // 如果有模板图片，使用模板匹配
        if (!string.IsNullOrEmpty(state.Config.TemplatePath) && File.Exists(state.Config.TemplatePath))
        {
            state.IsVisuallyReady = CheckSkillByTemplate(state.Config);
            return;
        }
        
        // 否则使用亮度检测（技能可用时图标较亮）
        state.IsVisuallyReady = CheckSkillByBrightness(region);
    }

    /// <summary>
    /// 通过模板匹配检测技能是否可用
    /// </summary>
    private bool CheckSkillByTemplate(SkillConfig skill)
    {
        var region = skill.IconRegion;
        if (region.Length < 4 || region[2] <= 0 || region[3] <= 0)
            return true;
        
        var frame = _image.GetScreenRegion(region[0], region[1], region[2], region[3]);
        if (frame == null) return true;
        
        try
        {
            var template = GetTemplate(skill.TemplatePath);
            if (template == null) return true;
            
            var similarity = _image.MatchTemplate(frame, template);
            return similarity >= skill.SimilarityThreshold;
        }
        finally
        {
            _image.ReturnMat(frame);
        }
    }

    /// <summary>
    /// 通过亮度检测技能是否可用（技能冷却中图标变暗，使用可配置阈值）
    /// </summary>
    private bool CheckSkillByBrightness(int[] region)
    {
        if (region.Length < 4 || region[2] <= 0 || region[3] <= 0)
            return true;
        
        var frame = _image.GetScreenRegion(region[0], region[1], region[2], region[3]);
        if (frame == null) return true;
        
        try
        {
            // 计算平均亮度
            using var gray = new Mat();
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
            var mean = Cv2.Mean(gray);
            
            // 使用可配置的亮度阈值
            return mean.Val0 > _config.AppSettings.SkillBrightnessThreshold;
        }
        finally
        {
            _image.ReturnMat(frame);
        }
    }

    /// <summary>
    /// 检查Buff依赖要求
    /// </summary>
    public bool CheckBuffRequirements(SkillConfig skill, GameState state)
    {
        if (skill.BuffRequirements.Count == 0)
            return true;
        
        foreach (var buff in skill.BuffRequirements)
        {
            var buffExists = CheckBuffExists(buff);
            
            if (buff.IsRequired && !buffExists)
                return false; // 需要存在但不存在
            
            if (!buff.IsRequired && buffExists)
                return false; // 需要不存在但存在
        }
        
        return true;
    }

    /// <summary>
    /// 检查Buff是否存在（公开方法）
    /// </summary>
    public bool CheckBuffExists(BuffRequirement buff)
    {
        var region = buff.IconRegion;
        
        // 如果没有配置区域，默认存在
        if (region.All(v => v == 0))
            return true;
        
        // 如果有模板，使用模板匹配
        if (!string.IsNullOrEmpty(buff.TemplatePath) && File.Exists(buff.TemplatePath))
        {
            return CheckBuffByTemplate(buff);
        }
        
        // 否则使用亮度检测
        return CheckBuffByBrightness(region);
    }

    /// <summary>
    /// 通过模板匹配检测Buff
    /// </summary>
    private bool CheckBuffByTemplate(BuffRequirement buff)
    {
        var region = buff.IconRegion;
        if (region.Length < 4 || region[2] <= 0 || region[3] <= 0)
            return true;
        
        var frame = _image.GetScreenRegion(region[0], region[1], region[2], region[3]);
        if (frame == null) return false;
        
        try
        {
            var template = GetTemplate(buff.TemplatePath);
            if (template == null) return false;
            
            var similarity = _image.MatchTemplate(frame, template);
            return similarity >= buff.SimilarityThreshold;
        }
        finally
        {
            _image.ReturnMat(frame);
        }
    }

    /// <summary>
    /// 通过亮度检测Buff是否存在（使用可配置阈值）
    /// </summary>
    private bool CheckBuffByBrightness(int[] region)
    {
        if (region.Length < 4 || region[2] <= 0 || region[3] <= 0)
            return true;
        
        var frame = _image.GetScreenRegion(region[0], region[1], region[2], region[3]);
        if (frame == null) return false;
        
        try
        {
            using var gray = new Mat();
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
            var mean = Cv2.Mean(gray);
            
            // 使用可配置的亮度阈值
            return mean.Val0 > _config.AppSettings.BuffBrightnessThreshold;
        }
        finally
        {
            _image.ReturnMat(frame);
        }
    }

    /// <summary>
    /// 获取模板图片（带缓存和大小限制）
    /// </summary>
    private Mat? GetTemplate(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        
        lock (_cacheLock)
        {
            if (_templateCache.TryGetValue(path, out var cached))
                return cached;
        }
        
        if (!File.Exists(path)) return null;
        
        try
        {
            var template = Cv2.ImRead(path, ImreadModes.Color);
            if (!template.Empty())
            {
                lock (_cacheLock)
                {
                    // LRU 简化实现：超过限制时清理一半缓存
                    if (_templateCache.Count >= MaxCacheSize)
                    {
                        var keysToRemove = _templateCache.Keys.Take(_templateCache.Count / 2).ToList();
                        foreach (var key in keysToRemove)
                        {
                            if (_templateCache.TryGetValue(key, out var mat))
                            {
                                mat.Dispose();
                                _templateCache.Remove(key);
                            }
                        }
                    }
                    _templateCache[path] = template;
                }
                return template;
            }
            template.Dispose();
        }
        catch { }
        
        return null;
    }

    /// <summary>
    /// 清理模板缓存
    /// </summary>
    public void ClearTemplateCache()
    {
        lock (_cacheLock)
        {
            foreach (var mat in _templateCache.Values)
                mat.Dispose();
            _templateCache.Clear();
        }
    }

    /// <summary>
    /// 检测是否处于战斗状态
    /// </summary>
    public bool DetectCombatState()
    {
        try
        {
            var state = DetectGameState();
            
            // 方案1：HP不满表示可能在战斗
            if (state.HpPercentage < 0.95)
                return true;
            
            // 方案2：公共CD激活表示正在释放技能
            if (state.IsGlobalCdActive)
                return true;
            
            // 方案3：检测目标框（如果配置了）
            var settings = _config.AppSettings;
            if (settings.DetectionRegion.Any(v => v > 0))
            {
                var frame = _image.GetScreenRegion(
                    settings.DetectionRegion[0], 
                    settings.DetectionRegion[1], 
                    settings.DetectionRegion[2], 
                    settings.DetectionRegion[3]);
                
                if (frame != null)
                {
                    try
                    {
                        // 检测区域内是否有明显的亮点（目标标记）
                        using var gray = new Mat();
                        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
                        using var threshold = new Mat();
                        Cv2.Threshold(gray, threshold, 200, 255, ThresholdTypes.Binary);
                        var nonZero = Cv2.CountNonZero(threshold);
                        
                        // 如果有足够多的亮点，认为有目标
                        if (nonZero > 100)
                            return true;
                    }
                    finally
                    {
                        _image.ReturnMat(frame);
                    }
                }
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }
}
