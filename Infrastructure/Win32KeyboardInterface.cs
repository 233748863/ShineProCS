using System.Runtime.InteropServices;
using ShineProCS.Core.Interfaces;

namespace ShineProCS.Infrastructure;

public class Win32KeyboardInterface : IKeyboardInterface
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
    
    private const uint KEYDOWN = 0, KEYUP = 2;
    
    // 可配置的按键延迟
    private int _keyPressDelay = 50;
    
    public int KeyPressDelay
    {
        get => _keyPressDelay;
        set => _keyPressDelay = Math.Max(10, Math.Min(500, value));
    }

    public bool PressKey(int keyCode)
    {
        if (keyCode < 0 || keyCode > 255)
            return false;
        
        try
        {
            keybd_event((byte)keyCode, 0, KEYDOWN, UIntPtr.Zero);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool ReleaseKey(int keyCode)
    {
        if (keyCode < 0 || keyCode > 255)
            return false;
        
        try
        {
            keybd_event((byte)keyCode, 0, KEYUP, UIntPtr.Zero);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool PressAndRelease(int keyCode)
    {
        if (keyCode < 0 || keyCode > 255)
            return false;
        
        try
        {
            keybd_event((byte)keyCode, 0, KEYDOWN, UIntPtr.Zero);
            Thread.Sleep(_keyPressDelay);
            keybd_event((byte)keyCode, 0, KEYUP, UIntPtr.Zero);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// 检查按键是否被按下
    /// </summary>
    public bool IsKeyPressed(int keyCode)
    {
        if (keyCode < 0 || keyCode > 255)
            return false;
        
        try
        {
            return (GetAsyncKeyState(keyCode) & 0x8000) != 0;
        }
        catch
        {
            return false;
        }
    }
}
