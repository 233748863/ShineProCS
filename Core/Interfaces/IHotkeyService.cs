using System.Windows;

namespace ShineProCS.Core.Interfaces;

/// <summary>
/// 热键配置信息
/// </summary>
public class HotkeyConfig
{
    /// <summary>
    /// 热键名称（用于标识）
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 修饰键（Ctrl=2, Alt=1, Shift=4, Win=8）
    /// </summary>
    public uint Modifiers { get; set; }
    
    /// <summary>
    /// 按键码
    /// </summary>
    public uint Key { get; set; }
    
    /// <summary>
    /// 触发时执行的动作
    /// </summary>
    public Action? Action { get; set; }
}

/// <summary>
/// 热键冲突信息
/// </summary>
public class HotkeyConflict
{
    /// <summary>
    /// 冲突的热键名称
    /// </summary>
    public string ConflictingHotkeyName { get; set; } = string.Empty;
    
    /// <summary>
    /// 冲突描述
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// 是否为系统热键冲突
    /// </summary>
    public bool IsSystemConflict { get; set; }
}

/// <summary>
/// 全局热键服务接口
/// 支持在任何窗口（包括游戏）中响应快捷键
/// </summary>
public interface IHotkeyService : IDisposable
{
    /// <summary>
    /// 快捷键触发事件
    /// </summary>
    event Action<string>? HotkeyTriggered;
    
    /// <summary>
    /// 初始化快捷键服务（需要在窗口加载后调用）
    /// </summary>
    /// <param name="window">主窗口</param>
    void Initialize(Window window);
    
    /// <summary>
    /// 注册全局快捷键
    /// </summary>
    /// <param name="name">快捷键名称（用于标识）</param>
    /// <param name="modifiers">修饰键（MOD_CTRL, MOD_ALT 等）</param>
    /// <param name="key">按键码</param>
    /// <param name="action">触发时执行的动作</param>
    /// <returns>是否注册成功</returns>
    bool RegisterHotkey(string name, uint modifiers, uint key, Action action);
    
    /// <summary>
    /// 注销指定快捷键
    /// </summary>
    /// <param name="name">快捷键名称</param>
    /// <returns>是否注销成功</returns>
    bool UnregisterHotkey(string name);
    
    /// <summary>
    /// 注销所有快捷键
    /// </summary>
    void UnregisterAll();
    
    /// <summary>
    /// 获取已注册的快捷键列表
    /// </summary>
    /// <returns>已注册的热键名称集合</returns>
    IReadOnlyCollection<string> GetRegisteredHotkeys();
    
    /// <summary>
    /// 检测热键冲突
    /// </summary>
    /// <param name="modifiers">修饰键</param>
    /// <param name="key">按键码</param>
    /// <param name="excludeName">排除的热键名称（用于编辑时排除自身）</param>
    /// <returns>冲突信息，如果没有冲突则返回null</returns>
    HotkeyConflict? CheckConflict(uint modifiers, uint key, string? excludeName = null);
    
    /// <summary>
    /// 获取热键显示文本
    /// </summary>
    /// <param name="modifiers">修饰键</param>
    /// <param name="key">按键码</param>
    /// <returns>格式化的热键文本（如 "Ctrl+F7"）</returns>
    string GetHotkeyDisplayText(uint modifiers, uint key);
    
    /// <summary>
    /// 检查服务是否已初始化
    /// </summary>
    bool IsInitialized { get; }
}
