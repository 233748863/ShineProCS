using ShineProCS.Core.Interfaces;
using ShineProCS.Infrastructure;
using ShineProCS.Models;

namespace ShineProCS.Core.Services;

/// <summary>
/// 输入服务实现
/// 统一管理键盘和鼠标输入，支持 Win32 和 GhostBox 驱动
/// 需求: 6.2 - 输入驱动设置
/// 需求: 7.4 - 作为单例服务注册
/// </summary>
public class InputService : IInputService
{
    private readonly InputDriverManager _driverManager;
    private bool _disposed;
    
    /// <summary>
    /// 驱动切换事件
    /// </summary>
    public event Action<InputDriverType>? DriverChanged;
    
    /// <summary>
    /// 连接状态变化事件
    /// </summary>
    public event Action<bool>? ConnectionStatusChanged;
    
    /// <summary>
    /// 设备连接/断开事件
    /// </summary>
    public event Action<bool>? DeviceConnectionChanged;
    
    public InputService() : this(InputDriverType.Win32)
    {
    }
    
    public InputService(InputDriverType initialDriverType)
    {
        _driverManager = new InputDriverManager(initialDriverType);
        
        // 订阅内部事件并转发
        _driverManager.DriverChanged += (sender, args) => 
            DriverChanged?.Invoke(args.NewDriverType);
        _driverManager.ConnectionStatusChanged += (sender, status) => 
            ConnectionStatusChanged?.Invoke(status == "已连接");
        _driverManager.DeviceConnectionChanged += (sender, args) => 
            DeviceConnectionChanged?.Invoke(args.IsConnected);
    }
    
    /// <summary>
    /// 当前输入驱动类型
    /// </summary>
    public InputDriverType CurrentDriverType => _driverManager.CurrentDriverType;
    
    /// <summary>
    /// 键盘接口
    /// </summary>
    public IKeyboardInterface Keyboard => _driverManager.KeyboardInterface;
    
    /// <summary>
    /// 鼠标接口（如果可用）
    /// </summary>
    public IMouseInterface? Mouse => _driverManager.MouseInterface;
    
    /// <summary>
    /// GhostBox 是否可用
    /// </summary>
    public bool IsGhostBoxAvailable => _driverManager.IsGhostBoxAvailable;
    
    /// <summary>
    /// GhostBox 是否已连接
    /// </summary>
    public bool IsGhostBoxConnected => _driverManager.IsGhostBoxConnected;
    
    /// <summary>
    /// GhostBox 连接状态描述
    /// </summary>
    public string GhostBoxStatus => _driverManager.GhostBoxStatus;
    
    /// <summary>
    /// GhostBox 最后错误信息
    /// </summary>
    public string GhostBoxLastError => _driverManager.GhostBoxLastError;
    
    /// <summary>
    /// GhostBox 设备型号
    /// </summary>
    public string GhostBoxDeviceModel => _driverManager.GhostBoxDeviceModel;
    
    /// <summary>
    /// GhostBox 序列号
    /// </summary>
    public string GhostBoxSerialNumber => _driverManager.GhostBoxSerialNumber;
    
    /// <summary>
    /// 切换输入驱动
    /// </summary>
    /// <param name="driverType">目标驱动类型</param>
    /// <returns>切换是否成功</returns>
    public bool SwitchDriver(InputDriverType driverType)
    {
        return _driverManager.SwitchDriver(driverType);
    }
    
    /// <summary>
    /// 尝试重新连接 GhostBox
    /// </summary>
    /// <returns>连接是否成功</returns>
    public bool TryReconnectGhostBox()
    {
        return _driverManager.ReconnectGhostBox();
    }
    
    /// <summary>
    /// 获取内部驱动管理器（用于兼容旧代码）
    /// </summary>
    internal InputDriverManager GetDriverManager() => _driverManager;
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _driverManager.Dispose();
        GC.SuppressFinalize(this);
    }
}
