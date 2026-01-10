using ShineProCS.Core.Interfaces;

namespace ShineProCS.Infrastructure;

/// <summary>
/// GhostBox 硬件键盘驱动
/// 使用 GhostBox 硬件设备进行键盘输入模拟
/// </summary>
public class GhostBoxKeyboardInterface : IKeyboardInterface
{
    private readonly GhostBoxDeviceManager _deviceManager;
    
    /// <summary>
    /// 创建 GhostBox 键盘驱动实例
    /// </summary>
    /// <param name="deviceManager">GhostBox 设备管理器实例</param>
    /// <exception cref="ArgumentNullException">当 deviceManager 为 null 时抛出</exception>
    public GhostBoxKeyboardInterface(GhostBoxDeviceManager deviceManager)
    {
        _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
    }
    
    /// <summary>
    /// 获取关联的设备管理器
    /// </summary>
    public GhostBoxDeviceManager DeviceManager => _deviceManager;
    
    /// <summary>
    /// 设备是否已连接
    /// </summary>
    public bool IsConnected => _deviceManager.IsConnected;
    
    /// <summary>
    /// 最后一次错误信息
    /// </summary>
    public string LastError => _deviceManager.LastError;
    
    /// <summary>
    /// 按下指定按键（不释放）
    /// </summary>
    /// <param name="keyCode">虚拟键码 (VK_*)</param>
    /// <returns>操作是否成功</returns>
    public bool PressKey(int keyCode)
    {
        if (!_deviceManager.IsConnected)
        {
            return false;
        }
        
        return _deviceManager.PressKey(keyCode);
    }
    
    /// <summary>
    /// 释放指定按键
    /// </summary>
    /// <param name="keyCode">虚拟键码 (VK_*)</param>
    /// <returns>操作是否成功</returns>
    public bool ReleaseKey(int keyCode)
    {
        if (!_deviceManager.IsConnected)
        {
            return false;
        }
        
        return _deviceManager.ReleaseKey(keyCode);
    }
    
    /// <summary>
    /// 按下并释放指定按键（完整的按键操作）
    /// </summary>
    /// <param name="keyCode">虚拟键码 (VK_*)</param>
    /// <returns>操作是否成功</returns>
    public bool PressAndRelease(int keyCode)
    {
        if (!_deviceManager.IsConnected)
        {
            return false;
        }
        
        return _deviceManager.PressAndReleaseKey(keyCode);
    }
    
    /// <summary>
    /// 释放所有按键
    /// </summary>
    /// <returns>操作是否成功</returns>
    public bool ReleaseAllKeys()
    {
        if (!_deviceManager.IsConnected)
        {
            return false;
        }
        
        return _deviceManager.ReleaseAllKeys();
    }
}
