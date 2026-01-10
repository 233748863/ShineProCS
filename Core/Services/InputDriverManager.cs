using System;
using ShineProCS.Core.Interfaces;
using ShineProCS.Infrastructure;
using ShineProCS.Models;

namespace ShineProCS.Core.Services;

/// <summary>
/// 驱动切换事件参数
/// </summary>
public class DriverChangedEventArgs : EventArgs
{
    /// <summary>
    /// 切换前的驱动类型
    /// </summary>
    public InputDriverType OldDriverType { get; }
    
    /// <summary>
    /// 切换后的驱动类型
    /// </summary>
    public InputDriverType NewDriverType { get; }
    
    /// <summary>
    /// 切换是否成功
    /// </summary>
    public bool Success { get; }
    
    /// <summary>
    /// 错误信息（切换失败时）
    /// </summary>
    public string? ErrorMessage { get; }
    
    public DriverChangedEventArgs(InputDriverType oldType, InputDriverType newType, bool success, string? errorMessage = null)
    {
        OldDriverType = oldType;
        NewDriverType = newType;
        Success = success;
        ErrorMessage = errorMessage;
    }
}

/// <summary>
/// 输入驱动管理器
/// 负责驱动的创建、切换和生命周期管理
/// </summary>
public class InputDriverManager : IDisposable
{
    private readonly object _lockObject = new object();
    private InputDriverType _currentDriverType;
    private IKeyboardInterface _keyboardInterface;
    private IMouseInterface? _mouseInterface;
    private readonly GhostBoxDeviceManager _ghostBoxDevice;
    private ConnectionMonitor? _connectionMonitor;
    private bool _disposed;

    #region 事件
    
    /// <summary>
    /// 驱动切换完成事件
    /// </summary>
    public event EventHandler<DriverChangedEventArgs>? DriverChanged;
    
    /// <summary>
    /// 连接状态变化事件
    /// </summary>
    public event EventHandler<string>? ConnectionStatusChanged;
    
    /// <summary>
    /// 设备连接状态变化事件（用于 UI 更新）
    /// </summary>
    public event EventHandler<ConnectionStateChangedEventArgs>? DeviceConnectionChanged;
    
    #endregion
    
    #region 属性
    
    /// <summary>
    /// 连接监控器实例
    /// </summary>
    public ConnectionMonitor? ConnectionMonitor => _connectionMonitor;
    
    /// <summary>
    /// 当前驱动类型
    /// </summary>
    public InputDriverType CurrentDriverType
    {
        get
        {
            lock (_lockObject)
            {
                return _currentDriverType;
            }
        }
    }
    
    /// <summary>
    /// 当前键盘接口
    /// </summary>
    public IKeyboardInterface KeyboardInterface
    {
        get
        {
            lock (_lockObject)
            {
                return _keyboardInterface;
            }
        }
    }
    
    /// <summary>
    /// 当前鼠标接口（可能为 null）
    /// </summary>
    public IMouseInterface? MouseInterface
    {
        get
        {
            lock (_lockObject)
            {
                return _mouseInterface;
            }
        }
    }

    /// <summary>
    /// GhostBox DLL 是否可用
    /// </summary>
    public bool IsGhostBoxAvailable => _ghostBoxDevice.IsDllAvailable;
    
    /// <summary>
    /// GhostBox 设备是否已连接
    /// </summary>
    public bool IsGhostBoxConnected => _ghostBoxDevice.IsConnected;
    
    /// <summary>
    /// GhostBox 连接状态文本
    /// </summary>
    public string GhostBoxStatus => _ghostBoxDevice.IsConnected ? "已连接" : "未连接";
    
    /// <summary>
    /// GhostBox 最后错误信息
    /// </summary>
    public string GhostBoxLastError => _ghostBoxDevice.LastError;
    
    /// <summary>
    /// GhostBox 设备型号
    /// </summary>
    public string GhostBoxDeviceModel => _ghostBoxDevice.DeviceModel;
    
    /// <summary>
    /// GhostBox 设备序列号
    /// </summary>
    public string GhostBoxSerialNumber => _ghostBoxDevice.SerialNumber;
    
    #endregion
    
    #region 构造函数
    
    /// <summary>
    /// 创建输入驱动管理器实例
    /// </summary>
    /// <param name="initialDriverType">初始驱动类型，默认为 Win32</param>
    public InputDriverManager(InputDriverType initialDriverType = InputDriverType.Win32)
    {
        _ghostBoxDevice = GhostBoxDeviceManager.Instance;
        _currentDriverType = InputDriverType.Win32;
        _keyboardInterface = new Win32KeyboardInterface();
        
        // 如果请求的初始驱动不是 Win32，尝试切换
        if (initialDriverType != InputDriverType.Win32)
        {
            SwitchDriver(initialDriverType);
        }
    }
    
    /// <summary>
    /// 创建输入驱动管理器实例（用于测试，允许注入 GhostBoxDeviceManager）
    /// </summary>
    /// <param name="ghostBoxDevice">GhostBox 设备管理器实例</param>
    /// <param name="initialDriverType">初始驱动类型</param>
    internal InputDriverManager(GhostBoxDeviceManager ghostBoxDevice, InputDriverType initialDriverType = InputDriverType.Win32)
    {
        _ghostBoxDevice = ghostBoxDevice ?? throw new ArgumentNullException(nameof(ghostBoxDevice));
        _currentDriverType = InputDriverType.Win32;
        _keyboardInterface = new Win32KeyboardInterface();
        
        if (initialDriverType != InputDriverType.Win32)
        {
            SwitchDriver(initialDriverType);
        }
    }
    
    #endregion

    #region 公共方法
    
    /// <summary>
    /// 切换到指定的驱动类型
    /// </summary>
    /// <param name="driverType">目标驱动类型</param>
    /// <returns>切换是否成功</returns>
    public bool SwitchDriver(InputDriverType driverType)
    {
        lock (_lockObject)
        {
            InputDriverType oldDriverType = _currentDriverType;
            
            // 如果已经是目标驱动类型，直接返回成功
            if (_currentDriverType == driverType)
            {
                return true;
            }
            
            try
            {
                switch (driverType)
                {
                    case InputDriverType.Win32:
                        return SwitchToWin32(oldDriverType);
                    
                    case InputDriverType.GhostBox:
                        return SwitchToGhostBox(oldDriverType);
                    
                    default:
                        OnDriverChanged(oldDriverType, driverType, false, $"不支持的驱动类型: {driverType}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                // 切换失败，保持当前驱动不变
                OnDriverChanged(oldDriverType, driverType, false, $"切换驱动时发生错误: {ex.Message}");
                return false;
            }
        }
    }
    
    /// <summary>
    /// 重新连接 GhostBox 设备
    /// </summary>
    /// <returns>连接是否成功</returns>
    public bool ReconnectGhostBox()
    {
        if (!_ghostBoxDevice.IsDllAvailable)
        {
            OnConnectionStatusChanged("GhostBox DLL 不可用");
            return false;
        }
        
        // 先断开现有连接
        _ghostBoxDevice.Disconnect();
        
        // 尝试重新连接
        bool connected = _ghostBoxDevice.Connect();
        
        if (connected)
        {
            OnConnectionStatusChanged("已连接");
            
            // 如果当前是 GhostBox 驱动，更新接口
            lock (_lockObject)
            {
                if (_currentDriverType == InputDriverType.GhostBox)
                {
                    _keyboardInterface = new GhostBoxKeyboardInterface(_ghostBoxDevice);
                    _mouseInterface = new GhostBoxMouseInterface(_ghostBoxDevice);
                }
            }
        }
        else
        {
            OnConnectionStatusChanged($"连接失败: {_ghostBoxDevice.LastError}");
        }
        
        return connected;
    }
    
    /// <summary>
    /// 刷新 GhostBox 连接状态
    /// </summary>
    /// <returns>当前连接状态</returns>
    public bool RefreshGhostBoxStatus()
    {
        bool isConnected = _ghostBoxDevice.RefreshConnectionStatus();
        OnConnectionStatusChanged(isConnected ? "已连接" : "未连接");
        return isConnected;
    }
    
    #endregion

    #region 私有方法
    
    /// <summary>
    /// 切换到 Win32 驱动
    /// </summary>
    private bool SwitchToWin32(InputDriverType oldDriverType)
    {
        // 如果之前是 GhostBox，停止监控并断开连接
        if (_currentDriverType == InputDriverType.GhostBox)
        {
            StopConnectionMonitor();
            _ghostBoxDevice.Disconnect();
        }
        
        _keyboardInterface = new Win32KeyboardInterface();
        _mouseInterface = null; // Win32 鼠标接口暂未实现
        _currentDriverType = InputDriverType.Win32;
        
        OnDriverChanged(oldDriverType, InputDriverType.Win32, true);
        OnConnectionStatusChanged("Win32 驱动已激活");
        
        return true;
    }
    
    /// <summary>
    /// 切换到 GhostBox 驱动
    /// </summary>
    private bool SwitchToGhostBox(InputDriverType oldDriverType)
    {
        // 检查 DLL 是否可用
        if (!_ghostBoxDevice.IsDllAvailable)
        {
            OnDriverChanged(oldDriverType, InputDriverType.GhostBox, false, "GhostBox DLL 文件不可用");
            return false;
        }
        
        // 尝试连接设备
        if (!_ghostBoxDevice.IsConnected)
        {
            bool connected = _ghostBoxDevice.Connect();
            if (!connected)
            {
                OnDriverChanged(oldDriverType, InputDriverType.GhostBox, false, 
                    $"无法连接 GhostBox 设备: {_ghostBoxDevice.LastError}");
                return false;
            }
        }
        
        // 创建 GhostBox 驱动实例
        _keyboardInterface = new GhostBoxKeyboardInterface(_ghostBoxDevice);
        _mouseInterface = new GhostBoxMouseInterface(_ghostBoxDevice);
        _currentDriverType = InputDriverType.GhostBox;
        
        // 启动连接监控器
        StartConnectionMonitor();
        
        OnDriverChanged(oldDriverType, InputDriverType.GhostBox, true);
        OnConnectionStatusChanged("已连接");
        
        return true;
    }
    
    /// <summary>
    /// 启动连接监控器
    /// </summary>
    private void StartConnectionMonitor()
    {
        // 停止现有监控器
        StopConnectionMonitor();
        
        // 创建并启动新的监控器
        _connectionMonitor = new ConnectionMonitor(_ghostBoxDevice);
        _connectionMonitor.ConnectionLost += OnMonitorConnectionLost;
        _connectionMonitor.ConnectionRestored += OnMonitorConnectionRestored;
        _connectionMonitor.StartMonitoring();
    }
    
    /// <summary>
    /// 停止连接监控器
    /// </summary>
    private void StopConnectionMonitor()
    {
        if (_connectionMonitor != null)
        {
            _connectionMonitor.ConnectionLost -= OnMonitorConnectionLost;
            _connectionMonitor.ConnectionRestored -= OnMonitorConnectionRestored;
            _connectionMonitor.StopMonitoring();
            _connectionMonitor.Dispose();
            _connectionMonitor = null;
        }
    }
    
    /// <summary>
    /// 监控器检测到连接丢失
    /// </summary>
    private void OnMonitorConnectionLost(object? sender, ConnectionStateChangedEventArgs e)
    {
        OnConnectionStatusChanged("未连接");
        DeviceConnectionChanged?.Invoke(this, e);
    }
    
    /// <summary>
    /// 监控器检测到连接恢复
    /// </summary>
    private void OnMonitorConnectionRestored(object? sender, ConnectionStateChangedEventArgs e)
    {
        // 重新创建驱动接口
        lock (_lockObject)
        {
            if (_currentDriverType == InputDriverType.GhostBox)
            {
                _keyboardInterface = new GhostBoxKeyboardInterface(_ghostBoxDevice);
                _mouseInterface = new GhostBoxMouseInterface(_ghostBoxDevice);
            }
        }
        
        OnConnectionStatusChanged("已连接");
        DeviceConnectionChanged?.Invoke(this, e);
    }
    
    /// <summary>
    /// 触发驱动切换事件
    /// </summary>
    private void OnDriverChanged(InputDriverType oldType, InputDriverType newType, bool success, string? errorMessage = null)
    {
        DriverChanged?.Invoke(this, new DriverChangedEventArgs(oldType, newType, success, errorMessage));
    }
    
    /// <summary>
    /// 触发连接状态变化事件
    /// </summary>
    private void OnConnectionStatusChanged(string status)
    {
        ConnectionStatusChanged?.Invoke(this, status);
    }
    
    #endregion
    
    #region IDisposable 实现
    
    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        
        lock (_lockObject)
        {
            if (_disposed) return;
            
            // 停止连接监控器
            StopConnectionMonitor();
            
            // 如果当前是 GhostBox 驱动，断开连接
            if (_currentDriverType == InputDriverType.GhostBox)
            {
                _ghostBoxDevice.Disconnect();
            }
            
            _disposed = true;
        }
    }
    
    #endregion
}
