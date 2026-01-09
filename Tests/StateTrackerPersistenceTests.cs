using FsCheck;
using FsCheck.Xunit;
using ShineProCS.Core.Services;

namespace ShineProCS.Tests;

/// <summary>
/// Property-based tests for StateTracker Persistence
/// **Feature: skill-logic-compatibility, Property 6: 状态追踪持久性**
/// **Validates: Requirements 6.1, 6.2, 6.3, 6.4, 6.5**
/// </summary>
public class StateTrackerPersistenceTests
{
    /// <summary>
    /// Property 6.1: SetState sets state to true
    /// WHEN SetState is called with true, GetState SHALL return true.
    /// **Validates: Requirements 6.1, 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool SetStateSetsStateToTrue(NonEmptyString stateNameGen)
    {
        var stateName = stateNameGen.Get;
        var tracker = new StateTracker();
        
        tracker.SetState(stateName, true);
        
        // Assert: 状态应该为true
        return tracker.GetState(stateName) == true;
    }
    
    /// <summary>
    /// Property 6.2: SetState sets state to false
    /// WHEN SetState is called with false, GetState SHALL return false.
    /// **Validates: Requirements 6.1, 6.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool SetStateSetsStateToFalse(NonEmptyString stateNameGen)
    {
        var stateName = stateNameGen.Get;
        var tracker = new StateTracker();
        
        // 先设置为true，再设置为false
        tracker.SetState(stateName, true);
        tracker.SetState(stateName, false);
        
        // Assert: 状态应该为false
        return tracker.GetState(stateName) == false;
    }
    
    /// <summary>
    /// Property 6.3: ClearState removes state
    /// WHEN ClearState is called, GetState SHALL return false.
    /// **Validates: Requirements 6.1, 6.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool ClearStateRemovesState(NonEmptyString stateNameGen)
    {
        var stateName = stateNameGen.Get;
        var tracker = new StateTracker();
        
        // 设置状态为true，然后清除
        tracker.SetState(stateName, true);
        tracker.ClearState(stateName);
        
        // Assert: 状态应该为false（不存在）
        return tracker.GetState(stateName) == false;
    }
    
    /// <summary>
    /// Property 6.4: State persists across multiple operations
    /// WHEN state is set, it SHALL persist until explicitly modified.
    /// **Validates: Requirements 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool StatePersistsAcrossMultipleOperations(NonEmptyString stateNameGen, PositiveInt operationCountGen)
    {
        var stateName = stateNameGen.Get;
        var operationCount = (operationCountGen.Get % 10) + 1; // 1-10次操作
        var tracker = new StateTracker();
        
        // 设置状态为true
        tracker.SetState(stateName, true);
        
        // 执行多次其他操作（设置其他状态）
        for (int i = 0; i < operationCount; i++)
        {
            tracker.SetState($"other_state_{i}", true);
        }
        
        // Assert: 原始状态应该仍然为true
        return tracker.GetState(stateName) == true;
    }
    
    /// <summary>
    /// Property 6.5: ClearAll removes all states
    /// WHEN ClearAll is called, all states SHALL be removed.
    /// **Validates: Requirements 6.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool ClearAllRemovesAllStates(PositiveInt stateCountGen)
    {
        var stateCount = (stateCountGen.Get % 10) + 1; // 1-10个状态
        var tracker = new StateTracker();
        
        // 设置多个状态
        var stateNames = new List<string>();
        for (int i = 0; i < stateCount; i++)
        {
            var name = $"state_{i}";
            stateNames.Add(name);
            tracker.SetState(name, true);
        }
        
        // 清除所有状态
        tracker.ClearAll();
        
        // Assert: 所有状态都应该为false
        return stateNames.All(name => tracker.GetState(name) == false) && tracker.Count == 0;
    }
    
    /// <summary>
    /// Property 6.6: GetState returns false for non-existent state
    /// WHEN GetState is called for a non-existent state, it SHALL return false.
    /// **Validates: Requirements 6.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool GetStateReturnsFalseForNonExistentState(NonEmptyString stateNameGen)
    {
        var stateName = stateNameGen.Get;
        var tracker = new StateTracker();
        
        // Assert: 不存在的状态应该返回false
        return tracker.GetState(stateName) == false;
    }
    
    /// <summary>
    /// Property 6.7: Empty or null state name returns false
    /// WHEN GetState is called with empty or null name, it SHALL return false.
    /// **Validates: Requirements 6.1**
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GetStateReturnsFalseForEmptyOrNullName(string? stateName)
    {
        var tracker = new StateTracker();
        
        // Assert: 空或null名称应该返回false
        Assert.False(tracker.GetState(stateName!));
    }
    
    /// <summary>
    /// Property 6.8: SetState ignores empty or null name
    /// WHEN SetState is called with empty or null name, it SHALL be ignored.
    /// **Validates: Requirements 6.1**
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SetStateIgnoresEmptyOrNullName(string? stateName)
    {
        var tracker = new StateTracker();
        
        tracker.SetState(stateName!, true);
        
        // Assert: 状态数量应该为0
        Assert.Equal(0, tracker.Count);
    }
    
    /// <summary>
    /// Property 6.9: ClearState ignores empty or null name
    /// WHEN ClearState is called with empty or null name, it SHALL be ignored.
    /// **Validates: Requirements 6.1**
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ClearStateIgnoresEmptyOrNullName(string? stateName)
    {
        var tracker = new StateTracker();
        tracker.SetState("valid_state", true);
        
        // 尝试清除空名称的状态
        tracker.ClearState(stateName!);
        
        // Assert: 有效状态应该仍然存在
        Assert.True(tracker.GetState("valid_state"));
        Assert.Equal(1, tracker.Count);
    }
    
    /// <summary>
    /// Property 6.10: StateChanged event fires on state change
    /// WHEN state value changes, StateChanged event SHALL fire.
    /// **Validates: Requirements 6.2, 6.3**
    /// </summary>
    [Fact]
    public void StateChangedEventFiresOnStateChange()
    {
        var tracker = new StateTracker();
        var eventFired = false;
        string? eventStateName = null;
        bool? eventValue = null;
        
        tracker.StateChanged += (name, value) =>
        {
            eventFired = true;
            eventStateName = name;
            eventValue = value;
        };
        
        tracker.SetState("test_state", true);
        
        // Assert: 事件应该触发
        Assert.True(eventFired);
        Assert.Equal("test_state", eventStateName);
        Assert.True(eventValue);
    }
    
    /// <summary>
    /// Property 6.11: StateChanged event fires on ClearState
    /// WHEN ClearState is called on an existing true state, StateChanged event SHALL fire with false.
    /// **Validates: Requirements 6.3**
    /// </summary>
    [Fact]
    public void StateChangedEventFiresOnClearState()
    {
        var tracker = new StateTracker();
        tracker.SetState("test_state", true);
        
        var eventFired = false;
        bool? eventValue = null;
        
        tracker.StateChanged += (name, value) =>
        {
            eventFired = true;
            eventValue = value;
        };
        
        tracker.ClearState("test_state");
        
        // Assert: 事件应该触发，值为false
        Assert.True(eventFired);
        Assert.False(eventValue);
    }
    
    /// <summary>
    /// Property 6.12: HasState returns true for existing state
    /// WHEN HasState is called for an existing state, it SHALL return true.
    /// **Validates: Requirements 6.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool HasStateReturnsTrueForExistingState(NonEmptyString stateNameGen, bool stateValue)
    {
        var stateName = stateNameGen.Get;
        var tracker = new StateTracker();
        
        tracker.SetState(stateName, stateValue);
        
        // Assert: HasState应该返回true
        return tracker.HasState(stateName) == true;
    }
    
    /// <summary>
    /// Property 6.13: HasState returns false for non-existing state
    /// WHEN HasState is called for a non-existing state, it SHALL return false.
    /// **Validates: Requirements 6.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool HasStateReturnsFalseForNonExistingState(NonEmptyString stateNameGen)
    {
        var stateName = stateNameGen.Get;
        var tracker = new StateTracker();
        
        // Assert: HasState应该返回false
        return tracker.HasState(stateName) == false;
    }
    
    /// <summary>
    /// Property 6.14: GetAllStates returns correct snapshot
    /// WHEN GetAllStates is called, it SHALL return a snapshot of all current states.
    /// **Validates: Requirements 6.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool GetAllStatesReturnsCorrectSnapshot(PositiveInt stateCountGen)
    {
        var stateCount = (stateCountGen.Get % 5) + 1; // 1-5个状态
        var tracker = new StateTracker();
        
        // 设置多个状态
        var expectedStates = new Dictionary<string, bool>();
        for (int i = 0; i < stateCount; i++)
        {
            var name = $"state_{i}";
            var value = i % 2 == 0; // 交替true/false
            expectedStates[name] = value;
            tracker.SetState(name, value);
        }
        
        var actualStates = tracker.GetAllStates();
        
        // Assert: 返回的状态应该与设置的一致
        if (actualStates.Count != expectedStates.Count)
            return false;
        
        foreach (var kvp in expectedStates)
        {
            if (!actualStates.TryGetValue(kvp.Key, out var actualValue) || actualValue != kvp.Value)
                return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Property 6.15: Count reflects number of tracked states
    /// The Count property SHALL reflect the number of states being tracked.
    /// **Validates: Requirements 6.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public bool CountReflectsNumberOfTrackedStates(PositiveInt stateCountGen)
    {
        var stateCount = (stateCountGen.Get % 10) + 1; // 1-10个状态
        var tracker = new StateTracker();
        
        // 设置多个状态
        for (int i = 0; i < stateCount; i++)
        {
            tracker.SetState($"state_{i}", true);
        }
        
        // Assert: Count应该等于设置的状态数量
        return tracker.Count == stateCount;
    }
}
