using System.IO;
using ShineProCS.Core.Interfaces;
using ShineProCS.Models;
using OpenCvSharp;

using DetectionConst = ShineProCS.Core.Constants.Detection;

namespace ShineProCS.Core.Services;

/// <summary>
/// 游戏状态检测器
/// 负责检测HP/MP、技能状态、Buff状态等
/// 优化：使用单次大区域截图 + ROI裁剪，减少截图API调用次数
/// </summary>
public class StateDetector : IDisposable
{
    private readonly IImageInterface _image;
    private readonly ConfigManager _config;
    private readonly LruTemplateCache _templateCache;
    private readonly TemplatePreloader? _preloader;
    private bool _disposed;
    
    // HP/MP 检测失败缓存（需求 3.2, 3.5）
    // 使用独立的缓存类管理连续失败计数和缓存值
    private readonly DetectionFailureCache _hpFailureCache;
    private readonly DetectionFailureCache _mpFailureCache;
    
    // 采样点检测器（需求 3.1）
    private SamplingBarDetector? _barDetector;
    
    // 缓存的大区域截图（用于单帧多检测优化）
    private Mat? _cachedFrame;
    private int _cachedFrameX, _cachedFrameY;
    
    // 边界框缓存（配置不变时不重新计算）
    // 需求 1.2: 截取单个边界框大图并从中提取 ROI
    private (int x, int y, int w, int h)? _cachedBoundingBox;
    private bool _boundingBoxDirty = true;
    // 缓存失效追踪：记录上次计算时的区域数量和哈希值
    private int _lastRegionCount;
    private int _lastRegionHash;
    
    // 多尺度模板匹配器（需求 2.3: 支持多尺度匹配以适应不同 UI 缩放）
    private readonly MultiScaleTemplateMatcher _multiScaleMatcher;

    public StateDetector(IImageInterface image, ConfigManager config, TemplatePreloader? preloader = null)
    {
        _image = image;
        _config = config;
        _preloader = preloader;
        
        // 从配置读取缓存大小，使用 LRU 缓存策略
        var cacheSize = config.AppSettings.TemplateCacheSize;
        if (cacheSize <= 0) cacheSize = 50; // 默认值
        _templateCache = new LruTemplateCache(cacheSize);
        
        // 初始化多尺度模板匹配器（需求 2.3）
        // 支持 0.8-1.2 倍缩放范围，步长 0.1
        _multiScaleMatcher = new MultiScaleTemplateMatcher(0.8, 1.2, 0.1);
        
        // 初始化 HP/MP 检测失败缓存（需求 3.2, 3.5）
        var maxFailures = Math.Max(1, config.AppSettings.BarDetectionMaxFailures);
        _hpFailureCache = new DetectionFailureCache(maxFailures, 100.0);
        _mpFailureCache = new DetectionFailureCache(maxFailures, 100.0);
        
        // 初始化采样点检测器（需求 3.1, 3.3, 3.4）
        InitializeBarDetector();
    }
    
    /// <summary>
    /// 初始化或重新初始化采样点检测器
    /// 需求 3.3: 支持可配置的颜色阈值
    /// </summary>
    private void InitializeBarDetector()
    {
        var settings = _config.AppSettings;
        var thresholds = BarColorThresholds.FromAppSettings(
            redMinR: settings.HealthRedMinR,
            redRGDiff: settings.HealthRedRGDiff,
            redRBDiff: settings.HealthRedRBDiff,
            greenMinG: settings.HealthGreenMinG,
            blueMinB: settings.ManaBlueMinB,
            blueBGTolerance: settings.ManaBlueBGTolerance
        );
        _barDetector = new SamplingBarDetector(thresholds);
    }
    
    /// <summary>
    /// 刷新颜色阈值配置（配置变更时调用）
    /// </summary>
    public void RefreshColorThresholds()
    {
        InitializeBarDetector();
    }
    
    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _templateCache.Dispose();
        ClearCachedFrame();
        GC.SuppressFinalize(this);
    }
    
    /// <summary>
    /// 清除缓存的帧
    /// </summary>
    private void ClearCachedFrame()
    {
        if (_cachedFrame != null)
        {
            _image.ReturnMat(_cachedFrame);
            _cachedFrame = null;
        }
    }
    
    /// <summary>
    /// 标记边界框缓存失效（配置变更时调用）
    /// </summary>
    public void InvalidateBoundingBoxCache()
    {
        _boundingBoxDirty = true;
        _cachedBoundingBox = null;
    }

    /// <summary>
    /// 设置当前帧的大区域截图（由引擎在循环开始时调用）
    /// 这样后续的所有检测都可以从这个大图中裁剪，避免重复截图
    /// </summary>
    /// <param name="frame">大区域截图</param>
    /// <param name="frameX">截图区域的屏幕X坐标</param>
    /// <param name="frameY">截图区域的屏幕Y坐标</param>
    public void SetCachedFrame(Mat frame, int frameX, int frameY)
    {
        ClearCachedFrame();
        _cachedFrame = frame;
        _cachedFrameX = frameX;
        _cachedFrameY = frameY;
    }
    
    /// <summary>
    /// 计算包含所有需要检测区域的边界框（带缓存）
    /// 包括：技能图标、HP/MP条、Buff图标等
    /// 需求 1.2: 截取单个边界框大图并从中提取 ROI
    /// </summary>
    /// <param name="skillStates">技能状态列表</param>
    /// <returns>边界框 (minX, minY, width, height)，如果没有有效区域则返回null</returns>
    public (int x, int y, int w, int h)? CalculateDetectionBoundingBox(IList<SkillRuntimeState> skillStates)
    {
        var settings = _config.AppSettings;
        var regions = new List<int[]>();
        
        // 收集所有需要检测的区域
        // 添加技能图标区域
        foreach (var state in skillStates)
        {
            if (state.Config.Enabled && state.Config.IconRegion.Any(v => v > 0))
                regions.Add(state.Config.IconRegion);
        }
        
        // 添加HP/MP条区域
        if (settings.HealthBarRegion.Any(v => v > 0))
            regions.Add(settings.HealthBarRegion);
        if (settings.ManaBarRegion.Any(v => v > 0))
            regions.Add(settings.ManaBarRegion);
        if (settings.TargetHealthBarRegion.Any(v => v > 0))
            regions.Add(settings.TargetHealthBarRegion);
        
        // 添加Buff库中的区域
        foreach (var buff in settings.BuffLibrary)
        {
            if (buff.Enabled && buff.IconRegion.Any(v => v > 0))
                regions.Add(buff.IconRegion);
        }
        
        // 计算区域哈希值用于缓存失效检测
        int regionHash = ComputeRegionHash(regions);
        
        // 检查缓存是否有效：区域数量和哈希值都未变化
        if (!_boundingBoxDirty && 
            _cachedBoundingBox.HasValue && 
            regions.Count == _lastRegionCount && 
            regionHash == _lastRegionHash)
        {
            return _cachedBoundingBox;
        }
        
        // 缓存失效，重新计算
        if (regions.Count == 0)
        {
            _cachedBoundingBox = null;
            _boundingBoxDirty = false;
            _lastRegionCount = 0;
            _lastRegionHash = 0;
            return null;
        }
        
        // 计算边界框：找到所有区域的最小外接矩形
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        
        foreach (var region in regions)
        {
            // 验证区域有效性
            if (region.Length < 4 || region[2] <= 0 || region[3] <= 0)
                continue;
            
            var x = region[0];
            var y = region[1];
            var w = region[2];
            var h = region[3];
            
            // 更新边界框范围
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x + w > maxX) maxX = x + w;
            if (y + h > maxY) maxY = y + h;
        }
        
        // 检查是否有有效区域
        if (minX == int.MaxValue)
        {
            _cachedBoundingBox = null;
            _boundingBoxDirty = false;
            _lastRegionCount = regions.Count;
            _lastRegionHash = regionHash;
            return null;
        }
        
        // 更新缓存
        _cachedBoundingBox = (minX, minY, maxX - minX, maxY - minY);
        _boundingBoxDirty = false;
        _lastRegionCount = regions.Count;
        _lastRegionHash = regionHash;
        
        return _cachedBoundingBox;
    }
    
    /// <summary>
    /// 计算区域列表的哈希值，用于检测区域配置是否变化
    /// </summary>
    /// <param name="regions">区域列表</param>
    /// <returns>哈希值</returns>
    private static int ComputeRegionHash(List<int[]> regions)
    {
        unchecked
        {
            int hash = 17;
            foreach (var region in regions)
            {
                if (region.Length >= 4)
                {
                    // 将区域的四个值组合成哈希
                    hash = hash * 31 + region[0];
                    hash = hash * 31 + region[1];
                    hash = hash * 31 + region[2];
                    hash = hash * 31 + region[3];
                }
            }
            return hash;
        }
    }
    
    /// <summary>
    /// 从缓存的大图中裁剪指定区域
    /// </summary>
    /// <param name="region">屏幕坐标的区域 [x, y, w, h]</param>
    /// <returns>裁剪后的子图，如果区域不在缓存范围内则返回null</returns>
    private Mat? GetRegionFromCache(int[] region)
    {
        if (_cachedFrame == null || region.Length < 4)
            return null;
        
        // 将屏幕坐标转换为缓存帧内的相对坐标
        int relX = region[0] - _cachedFrameX;
        int relY = region[1] - _cachedFrameY;
        int w = region[2];
        int h = region[3];
        
        // 检查是否在缓存帧范围内
        if (relX < 0 || relY < 0 || 
            relX + w > _cachedFrame.Width || 
            relY + h > _cachedFrame.Height)
        {
            return null; // 区域不在缓存范围内，需要单独截图
        }
        
        // 从缓存帧中裁剪ROI（不复制数据，只是创建视图）
        var roi = new Rect(relX, relY, w, h);
        return new Mat(_cachedFrame, roi).Clone(); // Clone确保数据独立
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
            state.CurrentHpPercent = DetectBarPercent(settings.HealthBarRegion, isHealth: true, out bool isHpCached);
            state.HpPercentage = state.CurrentHpPercent / 100.0;
            state.IsHpCached = isHpCached;
        }
        
        if (settings.ManaBarRegion.Any(v => v > 0))
        {
            state.CurrentMpPercent = DetectBarPercent(settings.ManaBarRegion, isHealth: false, out bool isMpCached);
            state.MpPercentage = state.CurrentMpPercent / 100.0;
            state.IsMpCached = isMpCached;
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
    /// 并行更新多个技能的视觉状态（优化版：使用缓存帧 + CD跳过）
    /// 需求 1.3: 当技能处于冷却中（剩余 CD > 0.5秒）时，跳过该技能的视觉检测
    /// </summary>
    public void UpdateSkillStatesParallel(IList<SkillRuntimeState> states)
    {
        if (states.Count == 0) return;

        // 需求 1.3: 跳过CD中的技能，直接标记为不可用，减少不必要的视觉检测
        var toDetect = new List<SkillRuntimeState>();
        foreach (var s in states)
        {
            // 未启用或无图标区域的技能，默认视为就绪
            if (!s.Config.Enabled || s.Config.IconRegion.All(v => v == 0))
            {
                s.IsVisuallyReady = true;
                continue;
            }
            
            // 需求 1.3: 如果技能在CD中（剩余CD > 0.5秒），跳过视觉检测
            // 这样可以避免对明显在冷却中的技能进行不必要的图像处理
            if (s.RemainingCooldown > 0.5)
            {
                s.IsVisuallyReady = false;
                s.SkippedByCD = true;  // 标记为被CD跳过
                continue;
            }
            
            s.SkippedByCD = false;  // 清除CD跳过标记
            toDetect.Add(s);
        }

        if (toDetect.Count == 0) return;

        // 使用并行处理，但每个任务从缓存帧裁剪而不是单独截图
        if (toDetect.Count >= DetectionConst.ParallelDetectionThreshold)
        {
            Parallel.ForEach(toDetect, new ParallelOptions { MaxDegreeOfParallelism = DetectionConst.MaxParallelDegree }, state =>
            {
                UpdateSkillStateOptimized(state);
            });
        }
        else
        {
            foreach (var state in toDetect)
                UpdateSkillStateOptimized(state);
        }
    }
    
    /// <summary>
    /// 优化版技能状态更新：优先从缓存帧裁剪
    /// </summary>
    private void UpdateSkillStateOptimized(SkillRuntimeState state)
    {
        var region = state.Config.IconRegion;
        
        if (region.All(v => v == 0))
        {
            state.IsVisuallyReady = true;
            return;
        }
        
        // 尝试从缓存帧获取
        var frame = GetRegionFromCache(region);
        bool fromCache = frame != null;
        
        // 如果缓存中没有，则单独截图
        if (frame == null)
        {
            frame = _image.GetScreenRegion(region[0], region[1], region[2], region[3]);
        }
        
        if (frame == null)
        {
            state.IsVisuallyReady = true;
            return;
        }
        
        try
        {
            if (!string.IsNullOrEmpty(state.Config.TemplatePath) && File.Exists(state.Config.TemplatePath))
            {
                state.IsVisuallyReady = CheckSkillByTemplateWithFrame(state.Config, frame);
            }
            else
            {
                state.IsVisuallyReady = CheckSkillByBrightnessWithFrame(frame);
            }
        }
        finally
        {
            // 从缓存裁剪的需要释放Clone的副本
            if (fromCache)
            {
                frame.Dispose();
            }
            else
            {
                _image.ReturnMat(frame);
            }
        }
    }
    
    /// <summary>
    /// 使用已有帧进行模板匹配检测（优化版：可配置的缩放加速匹配）
    /// 需求 1.4: 可选地将图像缩放以加速匹配
    /// </summary>
    private bool CheckSkillByTemplateWithFrame(SkillConfig skill, Mat frame)
    {
        var template = GetTemplate(skill.TemplatePath);
        if (template == null) return true;
        
        var settings = _config.AppSettings;
        
        // 检查是否启用缩放优化
        if (!settings.EnableTemplateScaling)
        {
            // 不启用缩放，直接使用原图匹配
            var similarity = _image.MatchTemplate(frame, template);
            return similarity >= skill.SimilarityThreshold;
        }
        
        // 从配置读取缩放参数
        var scaleFactor = Math.Clamp(settings.TemplateScaleFactor, 0.25, 1.0);
        var minScaledSize = Math.Max(8, settings.TemplateMinScaledSize);
        var thresholdAdjust = Math.Max(0, settings.TemplateScaleThresholdAdjust);
        
        // 计算缩放后的尺寸
        var newWidth = Math.Max(8, (int)(frame.Width * scaleFactor));
        var newHeight = Math.Max(8, (int)(frame.Height * scaleFactor));
        var templateNewWidth = Math.Max(8, (int)(template.Width * scaleFactor));
        var templateNewHeight = Math.Max(8, (int)(template.Height * scaleFactor));
        
        // 如果缩放后尺寸太小，直接用原图匹配
        if (newWidth < minScaledSize || newHeight < minScaledSize || 
            templateNewWidth < 8 || templateNewHeight < 8)
        {
            var similarity = _image.MatchTemplate(frame, template);
            return similarity >= skill.SimilarityThreshold;
        }
        
        // 执行缩放后的模板匹配
        using var smallFrame = new Mat();
        using var smallTemplate = new Mat();
        Cv2.Resize(frame, smallFrame, new OpenCvSharp.Size(newWidth, newHeight), 0, 0, InterpolationFlags.Area);
        Cv2.Resize(template, smallTemplate, new OpenCvSharp.Size(templateNewWidth, templateNewHeight), 0, 0, InterpolationFlags.Area);
        
        var sim = _image.MatchTemplate(smallFrame, smallTemplate);
        // 缩放后匹配精度略有下降，阈值降低配置的调整值进行补偿
        return sim >= (skill.SimilarityThreshold - thresholdAdjust);
    }
    
    /// <summary>
    /// 使用已有帧进行亮度检测
    /// </summary>
    private bool CheckSkillByBrightnessWithFrame(Mat frame)
    {
        using var gray = new Mat();
        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
        var mean = Cv2.Mean(gray);
        
        return mean.Val0 > _config.AppSettings.SkillBrightnessThreshold;
    }

    private double DetectBarPercent(int[] region, bool isHealth, out bool isCached)
    {
        isCached = false;
        var failureCache = isHealth ? _hpFailureCache : _mpFailureCache;
        
        // 验证区域参数
        if (region.Length < 4 || region[2] <= 0 || region[3] <= 0)
        {
            failureCache.RecordFailure();
            isCached = true;
            return failureCache.LastValidValue;
        }
        
        // 优先从缓存帧获取
        var frame = GetRegionFromCache(region);
        bool fromCache = frame != null;
        
        if (frame == null)
        {
            frame = _image.GetScreenRegion(region[0], region[1], region[2], region[3]);
        }
        
        // 无法获取帧，记录失败并返回缓存值
        if (frame == null)
        {
            failureCache.RecordFailure();
            isCached = true;
            
            // 需求 3.5: 超过最大失败次数时输出日志
            if (failureCache.IsAtMaxFailures)
            {
                var barType = isHealth ? "HP" : "MP";
                System.Diagnostics.Debug.WriteLine(
                    $"[StateDetector] {barType}检测连续失败{failureCache.ConsecutiveFailures}次，使用缓存值: {failureCache.LastValidValue}%");
            }
            
            return failureCache.LastValidValue;
        }
        
        try
        {
            // 需求 3.1: 使用采样点检测器进行检测
            // 采样点位于中线，数量在 5-20 之间
            double? detectedValue = null;
            
            if (_barDetector != null)
            {
                detectedValue = _barDetector.DetectPercentage(frame, isHealth);
            }
            else
            {
                // 回退到内联检测（兼容性）
                detectedValue = DetectBarPercentInline(frame, isHealth);
            }
            
            // 需求 3.2, 3.5: 使用失败缓存管理检测结果
            var result = failureCache.GetValueOrCache(detectedValue, out isCached);
            return result;
        }
        finally
        {
            if (fromCache)
            {
                frame.Dispose();
            }
            else
            {
                _image.ReturnMat(frame);
            }
        }
    }
    
    /// <summary>
    /// 内联采样点检测（回退方法）
    /// 需求 3.1: 使用中线采样，采样点数量 5-20 个
    /// </summary>
    private double DetectBarPercentInline(Mat frame, bool isHealth)
    {
        var settings = _config.AppSettings;
        int width = frame.Width;
        int height = frame.Height;
        
        // 需求 3.1: 采样点位于中线
        int midY = height / 2;
        
        // 需求 3.1: 采样点数量计算（5-20 个点）
        int sampleCount = SamplingBarDetector.CalculateSampleCount(width);
        int matchCount = 0;
        
        unsafe
        {
            var ptr = (byte*)frame.DataPointer;
            int stride = (int)frame.Step();
            int channels = frame.Channels();
            
            if (channels < 3)
                return 0.0;
            
            for (int i = 0; i < sampleCount; i++)
            {
                // 计算采样点 X 坐标
                int x = SamplingBarDetector.CalculateSampleX(i, sampleCount, width);
                if (x < 0 || x >= width)
                    continue;
                
                int offset = midY * stride + x * channels;
                
                byte b = ptr[offset];
                byte g = ptr[offset + 1];
                byte r = ptr[offset + 2];
                
                // 需求 3.4: 同时支持红色和绿色血条
                if (isHealth)
                {
                    bool isRed = r >= settings.HealthRedMinR && 
                                 r > g + settings.HealthRedRGDiff && 
                                 r > b + settings.HealthRedRBDiff;
                    bool isGreen = g >= settings.HealthGreenMinG && g > r && g > b;
                    if (isRed || isGreen) matchCount++;
                }
                else
                {
                    bool isBlue = b >= settings.ManaBlueMinB && 
                                  b > r && 
                                  b > g - settings.ManaBlueBGTolerance;
                    if (isBlue) matchCount++;
                }
            }
        }
        
        var percent = (double)matchCount / sampleCount * 100.0;
        return Math.Clamp(percent, 0.0, 100.0);
    }
    
    /// <summary>
    /// 记录检测失败（已废弃，使用 DetectionFailureCache）
    /// </summary>
    [Obsolete("使用 DetectionFailureCache 替代")]
    private void RecordDetectionFailure(bool isHealth)
    {
        var cache = isHealth ? _hpFailureCache : _mpFailureCache;
        cache.RecordFailure();
    }
    
    // 保留旧签名以兼容其他调用
    private double DetectBarPercent(int[] region, bool isHealth)
    {
        return DetectBarPercent(region, isHealth, out _);
    }

    private bool DetectGlobalCd(int[] point)
    {
        if (point.Length < 2) return false;
        
        var color = _image.GetPixelColor(point[0], point[1]);
        if (color == null) return false;
        
        var settings = _config.AppSettings;
        var mode = (GcdDetectionMode)settings.GlobalCdDetectionMode;
        
        // Auto模式：有颜色配置用颜色，否则用亮度
        if (mode == GcdDetectionMode.Auto)
        {
            mode = settings.GlobalCdColor.Length >= 3 && settings.GlobalCdColor.Any(v => v > 0)
                ? GcdDetectionMode.Color
                : GcdDetectionMode.Brightness;
        }
        
        return mode switch
        {
            GcdDetectionMode.Color => DetectGcdByColor(color.Value, settings),
            GcdDetectionMode.Brightness => DetectGcdByBrightness(color.Value, settings),
            _ => false
        };
    }
    
    private static bool DetectGcdByColor((byte r, byte g, byte b) color, AppSettings settings)
    {
        if (settings.GlobalCdColor.Length < 3)
            return false;
        
        var targetR = settings.GlobalCdColor[0];
        var targetG = settings.GlobalCdColor[1];
        var targetB = settings.GlobalCdColor[2];
        var tolerance = settings.GlobalCdColorTolerance;
        
        return Math.Abs(color.r - targetR) <= tolerance &&
               Math.Abs(color.g - targetG) <= tolerance &&
               Math.Abs(color.b - targetB) <= tolerance;
    }
    
    private static bool DetectGcdByBrightness((byte r, byte g, byte b) color, AppSettings settings)
    {
        var brightness = (color.r + color.g + color.b) / 3.0;
        
        if (brightness > settings.GlobalCdBrightnessThreshold)
            return true;
        
        if (color.r > 150 && color.g > 150 && color.b < 100)
            return true;
        
        return false;
    }

    /// <summary>
    /// 更新单个技能视觉状态（兼容旧接口）
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
            return true;
        
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
        
        // 需求 2.1: 优先从缓存帧获取区域
        var frame = GetRegionFromCache(region);
        bool fromCache = frame != null;
        
        // 需求 2.4: 回退到单独截图
        if (frame == null)
        {
            frame = _image.GetScreenRegion(region[0], region[1], region[2], region[3]);
        }
        
        // 需求 2.5: 检测失败时记录日志并返回安全默认值
        if (frame == null)
        {
            System.Diagnostics.Debug.WriteLine($"[StateDetector] Buff检测失败: 无法获取区域 {buff.Name}");
            return false;
        }
        
        try
        {
            var template = GetTemplate(buff.TemplatePath);
            if (template == null)
            {
                System.Diagnostics.Debug.WriteLine($"[StateDetector] Buff检测失败: 无法加载模板 {buff.TemplatePath}");
                return false;
            }
            
            var settings = _config.AppSettings;
            
            // 需求 2.3: 根据配置决定是否使用多尺度匹配
            if (settings.EnableMultiScaleBuffMatch)
            {
                // 使用多尺度模板匹配，适应不同 UI 缩放
                var result = _multiScaleMatcher.Match(frame, template, buff.SimilarityThreshold);
                return result.IsMatch;
            }
            else
            {
                // 使用标准单尺度匹配
                var similarity = _image.MatchTemplate(frame, template);
                return similarity >= buff.SimilarityThreshold;
            }
        }
        catch (Exception ex)
        {
            // 需求 2.5: 检测失败时记录日志并返回安全默认值
            System.Diagnostics.Debug.WriteLine($"[StateDetector] Buff检测异常 {buff.Name}: {ex.Message}");
            return false;
        }
        finally
        {
            if (fromCache)
                frame.Dispose();
            else
                _image.ReturnMat(frame);
        }
    }

    private bool CheckBuffByBrightness(int[] region)
    {
        if (region.Length < 4 || region[2] <= 0 || region[3] <= 0)
            return true;
        
        // 需求 2.1: 优先从缓存帧获取区域
        var frame = GetRegionFromCache(region);
        bool fromCache = frame != null;
        
        // 需求 2.4: 回退到单独截图
        if (frame == null)
        {
            frame = _image.GetScreenRegion(region[0], region[1], region[2], region[3]);
        }
        
        // 需求 2.5: 检测失败时返回安全默认值
        if (frame == null) return false;
        
        try
        {
            using var gray = new Mat();
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
            var mean = Cv2.Mean(gray);
            
            return mean.Val0 > _config.AppSettings.BuffBrightnessThreshold;
        }
        catch (Exception ex)
        {
            // 需求 2.5: 检测失败时记录日志并返回安全默认值
            System.Diagnostics.Debug.WriteLine($"[StateDetector] Buff亮度检测异常: {ex.Message}");
            return false;
        }
        finally
        {
            if (fromCache)
                frame.Dispose();
            else
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
        
        var cached = _templateCache.Get(path);
        if (cached != null)
            return cached;
        
        if (!File.Exists(path)) return null;
        
        try
        {
            var template = Cv2.ImRead(path, ImreadModes.Color);
            if (!template.Empty())
            {
                _templateCache.Set(path, template);
                return template;
            }
            template.Dispose();
        }
        catch { }
        
        return null;
    }

    public void ClearTemplateCache()
    {
        _templateCache.Clear();
    }
    
    public string GetTemplateCacheStatistics()
    {
        return _templateCache.GetStatistics();
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
            
            return false;
        }
        catch
        {
            return false;
        }
    }
}
