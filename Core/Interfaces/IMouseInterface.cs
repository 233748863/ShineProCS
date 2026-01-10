namespace ShineProCS.Core.Interfaces;

/// <summary>
/// 鼠标输入接口
/// 定义模拟鼠标操作的方法
/// </summary>
public interface IMouseInterface
{
    /// <summary>
    /// 移动鼠标到指定屏幕坐标
    /// </summary>
    /// <param name="x">目标 X 坐标</param>
    /// <param name="y">目标 Y 坐标</param>
    /// <returns>操作是否成功</returns>
    bool MoveTo(int x, int y);
    
    /// <summary>
    /// 按下鼠标按钮（不释放）
    /// </summary>
    /// <param name="button">鼠标按钮: 1=左键, 2=右键, 3=中键</param>
    /// <returns>操作是否成功</returns>
    bool PressButton(int button);
    
    /// <summary>
    /// 释放鼠标按钮
    /// </summary>
    /// <param name="button">鼠标按钮: 1=左键, 2=右键, 3=中键</param>
    /// <returns>操作是否成功</returns>
    bool ReleaseButton(int button);
    
    /// <summary>
    /// 点击鼠标按钮（按下并释放）
    /// </summary>
    /// <param name="button">鼠标按钮: 1=左键, 2=右键, 3=中键</param>
    /// <returns>操作是否成功</returns>
    bool Click(int button);
}
