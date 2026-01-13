using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ShineProCS.Core.Interfaces;

namespace ShineProCS.Core.Services;

/// <summary>
/// 全局快捷键服务
/// 支持在任何窗口（包括游戏）中响应快捷键
/// </summary>
public class GlobalHotkeyService : IHotkeyService
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
    private readonly Dictionary<string, (uint Modifiers, uint Key)> _hotkeyConfigs = [];
    private IntPtr _windowHandle;
    private HwndSource? _source;
    private int _currentId = 9000;
    private bool _disposed;

    /// <inheritdoc />
    public event Action<string>? HotkeyTriggered;
    
    /// <inheritdoc />
    public bool IsInitialized => _windowHandle != IntPtr.Zero;

    /// <inheritdoc />
    public void Initialize(Window window)
    {
        _windowHandle = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(HwndHook);
    }

    /// <inheritdoc />
    public bool RegisterHotkey(string name, uint modifiers, uint key, Action action)
    {
        if (_windowHandle == IntPtr.Zero) return false;
        
        // 如果已存在，先注销
        if (_hotkeyIds.TryGetValue(name, out var existingId))
        {
            UnregisterHotKey(_windowHandle, existingId);
            _hotkeyActions.Remove(existingId);
            _hotkeyIds.Remove(name);
            _hotkeyConfigs.Remove(name);
        }
        
        var id = _currentId++;
        if (RegisterHotKey(_windowHandle, id, modifiers, key))
        {
            _hotkeyActions[id] = action;
            _hotkeyIds[name] = id;
            _hotkeyConfigs[name] = (modifiers, key);
            return true;
        }
        
        return false;
    }

    /// <inheritdoc />
    public bool UnregisterHotkey(string name)
    {
        if (!_hotkeyIds.TryGetValue(name, out var id)) return false;
        
        var result = UnregisterHotKey(_windowHandle, id);
        _hotkeyActions.Remove(id);
        _hotkeyIds.Remove(name);
        _hotkeyConfigs.Remove(name);
        return result;
    }

    /// <inheritdoc />
    public void UnregisterAll()
    {
        foreach (var id in _hotkeyIds.Values)
        {
            UnregisterHotKey(_windowHandle, id);
        }
        _hotkeyActions.Clear();
        _hotkeyIds.Clear();
        _hotkeyConfigs.Clear();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetRegisteredHotkeys() => _hotkeyIds.Keys;
    
    /// <inheritdoc />
    public HotkeyConflict? CheckConflict(uint modifiers, uint key, string? excludeName = null)
    {
        // 检查与已注册热键的冲突
        foreach (var (name, config) in _hotkeyConfigs)
        {
            // 排除自身（用于编辑时）
            if (excludeName != null && name == excludeName)
                continue;
                
            if (config.Modifiers == modifiers && config.Key == key)
            {
                return new HotkeyConflict
                {
                    ConflictingHotkeyName = name,
                    Description = $"与已注册的热键 \"{name}\" ({GetHotkeyDisplayText(modifiers, key)}) 冲突",
                    IsSystemConflict = false
                };
            }
        }
        
        // 检查常见系统热键冲突
        var systemConflict = CheckSystemHotkeyConflict(modifiers, key);
        if (systemConflict != null)
        {
            return systemConflict;
        }
        
        return null;
    }
    
    /// <summary>
    /// 检查是否与常见系统热键冲突
    /// </summary>
    private HotkeyConflict? CheckSystemHotkeyConflict(uint modifiers, uint key)
    {
        // 常见系统热键列表
        var systemHotkeys = new List<(uint Mod, uint Key, string Desc)>
        {
            (MOD_CTRL, 0x43, "复制 (Ctrl+C)"),           // Ctrl+C
            (MOD_CTRL, 0x56, "粘贴 (Ctrl+V)"),           // Ctrl+V
            (MOD_CTRL, 0x58, "剪切 (Ctrl+X)"),           // Ctrl+X
            (MOD_CTRL, 0x5A, "撤销 (Ctrl+Z)"),           // Ctrl+Z
            (MOD_CTRL, 0x53, "保存 (Ctrl+S)"),           // Ctrl+S
            (MOD_CTRL, 0x41, "全选 (Ctrl+A)"),           // Ctrl+A
            (MOD_ALT, 0x73, "关闭窗口 (Alt+F4)"),        // Alt+F4
            (MOD_ALT, 0x09, "切换窗口 (Alt+Tab)"),       // Alt+Tab
            (MOD_WIN, 0x44, "显示桌面 (Win+D)"),         // Win+D
            (MOD_WIN, 0x45, "文件资源管理器 (Win+E)"),   // Win+E
            (MOD_WIN, 0x4C, "锁定 (Win+L)"),             // Win+L
            (MOD_WIN, 0x52, "运行 (Win+R)"),             // Win+R
            (MOD_CTRL | MOD_SHIFT, 0x1B, "任务管理器 (Ctrl+Shift+Esc)"), // Ctrl+Shift+Esc
        };
        
        foreach (var (mod, k, desc) in systemHotkeys)
        {
            if (modifiers == mod && key == k)
            {
                return new HotkeyConflict
                {
                    ConflictingHotkeyName = "系统热键",
                    Description = $"与系统热键冲突: {desc}",
                    IsSystemConflict = true
                };
            }
        }
        
        return null;
    }

    /// <inheritdoc />
    public string GetHotkeyDisplayText(uint modifiers, uint key)
    {
        return GetHotkeyDisplayTextStatic(modifiers, key);
    }
    
    /// <summary>
    /// 将修饰键和按键转换为显示文本（静态方法，保持向后兼容）
    /// </summary>
    public static string GetHotkeyDisplayTextStatic(uint modifiers, uint key)
    {
        var parts = new List<string>();
        
        if ((modifiers & MOD_CTRL) != 0) parts.Add("Ctrl");
        if ((modifiers & MOD_ALT) != 0) parts.Add("Alt");
        if ((modifiers & MOD_SHIFT) != 0) parts.Add("Shift");
        if ((modifiers & MOD_WIN) != 0) parts.Add("Win");
        
        parts.Add(Utils.KeyCodeHelper.GetKeyName((int)key));
        
        return string.Join("+", parts);
    }

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

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        UnregisterAll();
        _source?.RemoveHook(HwndHook);
        
        GC.SuppressFinalize(this);
    }
}
