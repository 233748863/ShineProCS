using System;
using System.Collections.Generic;

namespace ShineProCS.Core.Services;

/// <summary>
/// 贝塞尔曲线鼠标移动 - 模拟人类鼠标轨迹
/// 需求 6.3: 支持贝塞尔曲线鼠标移动以模拟人类动作
/// </summary>
public class BezierMouseMover
{
    // 随机数生成器，用于生成控制点的随机偏移
    private static readonly Random _random = new();
    private static readonly object _lockObject = new();
    
    /// <summary>
    /// 生成从起点到终点的贝塞尔曲线路径点
    /// 使用三次贝塞尔曲线（Cubic Bezier）模拟人类鼠标移动轨迹
    /// </summary>
    /// <param name="startX">起始 X 坐标</param>
    /// <param name="startY">起始 Y 坐标</param>
    /// <param name="endX">目标 X 坐标</param>
    /// <param name="endY">目标 Y 坐标</param>
    /// <param name="steps">路径点数量（默认 20）</param>
    /// <returns>路径点列表，第一个点是起点，最后一个点是终点</returns>
    /// <remarks>
    /// 属性 9: 贝塞尔曲线端点正确
    /// 对于任意贝塞尔曲线路径，第一个点应该是起点，最后一个点应该是终点
    /// </remarks>
    public List<(int x, int y)> GeneratePath(int startX, int startY, int endX, int endY, int steps = 20)
    {
        var path = new List<(int x, int y)>();
        
        // 确保至少有 2 个点（起点和终点）
        steps = Math.Max(2, steps);
        
        // 计算起点和终点之间的距离
        double dx = endX - startX;
        double dy = endY - startY;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        
        // 如果距离很小，直接返回起点和终点
        if (distance < 5)
        {
            path.Add((startX, startY));
            path.Add((endX, endY));
            return path;
        }
        
        // 生成两个控制点，用于三次贝塞尔曲线
        // 控制点在起点和终点之间，带有随机偏移以模拟人类移动
        var (cp1X, cp1Y) = GenerateControlPoint(startX, startY, endX, endY, 0.3, distance);
        var (cp2X, cp2Y) = GenerateControlPoint(startX, startY, endX, endY, 0.7, distance);
        
        // 生成路径点
        for (int i = 0; i < steps; i++)
        {
            // t 从 0 到 1
            double t = (double)i / (steps - 1);
            
            // 三次贝塞尔曲线公式：
            // B(t) = (1-t)³P0 + 3(1-t)²tP1 + 3(1-t)t²P2 + t³P3
            double oneMinusT = 1 - t;
            double oneMinusT2 = oneMinusT * oneMinusT;
            double oneMinusT3 = oneMinusT2 * oneMinusT;
            double t2 = t * t;
            double t3 = t2 * t;
            
            double x = oneMinusT3 * startX +
                       3 * oneMinusT2 * t * cp1X +
                       3 * oneMinusT * t2 * cp2X +
                       t3 * endX;
            
            double y = oneMinusT3 * startY +
                       3 * oneMinusT2 * t * cp1Y +
                       3 * oneMinusT * t2 * cp2Y +
                       t3 * endY;
            
            path.Add(((int)Math.Round(x), (int)Math.Round(y)));
        }
        
        // 确保最后一个点精确等于终点（避免浮点误差）
        if (path.Count > 0)
        {
            path[path.Count - 1] = (endX, endY);
        }
        
        // 确保第一个点精确等于起点
        if (path.Count > 0)
        {
            path[0] = (startX, startY);
        }
        
        return path;
    }
    
    /// <summary>
    /// 生成控制点
    /// </summary>
    /// <param name="startX">起点 X</param>
    /// <param name="startY">起点 Y</param>
    /// <param name="endX">终点 X</param>
    /// <param name="endY">终点 Y</param>
    /// <param name="ratio">控制点在起点和终点之间的比例 (0-1)</param>
    /// <param name="distance">起点和终点之间的距离</param>
    /// <returns>控制点坐标</returns>
    private (double x, double y) GenerateControlPoint(
        int startX, int startY, int endX, int endY, double ratio, double distance)
    {
        // 基础位置：在起点和终点之间的某个比例位置
        double baseX = startX + (endX - startX) * ratio;
        double baseY = startY + (endY - startY) * ratio;
        
        // 计算垂直于移动方向的偏移量
        // 偏移量与距离成正比，但有上限
        double maxOffset = Math.Min(distance * 0.3, 100);
        
        // 生成随机偏移
        double offsetX, offsetY;
        lock (_lockObject)
        {
            // 偏移方向垂直于移动方向
            double angle = Math.Atan2(endY - startY, endX - startX) + Math.PI / 2;
            double offsetMagnitude = (_random.NextDouble() - 0.5) * 2 * maxOffset;
            
            offsetX = Math.Cos(angle) * offsetMagnitude;
            offsetY = Math.Sin(angle) * offsetMagnitude;
        }
        
        return (baseX + offsetX, baseY + offsetY);
    }
    
    /// <summary>
    /// 生成简单的直线路径（用于短距离移动）
    /// </summary>
    /// <param name="startX">起始 X 坐标</param>
    /// <param name="startY">起始 Y 坐标</param>
    /// <param name="endX">目标 X 坐标</param>
    /// <param name="endY">目标 Y 坐标</param>
    /// <param name="steps">路径点数量</param>
    /// <returns>路径点列表</returns>
    public List<(int x, int y)> GenerateLinearPath(int startX, int startY, int endX, int endY, int steps = 10)
    {
        var path = new List<(int x, int y)>();
        
        steps = Math.Max(2, steps);
        
        for (int i = 0; i < steps; i++)
        {
            double t = (double)i / (steps - 1);
            int x = (int)Math.Round(startX + (endX - startX) * t);
            int y = (int)Math.Round(startY + (endY - startY) * t);
            path.Add((x, y));
        }
        
        // 确保端点精确
        if (path.Count > 0)
        {
            path[0] = (startX, startY);
            path[path.Count - 1] = (endX, endY);
        }
        
        return path;
    }
}
