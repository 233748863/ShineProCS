using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Services;
using ShineProCS.Models;

namespace ShineProCS.Infrastructure;

/// <summary>
/// GhostBox 硬件鼠标驱动
/// 使用 GhostBox 硬件设备进行鼠标输入模拟
/// 需求 6.3: 支持贝塞尔曲线鼠标移动以模拟人类动作
/// </summary>
public class GhostBoxMouseInterface : IMouseInterface
{
    private readonly GhostBoxDeviceManager _deviceManager;
    private readonly BezierMouseMover _bezierMover;
    private readonly RandomDelayGenerator _delayGenerator;
    private readonly AppSettings _settings;
    
    // 当前鼠标位置（用于贝塞尔曲线计算）
    private int _currentX;
    private int _currentY;
    private readonly object _positionLock = new();
    
    /// <summary>
    /// 创建 GhostBox 鼠标驱动实例
    /// </summary>
    /// <param name="deviceManager">GhostBox 设备管理器实例</param>
    /// <param name="settings">应用程序设置（可选，用于获取贝塞尔曲线配置）</param>
    /// <exception cref="ArgumentNullException">当 deviceManager 为 null 时抛出</exception>
    public GhostBoxMouseInterface(GhostBoxDeviceManager deviceManager, AppSettings? settings = null)
    {
        _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
        _bezierMover = new BezierMouseMover();
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
    /// 移动鼠标到指定屏幕坐标
    /// 需求 6.3: 支持贝塞尔曲线鼠标移动
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
        
        // 如果启用贝塞尔曲线移动
        if (_settings.UseBezierMouseMove)
        {
            return MoveWithBezier(x, y);
        }
        
        // 直接移动
        bool result = _deviceManager.MoveMousTo(x, y);
        if (result)
        {
            UpdateCurrentPosition(x, y);
        }
        return result;
    }
    
    /// <summary>
    /// 使用贝塞尔曲线移动鼠标
    /// 需求 6.3: 支持贝塞尔曲线鼠标移动以模拟人类动作
    /// </summary>
    /// <param name="targetX">目标 X 坐标</param>
    /// <param name="targetY">目标 Y 坐标</param>
    /// <returns>操作是否成功</returns>
    private bool MoveWithBezier(int targetX, int targetY)
    {
        int startX, startY;
        lock (_positionLock)
        {
            startX = _currentX;
            startY = _currentY;
        }
        
        // 生成贝塞尔曲线路径
        int steps = _settings.BezierMouseSteps;
        var path = _bezierMover.GeneratePath(startX, startY, targetX, targetY, steps);
        
        // 沿路径移动
        foreach (var (x, y) in path)
        {
            if (!_deviceManager.MoveMousTo(x, y))
            {
                return false;
            }
            
            // 在路径点之间添加小延迟，使移动更自然
            // 延迟时间根据路径点数量动态调整
            int delayMs = Math.Max(1, 100 / steps);
            Thread.Sleep(delayMs);
        }
        
        // 更新当前位置
        UpdateCurrentPosition(targetX, targetY);
        return true;
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
        
        bool result = _deviceManager.MoveMousRelative(deltaX, deltaY);
        if (result)
        {
            lock (_positionLock)
            {
                _currentX += deltaX;
                _currentY += deltaY;
            }
        }
        return result;
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
    
    /// <summary>
    /// 设置当前鼠标位置（用于初始化或同步）
    /// </summary>
    /// <param name="x">当前 X 坐标</param>
    /// <param name="y">当前 Y 坐标</param>
    public void SetCurrentPosition(int x, int y)
    {
        UpdateCurrentPosition(x, y);
    }
    
    /// <summary>
    /// 获取当前记录的鼠标位置
    /// </summary>
    /// <returns>当前位置 (x, y)</returns>
    public (int x, int y) GetCurrentPosition()
    {
        lock (_positionLock)
        {
            return (_currentX, _currentY);
        }
    }
    
    /// <summary>
    /// 更新当前位置
    /// </summary>
    private void UpdateCurrentPosition(int x, int y)
    {
        lock (_positionLock)
        {
            _currentX = x;
            _currentY = y;
        }
    }
}
