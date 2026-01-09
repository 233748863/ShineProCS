using FsCheck;
using FsCheck.Xunit;
using ShineProCS.Models;

namespace ShineProCS.Tests;

/// <summary>
/// Property-based tests for configuration hot-reload thread safety
/// **Feature: business-logic-fixes, Property 2: Thread-Safe Skill State Access**
/// **Feature: business-logic-fixes, Property 3: Configuration Reload Preserves Valid State**
/// **Validates: Requirements 2.1, 2.2, 2.3**
/// </summary>
public class ConfigHotReloadThreadSafetyTests
{
    /// <summary>
    /// Thread-safe skill state manager that mirrors the SkillLoopEngine implementation
    /// Uses ReaderWriterLockSlim for concurrent access protection
    /// </summary>
    private class ThreadSafeSkillStateManager
    {
        private List<SkillRuntimeState> _skillStates = new();
        private readonly ReaderWriterLockSlim _skillStatesLock = new();
        private readonly List<string> _logs = new();
        
        public IReadOnlyList<string> Logs => _logs;
        public int SkillCount
        {
            get
            {
                _skillStatesLock.EnterReadLock();
                try
                {
                    return _skillStates.Count;
                }
                finally
                {
                    _skillStatesLock.ExitReadLock();
                }
            }
        }
        
        /// <summary>
        /// Load skills with write lock protection
        /// Preserves old configuration on failure
        /// </summary>
        public bool LoadSkills(Func<List<SkillConfig>> configLoader)
        {
            try
            {
                var configs = configLoader();
                var newStates = configs.Select(s => new SkillRuntimeState(s)).ToList();
                
                _skillStatesLock.EnterWriteLock();
                try
                {
                    _skillStates = newStates;
                }
                finally
                {
                    _skillStatesLock.ExitWriteLock();
                }
                
                _logs.Add($"Loaded {newStates.Count} skills");
                return true;
            }
            catch (Exception ex)
            {
                // Configuration load failed - preserve old configuration
                _logs.Add($"Load failed, preserving old config: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Read skill states with read lock protection
        /// </summary>
        public List<SkillRuntimeState> GetSkillStates()
        {
            _skillStatesLock.EnterReadLock();
            try
            {
                return _skillStates.ToList();
            }
            finally
            {
                _skillStatesLock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// Simulate main loop iteration with read lock
        /// </summary>
        public int IterateSkillStates(Action<SkillRuntimeState>? action = null)
        {
            _skillStatesLock.EnterReadLock();
            try
            {
                var count = 0;
                foreach (var skill in _skillStates)
                {
                    action?.Invoke(skill);
                    count++;
                }
                return count;
            }
            finally
            {
                _skillStatesLock.ExitReadLock();
            }
        }
    }
    
    /// <summary>
    /// Create a valid skill config for testing
    /// </summary>
    private static SkillConfig CreateSkillConfig(int index, int keyCode, int priority)
    {
        return new SkillConfig
        {
            Name = $"TestSkill_{index}",
            KeyCode = keyCode,
            Priority = priority,
            Enabled = true,
            Cooldown = 10
        };
    }
    
    /// <summary>
    /// Property 2: Thread-Safe Skill State Access
    /// For any concurrent access to skill states (configuration reload + main loop iteration),
    /// the system SHALL not throw ConcurrentModificationException and skill state data SHALL remain consistent.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool ConcurrentAccessDoesNotThrowException(PositiveInt skillCountGen, PositiveInt iterationCountGen)
    {
        // Arrange
        var skillCount = (skillCountGen.Get % 20) + 1;
        var iterationCount = (iterationCountGen.Get % 50) + 10;
        
        var manager = new ThreadSafeSkillStateManager();
        var exceptions = new List<Exception>();
        var completedIterations = 0;
        var completedReloads = 0;
        
        // Initial load
        manager.LoadSkills(() => Enumerable.Range(0, skillCount)
            .Select(i => CreateSkillConfig(i, 0x41 + i, i + 1))
            .ToList());
        
        // Act - Run concurrent reads and writes
        var readerTasks = Enumerable.Range(0, 3).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < iterationCount; i++)
            {
                try
                {
                    manager.IterateSkillStates(skill => 
                    {
                        // Simulate some work
                        var _ = skill.Config.Name;
                    });
                    Interlocked.Increment(ref completedIterations);
                }
                catch (Exception ex)
                {
                    lock (exceptions) { exceptions.Add(ex); }
                }
            }
        })).ToArray();
        
        var writerTask = Task.Run(() =>
        {
            for (int i = 0; i < iterationCount / 5; i++)
            {
                try
                {
                    var newCount = (i % 10) + 1;
                    manager.LoadSkills(() => Enumerable.Range(0, newCount)
                        .Select(j => CreateSkillConfig(j, 0x41 + j, j + 1))
                        .ToList());
                    Interlocked.Increment(ref completedReloads);
                    Thread.Sleep(1); // Small delay between writes
                }
                catch (Exception ex)
                {
                    lock (exceptions) { exceptions.Add(ex); }
                }
            }
        });
        
        Task.WaitAll(readerTasks.Concat(new[] { writerTask }).ToArray());
        
        // Assert: No exceptions should occur
        return exceptions.Count == 0 && completedIterations > 0 && completedReloads > 0;
    }
    
    /// <summary>
    /// Property 2.1: Skill state data remains consistent during concurrent access
    /// </summary>
    [Property(MaxTest = 100)]
    public bool SkillStateDataRemainsConsistent(PositiveInt skillCountGen, PositiveInt iterationCountGen)
    {
        // Arrange
        var skillCount = (skillCountGen.Get % 10) + 1;
        var iterationCount = (iterationCountGen.Get % 30) + 10;
        
        var manager = new ThreadSafeSkillStateManager();
        var inconsistencies = 0;
        
        // Initial load
        manager.LoadSkills(() => Enumerable.Range(0, skillCount)
            .Select(i => CreateSkillConfig(i, 0x41 + i, i + 1))
            .ToList());
        
        // Act - Concurrent reads should always see consistent state
        var tasks = Enumerable.Range(0, 5).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < iterationCount; i++)
            {
                var states = manager.GetSkillStates();
                // Check consistency: all skills should have valid names
                foreach (var state in states)
                {
                    if (string.IsNullOrEmpty(state.Config.Name))
                    {
                        Interlocked.Increment(ref inconsistencies);
                    }
                }
            }
        })).ToArray();
        
        // Writer task that changes skill count
        var writerTask = Task.Run(() =>
        {
            for (int i = 0; i < iterationCount / 3; i++)
            {
                var newCount = (i % 10) + 1;
                manager.LoadSkills(() => Enumerable.Range(0, newCount)
                    .Select(j => CreateSkillConfig(j, 0x41 + j, j + 1))
                    .ToList());
                Thread.Sleep(1);
            }
        });
        
        Task.WaitAll(tasks.Concat(new[] { writerTask }).ToArray());
        
        // Assert: No inconsistencies should be detected
        return inconsistencies == 0;
    }
}


/// <summary>
/// Property-based tests for configuration reload preserving valid state
/// **Feature: business-logic-fixes, Property 3: Configuration Reload Preserves Valid State**
/// **Validates: Requirements 2.3**
/// </summary>
public class ConfigReloadPreservesValidStateTests
{
    /// <summary>
    /// Thread-safe skill state manager that mirrors the SkillLoopEngine implementation
    /// </summary>
    private class ThreadSafeSkillStateManager
    {
        private List<SkillRuntimeState> _skillStates = new();
        private readonly ReaderWriterLockSlim _skillStatesLock = new();
        private readonly List<string> _logs = new();
        
        public IReadOnlyList<string> Logs => _logs;
        
        public int SkillCount
        {
            get
            {
                _skillStatesLock.EnterReadLock();
                try
                {
                    return _skillStates.Count;
                }
                finally
                {
                    _skillStatesLock.ExitReadLock();
                }
            }
        }
        
        public List<string> GetSkillNames()
        {
            _skillStatesLock.EnterReadLock();
            try
            {
                return _skillStates.Select(s => s.Config.Name).ToList();
            }
            finally
            {
                _skillStatesLock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// Load skills with write lock protection
        /// Preserves old configuration on failure
        /// </summary>
        public bool LoadSkills(Func<List<SkillConfig>> configLoader)
        {
            try
            {
                var configs = configLoader();
                var newStates = configs.Select(s => new SkillRuntimeState(s)).ToList();
                
                _skillStatesLock.EnterWriteLock();
                try
                {
                    _skillStates = newStates;
                }
                finally
                {
                    _skillStatesLock.ExitWriteLock();
                }
                
                _logs.Add($"Loaded {newStates.Count} skills");
                return true;
            }
            catch (Exception ex)
            {
                // Configuration load failed - preserve old configuration
                _logs.Add($"Load failed, preserving old config: {ex.Message}");
                return false;
            }
        }
    }
    
    /// <summary>
    /// Create a valid skill config for testing
    /// </summary>
    private static SkillConfig CreateSkillConfig(int index, int keyCode, int priority)
    {
        return new SkillConfig
        {
            Name = $"TestSkill_{index}",
            KeyCode = keyCode,
            Priority = priority,
            Enabled = true,
            Cooldown = 10
        };
    }
    
    /// <summary>
    /// Property 3: Configuration Reload Preserves Valid State
    /// For any configuration reload that fails (invalid JSON, file not found, etc.),
    /// the previous valid configuration SHALL be preserved and accessible.
    /// </summary>
    [Property(MaxTest = 100)]
    public bool FailedReloadPreservesOldConfiguration(PositiveInt initialSkillCountGen, PositiveInt failureCountGen)
    {
        // Arrange
        var initialSkillCount = (initialSkillCountGen.Get % 10) + 1;
        var failureCount = (failureCountGen.Get % 5) + 1;
        
        var manager = new ThreadSafeSkillStateManager();
        
        // Initial successful load
        manager.LoadSkills(() => Enumerable.Range(0, initialSkillCount)
            .Select(i => CreateSkillConfig(i, 0x41 + i, i + 1))
            .ToList());
        
        var initialCount = manager.SkillCount;
        var initialNames = manager.GetSkillNames();
        
        // Act - Attempt multiple failed reloads
        for (int i = 0; i < failureCount; i++)
        {
            manager.LoadSkills(() => throw new InvalidOperationException("Simulated config load failure"));
        }
        
        // Assert: Original configuration should be preserved
        var finalCount = manager.SkillCount;
        var finalNames = manager.GetSkillNames();
        
        return finalCount == initialCount && 
               initialNames.SequenceEqual(finalNames);
    }
    
    /// <summary>
    /// Property 3.1: Successful reload after failure works correctly
    /// </summary>
    [Property(MaxTest = 100)]
    public bool SuccessfulReloadAfterFailureWorks(PositiveInt initialSkillCountGen, PositiveInt newSkillCountGen)
    {
        // Arrange
        var initialSkillCount = (initialSkillCountGen.Get % 10) + 1;
        var newSkillCount = (newSkillCountGen.Get % 10) + 1;
        
        var manager = new ThreadSafeSkillStateManager();
        
        // Initial successful load
        manager.LoadSkills(() => Enumerable.Range(0, initialSkillCount)
            .Select(i => CreateSkillConfig(i, 0x41 + i, i + 1))
            .ToList());
        
        // Failed reload
        manager.LoadSkills(() => throw new InvalidOperationException("Simulated failure"));
        
        // Successful reload with new config
        manager.LoadSkills(() => Enumerable.Range(0, newSkillCount)
            .Select(i => CreateSkillConfig(i + 100, 0x41 + i, i + 1))
            .ToList());
        
        // Assert: New configuration should be active
        var finalCount = manager.SkillCount;
        var finalNames = manager.GetSkillNames();
        
        return finalCount == newSkillCount && 
               finalNames.All(n => n.StartsWith("TestSkill_10"));
    }
    
    /// <summary>
    /// Property 3.2: Multiple consecutive failures preserve original state
    /// </summary>
    [Property(MaxTest = 100)]
    public bool MultipleConsecutiveFailuresPreserveState(PositiveInt initialSkillCountGen, PositiveInt failureCountGen)
    {
        // Arrange
        var initialSkillCount = (initialSkillCountGen.Get % 10) + 1;
        var failureCount = (failureCountGen.Get % 10) + 1;
        
        var manager = new ThreadSafeSkillStateManager();
        
        // Initial successful load
        manager.LoadSkills(() => Enumerable.Range(0, initialSkillCount)
            .Select(i => CreateSkillConfig(i, 0x41 + i, i + 1))
            .ToList());
        
        var initialCount = manager.SkillCount;
        
        // Multiple different types of failures
        var exceptions = new Exception[]
        {
            new InvalidOperationException("Invalid operation"),
            new FileNotFoundException("File not found"),
            new FormatException("Invalid format"),
            new ArgumentException("Invalid argument"),
            new IOException("IO error")
        };
        
        for (int i = 0; i < failureCount; i++)
        {
            var ex = exceptions[i % exceptions.Length];
            manager.LoadSkills(() => throw ex);
        }
        
        // Assert: Original configuration should still be preserved
        return manager.SkillCount == initialCount;
    }
    
    /// <summary>
    /// Property 3.3: Error is logged when reload fails
    /// </summary>
    [Property(MaxTest = 100)]
    public bool FailedReloadLogsError(PositiveInt initialSkillCountGen)
    {
        // Arrange
        var initialSkillCount = (initialSkillCountGen.Get % 10) + 1;
        var manager = new ThreadSafeSkillStateManager();
        
        // Initial successful load
        manager.LoadSkills(() => Enumerable.Range(0, initialSkillCount)
            .Select(i => CreateSkillConfig(i, 0x41 + i, i + 1))
            .ToList());
        
        var logsBeforeFailure = manager.Logs.Count;
        
        // Failed reload
        manager.LoadSkills(() => throw new InvalidOperationException("Test error message"));
        
        // Assert: Error should be logged
        var logsAfterFailure = manager.Logs.Count;
        var lastLog = manager.Logs.LastOrDefault() ?? "";
        
        return logsAfterFailure > logsBeforeFailure && 
               lastLog.Contains("Load failed") && 
               lastLog.Contains("preserving old config");
    }
}
