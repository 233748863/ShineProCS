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
    
    // HP/MP检测失败缓存字段 (Requirement 3)
    private double _lastValidHpPercent = 100.0;
    private double _lastValidMpPercent = 100.0;
    private int _consecutiveHpFailures = 0;
    private int _consecutiveMpFailures = 0;
    private const int MaxConsecutiveFailures = 5;
    
    // 缓存的大区域截图（用于单帧多检测优化）
    private Mat? _cachedFrame;
    private int _cachedFrameX, _cachedFrameY;
    
    // 边界框缓存（配置不变时不重新计算）
    private (int x, int y, int w, int h)? _cachedBoundingBox;
    private bool _boundingBoxDirty = true;

    public StateDetector(IImageInterface image, ConfigManager config, TemplatePreloader? preloader = null)
    {
        _image = image;
        _config = config;
        _preloader = preloader;
        
        // 从配置读取缓存大小，使用 LRU 缓存策略
        var cacheSize = config.AppSettings.TemplateCacheSize;
        if (cacheSize <= 0) cacheSize = 50; // 默认值
        _templateCache = new LruTemplateCache(cacheSize);
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
    /// </summary>
    /// <param name="skillStates">技能状态列表</param>
    /// <returns>边界框 (minX, minY, width, height)，如果没有有效区域则返回null</returns>
    public (int x, int y, int w, int h)? CalculateDetectionBoundingBox(IList<SkillRuntimeState> skillStates)
    {
        // 如果缓存有效，直接返回
        if (!_boundingBoxDirty && _cachedBoundingBox.HasValue)
            return _cachedBoundingBox;
        
        var settings = _config.AppSettings;
        var regions = new List<int[]>();
        
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
        
        if (regions.Count == 0)
        {
            _cachedBoundingBox = null;
            _boundingBoxDirty = false;
            return null;
        }
        
        // 计算边界框
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        
        foreach (var region in regions)
        {
            if (region.Length < 4 || region[2] <= 0 || region[3] <= 0)
                continue;
            
            var x = region[0];
            var y = region[1];
            var w = region[2];
            var h = region[3];
            
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x + w > maxX) maxX = x + w;
            if (y + h > maxY) maxY = y + h;
        }
        
        if (minX == int.MaxValue)
        {
            _cachedBoundingBox = null;
            _boundingBoxDirty = false;
            return null;
        }
        
        _cachedBoundingBox = (minX, minY, maxX - minX, maxY - minY);
        _boundingBoxDirty = false;
        return _cachedBoundingBox;
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
    /// </summary>
    public void UpdateSkillStatesParallel(IList<SkillRuntimeState> states)
    {
        if (states.Count == 0) return;

        // 优化1：跳过CD中的技能，直接标记为不可用
        var toDetect = new List<SkillRuntimeState>();
        foreach (var s in states)
        {
            if (!s.Config.Enabled || s.Config.IconRegion.All(v => v == 0))
            {
                s.IsVisuallyReady = true;
                continue;
            }
            
            // 如果技能在CD中（剩余CD > 0.5秒），跳过视觉检测
            if (s.RemainingCooldown > 0.5)
            {
                s.IsVisuallyReady = false;
                continue;
            }
            
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
    /// 使用已有帧进行模板匹配检测（优化版：缩小尺寸加速匹配）
    /// </summary>
    private bool CheckSkillByTemplateWithFrame(SkillConfig skill, Mat frame)
    {
        var template = GetTemplate(skill.TemplatePath);
        if (template == null) return true;
        
        // 优化3：缩小模板和帧到50%尺寸，加速匹配
        var scaleFactor = 0.5;
        var newWidth = Math.Max(8, (int)(frame.Width * scaleFactor));
        var newHeight = Math.Max(8, (int)(frame.Height * scaleFactor));
        var templateNewWidth = Math.Max(8, (int)(template.Width * scaleFactor));
        var templateNewHeight = Math.Max(8, (int)(template.Height * scaleFactor));
        
        // 如果尺寸太小，直接用原图匹配
        if (newWidth < 16 || newHeight < 16 || templateNewWidth < 8 || templateNewHeight < 8)
        {
            var similarity = _image.MatchTemplate(frame, template);
            return similarity >= skill.SimilarityThreshold;
        }
        
        using var smallFrame = new Mat();
        using var smallTemplate = new Mat();
        Cv2.Resize(frame, smallFrame, new OpenCvSharp.Size(newWidth, newHeight), 0, 0, InterpolationFlags.Area);
        Cv2.Resize(template, smallTemplate, new OpenCvSharp.Size(templateNewWidth, templateNewHeight), 0, 0, InterpolationFlags.Area);
        
        var sim = _image.MatchTemplate(smallFrame, smallTemplate);
        // 缩小后匹配精度略有下降，阈值降低5%
        return sim >= (skill.SimilarityThreshold - 0.05);
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
        
        if (region.Length < 4 || region[2] <= 0 || region[3] <= 0)
        {
            RecordDetectionFailure(isHealth);
            isCached = true;
            return isHealth ? _lastValidHpPercent : _lastValidMpPercent;
        }
        
        // 优先从缓存帧获取
        var frame = GetRegionFromCache(region);
        bool fromCache = frame != null;
        
        if (frame == null)
        {
            frame = _image.GetScreenRegion(region[0], region[1], region[2], region[3]);
        }
        
        if (frame == null)
        {
            RecordDetectionFailure(isHealth);
            isCached = true;
            return isHealth ? _lastValidHpPercent : _lastValidMpPercent;
        }
        
        try
        {
            // 优化2：使用采样点检测代替全区域HSV扫描
            // 沿着血条/蓝条的中线采样，检测有颜色的比例
            var settings = _config.AppSettings;
            int width = frame.Width;
            int height = frame.Height;
            int midY = height / 2;
            
            // 采样点数量（最多20个点，最少5个点）
            int sampleCount = Math.Max(5, Math.Min(20, width / 5));
            int matchCount = 0;
            
            unsafe
            {
                var ptr = (byte*)frame.DataPointer;
                int stride = (int)frame.Step();
                int channels = frame.Channels();
                
                for (int i = 0; i < sampleCount; i++)
                {
                    int x = (i * width) / sampleCount;
                    int offset = midY * stride + x * channels;
                    
                    byte b = ptr[offset];
                    byte g = ptr[offset + 1];
                    byte r = ptr[offset + 2];
                    
                    // 简化的颜色匹配（不用HSV转换）
                    if (isHealth)
                    {
                        // 红色血条：R高，G/B低 或 绿色血条：G高，R/B较低
                        bool isRed = r > 150 && r > g + 30 && r > b + 30;
                        bool isGreen = g > 100 && g > r && g > b;
                        if (isRed || isGreen) matchCount++;
                    }
                    else
                    {
                        // 蓝色蓝条：B高，R/G较低
                        bool isBlue = b > 100 && b > r && b > g - 30;
                        if (isBlue) matchCount++;
                    }
                }
            }
            
            var percent = (double)matchCount / sampleCount * 100.0;
            var result = Math.Min(100.0, Math.Max(0.0, percent));
            
            // 检测成功，更新缓存
            if (isHealth)
            {
                _lastValidHpPercent = result;
                _consecutiveHpFailures = 0;
            }
            else
            {
                _lastValidMpPercent = result;
                _consecutiveMpFailures = 0;
            }
            
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
    
    private void RecordDetectionFailure(bool isHealth)
    {
        if (isHealth)
        {
            _consecutiveHpFailures++;
            if (_consecutiveHpFailures >= MaxConsecutiveFailures)
            {
                System.Diagnostics.Debug.WriteLine($"[StateDetector] HP检测连续失败{_consecutiveHpFailures}次，使用缓存值: {_lastValidHpPercent}%");
            }
        }
        else
        {
            _consecutiveMpFailures++;
            if (_consecutiveMpFailures >= MaxConsecutiveFailures)
            {
                System.Diagnostics.Debug.WriteLine($"[StateDetector] MP检测连续失败{_consecutiveMpFailures}次，使用缓存值: {_lastValidMpPercent}%");
            }
        }
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
        
        // 优先从缓存帧获取
        var frame = GetRegionFromCache(region);
        bool fromCache = frame != null;
        
        if (frame == null)
        {
            frame = _image.GetScreenRegion(region[0], region[1], region[2], region[3]);
        }
        
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
        
        // 优先从缓存帧获取
        var frame = GetRegionFromCache(region);
        bool fromCache = frame != null;
        
        if (frame == null)
        {
            frame = _image.GetScreenRegion(region[0], region[1], region[2], region[3]);
        }
        
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
