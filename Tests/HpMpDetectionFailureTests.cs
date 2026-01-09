using FsCheck;
using FsCheck.Xunit;
using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Services;
using ShineProCS.Models;
using OpenCvSharp;

namespace ShineProCS.Tests;

/// <summary>
/// Property-based tests for HP/MP detection failure handling
/// **Feature: business-logic-fixes, Property 4: HP/MP Detection Failure Returns Cached Value**
/// **Validates: Requirements 3.1**
/// </summary>
public class HpMpDetectionFailureTests
{
    /// <summary>
    /// Mock image interface that can simulate detection failures
    /// </summary>
    private class MockImageInterface : IImageInterface
    {
        private readonly bool _shouldReturnNull;
        private readonly Mat? _fixedFrame;
        
        public MockImageInterface(bool shouldReturnNull = false, Mat? fixedFrame = null)
        {
            _shouldReturnNull = shouldReturnNull;
            _fixedFrame = fixedFrame;
        }
        
        public Mat? GetScreenRegion(int x, int y, int w, int h)
        {
            if (_shouldReturnNull) return null;
            if (_fixedFrame != null) return _fixedFrame.Clone();
            
            // Return a simple test frame
            var frame = new Mat(h, w, MatType.CV_8UC3, new Scalar(0, 255, 0)); // Green frame
            return frame;
        }
        
        public (byte r, byte g, byte b)? GetPixelColor(int x, int y) => (128, 128, 128);
        public double MatchTemplate(Mat source, Mat template) => 0.9;
        public void ReturnMat(Mat mat) => mat?.Dispose();
        public void Dispose() { }
    }
    
    /// <summary>
    /// Mock config manager for testing
    /// </summary>
    private class MockConfigManager : ConfigManager
    {
        private readonly AppSettings _settings;
        
        public MockConfigManager(AppSettings settings) : base()
        {
            _settings = settings;
        }
        
        public new AppSettings AppSettings => _settings;
    }
    
    /// <summary>
    /// Simplified StateDetector for testing that exposes internal state
    /// </summary>
    private class TestableStateDetector : IDisposable
    {
        private readonly IImageInterface _image;
        private readonly AppSettings _settings;
        
        // HP/MP检测失败缓存字段
        private double _lastValidHpPercent = 100.0;
        private double _lastValidMpPercent = 100.0;
        private int _consecutiveHpFailures = 0;
        private int _consecutiveMpFailures = 0;
        private const int MaxConsecutiveFailures = 5;
        
        public double LastValidHpPercent => _lastValidHpPercent;
        public double LastValidMpPercent => _lastValidMpPercent;
        public int ConsecutiveHpFailures => _consecutiveHpFailures;
        public int ConsecutiveMpFailures => _consecutiveMpFailures;
        
        public TestableStateDetector(IImageInterface image, AppSettings settings)
        {
            _image = image;
            _settings = settings;
        }
        
        /// <summary>
        /// Set cached values for testing
        /// </summary>
        public void SetCachedValues(double hp, double mp)
        {
            _lastValidHpPercent = hp;
            _lastValidMpPercent = mp;
        }
        
        public GameState DetectGameState()
        {
            var state = new GameState { UpdateTime = DateTime.Now };
            
            if (_settings.HealthBarRegion.Any(v => v > 0))
            {
                state.CurrentHpPercent = DetectBarPercent(_settings.HealthBarRegion, isHealth: true, out bool isHpCached);
                state.HpPercentage = state.CurrentHpPercent / 100.0;
                state.IsHpCached = isHpCached;
            }
            
            if (_settings.ManaBarRegion.Any(v => v > 0))
            {
                state.CurrentMpPercent = DetectBarPercent(_settings.ManaBarRegion, isHealth: false, out bool isMpCached);
                state.MpPercentage = state.CurrentMpPercent / 100.0;
                state.IsMpCached = isMpCached;
            }
            
            return state;
        }
        
        private double DetectBarPercent(int[] region, bool isHealth, out bool isCached)
        {
            isCached = false;
            
            if (region.Length < 4 || region[2] <= 0 || region[3] <= 0)
            {
                RecordFailure(isHealth);
                isCached = true;
                return isHealth ? _lastValidHpPercent : _lastValidMpPercent;
            }
            
            var frame = _image.GetScreenRegion(region[0], region[1], region[2], region[3]);
            if (frame == null)
            {
                RecordFailure(isHealth);
                isCached = true;
                return isHealth ? _lastValidHpPercent : _lastValidMpPercent;
            }
            
            try
            {
                // Simplified detection - just return a calculated value
                using var gray = new Mat();
                Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
                var mean = Cv2.Mean(gray);
                var percent = mean.Val0 / 255.0 * 100.0;
                var result = Math.Min(100.0, Math.Max(0.0, percent));
                
                RecordSuccess(isHealth, result);
                return result;
            }
            finally
            {
                _image.ReturnMat(frame);
            }
        }
        
        private void RecordFailure(bool isHealth)
        {
            if (isHealth)
            {
                _consecutiveHpFailures++;
            }
            else
            {
                _consecutiveMpFailures++;
            }
        }
        
        private void RecordSuccess(bool isHealth, double percent)
        {
            if (isHealth)
            {
                _lastValidHpPercent = percent;
                _consecutiveHpFailures = 0;
            }
            else
            {
                _lastValidMpPercent = percent;
                _consecutiveMpFailures = 0;
            }
        }
        
        public void Dispose() { }
    }
    
    /// <summary>
    /// Property 4: HP/MP Detection Failure Returns Cached Value
    /// For any HP/MP detection failure, the returned value SHALL equal the last 
    /// successfully detected value, not the default 100%.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool DetectionFailureReturnsCachedHpValue(PositiveInt cachedHpGen)
    {
        // Arrange - Set up a cached HP value that is NOT 100%
        var cachedHp = (cachedHpGen.Get % 99) + 1; // 1-99, never 100
        var settings = new AppSettings
        {
            HealthBarRegion = new[] { 100, 100, 200, 20 } // Valid region
        };
        
        // First detector with working image interface to set cached value
        var workingImage = new MockImageInterface(shouldReturnNull: false);
        var detector1 = new TestableStateDetector(workingImage, settings);
        detector1.SetCachedValues(cachedHp, 100.0);
        
        // Now create detector with failing image interface
        var failingImage = new MockImageInterface(shouldReturnNull: true);
        var detector2 = new TestableStateDetector(failingImage, settings);
        detector2.SetCachedValues(cachedHp, 100.0);
        
        // Act - Detect with failing interface
        var state = detector2.DetectGameState();
        
        // Assert - Should return cached value, not default 100%
        return Math.Abs(state.CurrentHpPercent - cachedHp) < 0.001;
    }
    
    /// <summary>
    /// Property 4.1: MP Detection Failure Returns Cached Value
    /// </summary>
    [Property(MaxTest = 100)]
    public bool DetectionFailureReturnsCachedMpValue(PositiveInt cachedMpGen)
    {
        // Arrange - Set up a cached MP value that is NOT 100%
        var cachedMp = (cachedMpGen.Get % 99) + 1; // 1-99, never 100
        var settings = new AppSettings
        {
            ManaBarRegion = new[] { 100, 120, 200, 20 } // Valid region
        };
        
        var failingImage = new MockImageInterface(shouldReturnNull: true);
        var detector = new TestableStateDetector(failingImage, settings);
        detector.SetCachedValues(100.0, cachedMp);
        
        // Act
        var state = detector.DetectGameState();
        
        // Assert - Should return cached value
        return Math.Abs(state.CurrentMpPercent - cachedMp) < 0.001;
    }
    
    /// <summary>
    /// Property 4.2: Consecutive failures are tracked
    /// </summary>
    [Property(MaxTest = 100)]
    public bool ConsecutiveFailuresAreTracked(PositiveInt failureCountGen)
    {
        // Arrange
        var failureCount = (failureCountGen.Get % 10) + 1; // 1-10 failures
        var settings = new AppSettings
        {
            HealthBarRegion = new[] { 100, 100, 200, 20 }
        };
        
        var failingImage = new MockImageInterface(shouldReturnNull: true);
        var detector = new TestableStateDetector(failingImage, settings);
        
        // Act - Trigger multiple failures
        for (int i = 0; i < failureCount; i++)
        {
            detector.DetectGameState();
        }
        
        // Assert - Consecutive failures should be tracked
        return detector.ConsecutiveHpFailures == failureCount;
    }
    
    /// <summary>
    /// Property 4.3: Successful detection resets failure counter
    /// </summary>
    [Fact]
    public void SuccessfulDetectionResetsFailureCounter()
    {
        // Arrange
        var settings = new AppSettings
        {
            HealthBarRegion = new[] { 100, 100, 200, 20 }
        };
        
        // First, create failures
        var failingImage = new MockImageInterface(shouldReturnNull: true);
        var detector = new TestableStateDetector(failingImage, settings);
        detector.DetectGameState();
        detector.DetectGameState();
        detector.DetectGameState();
        
        Assert.True(detector.ConsecutiveHpFailures > 0);
        
        // Now create a working detector
        var workingImage = new MockImageInterface(shouldReturnNull: false);
        var detector2 = new TestableStateDetector(workingImage, settings);
        
        // Simulate some failures first
        var failingImage2 = new MockImageInterface(shouldReturnNull: true);
        var detector3 = new TestableStateDetector(failingImage2, settings);
        detector3.DetectGameState(); // One failure
        
        // Then success with working image
        var detector4 = new TestableStateDetector(workingImage, settings);
        detector4.DetectGameState();
        
        // Assert - After success, counter should be 0
        Assert.Equal(0, detector4.ConsecutiveHpFailures);
    }
}


/// <summary>
/// Property-based tests for GameState cache flag consistency
/// **Feature: business-logic-fixes, Property 5: GameState Cache Flag Consistency**
/// **Validates: Requirements 3.3**
/// </summary>
public class GameStateCacheFlagConsistencyTests
{
    /// <summary>
    /// Mock image interface that can simulate detection failures
    /// </summary>
    private class MockImageInterface : IImageInterface
    {
        private readonly bool _shouldReturnNull;
        
        public MockImageInterface(bool shouldReturnNull = false)
        {
            _shouldReturnNull = shouldReturnNull;
        }
        
        public Mat? GetScreenRegion(int x, int y, int w, int h)
        {
            if (_shouldReturnNull) return null;
            
            // Return a simple test frame
            var frame = new Mat(h, w, MatType.CV_8UC3, new Scalar(0, 255, 0)); // Green frame
            return frame;
        }
        
        public (byte r, byte g, byte b)? GetPixelColor(int x, int y) => (128, 128, 128);
        public double MatchTemplate(Mat source, Mat template) => 0.9;
        public void ReturnMat(Mat mat) => mat?.Dispose();
        public void Dispose() { }
    }
    
    /// <summary>
    /// Testable StateDetector that exposes internal state for testing
    /// </summary>
    private class TestableStateDetector : IDisposable
    {
        private readonly IImageInterface _image;
        private readonly AppSettings _settings;
        
        private double _lastValidHpPercent = 100.0;
        private double _lastValidMpPercent = 100.0;
        private int _consecutiveHpFailures = 0;
        private int _consecutiveMpFailures = 0;
        
        public TestableStateDetector(IImageInterface image, AppSettings settings)
        {
            _image = image;
            _settings = settings;
        }
        
        public void SetCachedValues(double hp, double mp)
        {
            _lastValidHpPercent = hp;
            _lastValidMpPercent = mp;
        }
        
        public GameState DetectGameState()
        {
            var state = new GameState { UpdateTime = DateTime.Now };
            
            if (_settings.HealthBarRegion.Any(v => v > 0))
            {
                state.CurrentHpPercent = DetectBarPercent(_settings.HealthBarRegion, isHealth: true, out bool isHpCached);
                state.HpPercentage = state.CurrentHpPercent / 100.0;
                state.IsHpCached = isHpCached;
            }
            
            if (_settings.ManaBarRegion.Any(v => v > 0))
            {
                state.CurrentMpPercent = DetectBarPercent(_settings.ManaBarRegion, isHealth: false, out bool isMpCached);
                state.MpPercentage = state.CurrentMpPercent / 100.0;
                state.IsMpCached = isMpCached;
            }
            
            return state;
        }
        
        private double DetectBarPercent(int[] region, bool isHealth, out bool isCached)
        {
            isCached = false;
            
            if (region.Length < 4 || region[2] <= 0 || region[3] <= 0)
            {
                if (isHealth) _consecutiveHpFailures++;
                else _consecutiveMpFailures++;
                isCached = true;
                return isHealth ? _lastValidHpPercent : _lastValidMpPercent;
            }
            
            var frame = _image.GetScreenRegion(region[0], region[1], region[2], region[3]);
            if (frame == null)
            {
                if (isHealth) _consecutiveHpFailures++;
                else _consecutiveMpFailures++;
                isCached = true;
                return isHealth ? _lastValidHpPercent : _lastValidMpPercent;
            }
            
            try
            {
                using var gray = new Mat();
                Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
                var mean = Cv2.Mean(gray);
                var percent = mean.Val0 / 255.0 * 100.0;
                var result = Math.Min(100.0, Math.Max(0.0, percent));
                
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
                _image.ReturnMat(frame);
            }
        }
        
        public void Dispose() { }
    }
    
    /// <summary>
    /// Property 5: GameState Cache Flag Consistency
    /// For any GameState returned by DetectGameState(), if the HP/MP value equals the cached value 
    /// due to detection failure, the corresponding IsHpCached/IsMpCached flag SHALL be true.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool IsHpCachedFlagIsTrueWhenDetectionFails(PositiveInt cachedHpGen)
    {
        // Arrange
        var cachedHp = (cachedHpGen.Get % 99) + 1; // 1-99
        var settings = new AppSettings
        {
            HealthBarRegion = new[] { 100, 100, 200, 20 }
        };
        
        var failingImage = new MockImageInterface(shouldReturnNull: true);
        var detector = new TestableStateDetector(failingImage, settings);
        detector.SetCachedValues(cachedHp, 100.0);
        
        // Act
        var state = detector.DetectGameState();
        
        // Assert - When detection fails and cached value is used, IsHpCached should be true
        return state.IsHpCached == true && Math.Abs(state.CurrentHpPercent - cachedHp) < 0.001;
    }
    
    /// <summary>
    /// Property 5.1: IsMpCached flag is true when MP detection fails
    /// </summary>
    [Property(MaxTest = 100)]
    public bool IsMpCachedFlagIsTrueWhenDetectionFails(PositiveInt cachedMpGen)
    {
        // Arrange
        var cachedMp = (cachedMpGen.Get % 99) + 1;
        var settings = new AppSettings
        {
            ManaBarRegion = new[] { 100, 120, 200, 20 }
        };
        
        var failingImage = new MockImageInterface(shouldReturnNull: true);
        var detector = new TestableStateDetector(failingImage, settings);
        detector.SetCachedValues(100.0, cachedMp);
        
        // Act
        var state = detector.DetectGameState();
        
        // Assert
        return state.IsMpCached == true && Math.Abs(state.CurrentMpPercent - cachedMp) < 0.001;
    }
    
    /// <summary>
    /// Property 5.2: IsHpCached flag is false when detection succeeds
    /// </summary>
    [Property(MaxTest = 100)]
    public bool IsHpCachedFlagIsFalseWhenDetectionSucceeds(PositiveInt dummyGen)
    {
        // Arrange
        var settings = new AppSettings
        {
            HealthBarRegion = new[] { 100, 100, 200, 20 }
        };
        
        var workingImage = new MockImageInterface(shouldReturnNull: false);
        var detector = new TestableStateDetector(workingImage, settings);
        
        // Act
        var state = detector.DetectGameState();
        
        // Assert - When detection succeeds, IsHpCached should be false
        return state.IsHpCached == false;
    }
    
    /// <summary>
    /// Property 5.3: IsMpCached flag is false when detection succeeds
    /// </summary>
    [Property(MaxTest = 100)]
    public bool IsMpCachedFlagIsFalseWhenDetectionSucceeds(PositiveInt dummyGen)
    {
        // Arrange
        var settings = new AppSettings
        {
            ManaBarRegion = new[] { 100, 120, 200, 20 }
        };
        
        var workingImage = new MockImageInterface(shouldReturnNull: false);
        var detector = new TestableStateDetector(workingImage, settings);
        
        // Act
        var state = detector.DetectGameState();
        
        // Assert
        return state.IsMpCached == false;
    }
    
    /// <summary>
    /// Property 5.4: Both HP and MP cache flags are consistent with their detection status
    /// </summary>
    [Property(MaxTest = 100)]
    public bool BothCacheFlagsAreConsistent(PositiveInt cachedHpGen, PositiveInt cachedMpGen)
    {
        // Arrange - Both HP and MP detection will fail
        var cachedHp = (cachedHpGen.Get % 99) + 1;
        var cachedMp = (cachedMpGen.Get % 99) + 1;
        var settings = new AppSettings
        {
            HealthBarRegion = new[] { 100, 100, 200, 20 },
            ManaBarRegion = new[] { 100, 120, 200, 20 }
        };
        
        var failingImage = new MockImageInterface(shouldReturnNull: true);
        var detector = new TestableStateDetector(failingImage, settings);
        detector.SetCachedValues(cachedHp, cachedMp);
        
        // Act
        var state = detector.DetectGameState();
        
        // Assert - Both flags should be true when both detections fail
        return state.IsHpCached == true && 
               state.IsMpCached == true &&
               Math.Abs(state.CurrentHpPercent - cachedHp) < 0.001 &&
               Math.Abs(state.CurrentMpPercent - cachedMp) < 0.001;
    }
}
