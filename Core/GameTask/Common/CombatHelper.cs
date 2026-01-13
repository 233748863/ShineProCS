using ShineProCS.Core.Interfaces;
using ShineProCS.Core.GameTask.Triggers;
using ShineProCS.Core.Recognition.YOLO;
using OpenCvSharp;
using Point = OpenCvSharp.Point;

namespace ShineProCS.Core.GameTask.Common;

/// <summary>
/// 战斗辅助类
/// 提供战斗相关的通用检测和操作方法
/// 需求: 19.3 - 集成技能循环引擎进行自动战斗
/// </summary>
public class CombatHelper
{
    private readonly ICaptureService _captureService;
    private readonly IInputService _inputService;
    private readonly ILogService _logService;
    private readonly SkillLoopTrigger? _skillLoopTrigger;
    private readonly IYoloService? _yoloService;
    
    // 虚拟键码
    private const int VK_SPACE = 0x20;
    private const int VK_SHIFT = 0x10;
    private const int VK_W = 0x57;
    
    /// <summary>
    /// 战斗状态
    /// </summary>
    public class CombatStatus
    {
        /// <summary>
        /// 是否在战斗中
        /// </summary>
        public bool IsInCombat { get; set; }
        
        /// <summary>
        /// 检测到的敌人数量
        /// </summary>
        public int EnemyCount { get; set; }
        
        /// <summary>
        /// 自身血量百分比
        /// </summary>
        public double HpPercent { get; set; } = 100;
        
        /// <summary>
        /// 自身蓝量百分比
        /// </summary>
        public double MpPercent { get; set; } = 100;
        
        /// <summary>
        /// 是否处于危险状态（低血量）
        /// </summary>
        public bool IsDangerous => HpPercent < 30;
        
        /// <summary>
        /// 战斗持续时间（秒）
        /// </summary>
        public double CombatDurationSeconds { get; set; }
    }
    
    /// <summary>
    /// 敌人信息
    /// </summary>
    public class EnemyInfo
    {
        /// <summary>
        /// 敌人类型
        /// </summary>
        public string Type { get; set; } = "";
        
        /// <summary>
        /// 位置（屏幕坐标）
        /// </summary>
        public Point Position { get; set; }
        
        /// <summary>
        /// 边界框
        /// </summary>
        public Rect BoundingBox { get; set; }
        
        /// <summary>
        /// 置信度
        /// </summary>
        public double Confidence { get; set; }
        
        /// <summary>
        /// 距离（估算）
        /// </summary>
        public double Distance { get; set; }
    }
    
    public CombatHelper(
        ICaptureService captureService,
        IInputService inputService,
        ILogService logService,
        SkillLoopTrigger? skillLoopTrigger = null,
        IYoloService? yoloService = null)
    {
        _captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
        _inputService = inputService ?? throw new ArgumentNullException(nameof(inputService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _skillLoopTrigger = skillLoopTrigger;
        _yoloService = yoloService;
    }
    
    /// <summary>
    /// 启动自动战斗
    /// 需求: 19.3 - 集成技能循环引擎进行自动战斗
    /// </summary>
    public void StartAutoCombat()
    {
        if (_skillLoopTrigger != null)
        {
            _skillLoopTrigger.IsEnabled = true;
            _skillLoopTrigger.Start();
            Log("自动战斗已启动", 1);
        }
        else
        {
            Log("技能循环触发器不可用，无法启动自动战斗", 2);
        }
    }
    
    /// <summary>
    /// 停止自动战斗
    /// </summary>
    public void StopAutoCombat()
    {
        if (_skillLoopTrigger != null)
        {
            _skillLoopTrigger.Stop();
            Log("自动战斗已停止", 1);
        }
    }
    
    /// <summary>
    /// 暂停自动战斗
    /// </summary>
    public void PauseAutoCombat()
    {
        if (_skillLoopTrigger != null)
        {
            _skillLoopTrigger.TogglePause();
            Log("自动战斗已暂停", 1);
        }
    }
    
    /// <summary>
    /// 获取当前战斗状态
    /// </summary>
    public CombatStatus GetCombatStatus(int[] detectionRegion)
    {
        var status = new CombatStatus();
        
        try
        {
            // 从技能循环获取状态
            if (_skillLoopTrigger != null)
            {
                var engineStatus = _skillLoopTrigger.GetStatus();
                status.HpPercent = engineStatus.HpPercent;
                status.MpPercent = engineStatus.MpPercent;
                status.IsInCombat = engineStatus.IsRunning && !engineStatus.IsPaused;
            }
            
            // 使用 YOLO 检测敌人
            if (_yoloService != null && _yoloService.IsInitialized)
            {
                var screenshot = _captureService.GetScreenRegion(
                    detectionRegion[0], detectionRegion[1],
                    detectionRegion[2], detectionRegion[3]);
                
                if (screenshot != null)
                {
                    try
                    {
                        var results = _yoloService.Detect(screenshot, new[] { "enemy", "monster", "boss" });
                        status.EnemyCount = results.Detections.Count;
                        status.IsInCombat = status.EnemyCount > 0;
                    }
                    finally
                    {
                        _captureService.ReturnMat(screenshot);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"获取战斗状态异常: {ex.Message}", 2);
        }
        
        return status;
    }
    
    /// <summary>
    /// 检测敌人
    /// </summary>
    public List<EnemyInfo> DetectEnemies(int[] detectionRegion)
    {
        var enemies = new List<EnemyInfo>();
        
        if (_yoloService == null || !_yoloService.IsInitialized)
        {
            return enemies;
        }
        
        try
        {
            var screenshot = _captureService.GetScreenRegion(
                detectionRegion[0], detectionRegion[1],
                detectionRegion[2], detectionRegion[3]);
            
            if (screenshot == null)
            {
                return enemies;
            }
            
            try
            {
                var results = _yoloService.Detect(screenshot, new[] { "enemy", "monster", "boss", "hilichurl", "slime" });
                
                foreach (var result in results.Detections)
                {
                    var enemy = new EnemyInfo
                    {
                        Type = result.ClassName,
                        BoundingBox = result.BoundingBox,
                        Position = new Point(
                            result.BoundingBox.X + result.BoundingBox.Width / 2,
                            result.BoundingBox.Y + result.BoundingBox.Height / 2),
                        Confidence = result.Confidence,
                        Distance = EstimateDistance(result.BoundingBox, detectionRegion)
                    };
                    
                    enemies.Add(enemy);
                }
                
                // 按距离排序
                enemies = enemies.OrderBy(e => e.Distance).ToList();
            }
            finally
            {
                _captureService.ReturnMat(screenshot);
            }
        }
        catch (Exception ex)
        {
            Log($"检测敌人异常: {ex.Message}", 2);
        }
        
        return enemies;
    }
    
    /// <summary>
    /// 估算敌人距离（基于边界框大小）
    /// </summary>
    private double EstimateDistance(Rect boundingBox, int[] detectionRegion)
    {
        // 简单估算：边界框越大，距离越近
        var boxArea = boundingBox.Width * boundingBox.Height;
        var regionArea = detectionRegion[2] * detectionRegion[3];
        
        if (regionArea == 0) return double.MaxValue;
        
        var ratio = (double)boxArea / regionArea;
        
        // 反比关系：ratio 越大，距离越近
        return 1.0 / (ratio + 0.001);
    }
    
    /// <summary>
    /// 移动到最近的敌人
    /// </summary>
    public async Task MoveToNearestEnemyAsync(int[] detectionRegion, CancellationToken ct)
    {
        var enemies = DetectEnemies(detectionRegion);
        
        if (enemies.Count == 0)
        {
            Log("未检测到敌人", 0);
            return;
        }
        
        var nearest = enemies[0];
        var screenCenter = new Point(detectionRegion[2] / 2, detectionRegion[3] / 2);
        
        // 计算移动方向
        var dx = nearest.Position.X - screenCenter.X;
        var dy = nearest.Position.Y - screenCenter.Y;
        
        // 简单的移动逻辑
        if (Math.Abs(dx) > 50)
        {
            // 需要左右移动
            var key = dx > 0 ? 0x44 : 0x41; // D or A
            _inputService.Keyboard.PressKey(key);
            await Task.Delay(200, ct);
            _inputService.Keyboard.ReleaseKey(key);
        }
        
        // 向前移动
        _inputService.Keyboard.PressKey(VK_W);
        await Task.Delay(300, ct);
        _inputService.Keyboard.ReleaseKey(VK_W);
    }
    
    /// <summary>
    /// 执行闪避
    /// </summary>
    public async Task DodgeAsync(CancellationToken ct)
    {
        // 按住 Shift 并移动
        _inputService.Keyboard.PressKey(VK_SHIFT);
        await Task.Delay(50, ct);
        
        // 随机方向闪避
        var random = new Random();
        var direction = random.Next(4);
        var key = direction switch
        {
            0 => VK_W,
            1 => 0x41, // A
            2 => 0x53, // S
            3 => 0x44, // D
            _ => VK_W
        };
        
        _inputService.Keyboard.PressKey(key);
        await Task.Delay(200, ct);
        _inputService.Keyboard.ReleaseKey(key);
        _inputService.Keyboard.ReleaseKey(VK_SHIFT);
    }
    
    /// <summary>
    /// 执行跳跃
    /// </summary>
    public async Task JumpAsync(CancellationToken ct)
    {
        _inputService.Keyboard.PressAndRelease(VK_SPACE);
        await Task.Delay(500, ct);
    }
    
    /// <summary>
    /// 等待战斗结束
    /// </summary>
    public async Task<bool> WaitForCombatEndAsync(int[] detectionRegion, int timeoutSeconds, CancellationToken ct)
    {
        var startTime = DateTime.Now;
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        var noEnemyCount = 0;
        const int noEnemyThreshold = 3; // 连续3次检测不到敌人认为战斗结束
        
        while (!ct.IsCancellationRequested && DateTime.Now - startTime < timeout)
        {
            var status = GetCombatStatus(detectionRegion);
            
            if (status.EnemyCount == 0)
            {
                noEnemyCount++;
                if (noEnemyCount >= noEnemyThreshold)
                {
                    Log("战斗结束", 1);
                    return true;
                }
            }
            else
            {
                noEnemyCount = 0;
            }
            
            await Task.Delay(1000, ct);
        }
        
        Log("等待战斗结束超时", 2);
        return false;
    }
    
    private void Log(string message, int level)
    {
        var logLevel = level switch
        {
            0 => Interfaces.LogLevel.Debug,
            1 => Interfaces.LogLevel.Info,
            2 => Interfaces.LogLevel.Warning,
            3 => Interfaces.LogLevel.Error,
            _ => Interfaces.LogLevel.Info
        };
        
        _logService.Log($"[战斗助手] {message}", logLevel, "CombatHelper");
    }
}
