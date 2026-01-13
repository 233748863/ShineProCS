using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Services;
using ShineProCS.Models;

namespace ShineProCS.Infrastructure;

/// <summary>
/// GhostBox 硬件键盘驱动
/// 使用 GhostBox 硬件设备进行键盘输入模拟
/// 需求 6.1, 6.2, 6.6: 支持随机延迟和最小按键间隔
/// </summary>
public class GhostBoxKeyboardInterface : IKeyboardInterface
{
    private readonly GhostBoxDeviceManager _deviceManager;
    private readonly RandomDelayGenerator _delayGenerator;
    private readonly AppSettings _settings;
    
    // 记录上次按键时间，用于实现最小按键间隔（需求 6.6）
    private DateTime _lastKeyPressTime = DateTime.MinValue;
    private readonly object _timingLock = new();
    
    /// <summary>
    /// 创建 GhostBox 键盘驱动实例
    /// </summary>
    /// <param name="deviceManager">GhostBox 设备管理器实例</param>
    /// <param name="settings">应用程序设置（可选，用于获取延迟配置）</param>
    /// <exception cref="ArgumentNullException">当 deviceManager 为 null 时抛出</exception>
    public GhostBoxKeyboardInterface(GhostBoxDeviceManager deviceManager, AppSettings? settings = null)
    {
        _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
        _delayGenerator = new RandomDelayGenerator();
        _settings = settings ?? new AppSettings();
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
        
        // 应用最小按键间隔（需求 6.6）
        EnforceMinKeyInterval();
        
        // 应用随机延迟（需求 6.1, 6.2）
        ApplyRandomDelay();
        
        bool result = _deviceManager.PressKey(keyCode);
        
        // 记录按键时间
        RecordKeyPressTime();
        
        return result;
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
        
        // 应用最小按键间隔（需求 6.6）
        EnforceMinKeyInterval();
        
        // 应用随机延迟（需求 6.1, 6.2）
        ApplyRandomDelay();
        
        bool result = _deviceManager.PressAndReleaseKey(keyCode);
        
        // 记录按键时间
        RecordKeyPressTime();
        
        return result;
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
    
    #region 私有方法 - 延迟和间隔控制
    
    /// <summary>
    /// 应用随机延迟
    /// 需求 6.1, 6.2: 在配置范围内添加随机延迟
    /// </summary>
    private void ApplyRandomDelay()
    {
        if (_settings.EnableRandomDelay)
        {
            _delayGenerator.Delay(_settings.KeyPressMinDelayMs, _settings.KeyPressMaxDelayMs);
        }
    }
    
    /// <summary>
    /// 强制执行最小按键间隔
    /// 需求 6.6: 两次按键之间的间隔不应小于配置的最小间隔
    /// 属性 10: 最小按键间隔保证
    /// </summary>
    private void EnforceMinKeyInterval()
    {
        if (_settings.MinInterKeyDelayMs <= 0)
        {
            return;
        }
        
        lock (_timingLock)
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastKeyPressTime).TotalMilliseconds;
            var minInterval = _settings.MinInterKeyDelayMs;
            
            if (elapsed < minInterval)
            {
                // 需要等待的时间
                int waitTime = (int)(minInterval - elapsed);
                if (waitTime > 0)
                {
                    Thread.Sleep(waitTime);
                }
            }
        }
    }
    
    /// <summary>
    /// 记录按键时间
    /// </summary>
    private void RecordKeyPressTime()
    {
        lock (_timingLock)
        {
            _lastKeyPressTime = DateTime.UtcNow;
        }
    }
    
    /// <summary>
    /// 获取上次按键时间（用于测试）
    /// </summary>
    internal DateTime GetLastKeyPressTime()
    {
        lock (_timingLock)
        {
            return _lastKeyPressTime;
        }
    }
    
    #endregion
}
