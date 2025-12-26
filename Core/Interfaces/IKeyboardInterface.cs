namespace ShineProCS.Core.Interfaces;

/// <summary>
/// 键盘输入接口
/// 定义模拟键盘按键操作的方法
/// </summary>
public interface IKeyboardInterface
{
    /// <summary>
    /// 按下指定按键（不释放）
    /// </summary>
    /// <param name="keyCode">虚拟键码 (VK_*)</param>
    /// <returns>操作是否成功</returns>
    bool PressKey(int keyCode);
    
    /// <summary>
    /// 释放指定按键
    /// </summary>
    /// <param name="keyCode">虚拟键码 (VK_*)</param>
    /// <returns>操作是否成功</returns>
    bool ReleaseKey(int keyCode);
    
    /// <summary>
    /// 按下并释放指定按键（完整的按键操作）
    /// </summary>
    /// <param name="keyCode">虚拟键码 (VK_*)</param>
    /// <returns>操作是否成功</returns>
    bool PressAndRelease(int keyCode);
}
