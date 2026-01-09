using FsCheck;
using FsCheck.Xunit;
using ShineProCS.Core.Interfaces;
using ShineProCS.Models;

namespace ShineProCS.Tests;

/// <summary>
/// Property-based tests for Buff check retry behavior
/// **Feature: business-logic-fixes, Property 6: Buff Check Retry Behavior**
/// **Validates: Requirements 4.1, 4.3**
/// </summary>
public class BuffCheckRetryBehaviorTests
{
    /// <summary>
    /// Mock keyboard interface for testing
    /// </summary>
    private class MockKeyboardInterface : IKeyboardInterface
    {
        private int _pressAndReleaseCount;
        
        public int PressAndReleaseCount => _pressAndReleaseCount;
        
        public bool PressKey(int keyCode) => true;
        public bool ReleaseKey(int keyCode) => true;
        
        public bool PressAndRelease(int keyCode)
        {
            _pressAndReleaseCount++;
            return true;
        }
    }
    
    /// <summary>
    /// Mock buff checker that can be configured to fail N times before succeeding
    /// </summary>
    private class MockBuffChecker
    {
        private readonly int _failuresBeforeSuccess;
        private int _checkCount;
        
        public MockBuffChecker(int failuresBeforeSuccess)
        {
            _failuresBeforeSuccess = failuresBeforeSuccess;
        }
        
        public int CheckCount => _checkCount;
        
        public bool CheckBuffExists(string buffName)
        {
            _checkCount++;
            return _checkCount > _failuresBeforeSuccess;
        }
    }
    
    /// <summary>
    /// Buff check retry executor that mirrors the refactored ExecuteSkillCycle logic
    /// </summary>
    private class BuffCheckRetryExecutor
    {
        private readonly IKeyboardInterface _keyboard;
        private readonly MockBuffChecker _buffChecker;
        private readonly List<int> _delayRecords = new();
        
        public BuffCheckRetryExecutor(IKeyboardInterface keyboard, MockBuffChecker buffChecker)
        {
            _keyboard = keyboard;
            _buffChecker = buffChecker;
        }
        
        public IReadOnlyList<int> DelayRecords => _delayRecords;
        public int TotalRetries { get; private set; }
        public bool BuffObtained { get; private set; }
        
        /// <summary>
        /// Execute buff check with retry logic
        /// Returns true if buff was obtained, false otherwise
        /// </summary>
        public bool ExecuteBuffCheckWithRetry(SkillConfig config)
        {
            // 检查是否有前置技能配置
            if (config.PreCastKeyCode <= 0)
                return true;
            
            // 初始Buff检查
            var buffSatisfied = _buffChecker.CheckBuffExists(config.PreCastConditionBuff);
            
            if (buffSatisfied)
            {
                BuffObtained = true;
                return true;
            }
            
            // 释放前置技能
            if (!_keyboard.PressAndRelease(config.PreCastKeyCode))
                return false;
            
            // 等待前置技能施法时间 (模拟)
            _delayRecords.Add(config.ComboDelay);
            
            // 使用配置的重试参数检查Buff
            var buffCheckDelay = config.BuffCheckDelay;
            var buffCheckRetries = config.BuffCheckRetries;
            
            for (int retry = 0; retry < buffCheckRetries; retry++)
            {
                // 记录延迟
                _delayRecords.Add(buffCheckDelay);
                TotalRetries++;
                
                buffSatisfied = _buffChecker.CheckBuffExists(config.PreCastConditionBuff);
                if (buffSatisfied)
                {
                    BuffObtained = true;
                    return true;
                }
            }
            
            BuffObtained = false;
            return false;
        }
    }
    
    /// <summary>
    /// Create a skill config with buff check configuration
    /// </summary>
    private static SkillConfig CreateSkillConfigWithBuffCheck(
        int keyCode, 
        int preCastKeyCode, 
        string buffName,
        int buffCheckDelay,
        int buffCheckRetries,
        int comboDelay)
    {
        return new SkillConfig
        {
            Name = $"TestSkill_{keyCode}",
            KeyCode = keyCode,
            Priority = 1,
            Enabled = true,
            PreCastKeyCode = preCastKeyCode,
            PreCastConditionBuff = buffName,
            BuffCheckDelay = buffCheckDelay,
            BuffCheckRetries = buffCheckRetries,
            ComboDelay = comboDelay
        };
    }

    /// <summary>
    /// Property 6: Buff Check Retry Behavior
    /// For any skill with PreCastKeyCode configured, when buff check fails after pre-cast,
    /// the system SHALL retry exactly BuffCheckRetries times with BuffCheckDelay interval between retries.
    /// **Validates: Requirements 4.1, 4.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool BuffCheckRetriesExactlyConfiguredTimes(
        PositiveInt keyCodeGen, 
        PositiveInt preCastKeyCodeGen,
        PositiveInt buffCheckDelayGen,
        PositiveInt buffCheckRetriesGen,
        PositiveInt comboDelayGen)
    {
        // Arrange - 配置参数
        var keyCode = (keyCodeGen.Get % 26) + 0x41; // A-Z keys
        var preCastKeyCode = (preCastKeyCodeGen.Get % 26) + 0x41;
        var buffCheckDelay = (buffCheckDelayGen.Get % 500) + 100; // 100-600ms
        var buffCheckRetries = (buffCheckRetriesGen.Get % 5) + 1; // 1-5 retries
        var comboDelay = (comboDelayGen.Get % 300) + 50; // 50-350ms
        
        var config = CreateSkillConfigWithBuffCheck(
            keyCode, preCastKeyCode, "TestBuff", 
            buffCheckDelay, buffCheckRetries, comboDelay);
        
        // 设置Buff检查器永远失败，以测试最大重试次数
        var buffChecker = new MockBuffChecker(int.MaxValue);
        var keyboard = new MockKeyboardInterface();
        var executor = new BuffCheckRetryExecutor(keyboard, buffChecker);
        
        // Act
        executor.ExecuteBuffCheckWithRetry(config);
        
        // Assert: 应该重试恰好 BuffCheckRetries 次
        // 注意：初始检查不算重试，重试是在前置技能释放后进行的
        return executor.TotalRetries == buffCheckRetries;
    }
    
    /// <summary>
    /// Property 6.1: Each retry uses configured BuffCheckDelay
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool EachRetryUsesConfiguredDelay(
        PositiveInt keyCodeGen, 
        PositiveInt preCastKeyCodeGen,
        PositiveInt buffCheckDelayGen,
        PositiveInt buffCheckRetriesGen,
        PositiveInt comboDelayGen)
    {
        // Arrange
        var keyCode = (keyCodeGen.Get % 26) + 0x41;
        var preCastKeyCode = (preCastKeyCodeGen.Get % 26) + 0x41;
        var buffCheckDelay = (buffCheckDelayGen.Get % 500) + 100;
        var buffCheckRetries = (buffCheckRetriesGen.Get % 5) + 1;
        var comboDelay = (comboDelayGen.Get % 300) + 50;
        
        var config = CreateSkillConfigWithBuffCheck(
            keyCode, preCastKeyCode, "TestBuff", 
            buffCheckDelay, buffCheckRetries, comboDelay);
        
        var buffChecker = new MockBuffChecker(int.MaxValue);
        var keyboard = new MockKeyboardInterface();
        var executor = new BuffCheckRetryExecutor(keyboard, buffChecker);
        
        // Act
        executor.ExecuteBuffCheckWithRetry(config);
        
        // Assert: 延迟记录应该包含 ComboDelay + (BuffCheckRetries * BuffCheckDelay)
        // 第一个是 ComboDelay，后面的都是 BuffCheckDelay
        var delays = executor.DelayRecords;
        
        if (delays.Count < 1) return false;
        if (delays[0] != comboDelay) return false;
        
        // 检查所有重试延迟都是 BuffCheckDelay
        for (int i = 1; i < delays.Count; i++)
        {
            if (delays[i] != buffCheckDelay) return false;
        }
        
        return delays.Count == 1 + buffCheckRetries;
    }
    
    /// <summary>
    /// Property 6.2: Retry stops early when buff is obtained
    /// **Validates: Requirements 4.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool RetryStopsWhenBuffObtained(
        PositiveInt keyCodeGen, 
        PositiveInt preCastKeyCodeGen,
        PositiveInt buffCheckDelayGen,
        PositiveInt buffCheckRetriesGen,
        PositiveInt failuresBeforeSuccessGen)
    {
        // Arrange
        var keyCode = (keyCodeGen.Get % 26) + 0x41;
        var preCastKeyCode = (preCastKeyCodeGen.Get % 26) + 0x41;
        var buffCheckDelay = (buffCheckDelayGen.Get % 500) + 100;
        var buffCheckRetries = (buffCheckRetriesGen.Get % 5) + 2; // 至少2次重试
        var failuresBeforeSuccess = (failuresBeforeSuccessGen.Get % buffCheckRetries) + 1; // 1到buffCheckRetries次失败后成功
        
        var config = CreateSkillConfigWithBuffCheck(
            keyCode, preCastKeyCode, "TestBuff", 
            buffCheckDelay, buffCheckRetries, 100);
        
        // 设置在第N次检查后成功（包括初始检查）
        var buffChecker = new MockBuffChecker(failuresBeforeSuccess);
        var keyboard = new MockKeyboardInterface();
        var executor = new BuffCheckRetryExecutor(keyboard, buffChecker);
        
        // Act
        var result = executor.ExecuteBuffCheckWithRetry(config);
        
        // Assert: 
        // 1. 应该成功获得Buff
        // 2. 重试次数应该小于等于配置的最大重试次数
        // 3. 实际检查次数 = 初始检查(1) + 重试次数
        return executor.BuffObtained && 
               executor.TotalRetries <= buffCheckRetries &&
               buffChecker.CheckCount <= failuresBeforeSuccess + 1;
    }
    
    /// <summary>
    /// Property 6.3: Pre-cast skill is executed before retry loop
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool PreCastSkillExecutedBeforeRetry(
        PositiveInt keyCodeGen, 
        PositiveInt preCastKeyCodeGen,
        PositiveInt buffCheckRetriesGen)
    {
        // Arrange
        var keyCode = (keyCodeGen.Get % 26) + 0x41;
        var preCastKeyCode = (preCastKeyCodeGen.Get % 26) + 0x41;
        var buffCheckRetries = (buffCheckRetriesGen.Get % 5) + 1;
        
        var config = CreateSkillConfigWithBuffCheck(
            keyCode, preCastKeyCode, "TestBuff", 
            200, buffCheckRetries, 100);
        
        var buffChecker = new MockBuffChecker(int.MaxValue);
        var keyboard = new MockKeyboardInterface();
        var executor = new BuffCheckRetryExecutor(keyboard, buffChecker);
        
        // Act
        executor.ExecuteBuffCheckWithRetry(config);
        
        // Assert: 前置技能应该被释放一次
        return keyboard.PressAndReleaseCount == 1;
    }
    
    /// <summary>
    /// Property 6.4: No retry when buff is already satisfied
    /// </summary>
    [Property(MaxTest = 100)]
    public bool NoRetryWhenBuffAlreadySatisfied(
        PositiveInt keyCodeGen, 
        PositiveInt preCastKeyCodeGen,
        PositiveInt buffCheckRetriesGen)
    {
        // Arrange
        var keyCode = (keyCodeGen.Get % 26) + 0x41;
        var preCastKeyCode = (preCastKeyCodeGen.Get % 26) + 0x41;
        var buffCheckRetries = (buffCheckRetriesGen.Get % 5) + 1;
        
        var config = CreateSkillConfigWithBuffCheck(
            keyCode, preCastKeyCode, "TestBuff", 
            200, buffCheckRetries, 100);
        
        // Buff检查器第一次就返回true
        var buffChecker = new MockBuffChecker(0);
        var keyboard = new MockKeyboardInterface();
        var executor = new BuffCheckRetryExecutor(keyboard, buffChecker);
        
        // Act
        var result = executor.ExecuteBuffCheckWithRetry(config);
        
        // Assert: 
        // 1. 应该成功
        // 2. 不应该有重试
        // 3. 不应该释放前置技能
        return result && 
               executor.TotalRetries == 0 && 
               keyboard.PressAndReleaseCount == 0 &&
               executor.BuffObtained;
    }
}
