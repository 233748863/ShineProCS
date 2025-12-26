using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ShineProCS.Core.Services;

/// <summary>
/// 全局快捷键服务
/// 支持在任何窗口（包括游戏）中响应快捷键
/// </summary>
public class GlobalHotkeyService : IDisposable
{
    #region Win32 API
    
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    
    private const int WM_HOTKEY = 0x0312;
    
    #endregion
    
    #region 修饰键常量
    
    public const uint MOD_NONE = 0x0000;
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CTRL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    
    #endregion
    
    private readonly Dictionary<int, Action> _hotkeyActions = [];
    private readonly Dictionary<string, int> _hotkeyIds = [];
    private IntPtr _windowHandle;
    private HwndSource? _source;
    private int _currentId = 9000;
    private bool _disposed;

    /// <summary>
    /// 快捷键触发事件
    /// </summary>
    public event Action<string>? HotkeyTriggered;

    /// <summary>
    /// 初始化快捷键服务（需要在窗口加载后调用）
    /// </summary>
    public void Initialize(Window window)
    {
        _windowHandle = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(HwndHook);
    }

    /// <summary>
    /// 注册全局快捷键
    /// </summary>
    /// <param name="name">快捷键名称（用于标识）</param>
    /// <param name="modifiers">修饰键（MOD_CTRL, MOD_ALT 等）</param>
    /// <param name="key">按键码</param>
    /// <param name="action">触发时执行的动作</param>
    /// <returns>是否注册成功</returns>
    public bool RegisterHotkey(string name, uint modifiers, uint key, Action action)
    {
        if (_windowHandle == IntPtr.Zero) return false;
        
        // 如果已存在，先注销
        if (_hotkeyIds.TryGetValue(name, out var existingId))
        {
            UnregisterHotKey(_windowHandle, existingId);
            _hotkeyActions.Remove(existingId);
            _hotkeyIds.Remove(name);
        }
        
        var id = _currentId++;
        if (RegisterHotKey(_windowHandle, id, modifiers, key))
        {
            _hotkeyActions[id] = action;
            _hotkeyIds[name] = id;
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// 注销指定快捷键
    /// </summary>
    public bool UnregisterHotkey(string name)
    {
        if (!_hotkeyIds.TryGetValue(name, out var id)) return false;
        
        var result = UnregisterHotKey(_windowHandle, id);
        _hotkeyActions.Remove(id);
        _hotkeyIds.Remove(name);
        return result;
    }

    /// <summary>
    /// 注销所有快捷键
    /// </summary>
    public void UnregisterAll()
    {
        foreach (var id in _hotkeyIds.Values)
        {
            UnregisterHotKey(_windowHandle, id);
        }
        _hotkeyActions.Clear();
        _hotkeyIds.Clear();
    }

    /// <summary>
    /// 获取已注册的快捷键列表
    /// </summary>
    public IReadOnlyCollection<string> GetRegisteredHotkeys() => _hotkeyIds.Keys;

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_hotkeyActions.TryGetValue(id, out var action))
            {
                action.Invoke();
                
                // 查找名称并触发事件
                var name = _hotkeyIds.FirstOrDefault(x => x.Value == id).Key;
                if (!string.IsNullOrEmpty(name))
                    HotkeyTriggered?.Invoke(name);
                
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// 将修饰键和按键转换为显示文本
    /// </summary>
    public static string GetHotkeyDisplayText(uint modifiers, uint key)
    {
        var parts = new List<string>();
        
        if ((modifiers & MOD_CTRL) != 0) parts.Add("Ctrl");
        if ((modifiers & MOD_ALT) != 0) parts.Add("Alt");
        if ((modifiers & MOD_SHIFT) != 0) parts.Add("Shift");
        if ((modifiers & MOD_WIN) != 0) parts.Add("Win");
        
        parts.Add(Utils.KeyCodeHelper.GetKeyName((int)key));
        
        return string.Join("+", parts);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        UnregisterAll();
        _source?.RemoveHook(HwndHook);
        
        GC.SuppressFinalize(this);
    }
}
