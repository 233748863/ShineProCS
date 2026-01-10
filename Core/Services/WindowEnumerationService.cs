using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ShineProCS.Core.Interfaces;
using ShineProCS.Models;

namespace ShineProCS.Core.Services;

/// <summary>
/// 窗口枚举服务实现
/// 使用 Win32 API 枚举系统中的可见窗口
/// </summary>
public class WindowEnumerationService : IWindowEnumerationService
{
    #region Win32 API 声明
    
    /// <summary>
    /// 枚举窗口回调委托
    /// </summary>
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    
    /// <summary>
    /// 枚举所有顶级窗口
    /// </summary>
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    
    /// <summary>
    /// 检查窗口是否可见
    /// </summary>
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
    
    /// <summary>
    /// 获取窗口标题
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    
    /// <summary>
    /// 获取窗口标题长度
    /// </summary>
    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);
    
    /// <summary>
    /// 获取窗口所属进程ID
    /// </summary>
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    
    /// <summary>
    /// 检查窗口句柄是否有效
    /// </summary>
    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);
    
    #endregion
    
    /// <summary>
    /// 获取所有可见窗口列表
    /// </summary>
    /// <returns>窗口信息列表，按进程名和窗口标题排序</returns>
    public List<WindowInfo> GetVisibleWindows()
    {
        var windows = new List<WindowInfo>();
        
        EnumWindows((hWnd, lParam) =>
        {
            // 跳过不可见窗口
            if (!IsWindowVisible(hWnd))
                return true;
            
            // 获取窗口标题长度
            int length = GetWindowTextLength(hWnd);
            if (length == 0)
                return true;
            
            // 获取窗口标题
            var sb = new StringBuilder(length + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            string title = sb.ToString();
            
            // 跳过空标题窗口
            if (string.IsNullOrWhiteSpace(title))
                return true;
            
            // 获取进程信息
            GetWindowThreadProcessId(hWnd, out uint processId);
            string processName = GetProcessName((int)processId);
            
            // 添加到列表
            windows.Add(new WindowInfo
            {
                Handle = hWnd,
                Title = title,
                ProcessName = processName,
                ProcessId = (int)processId
            });
            
            return true;
        }, IntPtr.Zero);
        
        // 按进程名和窗口标题排序
        return windows.OrderBy(w => w.ProcessName).ThenBy(w => w.Title).ToList();
    }
    
    /// <summary>
    /// 检查窗口是否仍然存在且可见
    /// </summary>
    /// <param name="handle">窗口句柄</param>
    /// <returns>窗口是否有效（存在且可见）</returns>
    public bool IsWindowValid(IntPtr handle)
    {
        return IsWindow(handle) && IsWindowVisible(handle);
    }
    
    /// <summary>
    /// 根据标题查找窗口
    /// </summary>
    /// <param name="title">窗口标题（支持精确匹配和部分匹配）</param>
    /// <returns>匹配的窗口信息，未找到返回null</returns>
    public WindowInfo? FindWindowByTitle(string title)
    {
        if (string.IsNullOrEmpty(title))
            return null;
        
        var windows = GetVisibleWindows();
        
        // 优先精确匹配
        var exactMatch = windows.FirstOrDefault(w => 
            w.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
        if (exactMatch != null)
            return exactMatch;
        
        // 其次部分匹配
        return windows.FirstOrDefault(w => 
            w.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// 根据进程ID获取进程名称
    /// </summary>
    /// <param name="processId">进程ID</param>
    /// <returns>进程名称，获取失败返回 "Unknown"</returns>
    private static string GetProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch
        {
            return "Unknown";
        }
    }
}
