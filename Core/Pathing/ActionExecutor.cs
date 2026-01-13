using ShineProCS.Core.Interfaces;
using ShineProCS.Core.GameTask.Triggers;

namespace ShineProCS.Core.Pathing;

/// <summary>
/// 动作执行结果
/// </summary>
public class ActionResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 执行时间（毫秒）
    /// </summary>
    public long ExecutionTimeMs { get; set; }
    
    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static ActionResult Ok(long executionTimeMs = 0) => new()
    {
        Success = true,
        ExecutionTimeMs = executionTimeMs
    };
    
    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static ActionResult Fail(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };
}


/// <summary>
/// 动作执行器
/// 需求: 20.4 - 支持在路径点执行自定义动作
/// </summary>
public class ActionExecutor : IDisposable
{
    #region 虚拟键码
    
    private const int VK_F = 0x46;
    private const int VK_E = 0x45;
    private const int VK_SPACE = 0x20;
    private const int VK_LSHIFT = 0xA0;
    private const int VK_ESCAPE = 0x1B;
    
    #endregion
    
    #region 默认延迟
    
    private const int DefaultCollectDelayMs = 500;
    private const int DefaultInteractDelayMs = 1000;
    private const int DefaultCombatDurationMs = 10000;
    private const int DefaultWaitTimeMs = 1000;
    private const int DefaultJumpDelayMs = 500;
    private const int DefaultSprintDurationMs = 1000;
    private const int DefaultKeyPressDelayMs = 200;
    
    #endregion
    
    #region 依赖组件
    
    private readonly IInputService _inputService;
    private readonly ILogService _logService;
    private readonly SkillLoopTrigger? _skillLoopTrigger;
    
    #endregion
    
    #region 状态
    
    private bool _disposed;
    
    #endregion
    
    #region 事件
    
    /// <summary>
    /// 动作开始事件
    /// </summary>
    public event Action<PathPoint, PathPointAction>? ActionStarted;
    
    /// <summary>
    /// 动作完成事件
    /// </summary>
    public event Action<PathPoint, PathPointAction, ActionResult>? ActionCompleted;
    
    #endregion
    
    #region 构造函数
    
    /// <summary>
    /// 创建动作执行器
    /// </summary>
    public ActionExecutor(
        IInputService inputService,
        ILogService logService,
        SkillLoopTrigger? skillLoopTrigger = null)
    {
        _inputService = inputService ?? throw new ArgumentNullException(nameof(inputService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _skillLoopTrigger = skillLoopTrigger;
    }
    
    #endregion
    
    #region 动作执行
    
    /// <summary>
    /// 执行路径点动作
    /// 需求: 20.4 - 支持在路径点执行自定义动作
    /// </summary>
    public async Task<ActionResult> ExecuteActionAsync(PathPoint point, CancellationToken ct)
    {
        if (point.Action == PathPointAction.None)
        {
            return ActionResult.Ok();
        }
        
        var startTime = DateTime.Now;
        ActionStarted?.Invoke(point, point.Action);
        Log($"执行动作: {point.Action} at {point}", 0);
        
        try
        {
            var result = point.Action switch
            {
                PathPointAction.Collect => await ExecuteCollectAsync(point, ct),
                PathPointAction.Combat => await ExecuteCombatAsync(point, ct),
                PathPointAction.Interact => await ExecuteInteractAsync(point, ct),
                PathPointAction.Wait => await ExecuteWaitAsync(point, ct),
                PathPointAction.Jump => await ExecuteJumpAsync(point, ct),
                PathPointAction.Sprint => await ExecuteSprintAsync(point, ct),
                PathPointAction.Teleport => await ExecuteTeleportAsync(point, ct),
                PathPointAction.CustomKey => await ExecuteCustomKeyAsync(point, ct),
                _ => ActionResult.Ok()
            };
            
            result.ExecutionTimeMs = (long)(DateTime.Now - startTime).TotalMilliseconds;
            ActionCompleted?.Invoke(point, point.Action, result);
            
            return result;
        }
        catch (OperationCanceledException)
        {
            return ActionResult.Fail("动作被取消");
        }
        catch (Exception ex)
        {
            Log($"动作执行异常: {ex.Message}", 3);
            return ActionResult.Fail(ex.Message);
        }
    }


    /// <summary>
    /// 执行采集动作
    /// 需求: 20.4 - 支持采集动作
    /// </summary>
    private async Task<ActionResult> ExecuteCollectAsync(PathPoint point, CancellationToken ct)
    {
        var keyboard = _inputService.Keyboard;
        var delay = ParseActionParam(point.ActionParam, DefaultCollectDelayMs);
        
        // 按 F 键采集
        keyboard.PressAndRelease(VK_F);
        await Task.Delay(delay, ct);
        
        Log($"采集完成: {point}", 0);
        return ActionResult.Ok();
    }
    
    /// <summary>
    /// 执行战斗动作
    /// 需求: 20.4 - 支持战斗动作
    /// </summary>
    private async Task<ActionResult> ExecuteCombatAsync(PathPoint point, CancellationToken ct)
    {
        var duration = ParseActionParam(point.ActionParam, DefaultCombatDurationMs);
        
        // 启动技能循环
        if (_skillLoopTrigger != null)
        {
            _skillLoopTrigger.IsEnabled = true;
            _skillLoopTrigger.Start();
            Log("技能循环已启动", 0);
        }
        else
        {
            Log("技能循环触发器不可用，使用简单战斗", 2);
        }
        
        try
        {
            // 等待战斗时间
            await Task.Delay(duration, ct);
            
            Log($"战斗完成: {point}", 0);
            return ActionResult.Ok();
        }
        finally
        {
            // 停止技能循环
            if (_skillLoopTrigger != null)
            {
                _skillLoopTrigger.Stop();
                Log("技能循环已停止", 0);
            }
        }
    }
    
    /// <summary>
    /// 执行交互动作
    /// </summary>
    private async Task<ActionResult> ExecuteInteractAsync(PathPoint point, CancellationToken ct)
    {
        var keyboard = _inputService.Keyboard;
        var delay = ParseActionParam(point.ActionParam, DefaultInteractDelayMs);
        
        // 按 F 键交互
        keyboard.PressAndRelease(VK_F);
        await Task.Delay(delay, ct);
        
        Log($"交互完成: {point}", 0);
        return ActionResult.Ok();
    }
    
    /// <summary>
    /// 执行等待动作
    /// </summary>
    private async Task<ActionResult> ExecuteWaitAsync(PathPoint point, CancellationToken ct)
    {
        var waitTime = ParseActionParam(point.ActionParam, DefaultWaitTimeMs);
        
        Log($"等待 {waitTime}ms", 0);
        await Task.Delay(waitTime, ct);
        
        return ActionResult.Ok();
    }
    
    /// <summary>
    /// 执行跳跃动作
    /// </summary>
    private async Task<ActionResult> ExecuteJumpAsync(PathPoint point, CancellationToken ct)
    {
        var keyboard = _inputService.Keyboard;
        var delay = ParseActionParam(point.ActionParam, DefaultJumpDelayMs);
        
        keyboard.PressAndRelease(VK_SPACE);
        await Task.Delay(delay, ct);
        
        Log($"跳跃完成: {point}", 0);
        return ActionResult.Ok();
    }
    
    /// <summary>
    /// 执行冲刺动作
    /// </summary>
    private async Task<ActionResult> ExecuteSprintAsync(PathPoint point, CancellationToken ct)
    {
        var keyboard = _inputService.Keyboard;
        var duration = ParseActionParam(point.ActionParam, DefaultSprintDurationMs);
        
        keyboard.PressKey(VK_LSHIFT);
        await Task.Delay(duration, ct);
        keyboard.ReleaseKey(VK_LSHIFT);
        
        Log($"冲刺完成: {point}", 0);
        return ActionResult.Ok();
    }
    
    /// <summary>
    /// 执行传送动作
    /// </summary>
    private async Task<ActionResult> ExecuteTeleportAsync(PathPoint point, CancellationToken ct)
    {
        // 传送需要打开地图并选择传送点
        // 这里只是一个简化实现
        Log("传送动作需要手动实现具体逻辑", 2);
        
        var delay = ParseActionParam(point.ActionParam, 5000);
        await Task.Delay(delay, ct);
        
        return ActionResult.Ok();
    }
    
    /// <summary>
    /// 执行自定义按键动作
    /// </summary>
    private async Task<ActionResult> ExecuteCustomKeyAsync(PathPoint point, CancellationToken ct)
    {
        var keyboard = _inputService.Keyboard;
        
        // 解析参数：格式为 "keyCode" 或 "keyCode,delay"
        var parts = point.ActionParam.Split(',');
        
        if (parts.Length == 0 || !int.TryParse(parts[0].Trim(), out var keyCode))
        {
            return ActionResult.Fail("无效的按键码");
        }
        
        var delay = parts.Length > 1 && int.TryParse(parts[1].Trim(), out var d) 
            ? d 
            : DefaultKeyPressDelayMs;
        
        keyboard.PressAndRelease(keyCode);
        await Task.Delay(delay, ct);
        
        Log($"自定义按键 {keyCode} 完成: {point}", 0);
        return ActionResult.Ok();
    }
    
    #endregion


    #region 辅助方法
    
    /// <summary>
    /// 解析动作参数
    /// </summary>
    private static int ParseActionParam(string param, int defaultValue)
    {
        if (string.IsNullOrEmpty(param))
            return defaultValue;
        
        // 支持 "value" 或 "value,..." 格式
        var parts = param.Split(',');
        if (parts.Length > 0 && int.TryParse(parts[0].Trim(), out var value))
            return value;
        
        return defaultValue;
    }
    
    #endregion
    
    #region 日志方法
    
    private void Log(string message, int level)
    {
        var logLevel = level switch
        {
            0 => Interfaces.LogLevel.Debug,
            1 => Interfaces.LogLevel.Info,
            2 => Interfaces.LogLevel.Warning,
            3 => Interfaces.LogLevel.Error,
            _ => Interfaces.LogLevel.Info
        };
        
        _logService.Log($"[动作执行] {message}", logLevel, "ActionExecutor");
    }
    
    #endregion
    
    #region IDisposable 实现
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
    
    #endregion
}
