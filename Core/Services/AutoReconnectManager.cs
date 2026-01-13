using System;
using System.Threading;
using System.Threading.Tasks;
using ShineProCS.Infrastructure;

namespace ShineProCS.Core.Services;

/// <summary>
/// 自动重连管理器 - 处理 GhostBox 设备断开和重连
/// 需求 6.4, 6.5: 优雅处理设备断开错误并支持自动重连尝试
/// </summary>
public class AutoReconnectManager : IDisposable
{
    private readonly GhostBoxDeviceManager _deviceManager;
    private readonly int _retryIntervalMs;
    private readonly int _maxRetries;
    
    private CancellationTokenSource? _cts;
    private Task? _reconnectTask;
    private bool _isReconnecting;
    private int _currentRetryCount;
    private bool _disposed;
    private readonly object _lock = new();
    
    /// <summary>
    /// 重连成功时触发
    /// 需求 6.4: 通知引擎设备已重新连接
    /// </summary>
    public event Action? OnReconnected;
    
    /// <summary>
    /// 重连失败时触发（达到最大重试次数）
    /// 需求 6.4: 通知引擎重连失败
    /// </summary>
    public event Action<string>? OnReconnectFailed;
    
    /// <summary>
    /// 重连尝试时触发（每次尝试）
    /// </summary>
    public event Action<int, int>? OnReconnectAttempt;
    
    /// <summary>
    /// 设备断开时触发
    /// 需求 6.4: 优雅处理设备断开错误
    /// </summary>
    public event Action? OnDeviceDisconnected;
    
    /// <summary>
    /// 是否正在重连中
    /// </summary>
    public bool IsReconnecting
    {
        get { lock (_lock) { return _isReconnecting; } }
        private set { lock (_lock) { _isReconnecting = value; } }
    }
    
    /// <summary>
    /// 当前重试次数
    /// </summary>
    public int CurrentRetryCount
    {
        get { lock (_lock) { return _currentRetryCount; } }
        private set { lock (_lock) { _currentRetryCount = value; } }
    }
    
    /// <summary>
    /// 最大重试次数（0 = 无限）
    /// </summary>
    public int MaxRetries => _maxRetries;
    
    /// <summary>
    /// 重试间隔（毫秒）
    /// </summary>
    public int RetryIntervalMs => _retryIntervalMs;
    
    /// <summary>
    /// 创建自动重连管理器实例
    /// </summary>
    /// <param name="deviceManager">GhostBox 设备管理器</param>
    /// <param name="retryIntervalMs">重试间隔（毫秒），默认 2000</param>
    /// <param name="maxRetries">最大重试次数（0 = 无限），默认 5</param>
    public AutoReconnectManager(
        GhostBoxDeviceManager deviceManager,
        int retryIntervalMs = 2000,
        int maxRetries = 5)
    {
        _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
        _retryIntervalMs = Math.Max(100, retryIntervalMs);  // 最小 100ms
        _maxRetries = Math.Max(0, maxRetries);
    }
    
    /// <summary>
    /// 检查设备连接状态，如果断开则触发重连
    /// 需求 6.4: 优雅处理设备断开错误
    /// </summary>
    /// <returns>设备是否已连接</returns>
    public bool CheckAndReconnect()
    {
        if (_disposed) return false;
        
        // 刷新连接状态
        bool isConnected = _deviceManager.RefreshConnectionStatus();
        
        if (!isConnected && !IsReconnecting)
        {
            // 设备断开，触发事件并开始重连
            OnDeviceDisconnected?.Invoke();
            StartReconnectAsync();
        }
        
        return isConnected;
    }
    
    /// <summary>
    /// 开始异步重连
    /// 需求 6.5: 支持自动重连尝试，重试间隔可配置
    /// </summary>
    /// <returns>重连任务</returns>
    public Task StartReconnectAsync()
    {
        if (_disposed) return Task.CompletedTask;
        
        lock (_lock)
        {
            if (_isReconnecting)
            {
                // 已经在重连中，返回现有任务
                return _reconnectTask ?? Task.CompletedTask;
            }
            
            _isReconnecting = true;
            _currentRetryCount = 0;
            _cts = new CancellationTokenSource();
            _reconnectTask = ReconnectLoopAsync(_cts.Token);
            return _reconnectTask;
        }
    }
    
    /// <summary>
    /// 停止重连尝试
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _isReconnecting = false;
        }
    }
    
    /// <summary>
    /// 重连循环
    /// </summary>
    private async Task ReconnectLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                CurrentRetryCount++;
                
                // 触发重连尝试事件
                OnReconnectAttempt?.Invoke(CurrentRetryCount, _maxRetries);
                
                // 尝试重连
                bool success = _deviceManager.Connect();
                
                if (success)
                {
                    // 重连成功
                    IsReconnecting = false;
                    OnReconnected?.Invoke();
                    return;
                }
                
                // 检查是否达到最大重试次数
                if (_maxRetries > 0 && CurrentRetryCount >= _maxRetries)
                {
                    // 达到最大重试次数，停止重连
                    IsReconnecting = false;
                    OnReconnectFailed?.Invoke($"重连失败：已达到最大重试次数 {_maxRetries}");
                    return;
                }
                
                // 等待重试间隔
                try
                {
                    await Task.Delay(_retryIntervalMs, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            IsReconnecting = false;
            OnReconnectFailed?.Invoke($"重连过程中发生错误: {ex.Message}");
        }
        finally
        {
            IsReconnecting = false;
        }
    }
    
    /// <summary>
    /// 手动触发一次重连尝试
    /// </summary>
    /// <returns>是否重连成功</returns>
    public bool TryReconnectOnce()
    {
        if (_disposed) return false;
        
        return _deviceManager.Connect();
    }
    
    /// <summary>
    /// 重置重试计数
    /// </summary>
    public void ResetRetryCount()
    {
        CurrentRetryCount = 0;
    }
    
    #region IDisposable 实现
    
    public void Dispose()
    {
        if (_disposed) return;
        
        Stop();
        _cts?.Dispose();
        _disposed = true;
    }
    
    #endregion
}
