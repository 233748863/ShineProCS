using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Services;
using ShineProCS.Models;

namespace ShineProCS.Core.Engine;

/// <summary>
/// 技能循环触发器
/// 将 SkillLoopEngine 适配为 ITaskTrigger 接口，集成到新的任务系统中
/// 需求: 8.1, 8.2 - 任务触发器接口
/// 需求: 14.6 - 技能引擎作为 ITaskTrigger 实现
/// </summary>
public class SkillLoopTrigger : ITaskTrigger
{
    private readonly SkillLoopEngine _engine;
    private readonly ConfigManager _configManager;
    private bool _isEnabled = true;
    
    /// <summary>
    /// 触发器名称
    /// </summary>
    public string Name => "技能循环";
    
    /// <summary>
    /// 触发器优先级（数值越大越先执行）
    /// 技能循环是核心功能，设置较高优先级
    /// </summary>
    public int Priority => 100;
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            
            if (!_isEnabled && _engine.GetStatus().IsRunning)
            {
                _engine.Stop();
            }
        }
    }
    
    /// <summary>
    /// 是否处于独占模式
    /// 技能循环不需要独占，可以与其他触发器并行
    /// </summary>
    public bool IsExclusive => false;
    
    /// <summary>
    /// 状态变化事件
    /// </summary>
    public event Action<EngineStatus>? StatusChanged;
    
    /// <summary>
    /// 日志消息事件
    /// </summary>
    public event Action<string, int>? LogMessage;
    
    /// <summary>
    /// 创建技能循环触发器
    /// </summary>
    /// <param name="keyboard">键盘接口</param>
    /// <param name="image">图像接口</param>
    /// <param name="configManager">配置管理器</param>
    public SkillLoopTrigger(IKeyboardInterface keyboard, IImageInterface image, ConfigManager configManager)
    {
        _configManager = configManager;
        _engine = new SkillLoopEngine(keyboard, image, configManager);
        
        // 转发引擎事件
        _engine.StatusChanged += status => StatusChanged?.Invoke(status);
        _engine.LogMessage += (msg, level) => LogMessage?.Invoke(msg, level);
    }
    
    /// <summary>
    /// 初始化触发器
    /// 在触发器启用时调用
    /// </summary>
    public void Init()
    {
        // 引擎在构造时已初始化，此处可进行额外配置
    }
    
    /// <summary>
    /// 捕获图像后的处理
    /// 注意：技能循环引擎有自己的截图和主循环，此方法仅用于外部触发
    /// 实际的技能循环逻辑由引擎内部控制
    /// </summary>
    /// <param name="content">捕获的内容</param>
    public void OnCapture(CaptureContent content)
    {
        // 技能循环引擎有自己的截图循环，不依赖外部捕获
        // 此方法保留用于未来可能的集成需求
    }
    
    /// <summary>
    /// 启动技能循环
    /// </summary>
    public void Start()
    {
        if (!_isEnabled) return;
        _engine.Start();
    }
    
    /// <summary>
    /// 停止技能循环
    /// </summary>
    public void Stop()
    {
        _engine.Stop();
    }
    
    /// <summary>
    /// 切换暂停状态
    /// </summary>
    public void TogglePause()
    {
        _engine.TogglePause();
    }
    
    /// <summary>
    /// 获取当前状态
    /// </summary>
    public EngineStatus GetStatus()
    {
        return _engine.GetStatus();
    }
    
    /// <summary>
    /// 更新键盘接口（用于运行时切换输入驱动）
    /// </summary>
    /// <param name="newKeyboard">新的键盘接口实例</param>
    public void UpdateKeyboardInterface(IKeyboardInterface newKeyboard)
    {
        _engine.UpdateKeyboardInterface(newKeyboard);
    }
    
    /// <summary>
    /// 处理设备断开事件
    /// </summary>
    public void OnDeviceDisconnected()
    {
        _engine.OnDeviceDisconnected();
    }
    
    /// <summary>
    /// 处理设备重连事件
    /// </summary>
    public void OnDeviceReconnected()
    {
        _engine.OnDeviceReconnected();
    }
    
    /// <summary>
    /// 获取冷却追踪器
    /// </summary>
    public SkillCooldownTracker CooldownTracker => _engine.CooldownTracker;
    
    /// <summary>
    /// 获取内部引擎实例（用于兼容旧代码）
    /// </summary>
    internal SkillLoopEngine GetEngine() => _engine;
}
