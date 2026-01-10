using System;
using System.Timers;
using ShineProCS.Infrastructure;
using Timer = System.Timers.Timer;

namespace ShineProCS.Core.Services;

/// <summary>
/// 连接状态变化事件参数
/// </summary>
public class ConnectionStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// 设备是否已连接
    /// </summary>
    public bool IsConnected { get; }
    
    /// <summary>
    /// 状态变化消息
    /// </summary>
    public string Message { get; }
    
    /// <summary>
    /// 状态变化时间戳
    /// </summary>
    public DateTime Timestamp { get; }
    
    public ConnectionStateChangedEventArgs(bool isConnected, string message)
    {
        IsConnected = isConnected;
        Message = message;
        Timestamp = DateTime.Now;
    }
}


/// <summary>
/// GhostBox 连接监控器
/// 定期检查设备连接状态，触发断开/重连事件，支持指数退避自动重连
/// </summary>
public class ConnectionMonitor : IDisposable
{
    #region 配置参数
    
    /// <summary>
    /// 连接状态检查间隔（毫秒）
    /// </summary>
    public int CheckIntervalMs { get; set; } = 1000;
    
    /// <summary>
    /// 初始重连间隔（毫秒）
    /// </summary>
    public int InitialReconnectIntervalMs { get; set; } = 2000;
    
    /// <summary>
    /// 最大重连间隔（毫秒）
    /// </summary>
    public int MaxReconnectIntervalMs { get; set; } = 8000;
    
    /// <summary>
    /// 退避倍数
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;
    
    #endregion
    
    #region 状态属性
    
    /// <summary>
    /// 是否正在监控
    /// </summary>
    public bool IsMonitoring { get; private set; }
    
    /// <summary>
    /// 设备是否已连接
    /// </summary>
    public bool IsConnected { get; private set; }
    
    /// <summary>
    /// 当前重连间隔（毫秒）
    /// </summary>
    public int CurrentReconnectIntervalMs { get; private set; }
    
    #endregion
    
    #region 事件
    
    /// <summary>
    /// 连接丢失事件
    /// </summary>
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionLost;
    
    /// <summary>
    /// 连接恢复事件
    /// </summary>
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionRestored;
    
    /// <summary>
    /// 重连尝试事件，参数为当前重连间隔
    /// </summary>
    public event EventHandler<int>? ReconnectAttempted;
    
    #endregion
    
    #region 私有字段
    
    private readonly GhostBoxDeviceManager _deviceManager;
    private Timer? _checkTimer;
    private Timer? _reconnectTimer;
    private readonly object _lockObject = new();
    private bool _disposed;
    private bool _isReconnecting;
    
    #endregion
    
    #region 构造函数
    
    /// <summary>
    /// 创建连接监控器实例
    /// </summary>
    /// <param name="deviceManager">GhostBox 设备管理器</param>
    public ConnectionMonitor(GhostBoxDeviceManager deviceManager)
    {
        _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
        CurrentReconnectIntervalMs = InitialReconnectIntervalMs;
        IsConnected = _deviceManager.IsConnected;
    }
    
    #endregion
    
    #region 公共方法
    
    /// <summary>
    /// 启动连接监控
    /// </summary>
    public void StartMonitoring()
    {
        lock (_lockObject)
        {
            if (_disposed || IsMonitoring) return;
            
            IsMonitoring = true;
            IsConnected = _deviceManager.IsConnected;
            CurrentReconnectIntervalMs = InitialReconnectIntervalMs;
            
            // 创建并启动检查定时器
            _checkTimer = new Timer(CheckIntervalMs);
            _checkTimer.Elapsed += OnCheckTimerElapsed;
            _checkTimer.AutoReset = true;
            _checkTimer.Start();
        }
    }
    
    /// <summary>
    /// 停止连接监控
    /// </summary>
    public void StopMonitoring()
    {
        lock (_lockObject)
        {
            if (!IsMonitoring) return;
            
            IsMonitoring = false;
            
            // 停止检查定时器
            if (_checkTimer != null)
            {
                _checkTimer.Stop();
                _checkTimer.Elapsed -= OnCheckTimerElapsed;
                _checkTimer.Dispose();
                _checkTimer = null;
            }
            
            // 停止重连定时器
            StopReconnectTimer();
        }
    }
    
    /// <summary>
    /// 重置退避间隔到初始值
    /// </summary>
    public void ResetBackoffInterval()
    {
        lock (_lockObject)
        {
            CurrentReconnectIntervalMs = InitialReconnectIntervalMs;
        }
    }
    
    #endregion
    
    #region 私有方法
    
    /// <summary>
    /// 检查定时器回调
    /// </summary>
    private void OnCheckTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        lock (_lockObject)
        {
            if (_disposed || !IsMonitoring || _isReconnecting) return;
        }
        
        try
        {
            // 实时检查设备连接状态
            bool currentlyConnected = _deviceManager.RefreshConnectionStatus();
            
            lock (_lockObject)
            {
                // 检测状态变化
                if (IsConnected && !currentlyConnected)
                {
                    // 连接丢失
                    IsConnected = false;
                    OnConnectionLost("设备连接已断开");
                    StartReconnectTimer();
                }
                else if (!IsConnected && currentlyConnected)
                {
                    // 连接恢复（可能是外部重连）
                    IsConnected = true;
                    StopReconnectTimer();
                    ResetBackoffInterval();
                    OnConnectionRestored("设备连接已恢复");
                }
            }
        }
        catch (Exception)
        {
            // 检查过程中发生异常，视为断开
            lock (_lockObject)
            {
                if (IsConnected)
                {
                    IsConnected = false;
                    OnConnectionLost("设备连接检查失败");
                    StartReconnectTimer();
                }
            }
        }
    }
    
    /// <summary>
    /// 触发连接丢失事件
    /// </summary>
    private void OnConnectionLost(string message)
    {
        ConnectionLost?.Invoke(this, new ConnectionStateChangedEventArgs(false, message));
    }
    
    /// <summary>
    /// 触发连接恢复事件
    /// </summary>
    private void OnConnectionRestored(string message)
    {
        ConnectionRestored?.Invoke(this, new ConnectionStateChangedEventArgs(true, message));
    }
    
    /// <summary>
    /// 启动重连定时器
    /// </summary>
    private void StartReconnectTimer()
    {
        if (_isReconnecting) return;
        
        _isReconnecting = true;
        
        _reconnectTimer = new Timer(CurrentReconnectIntervalMs);
        _reconnectTimer.Elapsed += OnReconnectTimerElapsed;
        _reconnectTimer.AutoReset = false; // 单次触发，每次重连后手动重启
        _reconnectTimer.Start();
    }
    
    /// <summary>
    /// 停止重连定时器
    /// </summary>
    private void StopReconnectTimer()
    {
        _isReconnecting = false;
        
        if (_reconnectTimer != null)
        {
            _reconnectTimer.Stop();
            _reconnectTimer.Elapsed -= OnReconnectTimerElapsed;
            _reconnectTimer.Dispose();
            _reconnectTimer = null;
        }
    }
    
    /// <summary>
    /// 重连定时器回调
    /// </summary>
    private void OnReconnectTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        lock (_lockObject)
        {
            if (_disposed || !IsMonitoring || IsConnected)
            {
                StopReconnectTimer();
                return;
            }
        }
        
        try
        {
            // 触发重连尝试事件
            ReconnectAttempted?.Invoke(this, CurrentReconnectIntervalMs);
            
            // 尝试重新连接
            bool reconnected = _deviceManager.Connect();
            
            lock (_lockObject)
            {
                if (reconnected)
                {
                    // 重连成功
                    IsConnected = true;
                    StopReconnectTimer();
                    ResetBackoffInterval();
                    OnConnectionRestored("设备自动重连成功");
                }
                else
                {
                    // 重连失败，增加退避间隔并继续尝试
                    IncreaseBackoffInterval();
                    
                    // 重新启动重连定时器
                    if (_reconnectTimer != null)
                    {
                        _reconnectTimer.Interval = CurrentReconnectIntervalMs;
                        _reconnectTimer.Start();
                    }
                }
            }
        }
        catch (Exception)
        {
            // 重连过程中发生异常，继续尝试
            lock (_lockObject)
            {
                IncreaseBackoffInterval();
                
                if (_reconnectTimer != null)
                {
                    _reconnectTimer.Interval = CurrentReconnectIntervalMs;
                    _reconnectTimer.Start();
                }
            }
        }
    }
    
    /// <summary>
    /// 增加退避间隔（指数退避）
    /// </summary>
    private void IncreaseBackoffInterval()
    {
        int newInterval = (int)(CurrentReconnectIntervalMs * BackoffMultiplier);
        CurrentReconnectIntervalMs = Math.Min(newInterval, MaxReconnectIntervalMs);
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
            _disposed = true;
            
            StopMonitoring();
        }
    }
    
    #endregion
}
