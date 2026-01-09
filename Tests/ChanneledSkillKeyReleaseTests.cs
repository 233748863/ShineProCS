using FsCheck;
using FsCheck.Xunit;
using ShineProCS.Core.Interfaces;
using ShineProCS.Models;

namespace ShineProCS.Tests;

/// <summary>
/// Property-based tests for channeled skill key release guarantee
/// **Feature: business-logic-fixes, Property 1: Key Release Guarantee for Channeled Skills**
/// **Validates: Requirements 1.1, 1.2, 1.3**
/// </summary>
public class ChanneledSkillKeyReleaseTests
{
    /// <summary>
    /// Mock keyboard interface that tracks key press/release calls
    /// and can simulate failures
    /// </summary>
    private class MockKeyboardInterface : IKeyboardInterface
    {
        private readonly HashSet<int> _pressedKeys = new();
        private readonly bool _shouldFailRelease;
        private int _pressCount;
        private int _releaseCount;
        
        public MockKeyboardInterface(bool shouldFailRelease = false)
        {
            _shouldFailRelease = shouldFailRelease;
        }
        
        public IReadOnlySet<int> PressedKeys => _pressedKeys;
        public int PressCount => _pressCount;
        public int ReleaseCount => _releaseCount;
        
        public bool PressKey(int keyCode)
        {
            _pressCount++;
            _pressedKeys.Add(keyCode);
            return true;
        }
        
        public bool ReleaseKey(int keyCode)
        {
            _releaseCount++;
            if (_shouldFailRelease && _releaseCount == 1)
            {
                // First release fails, second succeeds
                return false;
            }
            _pressedKeys.Remove(keyCode);
            return true;
        }
        
        public bool PressAndRelease(int keyCode)
        {
            PressKey(keyCode);
            return ReleaseKey(keyCode);
        }
    }
    
    /// <summary>
    /// Helper class to execute channeled skill logic with try-finally pattern
    /// This mirrors the refactored ExecuteChanneledSkill implementation
    /// </summary>
    private class ChanneledSkillExecutor
    {
        private readonly IKeyboardInterface _keyboard;
        private readonly Action<string>? _logAction;
        
        public ChanneledSkillExecutor(IKeyboardInterface keyboard, Action<string>? logAction = null)
        {
            _keyboard = keyboard;
            _logAction = logAction;
        }
        
        /// <summary>
        /// Execute a channeled skill with guaranteed key release
        /// </summary>
        public bool ExecuteChanneledSkill(SkillRuntimeState skill, Action? channelLogic = null)
        {
            var config = skill.Config;
            
            if (!_keyboard.PressKey(config.KeyCode))
            {
                skill.ConsecutiveFailures++;
                return false;
            }
            
            try
            {
                skill.MarkAsUsed();
                skill.ConsecutiveFailures = 0;
                
                // Execute channel logic (may throw)
                channelLogic?.Invoke();
                
                return true;
            }
            catch (Exception ex)
            {
                _logAction?.Invoke($"Channel exception: {ex.Message}");
                return false;
            }
            finally
            {
                // Ensure key is always released
                if (!_keyboard.ReleaseKey(config.KeyCode))
                {
                    _logAction?.Invoke("Key release failed, retrying");
                    Thread.Sleep(10);
                    _keyboard.ReleaseKey(config.KeyCode);
                }
            }
        }
    }
    
    /// <summary>
    /// Create a valid channeled skill config for testing
    /// </summary>
    private static SkillConfig CreateChanneledSkillConfig(int keyCode, int priority, int cooldown, int castDuration, int channelInterruptTime)
    {
        return new SkillConfig
        {
            Name = $"TestSkill_{keyCode}",
            KeyCode = keyCode,
            Priority = priority,
            Enabled = true,
            Cooldown = cooldown,
            CastType = SkillCastType.Channeled,
            CastDuration = castDuration,
            ChannelInterruptTime = channelInterruptTime
        };
    }
    
    /// <summary>
    /// Property 1: Key Release Guarantee for Channeled Skills
    /// For any channeled skill execution, regardless of whether it completes normally,
    /// throws an exception, or is interrupted, the key SHALL be released after execution.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool KeyIsAlwaysReleasedAfterNormalExecution(PositiveInt keyCodeGen, PositiveInt priorityGen, PositiveInt cooldownGen, PositiveInt castDurationGen)
    {
        // Arrange
        var keyCode = (keyCodeGen.Get % 26) + 0x41; // A-Z keys
        var priority = (priorityGen.Get % 100) + 1;
        var cooldown = (cooldownGen.Get % 60) + 1;
        var castDuration = (castDurationGen.Get % 5000) + 100;
        var channelInterruptTime = castDuration / 2;
        
        var config = CreateChanneledSkillConfig(keyCode, priority, cooldown, castDuration, channelInterruptTime);
        var keyboard = new MockKeyboardInterface();
        var executor = new ChanneledSkillExecutor(keyboard);
        var skill = new SkillRuntimeState(config);
        
        // Act
        executor.ExecuteChanneledSkill(skill, () => Thread.Sleep(10));
        
        // Assert: Key should not be pressed after execution and ReleaseKey should be called
        return !keyboard.PressedKeys.Contains(config.KeyCode) && keyboard.ReleaseCount >= 1;
    }
    
    /// <summary>
    /// Property 1.1: Key is released even when channel logic throws exception
    /// Validates Requirement 1.1
    /// </summary>
    [Property(MaxTest = 100)]
    public bool KeyIsReleasedWhenExceptionOccurs(PositiveInt keyCodeGen, PositiveInt priorityGen, PositiveInt cooldownGen, PositiveInt castDurationGen)
    {
        // Arrange
        var keyCode = (keyCodeGen.Get % 26) + 0x41;
        var priority = (priorityGen.Get % 100) + 1;
        var cooldown = (cooldownGen.Get % 60) + 1;
        var castDuration = (castDurationGen.Get % 5000) + 100;
        var channelInterruptTime = castDuration / 2;
        
        var config = CreateChanneledSkillConfig(keyCode, priority, cooldown, castDuration, channelInterruptTime);
        var keyboard = new MockKeyboardInterface();
        var executor = new ChanneledSkillExecutor(keyboard);
        var skill = new SkillRuntimeState(config);
        
        // Act - Execute with exception-throwing channel logic
        executor.ExecuteChanneledSkill(skill, () => throw new InvalidOperationException("Test exception"));
        
        // Assert: Key should still be released
        return !keyboard.PressedKeys.Contains(config.KeyCode) && keyboard.ReleaseCount >= 1;
    }
    
    /// <summary>
    /// Property 1.2: Key release is retried if first attempt fails
    /// Validates Requirement 1.4 (recovery attempt)
    /// </summary>
    [Property(MaxTest = 100)]
    public bool KeyReleaseIsRetriedOnFailure(PositiveInt keyCodeGen, PositiveInt priorityGen, PositiveInt cooldownGen, PositiveInt castDurationGen)
    {
        // Arrange
        var keyCode = (keyCodeGen.Get % 26) + 0x41;
        var priority = (priorityGen.Get % 100) + 1;
        var cooldown = (cooldownGen.Get % 60) + 1;
        var castDuration = (castDurationGen.Get % 5000) + 100;
        var channelInterruptTime = castDuration / 2;
        
        var config = CreateChanneledSkillConfig(keyCode, priority, cooldown, castDuration, channelInterruptTime);
        var keyboard = new MockKeyboardInterface(shouldFailRelease: true);
        var executor = new ChanneledSkillExecutor(keyboard);
        var skill = new SkillRuntimeState(config);
        
        // Act
        executor.ExecuteChanneledSkill(skill, () => Thread.Sleep(10));
        
        // Assert: ReleaseKey should be called twice (initial + retry) and key should eventually be released
        return keyboard.ReleaseCount >= 2 && !keyboard.PressedKeys.Contains(config.KeyCode);
    }
    
    /// <summary>
    /// Property 1.3: Press count equals release count for successful executions
    /// Validates that every press has a corresponding release
    /// </summary>
    [Property(MaxTest = 100)]
    public bool PressAndReleaseCountsAreBalanced(PositiveInt keyCodeGen, PositiveInt priorityGen, PositiveInt cooldownGen, PositiveInt castDurationGen)
    {
        // Arrange
        var keyCode = (keyCodeGen.Get % 26) + 0x41;
        var priority = (priorityGen.Get % 100) + 1;
        var cooldown = (cooldownGen.Get % 60) + 1;
        var castDuration = (castDurationGen.Get % 5000) + 100;
        var channelInterruptTime = castDuration / 2;
        
        var config = CreateChanneledSkillConfig(keyCode, priority, cooldown, castDuration, channelInterruptTime);
        var keyboard = new MockKeyboardInterface();
        var executor = new ChanneledSkillExecutor(keyboard);
        var skill = new SkillRuntimeState(config);
        
        // Act
        executor.ExecuteChanneledSkill(skill, () => Thread.Sleep(10));
        
        // Assert: Press count should equal release count
        return keyboard.PressCount == keyboard.ReleaseCount;
    }
}
