using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace ShineProCS.Infrastructure;

/// <summary>
/// GhostBox 设备管理器 - 单例模式
/// 管理 GhostBox 硬件设备的连接，供键盘和鼠标驱动共享
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
    
    // 设置 DLL 搜索目录
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);
    
    // 64位 DLL P/Invoke 声明
    private static class Native64
    {
        private const string DllName = "gbild64";
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "opendevice")]
        public static extern int OpenDevice(int index = 0);
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "isconnected")]
        public static extern int IsConnected();
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "closedevice")]
        public static extern int CloseDevice();
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "resetdevice")]
        public static extern int ResetDevice();
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "getmodel")]
        public static extern IntPtr GetModel();

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "getserialnumber")]
        public static extern IntPtr GetSerialNumber();
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "presskeybyvalue")]
        public static extern int PressKeyByValue(int keyValue);
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "releasekeybyvalue")]
        public static extern int ReleaseKeyByValue(int keyValue);
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "pressandreleasekeybyvalue")]
        public static extern int PressAndReleaseKeyByValue(int keyValue);
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "releaseallkey")]
        public static extern int ReleaseAllKey(int reserved = 0);
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "movemouseto")]
        public static extern int MoveMousTo(int x, int y);
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "movemouserelative")]
        public static extern int MoveMousRelative(int x, int y);
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "pressmousebutton")]
        public static extern int PressMouseButton(int button);
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "releasemousebutton")]
        public static extern int ReleaseMouseButton(int button);
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "pressandreleasemousebutton")]
        public static extern int PressAndReleaseMouseButton(int button);
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "releaseallmousebutton")]
        public static extern int ReleaseAllMouseButton();
    }
    
    // 32位 DLL P/Invoke 声明
    private static class Native32
    {
        private const string DllName = "gbild32";
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "opendevice")]
        public static extern int OpenDevice(int index = 0);
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "isconnected")]
        public static extern int IsConnected();
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "closedevice")]
        public static extern int CloseDevice();
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "resetdevice")]
        public static extern int ResetDevice();
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "getmodel")]
        public static extern IntPtr GetModel();
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "getserialnumber")]
        public static extern IntPtr GetSerialNumber();
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "presskeybyvalue")]
        public static extern int PressKeyByValue(int keyValue);
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "releasekeybyvalue")]
        public static extern int ReleaseKeyByValue(int keyValue);
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "pressandreleasekeybyvalue")]
        public static extern int PressAndReleaseKeyByValue(int keyValue);
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "releaseallkey")]
        public static extern int ReleaseAllKey(int reserved = 0);
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "movemouseto")]
        public static extern int MoveMousTo(int x, int y);
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "movemouserelative")]
        public static extern int MoveMousRelative(int x, int y);
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "pressmousebutton")]
        public static extern int PressMouseButton(int button);
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "releasemousebutton")]
        public static extern int ReleaseMouseButton(int button);
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "pressandreleasemousebutton")]
        public static extern int PressAndReleaseMouseButton(int button);
        
        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "releaseallmousebutton")]
        public static extern int ReleaseAllMouseButton();
    }
    
    // 运行时选择正确的 Native 调用
    private static readonly bool Is64Bit = Environment.Is64BitProcess;
    
    private static int NativeOpenDevice(int index) => Is64Bit ? Native64.OpenDevice(index) : Native32.OpenDevice(index);
    private static int NativeIsConnected() => Is64Bit ? Native64.IsConnected() : Native32.IsConnected();
    private static int NativeCloseDevice() => Is64Bit ? Native64.CloseDevice() : Native32.CloseDevice();
    private static int NativeResetDevice() => Is64Bit ? Native64.ResetDevice() : Native32.ResetDevice();
    private static IntPtr NativeGetModel() => Is64Bit ? Native64.GetModel() : Native32.GetModel();
    private static IntPtr NativeGetSerialNumber() => Is64Bit ? Native64.GetSerialNumber() : Native32.GetSerialNumber();
    private static int NativePressKeyByValue(int keyValue) => Is64Bit ? Native64.PressKeyByValue(keyValue) : Native32.PressKeyByValue(keyValue);
    private static int NativeReleaseKeyByValue(int keyValue) => Is64Bit ? Native64.ReleaseKeyByValue(keyValue) : Native32.ReleaseKeyByValue(keyValue);
    private static int NativePressAndReleaseKeyByValue(int keyValue) => Is64Bit ? Native64.PressAndReleaseKeyByValue(keyValue) : Native32.PressAndReleaseKeyByValue(keyValue);
    private static int NativeReleaseAllKey(int reserved) => Is64Bit ? Native64.ReleaseAllKey(reserved) : Native32.ReleaseAllKey(reserved);
    private static int NativeMoveMousTo(int x, int y) => Is64Bit ? Native64.MoveMousTo(x, y) : Native32.MoveMousTo(x, y);
    private static int NativeMoveMousRelative(int x, int y) => Is64Bit ? Native64.MoveMousRelative(x, y) : Native32.MoveMousRelative(x, y);
    private static int NativePressMouseButton(int button) => Is64Bit ? Native64.PressMouseButton(button) : Native32.PressMouseButton(button);
    private static int NativeReleaseMouseButton(int button) => Is64Bit ? Native64.ReleaseMouseButton(button) : Native32.ReleaseMouseButton(button);
    private static int NativePressAndReleaseMouseButton(int button) => Is64Bit ? Native64.PressAndReleaseMouseButton(button) : Native32.PressAndReleaseMouseButton(button);
    private static int NativeReleaseAllMouseButton() => Is64Bit ? Native64.ReleaseAllMouseButton() : Native32.ReleaseAllMouseButton();
    
    // 静态构造函数，设置 DLL 搜索路径
    static GhostBoxDeviceManager()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string ghostboxDir = Path.Combine(baseDir, "libs", "ghostbox");
            
            if (Directory.Exists(ghostboxDir))
            {
                // 添加 libs/ghostbox 目录到 DLL 搜索路径
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
    
    /// <summary>
    /// 设备是否已连接
    /// </summary>
    public bool IsConnected
    {
        get
        {
            lock (_lockObject)
            {
                return _isConnected;
            }
        }
        private set
        {
            lock (_lockObject)
            {
                _isConnected = value;
            }
        }
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
        get
        {
            lock (_lockObject)
            {
                return _lastError;
            }
        }
        private set
        {
            lock (_lockObject)
            {
                _lastError = value;
            }
        }
    }
    
    /// <summary>
    /// 设备型号（连接后可用）
    /// </summary>
    public string DeviceModel { get; private set; } = string.Empty;
    
    /// <summary>
    /// 设备序列号（连接后可用）
    /// </summary>
    public string SerialNumber { get; private set; } = string.Empty;
    
    #endregion

    #region 构造函数
    
    /// <summary>
    /// 私有构造函数，检测 DLL 可用性
    /// </summary>
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
    /// 连接 GhostBox 设备
    /// </summary>
    /// <param name="deviceIndex">设备索引，默认为 0</param>
    /// <returns>连接是否成功</returns>
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
            // 如果已连接，先断开
            if (IsConnected)
            {
                Disconnect();
            }
            
            int result = NativeOpenDevice(deviceIndex);
            if (result != 0)
            {
                // 验证连接状态
                if (NativeIsConnected() != 0)
                {
                    IsConnected = true;
                    LastError = string.Empty;
                    
                    // 获取设备信息
                    UpdateDeviceInfo();
                    
                    return true;
                }
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
            // 释放所有按键
            NativeReleaseAllKey(0);
            NativeReleaseAllMouseButton();
            
            // 关闭设备
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
    /// <returns>重置是否成功</returns>
    public bool Reset()
    {
        if (!IsDllAvailable)
        {
            LastError = "GhostBox DLL 文件不可用";
            return false;
        }
        
        try
        {
            int result = NativeResetDevice();
            return result != 0;
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
    /// <returns>当前连接状态</returns>
    public bool RefreshConnectionStatus()
    {
        if (!IsDllAvailable)
        {
            IsConnected = false;
            return false;
        }
        
        try
        {
            IsConnected = NativeIsConnected() != 0;
            return IsConnected;
        }
        catch (Exception)
        {
            IsConnected = false;
            return false;
        }
    }
    
    #endregion

    #region 键盘操作
    
    /// <summary>
    /// 按下按键
    /// </summary>
    /// <param name="keyCode">虚拟键码</param>
    /// <returns>操作是否成功</returns>
    public bool PressKey(int keyCode)
    {
        if (!EnsureConnected()) return false;
        
        try
        {
            return NativePressKeyByValue(keyCode) != 0;
        }
        catch (Exception ex)
        {
            LastError = $"按下按键失败: {ex.Message}";
            return false;
        }
    }
    
    /// <summary>
    /// 释放按键
    /// </summary>
    /// <param name="keyCode">虚拟键码</param>
    /// <returns>操作是否成功</returns>
    public bool ReleaseKey(int keyCode)
    {
        if (!EnsureConnected()) return false;
        
        try
        {
            return NativeReleaseKeyByValue(keyCode) != 0;
        }
        catch (Exception ex)
        {
            LastError = $"释放按键失败: {ex.Message}";
            return false;
        }
    }
    
    /// <summary>
    /// 按下并释放按键
    /// </summary>
    /// <param name="keyCode">虚拟键码</param>
    /// <returns>操作是否成功</returns>
    public bool PressAndReleaseKey(int keyCode)
    {
        if (!EnsureConnected()) return false;
        
        try
        {
            return NativePressAndReleaseKeyByValue(keyCode) != 0;
        }
        catch (Exception ex)
        {
            LastError = $"按键操作失败: {ex.Message}";
            return false;
        }
    }
    
    /// <summary>
    /// 释放所有按键
    /// </summary>
    /// <returns>操作是否成功</returns>
    public bool ReleaseAllKeys()
    {
        if (!EnsureConnected()) return false;
        
        try
        {
            return NativeReleaseAllKey(0) != 0;
        }
        catch (Exception ex)
        {
            LastError = $"释放所有按键失败: {ex.Message}";
            return false;
        }
    }
    
    #endregion

    #region 鼠标操作
    
    /// <summary>
    /// 移动鼠标到指定坐标
    /// </summary>
    /// <param name="x">X 坐标</param>
    /// <param name="y">Y 坐标</param>
    /// <returns>操作是否成功</returns>
    public bool MoveMousTo(int x, int y)
    {
        if (!EnsureConnected()) return false;
        
        try
        {
            return NativeMoveMousTo(x, y) != 0;
        }
        catch (Exception ex)
        {
            LastError = $"移动鼠标失败: {ex.Message}";
            return false;
        }
    }
    
    /// <summary>
    /// 相对移动鼠标
    /// </summary>
    /// <param name="deltaX">X 方向偏移</param>
    /// <param name="deltaY">Y 方向偏移</param>
    /// <returns>操作是否成功</returns>
    public bool MoveMousRelative(int deltaX, int deltaY)
    {
        if (!EnsureConnected()) return false;
        
        try
        {
            return NativeMoveMousRelative(deltaX, deltaY) != 0;
        }
        catch (Exception ex)
        {
            LastError = $"相对移动鼠标失败: {ex.Message}";
            return false;
        }
    }
    
    /// <summary>
    /// 按下鼠标按钮
    /// </summary>
    /// <param name="button">鼠标按钮: 1=左键, 2=右键, 3=中键</param>
    /// <returns>操作是否成功</returns>
    public bool PressMouseButton(int button)
    {
        if (!EnsureConnected()) return false;
        
        try
        {
            return NativePressMouseButton(button) != 0;
        }
        catch (Exception ex)
        {
            LastError = $"按下鼠标按钮失败: {ex.Message}";
            return false;
        }
    }
    
    /// <summary>
    /// 释放鼠标按钮
    /// </summary>
    /// <param name="button">鼠标按钮: 1=左键, 2=右键, 3=中键</param>
    /// <returns>操作是否成功</returns>
    public bool ReleaseMouseButton(int button)
    {
        if (!EnsureConnected()) return false;
        
        try
        {
            return NativeReleaseMouseButton(button) != 0;
        }
        catch (Exception ex)
        {
            LastError = $"释放鼠标按钮失败: {ex.Message}";
            return false;
        }
    }
    
    /// <summary>
    /// 点击鼠标按钮（按下并释放）
    /// </summary>
    /// <param name="button">鼠标按钮: 1=左键, 2=右键, 3=中键</param>
    /// <returns>操作是否成功</returns>
    public bool ClickMouseButton(int button)
    {
        if (!EnsureConnected()) return false;
        
        try
        {
            return NativePressAndReleaseMouseButton(button) != 0;
        }
        catch (Exception ex)
        {
            LastError = $"点击鼠标按钮失败: {ex.Message}";
            return false;
        }
    }
    
    /// <summary>
    /// 释放所有鼠标按钮
    /// </summary>
    /// <returns>操作是否成功</returns>
    public bool ReleaseAllMouseButtons()
    {
        if (!EnsureConnected()) return false;
        
        try
        {
            return NativeReleaseAllMouseButton() != 0;
        }
        catch (Exception ex)
        {
            LastError = $"释放所有鼠标按钮失败: {ex.Message}";
            return false;
        }
    }
    
    #endregion

    #region 私有方法
    
    /// <summary>
    /// 检查 DLL 文件是否可用
    /// </summary>
    private bool CheckDllAvailability()
    {
        // 检查可能的 DLL 路径
        string[] possiblePaths = GetPossibleDllPaths();
        
        foreach (string path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// 获取可能的 DLL 文件路径
    /// </summary>
    private string[] GetPossibleDllPaths()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        bool is64Bit = Environment.Is64BitProcess;
        
        // 根据进程架构选择对应的 DLL
        string dllName = is64Bit ? "gbild64.dll" : "gbild32.dll";
        string genericDllName = "gbild.dll";
        
        return new[]
        {
            // 直接在输出目录
            Path.Combine(baseDir, dllName),
            Path.Combine(baseDir, genericDllName),
            // libs/ghostbox 子目录
            Path.Combine(baseDir, "libs", "ghostbox", dllName),
            Path.Combine(baseDir, "libs", "ghostbox", genericDllName),
            // 当前工作目录
            Path.Combine(Environment.CurrentDirectory, dllName),
            Path.Combine(Environment.CurrentDirectory, genericDllName),
        };
    }
    
    /// <summary>
    /// 确保设备已连接
    /// </summary>
    private bool EnsureConnected()
    {
        if (!IsDllAvailable)
        {
            LastError = "GhostBox DLL 文件不可用";
            return false;
        }
        
        if (!IsConnected)
        {
            LastError = "GhostBox 设备未连接";
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// 更新设备信息
    /// </summary>
    private void UpdateDeviceInfo()
    {
        try
        {
            IntPtr modelPtr = NativeGetModel();
            if (modelPtr != IntPtr.Zero)
            {
                DeviceModel = Marshal.PtrToStringAnsi(modelPtr) ?? string.Empty;
            }
            
            IntPtr serialPtr = NativeGetSerialNumber();
            if (serialPtr != IntPtr.Zero)
            {
                SerialNumber = Marshal.PtrToStringAnsi(serialPtr) ?? string.Empty;
            }
        }
        catch (Exception)
        {
            // 获取设备信息失败不影响连接状态
            DeviceModel = "未知";
            SerialNumber = "未知";
        }
    }
    
    #endregion
    
    #region IDisposable 实现
    
    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        
        Disconnect();
        _disposed = true;
    }
    
    #endregion
}
