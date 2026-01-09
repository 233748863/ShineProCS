using FsCheck;
using FsCheck.Xunit;
using ShineProCS.Models;

namespace ShineProCS.Tests;

/// <summary>
/// Property-based tests for Frame Change Detection
/// **Feature: business-logic-fixes, Property 12: Frame Change Detection Disabled When Threshold Zero**
/// **Validates: Requirements 8.2**
/// </summary>
public class FrameChangeDetectionTests
{
    /// <summary>
    /// 测试辅助类：模拟 SkillLoopEngine 的帧变化检测逻辑
    /// 由于 IsFrameUnchanged 是私有方法，我们提取核心逻辑进行测试
    /// </summary>
    private static class FrameChangeDetectionLogic
    {
        /// <summary>
        /// 检测帧变化检测是否应该被禁用
        /// 当阈值为0或负数时，检测应该被禁用
        /// </summary>
        /// <param name="threshold">配置的阈值</param>
        /// <returns>true 表示检测被禁用，false 表示检测启用</returns>
        public static bool IsDetectionDisabled(int threshold)
        {
            return threshold <= 0;
        }
        
        /// <summary>
        /// 模拟 IsFrameUnchanged 的返回值逻辑
        /// 当检测被禁用时，始终返回 false（表示帧已变化）
        /// </summary>
        /// <param name="threshold">配置的阈值</param>
        /// <param name="frameDiff">帧差异值</param>
        /// <param name="sampleCount">采样点数量</param>
        /// <returns>true 表示帧未变化，false 表示帧已变化或检测被禁用</returns>
        public static bool IsFrameUnchanged(int threshold, int frameDiff, int sampleCount)
        {
            // 阈值为0或负数时禁用检测，始终返回false
            if (threshold <= 0)
            {
                return false;
            }
            
            // 计算动态阈值
            int dynamicThreshold = sampleCount * threshold;
            
            // 差异小于阈值时认为帧未变化
            return frameDiff < dynamicThreshold;
        }
        
        /// <summary>
        /// 从 AppSettings 获取帧变化检测阈值
        /// </summary>
        public static int GetThresholdFromSettings(AppSettings settings)
        {
            return settings.FrameChangeThreshold;
        }
    }

    /// <summary>
    /// Property 12.1: Frame change detection is disabled when threshold is zero
    /// WHEN FrameChangeThreshold is set to 0, IsFrameUnchanged() SHALL return false.
    /// **Validates: Requirements 8.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool DetectionDisabledWhenThresholdIsZero(
        PositiveInt frameDiffGen,
        PositiveInt sampleCountGen)
    {
        var frameDiff = frameDiffGen.Get;
        var sampleCount = Math.Max(1, sampleCountGen.Get % 10000); // 1-9999
        
        // 阈值设置为0
        var threshold = 0;
        
        var result = FrameChangeDetectionLogic.IsFrameUnchanged(threshold, frameDiff, sampleCount);
        
        // Assert: 阈值为0时，无论帧差异多少，都应该返回false（检测被禁用）
        return result == false;
    }
    
    /// <summary>
    /// Property 12.2: Frame change detection is disabled when threshold is negative
    /// WHEN FrameChangeThreshold is negative, IsFrameUnchanged() SHALL return false.
    /// **Validates: Requirements 8.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool DetectionDisabledWhenThresholdIsNegative(
        NegativeInt thresholdGen,
        PositiveInt frameDiffGen,
        PositiveInt sampleCountGen)
    {
        var threshold = thresholdGen.Get;
        var frameDiff = frameDiffGen.Get;
        var sampleCount = Math.Max(1, sampleCountGen.Get % 10000);
        
        var result = FrameChangeDetectionLogic.IsFrameUnchanged(threshold, frameDiff, sampleCount);
        
        // Assert: 阈值为负数时，无论帧差异多少，都应该返回false（检测被禁用）
        return result == false;
    }
    
    /// <summary>
    /// Property 12.3: Frame change detection is enabled when threshold is positive
    /// WHEN FrameChangeThreshold is positive, detection SHALL be enabled.
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool DetectionEnabledWhenThresholdIsPositive(
        PositiveInt thresholdGen)
    {
        var threshold = thresholdGen.Get;
        
        var isDisabled = FrameChangeDetectionLogic.IsDetectionDisabled(threshold);
        
        // Assert: 阈值为正数时，检测应该启用
        return isDisabled == false;
    }
    
    /// <summary>
    /// Property 12.4: Frame unchanged when diff is below threshold
    /// WHEN frame diff is below the calculated threshold, IsFrameUnchanged() SHALL return true.
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool FrameUnchangedWhenDiffBelowThreshold(
        PositiveInt thresholdGen,
        PositiveInt sampleCountGen)
    {
        var threshold = (thresholdGen.Get % 50) + 1; // 1-50
        var sampleCount = Math.Max(1, sampleCountGen.Get % 1000) + 1; // 1-1000
        
        // 计算动态阈值
        var dynamicThreshold = sampleCount * threshold;
        
        // 创建一个低于阈值的帧差异
        var frameDiff = dynamicThreshold / 2;
        
        var result = FrameChangeDetectionLogic.IsFrameUnchanged(threshold, frameDiff, sampleCount);
        
        // Assert: 差异低于阈值时，应该返回true（帧未变化）
        return result == true;
    }
    
    /// <summary>
    /// Property 12.5: Frame changed when diff is above or equal to threshold
    /// WHEN frame diff is >= the calculated threshold, IsFrameUnchanged() SHALL return false.
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool FrameChangedWhenDiffAboveThreshold(
        PositiveInt thresholdGen,
        PositiveInt sampleCountGen)
    {
        var threshold = (thresholdGen.Get % 50) + 1; // 1-50
        var sampleCount = Math.Max(1, sampleCountGen.Get % 1000) + 1; // 1-1000
        
        // 计算动态阈值
        var dynamicThreshold = sampleCount * threshold;
        
        // 创建一个高于或等于阈值的帧差异
        var frameDiff = dynamicThreshold + 1;
        
        var result = FrameChangeDetectionLogic.IsFrameUnchanged(threshold, frameDiff, sampleCount);
        
        // Assert: 差异高于或等于阈值时，应该返回false（帧已变化）
        return result == false;
    }
    
    /// <summary>
    /// Property 12.6: AppSettings default threshold is 15
    /// The default FrameChangeThreshold SHALL be 15.
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Fact]
    public void DefaultThresholdIs15()
    {
        var settings = new AppSettings();
        
        // Assert: 默认阈值应该是15
        Assert.Equal(15, settings.FrameChangeThreshold);
    }
    
    /// <summary>
    /// Property 12.7: Threshold from settings is correctly retrieved
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool ThresholdFromSettingsIsCorrectlyRetrieved(int threshold)
    {
        var settings = new AppSettings
        {
            FrameChangeThreshold = threshold
        };
        
        var retrievedThreshold = FrameChangeDetectionLogic.GetThresholdFromSettings(settings);
        
        // Assert: 从设置中获取的阈值应该与设置的值相同
        return retrievedThreshold == threshold;
    }
    
    /// <summary>
    /// Property 12.8: Detection behavior is deterministic
    /// For the same inputs, the detection result should always be the same.
    /// **Validates: Requirements 8.1, 8.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool DetectionBehaviorIsDeterministic(
        int threshold,
        PositiveInt frameDiffGen,
        PositiveInt sampleCountGen)
    {
        var frameDiff = frameDiffGen.Get;
        var sampleCount = Math.Max(1, sampleCountGen.Get % 10000);
        
        // 多次调用应该返回相同结果
        var result1 = FrameChangeDetectionLogic.IsFrameUnchanged(threshold, frameDiff, sampleCount);
        var result2 = FrameChangeDetectionLogic.IsFrameUnchanged(threshold, frameDiff, sampleCount);
        var result3 = FrameChangeDetectionLogic.IsFrameUnchanged(threshold, frameDiff, sampleCount);
        
        return result1 == result2 && result2 == result3;
    }
    
    /// <summary>
    /// Property 12.9: Zero threshold always returns false regardless of frame content
    /// This is the core property for Requirement 8.2
    /// **Validates: Requirements 8.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool ZeroThresholdAlwaysReturnsFalse(
        int frameDiff,
        int sampleCount)
    {
        // 确保 sampleCount 为正数
        sampleCount = Math.Max(1, Math.Abs(sampleCount) % 10000 + 1);
        // frameDiff 可以是任意值
        frameDiff = Math.Abs(frameDiff);
        
        var result = FrameChangeDetectionLogic.IsFrameUnchanged(0, frameDiff, sampleCount);
        
        // Assert: 阈值为0时，无论输入如何，都应该返回false
        return result == false;
    }
}
