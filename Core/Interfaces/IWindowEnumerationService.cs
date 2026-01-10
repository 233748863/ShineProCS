using System;
using System.Collections.Generic;
using ShineProCS.Models;

namespace ShineProCS.Core.Interfaces;

/// <summary>
/// 窗口枚举服务接口
/// 定义获取系统窗口列表、验证窗口有效性等操作
/// </summary>
public interface IWindowEnumerationService
{
    /// <summary>
    /// 获取所有可见窗口列表
    /// </summary>
    /// <returns>窗口信息列表，按进程名和窗口标题排序</returns>
    List<WindowInfo> GetVisibleWindows();
    
    /// <summary>
    /// 检查窗口是否仍然存在且可见
    /// </summary>
    /// <param name="handle">窗口句柄</param>
    /// <returns>窗口是否有效（存在且可见）</returns>
    bool IsWindowValid(IntPtr handle);
    
    /// <summary>
    /// 根据标题查找窗口
    /// </summary>
    /// <param name="title">窗口标题（支持精确匹配和部分匹配）</param>
    /// <returns>匹配的窗口信息，未找到返回null</returns>
    WindowInfo? FindWindowByTitle(string title);
}
