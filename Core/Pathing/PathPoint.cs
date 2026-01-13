using System.Text.Json.Serialization;

namespace ShineProCS.Core.Pathing;

/// <summary>
/// 路径点动作类型
/// 需求: 20.4 - 支持在路径点执行自定义动作
/// </summary>
public enum PathPointAction
{
    /// <summary>
    /// 无动作，仅移动到该点
    /// </summary>
    None = 0,
    
    /// <summary>
    /// 采集动作（按 F 键）
    /// </summary>
    Collect = 1,
    
    /// <summary>
    /// 战斗动作（启用技能循环）
    /// </summary>
    Combat = 2,
    
    /// <summary>
    /// 交互动作（按 F 键并等待）
    /// </summary>
    Interact = 3,
    
    /// <summary>
    /// 等待动作（在该点等待指定时间）
    /// </summary>
    Wait = 4,
    
    /// <summary>
    /// 跳跃动作
    /// </summary>
    Jump = 5,
    
    /// <summary>
    /// 冲刺动作
    /// </summary>
    Sprint = 6,
    
    /// <summary>
    /// 传送动作（需要打开地图）
    /// </summary>
    Teleport = 7,
    
    /// <summary>
    /// 自定义按键动作
    /// </summary>
    CustomKey = 8
}

/// <summary>
/// 路径点移动类型
/// </summary>
public enum PathPointMoveType
{
    /// <summary>
    /// 行走
    /// </summary>
    Walk = 0,
    
    /// <summary>
    /// 奔跑
    /// </summary>
    Run = 1,
    
    /// <summary>
    /// 冲刺
    /// </summary>
    Sprint = 2,
    
    /// <summary>
    /// 飞行/滑翔
    /// </summary>
    Fly = 3,
    
    /// <summary>
    /// 游泳
    /// </summary>
    Swim = 4
}

/// <summary>
/// 路径点数据模型
/// 需求: 20.1 - 地图追踪通过小地图识别当前位置和方向
/// 需求: 20.2 - 支持加载预设的路径文件（JSON 格式）
/// </summary>
public class PathPoint
{
    /// <summary>
    /// 路径点唯一标识
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    /// <summary>
    /// 路径点名称（可选，用于调试和显示）
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    
    /// <summary>
    /// X 坐标（游戏世界坐标或小地图坐标）
    /// </summary>
    [JsonPropertyName("x")]
    public double X { get; set; }
    
    /// <summary>
    /// Y 坐标（游戏世界坐标或小地图坐标）
    /// </summary>
    [JsonPropertyName("y")]
    public double Y { get; set; }
    
    /// <summary>
    /// Z 坐标（高度，可选）
    /// </summary>
    [JsonPropertyName("z")]
    public double Z { get; set; }
    
    /// <summary>
    /// 到达该点后执行的动作
    /// 需求: 20.4 - 支持在路径点执行自定义动作
    /// </summary>
    [JsonPropertyName("action")]
    public PathPointAction Action { get; set; } = PathPointAction.None;
    
    /// <summary>
    /// 移动到该点的方式
    /// </summary>
    [JsonPropertyName("moveType")]
    public PathPointMoveType MoveType { get; set; } = PathPointMoveType.Run;
    
    /// <summary>
    /// 动作参数（如等待时间、自定义按键码等）
    /// </summary>
    [JsonPropertyName("actionParam")]
    public string ActionParam { get; set; } = "";
    
    /// <summary>
    /// 到达该点的容差半径（像素或游戏单位）
    /// </summary>
    [JsonPropertyName("tolerance")]
    public double Tolerance { get; set; } = 10.0;
    
    /// <summary>
    /// 到达该点的超时时间（毫秒，0 表示使用默认值）
    /// </summary>
    [JsonPropertyName("timeout")]
    public int TimeoutMs { get; set; } = 0;
    
    /// <summary>
    /// 是否为关键点（关键点必须到达，非关键点可以跳过）
    /// </summary>
    [JsonPropertyName("isKeyPoint")]
    public bool IsKeyPoint { get; set; } = true;
    
    /// <summary>
    /// 备注信息
    /// </summary>
    [JsonPropertyName("comment")]
    public string Comment { get; set; } = "";
    
    /// <summary>
    /// 计算到另一个点的距离
    /// </summary>
    public double DistanceTo(PathPoint other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
    
    /// <summary>
    /// 计算到指定坐标的距离
    /// </summary>
    public double DistanceTo(double x, double y)
    {
        var dx = X - x;
        var dy = Y - y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
    
    /// <summary>
    /// 计算从当前位置到该点的方向角度（弧度）
    /// </summary>
    public double AngleFrom(double fromX, double fromY)
    {
        return Math.Atan2(Y - fromY, X - fromX);
    }
    
    /// <summary>
    /// 计算从当前位置到该点的方向角度（度数，0-360）
    /// </summary>
    public double AngleFromDegrees(double fromX, double fromY)
    {
        var radians = AngleFrom(fromX, fromY);
        var degrees = radians * 180.0 / Math.PI;
        return (degrees + 360) % 360;
    }
    
    public override string ToString()
    {
        return string.IsNullOrEmpty(Name) 
            ? $"Point[{Id}]({X:F1}, {Y:F1})" 
            : $"{Name}[{Id}]({X:F1}, {Y:F1})";
    }
}

/// <summary>
/// 路径文件数据模型
/// 需求: 20.2 - 支持加载预设的路径文件（JSON 格式）
/// </summary>
public class PathData
{
    /// <summary>
    /// 路径名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    
    /// <summary>
    /// 路径描述
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
    
    /// <summary>
    /// 路径版本
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";
    
    /// <summary>
    /// 创建时间
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// 路径点列表
    /// </summary>
    [JsonPropertyName("points")]
    public List<PathPoint> Points { get; set; } = [];
    
    /// <summary>
    /// 是否循环执行
    /// </summary>
    [JsonPropertyName("loop")]
    public bool Loop { get; set; } = false;
    
    /// <summary>
    /// 循环次数（0 表示无限循环）
    /// </summary>
    [JsonPropertyName("loopCount")]
    public int LoopCount { get; set; } = 0;
    
    /// <summary>
    /// 默认移动类型
    /// </summary>
    [JsonPropertyName("defaultMoveType")]
    public PathPointMoveType DefaultMoveType { get; set; } = PathPointMoveType.Run;
    
    /// <summary>
    /// 默认到达容差
    /// </summary>
    [JsonPropertyName("defaultTolerance")]
    public double DefaultTolerance { get; set; } = 10.0;
    
    /// <summary>
    /// 默认超时时间（毫秒）
    /// </summary>
    [JsonPropertyName("defaultTimeoutMs")]
    public int DefaultTimeoutMs { get; set; } = 30000;
    
    /// <summary>
    /// 获取路径点数量
    /// </summary>
    [JsonIgnore]
    public int PointCount => Points.Count;
    
    /// <summary>
    /// 获取指定索引的路径点
    /// </summary>
    public PathPoint? GetPoint(int index)
    {
        if (index < 0 || index >= Points.Count)
            return null;
        return Points[index];
    }
    
    /// <summary>
    /// 根据 ID 获取路径点
    /// </summary>
    public PathPoint? GetPointById(int id)
    {
        return Points.FirstOrDefault(p => p.Id == id);
    }
}
