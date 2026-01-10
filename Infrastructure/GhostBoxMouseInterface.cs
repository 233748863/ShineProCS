using ShineProCS.Core.Interfaces;

namespace ShineProCS.Infrastructure;

/// <summary>
/// GhostBox 硬件鼠标驱动
/// 使用 GhostBox 硬件设备进行鼠标输入模拟
/// </summary>
public class GhostBoxMouseInterface : IMouseInterface
{
    private readonly GhostBoxDeviceManager _deviceManager;
    
    /// <summary>
    /// 创建 GhostBox 鼠标驱动实例
    /// </summary>
    /// <param name="deviceManager">GhostBox 设备管理器实例</param>
    /// <exception cref="ArgumentNullException">当 deviceManager 为 null 时抛出</exception>
    public GhostBoxMouseInterface(GhostBoxDeviceManager deviceManager)
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
    /// 移动鼠标到指定屏幕坐标
    /// </summary>
    /// <param name="x">目标 X 坐标</param>
    /// <param name="y">目标 Y 坐标</param>
    /// <returns>操作是否成功</returns>
    public bool MoveTo(int x, int y)
    {
        if (!_deviceManager.IsConnected)
        {
            return false;
        }
        
        return _deviceManager.MoveMousTo(x, y);
    }
    
    /// <summary>
    /// 按下鼠标按钮（不释放）
    /// </summary>
    /// <param name="button">鼠标按钮: 1=左键, 2=右键, 3=中键</param>
    /// <returns>操作是否成功</returns>
    public bool PressButton(int button)
    {
        if (!_deviceManager.IsConnected)
        {
            return false;
        }
        
        return _deviceManager.PressMouseButton(button);
    }
    
    /// <summary>
    /// 释放鼠标按钮
    /// </summary>
    /// <param name="button">鼠标按钮: 1=左键, 2=右键, 3=中键</param>
    /// <returns>操作是否成功</returns>
    public bool ReleaseButton(int button)
    {
        if (!_deviceManager.IsConnected)
        {
            return false;
        }
        
        return _deviceManager.ReleaseMouseButton(button);
    }
    
    /// <summary>
    /// 点击鼠标按钮（按下并释放）
    /// </summary>
    /// <param name="button">鼠标按钮: 1=左键, 2=右键, 3=中键</param>
    /// <returns>操作是否成功</returns>
    public bool Click(int button)
    {
        if (!_deviceManager.IsConnected)
        {
            return false;
        }
        
        return _deviceManager.ClickMouseButton(button);
    }
    
    /// <summary>
    /// 相对移动鼠标
    /// </summary>
    /// <param name="deltaX">X 方向偏移</param>
    /// <param name="deltaY">Y 方向偏移</param>
    /// <returns>操作是否成功</returns>
    public bool MoveRelative(int deltaX, int deltaY)
    {
        if (!_deviceManager.IsConnected)
        {
            return false;
        }
        
        return _deviceManager.MoveMousRelative(deltaX, deltaY);
    }
    
    /// <summary>
    /// 释放所有鼠标按钮
    /// </summary>
    /// <returns>操作是否成功</returns>
    public bool ReleaseAllButtons()
    {
        if (!_deviceManager.IsConnected)
        {
            return false;
        }
        
        return _deviceManager.ReleaseAllMouseButtons();
    }
}
