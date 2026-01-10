namespace ShineProCS.Models;

/// <summary>
/// 输入驱动类型枚举
/// 定义系统支持的输入驱动方式
/// </summary>
public enum InputDriverType
{
    /// <summary>
    /// Win32 软件模拟驱动 (默认)
    /// 使用 Windows user32.dll API 进行键盘/鼠标模拟
    /// </summary>
    Win32 = 0,
    
    /// <summary>
    /// GhostBox 硬件驱动
    /// 使用幽灵键鼠硬件设备进行硬件级模拟
    /// </summary>
    GhostBox = 1
}
