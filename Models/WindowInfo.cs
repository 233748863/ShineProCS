using System;

namespace ShineProCS.Models;

/// <summary>
/// 窗口信息模型，用于存储枚举到的窗口信息
/// </summary>
public class WindowInfo
{
    /// <summary>
    /// 窗口句柄
    /// </summary>
    public IntPtr Handle { get; set; }
    
    /// <summary>
    /// 窗口标题
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// 进程名称
    /// </summary>
    public string ProcessName { get; set; } = string.Empty;
    
    /// <summary>
    /// 进程ID
    /// </summary>
    public int ProcessId { get; set; }
    
    /// <summary>
    /// 显示文本，格式为 "[进程名] 窗口标题"
    /// </summary>
    public string DisplayText => $"[{ProcessName}] {Title}";
    
    /// <summary>
    /// 重写 Equals 方法，用于比较两个 WindowInfo 是否相等
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is WindowInfo other)
        {
            return Handle == other.Handle && Title == other.Title;
        }
        return false;
    }
    
    /// <summary>
    /// 重写 GetHashCode 方法
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(Handle, Title);
    }
    
    /// <summary>
    /// 重写 ToString 方法，返回显示文本
    /// </summary>
    public override string ToString()
    {
        return DisplayText;
    }
}
