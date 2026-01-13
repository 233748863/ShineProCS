using System.Runtime.InteropServices;

namespace ShineProCS.Core.Services;

/// <summary>
/// 后台消息模拟器
/// 使用 Windows PostMessage API 实现后台键鼠操作
/// 
/// 实现原理：
/// 1. PostMessage 是 Windows API，可以向指定窗口发送消息而不需要窗口处于前台
/// 2. 与 SendInput 不同，PostMessage 不需要窗口获得焦点
/// 3. 通过发送 WM_KEYDOWN/WM_KEYUP 和 WM_LBUTTONDOWN/WM_LBUTTONUP 消息模拟输入
/// 4. WM_ACTIVATE 消息用于激活窗口的消息处理，但不会将窗口带到前台
/// 
/// 注意事项：
/// - 部分游戏可能会检测并阻止 PostMessage 输入
/// - 某些反作弊系统可能会标记此类操作
/// - 鼠标坐标需要转换为窗口客户区坐标
/// </summary>
public class PostMessageSimulator
{
    #region Windows API 常量
    
    // 鼠标消息
    private const uint WM_LBUTTONDOWN = 0x0201;  // 鼠标左键按下
    private const uint WM_LBUTTONUP = 0x0202;    // 鼠标左键释放
    private const uint WM_RBUTTONDOWN = 0x0204;  // 鼠标右键按下
    private const uint WM_RBUTTONUP = 0x0205;    // 鼠标右键释放
    private const uint WM_MBUTTONDOWN = 0x0207;  // 鼠标中键按下
    private const uint WM_MBUTTONUP = 0x0208;    // 鼠标中键释放
    
    // 键盘消息
    private const uint WM_KEYDOWN = 0x0100;      // 键盘按下
    private const uint WM_KEYUP = 0x0101;        // 键盘释放
    private const uint WM_CHAR = 0x0102;         // 字符消息
    
    // 窗口消息
    private const uint WM_ACTIVATE = 0x0006;     // 窗口激活
    
    #endregion
    
    #region Windows API 导入
    
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hWnd);
    
    #endregion
    
    private readonly IntPtr _hWnd;
    
    /// <summary>
    /// 创建后台消息模拟器
    /// </summary>
    /// <param name="hWnd">目标窗口句柄</param>
    public PostMessageSimulator(IntPtr hWnd)
    {
        _hWnd = hWnd;
    }
    
    /// <summary>
    /// 检查目标窗口是否有效
    /// </summary>
    public bool IsWindowValid => IsWindow(_hWnd);
    
    /// <summary>
    /// 检查目标窗口是否在前台
    /// </summary>
    public bool IsWindowForeground => GetForegroundWindow() == _hWnd;
    
    #region 鼠标操作
    
    /// <summary>
    /// 后台鼠标左键点击（默认位置）
    /// </summary>
    public bool LeftButtonClickBackground()
    {
        return LeftButtonClickBackground(16, 16);
    }
    
    /// <summary>
    /// 后台鼠标左键点击（指定位置）
    /// </summary>
    /// <param name="x">客户区 X 坐标</param>
    /// <param name="y">客户区 Y 坐标</param>
    public bool LeftButtonClickBackground(int x, int y)
    {
        if (!IsWindowValid) return false;
        
        // 先发送激活消息
        PostMessage(_hWnd, WM_ACTIVATE, (IntPtr)1, IntPtr.Zero);
        
        var lParam = MakeLParam(x, y);
        
        // 按下
        if (!PostMessage(_hWnd, WM_LBUTTONDOWN, (IntPtr)1, lParam))
            return false;
        
        Thread.Sleep(50);
        
        // 释放
        return PostMessage(_hWnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
    }
    
    /// <summary>
    /// 后台鼠标右键点击
    /// </summary>
    public bool RightButtonClickBackground(int x, int y)
    {
        if (!IsWindowValid) return false;
        
        PostMessage(_hWnd, WM_ACTIVATE, (IntPtr)1, IntPtr.Zero);
        
        var lParam = MakeLParam(x, y);
        
        if (!PostMessage(_hWnd, WM_RBUTTONDOWN, IntPtr.Zero, lParam))
            return false;
        
        Thread.Sleep(50);
        
        return PostMessage(_hWnd, WM_RBUTTONUP, IntPtr.Zero, lParam);
    }
    
    /// <summary>
    /// 后台鼠标中键点击
    /// </summary>
    public bool MiddleButtonClickBackground(int x, int y)
    {
        if (!IsWindowValid) return false;
        
        PostMessage(_hWnd, WM_ACTIVATE, (IntPtr)1, IntPtr.Zero);
        
        var lParam = MakeLParam(x, y);
        
        if (!PostMessage(_hWnd, WM_MBUTTONDOWN, IntPtr.Zero, lParam))
            return false;
        
        Thread.Sleep(50);
        
        return PostMessage(_hWnd, WM_MBUTTONUP, IntPtr.Zero, lParam);
    }
    
    #endregion
    
    #region 键盘操作
    
    /// <summary>
    /// 后台按键（按下并释放）
    /// </summary>
    /// <param name="vk">虚拟键码</param>
    public bool KeyPressBackground(int vk)
    {
        if (!IsWindowValid) return false;
        
        // 发送激活消息
        PostMessage(_hWnd, WM_ACTIVATE, (IntPtr)1, IntPtr.Zero);
        
        // 构造 lParam：扫描码和重复计数
        // 格式: [31:重复标志][30:上一状态][29:上下文][24:扩展键][23-16:扫描码][15-0:重复计数]
        var lParamDown = (IntPtr)0x001E0001;  // 按下
        var lParamUp = unchecked((IntPtr)0xC01E0001);  // 释放
        
        // 按下
        if (!PostMessage(_hWnd, WM_KEYDOWN, (IntPtr)vk, lParamDown))
            return false;
        
        // 字符消息（某些程序需要）
        PostMessage(_hWnd, WM_CHAR, (IntPtr)vk, lParamDown);
        
        // 释放
        return PostMessage(_hWnd, WM_KEYUP, (IntPtr)vk, lParamUp);
    }
    
    /// <summary>
    /// 后台按键（带延迟）
    /// </summary>
    /// <param name="vk">虚拟键码</param>
    /// <param name="holdMs">按住时间（毫秒）</param>
    public bool KeyPressBackground(int vk, int holdMs)
    {
        if (!IsWindowValid) return false;
        
        PostMessage(_hWnd, WM_ACTIVATE, (IntPtr)1, IntPtr.Zero);
        
        var lParamDown = (IntPtr)0x001E0001;
        var lParamUp = unchecked((IntPtr)0xC01E0001);
        
        if (!PostMessage(_hWnd, WM_KEYDOWN, (IntPtr)vk, lParamDown))
            return false;
        
        Thread.Sleep(holdMs);
        
        PostMessage(_hWnd, WM_CHAR, (IntPtr)vk, lParamDown);
        
        return PostMessage(_hWnd, WM_KEYUP, (IntPtr)vk, lParamUp);
    }
    
    /// <summary>
    /// 后台按键按下（不释放）
    /// </summary>
    public bool KeyDownBackground(int vk)
    {
        if (!IsWindowValid) return false;
        
        PostMessage(_hWnd, WM_ACTIVATE, (IntPtr)1, IntPtr.Zero);
        return PostMessage(_hWnd, WM_KEYDOWN, (IntPtr)vk, (IntPtr)0x001E0001);
    }
    
    /// <summary>
    /// 后台按键释放
    /// </summary>
    public bool KeyUpBackground(int vk)
    {
        if (!IsWindowValid) return false;
        
        PostMessage(_hWnd, WM_ACTIVATE, (IntPtr)1, IntPtr.Zero);
        return PostMessage(_hWnd, WM_KEYUP, (IntPtr)vk, unchecked((IntPtr)0xC01E0001));
    }
    
    #endregion
    
    #region 辅助方法
    
    /// <summary>
    /// 构造鼠标坐标 lParam
    /// 低16位为X坐标，高16位为Y坐标
    /// </summary>
    private static IntPtr MakeLParam(int x, int y)
    {
        return (IntPtr)((y << 16) | (x & 0xFFFF));
    }
    
    #endregion
}
