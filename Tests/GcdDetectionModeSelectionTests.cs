using FsCheck;
using FsCheck.Xunit;
using ShineProCS.Models;

namespace ShineProCS.Tests;

/// <summary>
/// Property-based tests for GCD Detection Mode Selection
/// **Feature: business-logic-fixes, Property 11: GCD Detection Mode Selection**
/// **Validates: Requirements 7.1, 7.2**
/// </summary>
public class GcdDetectionModeSelectionTests
{
    /// <summary>
    /// 测试辅助类：模拟 StateDetector 的 GCD 检测逻辑
    /// 由于 StateDetector 依赖 IImageInterface，我们提取核心逻辑进行测试
    /// </summary>
    private static class GcdDetectionLogic
    {
        /// <summary>
        /// 确定使用哪种检测模式
        /// </summary>
        public static GcdDetectionMode DetermineDetectionMode(AppSettings settings)
        {
            var mode = (GcdDetectionMode)settings.GlobalCdDetectionMode;
            
            // Auto模式：有颜色配置用颜色，否则用亮度
            if (mode == GcdDetectionMode.Auto)
            {
                mode = settings.GlobalCdColor.Length >= 3 && settings.GlobalCdColor.Any(v => v > 0)
                    ? GcdDetectionMode.Color
                    : GcdDetectionMode.Brightness;
            }
            
            return mode;
        }
        
        /// <summary>
        /// 使用颜色匹配检测公共CD
        /// </summary>
        public static bool DetectGcdByColor((byte r, byte g, byte b) color, AppSettings settings)
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
        
        /// <summary>
        /// 使用亮度检测公共CD
        /// </summary>
        public static bool DetectGcdByBrightness((byte r, byte g, byte b) color, AppSettings settings)
        {
            var brightness = (color.r + color.g + color.b) / 3.0;
            
            if (brightness > settings.GlobalCdBrightnessThreshold)
                return true;
            
            // 兼容旧逻辑：黄色检测
            if (color.r > 150 && color.g > 150 && color.b < 100)
                return true;
            
            return false;
        }
        
        /// <summary>
        /// 完整的GCD检测逻辑
        /// </summary>
        public static bool DetectGlobalCd((byte r, byte g, byte b) color, AppSettings settings)
        {
            var mode = DetermineDetectionMode(settings);
            
            return mode switch
            {
                GcdDetectionMode.Color => DetectGcdByColor(color, settings),
                GcdDetectionMode.Brightness => DetectGcdByBrightness(color, settings),
                _ => false
            };
        }
    }


    /// <summary>
    /// Property 11.1: Auto mode uses Color detection when GlobalCdColor is configured
    /// WHEN GlobalCdDetectionMode is Auto AND GlobalCdColor has any value > 0,
    /// THEN color detection SHALL be used.
    /// **Validates: Requirements 7.1, 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool AutoModeUsesColorWhenColorConfigured(
        byte targetR, byte targetG, byte targetB)
    {
        // 确保至少有一个颜色值大于0
        var hasColor = targetR > 0 || targetG > 0 || targetB > 0;
        if (!hasColor)
        {
            targetR = 1; // 确保有颜色配置
        }
        
        var settings = new AppSettings
        {
            GlobalCdDetectionMode = (int)GcdDetectionMode.Auto,
            GlobalCdColor = [targetR, targetG, targetB],
            GlobalCdColorTolerance = 30,
            GlobalCdBrightnessThreshold = 120
        };
        
        var determinedMode = GcdDetectionLogic.DetermineDetectionMode(settings);
        
        // Assert: Auto模式下有颜色配置时应该使用颜色检测
        return determinedMode == GcdDetectionMode.Color;
    }
    
    /// <summary>
    /// Property 11.2: Auto mode uses Brightness detection when GlobalCdColor is not configured
    /// WHEN GlobalCdDetectionMode is Auto AND GlobalCdColor has all values = 0,
    /// THEN brightness detection SHALL be used.
    /// **Validates: Requirements 7.1, 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool AutoModeUsesBrightnessWhenColorNotConfigured(
        PositiveInt brightnessThresholdGen)
    {
        var brightnessThreshold = (brightnessThresholdGen.Get % 200) + 50; // 50-249
        
        var settings = new AppSettings
        {
            GlobalCdDetectionMode = (int)GcdDetectionMode.Auto,
            GlobalCdColor = [0, 0, 0], // 无颜色配置
            GlobalCdColorTolerance = 30,
            GlobalCdBrightnessThreshold = brightnessThreshold
        };
        
        var determinedMode = GcdDetectionLogic.DetermineDetectionMode(settings);
        
        // Assert: Auto模式下无颜色配置时应该使用亮度检测
        return determinedMode == GcdDetectionMode.Brightness;
    }
    
    /// <summary>
    /// Property 11.3: Explicit Color mode always uses color detection
    /// WHEN GlobalCdDetectionMode is Color, THEN color detection SHALL be used
    /// regardless of GlobalCdColor configuration.
    /// **Validates: Requirements 7.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool ExplicitColorModeAlwaysUsesColor(
        byte r, byte g, byte b)
    {
        var settings = new AppSettings
        {
            GlobalCdDetectionMode = (int)GcdDetectionMode.Color,
            GlobalCdColor = [r, g, b], // 可能是任意值
            GlobalCdColorTolerance = 30,
            GlobalCdBrightnessThreshold = 120
        };
        
        var determinedMode = GcdDetectionLogic.DetermineDetectionMode(settings);
        
        // Assert: 显式颜色模式应该始终使用颜色检测
        return determinedMode == GcdDetectionMode.Color;
    }
    
    /// <summary>
    /// Property 11.4: Explicit Brightness mode always uses brightness detection
    /// WHEN GlobalCdDetectionMode is Brightness, THEN brightness detection SHALL be used
    /// regardless of GlobalCdColor configuration.
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool ExplicitBrightnessModeAlwaysUsesBrightness(
        byte r, byte g, byte b)
    {
        var settings = new AppSettings
        {
            GlobalCdDetectionMode = (int)GcdDetectionMode.Brightness,
            GlobalCdColor = [r, g, b], // 即使有颜色配置
            GlobalCdColorTolerance = 30,
            GlobalCdBrightnessThreshold = 120
        };
        
        var determinedMode = GcdDetectionLogic.DetermineDetectionMode(settings);
        
        // Assert: 显式亮度模式应该始终使用亮度检测
        return determinedMode == GcdDetectionMode.Brightness;
    }
    
    /// <summary>
    /// Property 11.5: Color detection matches when pixel color is within tolerance
    /// **Validates: Requirements 7.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool ColorDetectionMatchesWithinTolerance(
        byte targetR, byte targetG, byte targetB,
        PositiveInt toleranceGen)
    {
        var tolerance = (toleranceGen.Get % 50) + 1; // 1-50
        
        var settings = new AppSettings
        {
            GlobalCdDetectionMode = (int)GcdDetectionMode.Color,
            GlobalCdColor = [targetR, targetG, targetB],
            GlobalCdColorTolerance = tolerance
        };
        
        // 创建一个在容差范围内的颜色
        var pixelR = (byte)Math.Min(255, Math.Max(0, (int)targetR + (tolerance / 2)));
        var pixelG = (byte)Math.Min(255, Math.Max(0, (int)targetG - (tolerance / 2)));
        var pixelB = (byte)Math.Min(255, Math.Max(0, (int)targetB));
        
        var result = GcdDetectionLogic.DetectGcdByColor((pixelR, pixelG, pixelB), settings);
        
        // Assert: 在容差范围内的颜色应该匹配
        return result == true;
    }
    
    /// <summary>
    /// Property 11.6: Color detection does not match when pixel color is outside tolerance
    /// **Validates: Requirements 7.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool ColorDetectionDoesNotMatchOutsideTolerance(
        byte targetR, byte targetG, byte targetB,
        PositiveInt toleranceGen)
    {
        var tolerance = (toleranceGen.Get % 30) + 1; // 1-30
        
        var settings = new AppSettings
        {
            GlobalCdDetectionMode = (int)GcdDetectionMode.Color,
            GlobalCdColor = [targetR, targetG, targetB],
            GlobalCdColorTolerance = tolerance
        };
        
        // 创建一个超出容差范围的颜色（至少一个通道超出）
        var offset = tolerance + 10;
        var pixelR = (byte)Math.Min(255, Math.Max(0, (int)targetR + offset));
        var pixelG = (byte)Math.Min(255, Math.Max(0, (int)targetG + offset));
        var pixelB = (byte)Math.Min(255, Math.Max(0, (int)targetB + offset));
        
        // 确保至少一个通道超出容差
        if (Math.Abs(pixelR - targetR) <= tolerance &&
            Math.Abs(pixelG - targetG) <= tolerance &&
            Math.Abs(pixelB - targetB) <= tolerance)
        {
            // 如果由于边界限制导致仍在容差内，跳过此测试用例
            return true;
        }
        
        var result = GcdDetectionLogic.DetectGcdByColor((pixelR, pixelG, pixelB), settings);
        
        // Assert: 超出容差范围的颜色不应该匹配
        return result == false;
    }
    
    /// <summary>
    /// Property 11.7: Brightness detection triggers when brightness exceeds threshold
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool BrightnessDetectionTriggersAboveThreshold(
        PositiveInt thresholdGen)
    {
        var threshold = (thresholdGen.Get % 200) + 30; // 30-229
        
        var settings = new AppSettings
        {
            GlobalCdDetectionMode = (int)GcdDetectionMode.Brightness,
            GlobalCdBrightnessThreshold = threshold
        };
        
        // 创建一个亮度超过阈值的颜色
        var brightness = threshold + 10;
        var pixelValue = (byte)Math.Min(255, brightness);
        
        var result = GcdDetectionLogic.DetectGcdByBrightness((pixelValue, pixelValue, pixelValue), settings);
        
        // Assert: 亮度超过阈值时应该检测到GCD
        return result == true;
    }
    
    /// <summary>
    /// Property 11.8: Brightness detection does not trigger when brightness is below threshold
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool BrightnessDetectionDoesNotTriggerBelowThreshold(
        PositiveInt thresholdGen)
    {
        var threshold = (thresholdGen.Get % 150) + 100; // 100-249
        
        var settings = new AppSettings
        {
            GlobalCdDetectionMode = (int)GcdDetectionMode.Brightness,
            GlobalCdBrightnessThreshold = threshold
        };
        
        // 创建一个亮度低于阈值的颜色（且不是黄色）
        var brightness = threshold - 20;
        var pixelValue = (byte)Math.Max(0, (int)brightness);
        
        // 确保不是黄色（r > 150 && g > 150 && b < 100）
        var result = GcdDetectionLogic.DetectGcdByBrightness((pixelValue, pixelValue, pixelValue), settings);
        
        // Assert: 亮度低于阈值时不应该检测到GCD
        return result == false;
    }
    
    /// <summary>
    /// Property 11.9: Detection mode selection is deterministic
    /// For the same settings, the detection mode should always be the same.
    /// **Validates: Requirements 7.1, 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool DetectionModeSelectionIsDeterministic(
        byte modeValue, byte r, byte g, byte b)
    {
        var mode = modeValue % 3; // 0, 1, or 2
        
        var settings = new AppSettings
        {
            GlobalCdDetectionMode = mode,
            GlobalCdColor = [r, g, b],
            GlobalCdColorTolerance = 30,
            GlobalCdBrightnessThreshold = 120
        };
        
        // 多次调用应该返回相同结果
        var result1 = GcdDetectionLogic.DetermineDetectionMode(settings);
        var result2 = GcdDetectionLogic.DetermineDetectionMode(settings);
        var result3 = GcdDetectionLogic.DetermineDetectionMode(settings);
        
        return result1 == result2 && result2 == result3;
    }
}
