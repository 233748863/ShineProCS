using ShineProCS.Models;

namespace ShineProCS.Core.Interfaces;

/// <summary>
/// 输入服务接口
/// 统一管理键盘和鼠标输入，支持多种输入驱动
/// 需求: 6.2 - 输入驱动设置
/// </summary>
public interface IInputService : IDisposable
{
    /// <summary>
    /// 当前输入驱动类型
    /// </summary>
    InputDriverType CurrentDriverType { get; }
    
    /// <summary>
    /// 键盘接口
    /// </summary>
    IKeyboardInterface Keyboard { get; }
    
    /// <summary>
    /// 鼠标接口（如果可用）
    /// </summary>
    IMouseInterface? Mouse { get; }
    
    /// <summary>
    /// GhostBox 是否可用
    /// </summary>
    bool IsGhostBoxAvailable { get; }
    
    /// <summary>
    /// GhostBox 是否已连接
    /// </summary>
    bool IsGhostBoxConnected { get; }
    
    /// <summary>
    /// GhostBox 连接状态描述
    /// </summary>
    string GhostBoxStatus { get; }
    
    /// <summary>
    /// GhostBox 最后错误信息
    /// </summary>
    string GhostBoxLastError { get; }
    
    /// <summary>
    /// GhostBox 设备型号
    /// </summary>
    string GhostBoxDeviceModel { get; }
    
    /// <summary>
    /// GhostBox 序列号
    /// </summary>
    string GhostBoxSerialNumber { get; }
    
    /// <summary>
    /// 切换输入驱动
    /// </summary>
    /// <param name="driverType">目标驱动类型</param>
    /// <returns>切换是否成功</returns>
    bool SwitchDriver(InputDriverType driverType);
    
    /// <summary>
    /// 尝试重新连接 GhostBox
    /// </summary>
    /// <returns>连接是否成功</returns>
    bool TryReconnectGhostBox();
    
    /// <summary>
    /// 驱动切换事件
    /// </summary>
    event Action<InputDriverType>? DriverChanged;
    
    /// <summary>
    /// 连接状态变化事件
    /// </summary>
    event Action<bool>? ConnectionStatusChanged;
    
    /// <summary>
    /// 设备连接/断开事件
    /// </summary>
    event Action<bool>? DeviceConnectionChanged;
}
