using System.Collections.Concurrent;
using System.IO;
using ShineProCS.Core.Interfaces;
using ShineProCS.Models;
using OpenCvSharp;

namespace ShineProCS.Core.Services;

public class StateDetector
{
    private readonly IImageInterface _image;
    private readonly ConfigManager _config;
    private readonly ConcurrentDictionary<string, Mat> _templateCache = new();
    private readonly TemplatePreloader? _preloader;
    private const int MaxCacheSize = 50;

    public StateDetector(IImageInterface image, ConfigManager config, TemplatePreloader? preloader = null)
    {
        _image = image;
        _config = config;
        _preloader = preloader;
    }

    /// <summary>
    /// 检测游戏状态（HP/MP/公共CD/读条）
    /// </summary>
    public GameState DetectGameState()
    {
        var state = new GameState { UpdateTime = DateTime.Now };
        var settings = _config.AppSettings;
        
        if (settings.HealthBarRegion.Any(v => v > 0))
        {
            state.CurrentHpPercent = DetectBarPercent(settings.HealthBarRegion, isHealth: true);
            state.HpPercentage = state.CurrentHpPercent / 100.0;
        }
        
        if (settings.ManaBarRegion.Any(v => v > 0))
        {
            state.CurrentMpPercent = DetectBarPercent(settings.ManaBarRegion, isHealth: false);
            state.MpPercentage = state.CurrentMpPercent / 100.0;
        }
        
        // 检测目标HP
        if (settings.TargetHealthBarRegion.Any(v => v > 0))
        {
            state.TargetHpPercent = DetectBarPercent(settings.TargetHealthBarRegion, isHealth: true);
        }
        
        if (settings.GlobalCdPoint.Any(v => v > 0))
        {
            state.IsGlobalCdActive = DetectGlobalCd(settings.GlobalCdPoint);
            state.IsCasting = state.IsGlobalCdActive;
        }
        
        return state;
    }

    /// <summary>
    /// 并行更新多个技能的视觉状态
    /// </summary>
    public void UpdateSkillStatesParallel(IList<SkillRuntimeState> states)
    {
        if (states.Count == 0) return;
        
        if (states.Count < 3)
        {
            foreach (var state in states)
                UpdateSkillState(state);
            return;
        }

        var toDetect = states.Where(s => 
            s.Config.Enabled && 
            s.Config.IconRegion.Any(v => v > 0)).ToList();

        if (toDetect.Count == 0)
        {
            foreach (var s in states)
                s.IsVisuallyReady = true;
            return;
        }

        Parallel.ForEach(toDetect, new ParallelOptions { MaxDegreeOfParallelism = 4 }, state =>
        {
            UpdateSkillState(state);
        });

        foreach (var s in states.Where(s => !toDetect.Contains(s)))
            s.IsVisuallyReady = true;
    }

    private double DetectBarPercent(int[] region, bool isHealth)
    {
        if (region.Length < 4 || region[2] <= 0 || region[3] <= 0)
            return 100.0;
        
        var frame = _image.GetScreenRegion(region[0], region[1], region[2], region[3]);
        if (frame == null) return 100.0;
        
        try
        {
            var settings = _config.AppSettings;
            
            using var hsv = new Mat();
            Cv2.CvtColor(frame, hsv, ColorConversionCodes.BGR2HSV);
            
            using var mask = new Mat();
            if (isHealth)
            {
                using var maskRed = new Mat();
                using var maskGreen = new Mat();
                
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
                Cv2.InRange(hsv, 
                    new Scalar(settings.ManaHueMin, settings.ManaSatMin, settings.ManaValMin), 
                    new Scalar(settings.ManaHueMax, 255, 255), 
                    mask);
            }
            
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
        
        if (brightness > settings.GlobalCdBrightnessThreshold)
            return true;
        
        if (r > 150 && g > 150 && b < 100)
            return true;
        
        return false;
    }

    /// <summary>
    /// 更新单个技能视觉状态
    /// </summary>
    public void UpdateSkillState(SkillRuntimeState state, Mat? frame = null)
    {
        var region = state.Config.IconRegion;
        
        if (region.All(v => v == 0))
        {
            state.IsVisuallyReady = true;
            return;
        }
        
        if (!string.IsNullOrEmpty(state.Config.TemplatePath) && File.Exists(state.Config.TemplatePath))
        {
            state.IsVisuallyReady = CheckSkillByTemplate(state.Config);
            return;
        }
        
        state.IsVisuallyReady = CheckSkillByBrightness(region);
    }

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

    private bool CheckSkillByBrightness(int[] region)
    {
        if (region.Length < 4 || region[2] <= 0 || region[3] <= 0)
            return true;
        
        var frame = _image.GetScreenRegion(region[0], region[1], region[2], region[3]);
        if (frame == null) return true;
        
        try
        {
            using var gray = new Mat();
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
            var mean = Cv2.Mean(gray);
            
            return mean.Val0 > _config.AppSettings.SkillBrightnessThreshold;
        }
        finally
        {
            _image.ReturnMat(frame);
        }
    }

    /// <summary>
    /// 从Buff库检查Buff是否存在
    /// </summary>
    public bool CheckBuffExists(string buffName)
    {
        if (string.IsNullOrEmpty(buffName))
            return true;
        
        var buffConfig = _config.AppSettings.BuffLibrary
            .FirstOrDefault(b => b.Name == buffName && b.Enabled);
        
        if (buffConfig == null)
            return true; // 未配置的Buff默认视为存在
        
        var region = buffConfig.IconRegion;
        if (region.All(v => v == 0))
            return true;
        
        if (!string.IsNullOrEmpty(buffConfig.TemplatePath) && File.Exists(buffConfig.TemplatePath))
            return CheckBuffByTemplate(buffConfig);
        
        return CheckBuffByBrightness(region);
    }
    
    private bool CheckBuffByTemplate(BuffConfig buff)
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
            
            return mean.Val0 > _config.AppSettings.BuffBrightnessThreshold;
        }
        finally
        {
            _image.ReturnMat(frame);
        }
    }

    private Mat? GetTemplate(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        
        if (_preloader != null)
        {
            var preloaded = _preloader.GetTemplate(path);
            if (preloaded != null) return preloaded;
        }
        
        if (_templateCache.TryGetValue(path, out var cached))
            return cached;
        
        if (!File.Exists(path)) return null;
        
        try
        {
            var template = Cv2.ImRead(path, ImreadModes.Color);
            if (!template.Empty())
            {
                if (_templateCache.Count >= MaxCacheSize)
                {
                    var keysToRemove = _templateCache.Keys.Take(_templateCache.Count / 2).ToList();
                    foreach (var key in keysToRemove)
                    {
                        if (_templateCache.TryRemove(key, out var mat))
                            mat.Dispose();
                    }
                }
                
                _templateCache[path] = template;
                return template;
            }
            template.Dispose();
        }
        catch { }
        
        return null;
    }

    public void ClearTemplateCache()
    {
        foreach (var kvp in _templateCache)
            kvp.Value.Dispose();
        _templateCache.Clear();
    }

    public bool DetectCombatState()
    {
        try
        {
            var state = DetectGameState();
            
            if (state.HpPercentage < 0.95)
                return true;
            
            if (state.IsGlobalCdActive)
                return true;
            
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
                        using var gray = new Mat();
                        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
                        using var threshold = new Mat();
                        Cv2.Threshold(gray, threshold, 200, 255, ThresholdTypes.Binary);
                        var nonZero = Cv2.CountNonZero(threshold);
                        
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
