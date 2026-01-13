using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using ShineProCS.Core.Services;

namespace ShineProCS.Infrastructure;

/// <summary>
/// GhostBox 设备管理器 - 单例模式
/// 管理 GhostBox 硬件设备的连接，供键盘和鼠标驱动共享
/// 仅支持 64 位系统
/// 需求 6.4, 6.5: 支持设备断开处理和自动重连
/// </summary>
public sealed class GhostBoxDeviceManager : IDisposable
{
    #region 单例实现
    
    private static readonly Lazy<GhostBoxDeviceManager> _instance = 
        new Lazy<GhostBoxDeviceManager>(() => new GhostBoxDeviceManager(), LazyThreadSafetyMode.ExecutionAndPublication);
    
    /// <summary>
    /// 获取 GhostBoxDeviceManager 单例实例
    /// </summary>
    public static GhostBoxDeviceManager Instance => _instance.Value;
    
    #endregion
    
    #region P/Invoke 声明
    
    private const string DllName = "gbild64";
    
    // 设置 DLL 搜索目录
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);
    
    // GhostBox 64位 DLL P/Invoke 声明
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "opendevice")]
    private static extern int NativeOpenDevice(int index = 0);
    
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "isconnected")]
    private static extern int NativeIsConnected();
    
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "closedevice")]
    private static extern int NativeCloseDevice();
    
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "resetdevice")]
    private static extern int NativeResetDevice();
    
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "getmodel")]
    private static extern IntPtr NativeGetModel();

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "getserialnumber")]
    private static extern IntPtr NativeGetSerialNumber();
    
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "presskeybyvalue")]
    private static extern int NativePressKeyByValue(int keyValue);
    
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "releasekeybyvalue")]
    private static extern int NativeReleaseKeyByValue(int keyValue);
    
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "pressandreleasekeybyvalue")]
    private static extern int NativePressAndReleaseKeyByValue(int keyValue);
    
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "releaseallkey")]
    private static extern int NativeReleaseAllKey(int reserved = 0);
    
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "movemouseto")]
    private static extern int NativeMoveMousTo(int x, int y);
    
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "movemouserelative")]
    private static extern int NativeMoveMousRelative(int x, int y);
    
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "pressmousebutton")]
    private static extern int NativePressMouseButton(int button);
    
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "releasemousebutton")]
    private static extern int NativeReleaseMouseButton(int button);
    
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "pressandreleasemousebutton")]
    private static extern int NativePressAndReleaseMouseButton(int button);
    
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "releaseallmousebutton")]
    private static extern int NativeReleaseAllMouseButton();
    
    // 静态构造函数，设置 DLL 搜索路径
    static GhostBoxDeviceManager()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string ghostboxDir = Path.Combine(baseDir, "libs", "ghostbox");
            
            if (Directory.Exists(ghostboxDir))
            {
                SetDllDirectory(ghostboxDir);
            }
        }
        catch
        {
            // 忽略设置 DLL 目录失败的情况
        }
    }
    
    #endregion

    #region 字段和属性
    
    private bool _disposed;
    private bool _isConnected;
    private string _lastError = string.Empty;
    private readonly object _lockObject = new object();
    
    // 自动重连管理器（需求 6.4, 6.5）
    private AutoReconnectManager? _reconnectManager;
    
    /// <summary>
    /// 设备是否已连接
    /// </summary>
    public bool IsConnected
    {
        get { lock (_lockObject) { return _isConnected; } }
        private set { lock (_lockObject) { _isConnected = value; } }
    }
    
    /// <summary>
    /// DLL 文件是否可用
    /// </summary>
    public bool IsDllAvailable { get; private set; }
    
    /// <summary>
    /// 最后一次错误信息
    /// </summary>
    public string LastError
    {
        get { lock (_lockObject) { return _lastError; } }
        private set { lock (_lockObject) { _lastError = value; } }
    }
    
    /// <summary>
    /// 设备型号（连接后可用）
    /// </summary>
    public string DeviceModel { get; private set; } = string.Empty;
    
    /// <summary>
    /// 设备序列号（连接后可用）
    /// </summary>
    public string SerialNumber { get; private set; } = string.Empty;
    
    /// <summary>
    /// 获取自动重连管理器
    /// 需求 6.4, 6.5: 支持自动重连
    /// </summary>
    public AutoReconnectManager? ReconnectManager => _reconnectManager;
    
    #endregion
    
    #region 事件（需求 6.4）
    
    /// <summary>
    /// 设备断开时触发
    /// 需求 6.4: 优雅处理设备断开错误并通知引擎
    /// </summary>
    public event Action? OnDeviceDisconnected;
    
    /// <summary>
    /// 设备重连成功时触发
    /// 需求 6.4: 通知引擎设备已重新连接
    /// </summary>
    public event Action? OnDeviceReconnected;
    
    /// <summary>
    /// 重连失败时触发
    /// 需求 6.4: 通知引擎重连失败
    /// </summary>
    public event Action<string>? OnReconnectFailed;
    
    #endregion

    #region 构造函数
    
    private GhostBoxDeviceManager()
    {
        IsDllAvailable = CheckDllAvailability();
        if (!IsDllAvailable)
        {
            LastError = "GhostBox DLL 文件不存在或无法加载";
        }
    }
    
    #endregion
    
    #region 公共方法
    
    /// <summary>
    /// 初始化自动重连管理器
    /// 需求 6.5: 支持自动重连尝试，重试间隔可配置
    /// </summary>
    /// <param name="retryIntervalMs">重试间隔（毫秒）</param>
    /// <param name="maxRetries">最大重试次数（0 = 无限）</param>
    public void InitializeAutoReconnect(int retryIntervalMs = 2000, int maxRetries = 5)
    {
        // 清理旧的重连管理器
        _reconnectManager?.Dispose();
        
        // 创建新的重连管理器
        _reconnectManager = new AutoReconnectManager(this, retryIntervalMs, maxRetries);
        
        // 订阅事件
        _reconnectManager.OnDeviceDisconnected += () => OnDeviceDisconnected?.Invoke();
        _reconnectManager.OnReconnected += () => OnDeviceReconnected?.Invoke();
        _reconnectManager.OnReconnectFailed += (msg) => OnReconnectFailed?.Invoke(msg);
    }
    
    /// <summary>
    /// 连接 GhostBox 设备
    /// </summary>
    public bool Connect(int deviceIndex = 0)
    {
        if (_disposed)
        {
            LastError = "设备管理器已释放";
            return false;
        }
        
        if (!IsDllAvailable)
        {
            LastError = "GhostBox DLL 文件不可用";
            return false;
        }
        
        try
        {
            if (IsConnected) Disconnect();
            
            int result = NativeOpenDevice(deviceIndex);
            if (result != 0 && NativeIsConnected() != 0)
            {
                IsConnected = true;
                LastError = string.Empty;
                UpdateDeviceInfo();
                return true;
            }
            
            IsConnected = false;
            LastError = "无法连接到 GhostBox 设备，请检查设备是否已插入";
            return false;
        }
        catch (DllNotFoundException ex)
        {
            IsDllAvailable = false;
            IsConnected = false;
            LastError = $"找不到 GhostBox DLL: {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            IsConnected = false;
            LastError = $"连接设备时发生错误: {ex.Message}";
            return false;
        }
    }
    
    /// <summary>
    /// 断开 GhostBox 设备连接
    /// </summary>
    public void Disconnect()
    {
        if (!IsDllAvailable || !IsConnected)
        {
            IsConnected = false;
            return;
        }
        
        try
        {
            NativeReleaseAllKey(0);
            NativeReleaseAllMouseButton();
            NativeCloseDevice();
        }
        catch (Exception ex)
        {
            LastError = $"断开设备时发生错误: {ex.Message}";
        }
        finally
        {
            IsConnected = false;
            DeviceModel = string.Empty;
            SerialNumber = string.Empty;
        }
    }
    
    /// <summary>
    /// 重置设备连接
    /// </summary>
    public bool Reset()
    {
        if (!IsDllAvailable)
        {
            LastError = "GhostBox DLL 文件不可用";
            return false;
        }
        
        try
        {
            return NativeResetDevice() != 0;
        }
        catch (Exception ex)
        {
            LastError = $"重置设备时发生错误: {ex.Message}";
            return false;
        }
    }
    
    /// <summary>
    /// 刷新连接状态
    /// </summary>
    public bool RefreshConnectionStatus()
    {
        if (!IsDllAvailable)
        {
            IsConnected = false;
            return false;
        }
        
        try
        {
            bool wasConnected = IsConnected;
            IsConnected = NativeIsConnected() != 0;
            
            // 检测到断开，触发事件（需求 6.4）
            if (wasConnected && !IsConnected)
            {
                OnDeviceDisconnected?.Invoke();
            }
            
            return IsConnected;
        }
        catch
        {
            IsConnected = false;
            return false;
        }
    }
    
    /// <summary>
    /// 检查连接状态，如果断开则尝试自动重连
    /// 需求 6.4, 6.5: 优雅处理设备断开并支持自动重连
    /// </summary>
    /// <returns>设备是否已连接</returns>
    public bool CheckAndAutoReconnect()
    {
        if (_reconnectManager != null)
        {
            return _reconnectManager.CheckAndReconnect();
        }
        
        return RefreshConnectionStatus();
    }
    
    #endregion

    #region 键盘操作
    
    public bool PressKey(int keyCode)
    {
        if (!EnsureConnected()) return false;
        try { return NativePressKeyByValue(keyCode) != 0; }
        catch (Exception ex) { LastError = $"按下按键失败: {ex.Message}"; return false; }
    }
    
    public bool ReleaseKey(int keyCode)
    {
        if (!EnsureConnected()) return false;
        try { return NativeReleaseKeyByValue(keyCode) != 0; }
        catch (Exception ex) { LastError = $"释放按键失败: {ex.Message}"; return false; }
    }
    
    public bool PressAndReleaseKey(int keyCode)
    {
        if (!EnsureConnected()) return false;
        try { return NativePressAndReleaseKeyByValue(keyCode) != 0; }
        catch (Exception ex) { LastError = $"按键操作失败: {ex.Message}"; return false; }
    }
    
    public bool ReleaseAllKeys()
    {
        if (!EnsureConnected()) return false;
        try { return NativeReleaseAllKey(0) != 0; }
        catch (Exception ex) { LastError = $"释放所有按键失败: {ex.Message}"; return false; }
    }
    
    #endregion

    #region 鼠标操作
    
    public bool MoveMousTo(int x, int y)
    {
        if (!EnsureConnected()) return false;
        try { return NativeMoveMousTo(x, y) != 0; }
        catch (Exception ex) { LastError = $"移动鼠标失败: {ex.Message}"; return false; }
    }
    
    public bool MoveMousRelative(int deltaX, int deltaY)
    {
        if (!EnsureConnected()) return false;
        try { return NativeMoveMousRelative(deltaX, deltaY) != 0; }
        catch (Exception ex) { LastError = $"相对移动鼠标失败: {ex.Message}"; return false; }
    }
    
    public bool PressMouseButton(int button)
    {
        if (!EnsureConnected()) return false;
        try { return NativePressMouseButton(button) != 0; }
        catch (Exception ex) { LastError = $"按下鼠标按钮失败: {ex.Message}"; return false; }
    }
    
    public bool ReleaseMouseButton(int button)
    {
        if (!EnsureConnected()) return false;
        try { return NativeReleaseMouseButton(button) != 0; }
        catch (Exception ex) { LastError = $"释放鼠标按钮失败: {ex.Message}"; return false; }
    }
    
    public bool ClickMouseButton(int button)
    {
        if (!EnsureConnected()) return false;
        try { return NativePressAndReleaseMouseButton(button) != 0; }
        catch (Exception ex) { LastError = $"点击鼠标按钮失败: {ex.Message}"; return false; }
    }
    
    public bool ReleaseAllMouseButtons()
    {
        if (!EnsureConnected()) return false;
        try { return NativeReleaseAllMouseButton() != 0; }
        catch (Exception ex) { LastError = $"释放所有鼠标按钮失败: {ex.Message}"; return false; }
    }
    
    #endregion

    #region 私有方法
    
    private bool CheckDllAvailability()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string[] paths = {
            Path.Combine(baseDir, "gbild64.dll"),
            Path.Combine(baseDir, "libs", "ghostbox", "gbild64.dll"),
            Path.Combine(Environment.CurrentDirectory, "gbild64.dll")
        };
        
        foreach (string path in paths)
        {
            if (File.Exists(path)) return true;
        }
        return false;
    }
    
    private bool EnsureConnected()
    {
        if (!IsDllAvailable) { LastError = "GhostBox DLL 文件不可用"; return false; }
        if (!IsConnected) { LastError = "GhostBox 设备未连接"; return false; }
        return true;
    }
    
    private void UpdateDeviceInfo()
    {
        try
        {
            IntPtr modelPtr = NativeGetModel();
            if (modelPtr != IntPtr.Zero)
                DeviceModel = Marshal.PtrToStringAnsi(modelPtr) ?? string.Empty;
            
            IntPtr serialPtr = NativeGetSerialNumber();
            if (serialPtr != IntPtr.Zero)
                SerialNumber = Marshal.PtrToStringAnsi(serialPtr) ?? string.Empty;
        }
        catch
        {
            DeviceModel = "未知";
            SerialNumber = "未知";
        }
    }
    
    #endregion
    
    #region IDisposable 实现
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _reconnectManager?.Dispose();
        Disconnect();
        _disposed = true;
    }
    
    #endregion
}
