namespace ShineProCS.Core;

/// <summary>
/// 应用程序常量定义
/// </summary>
public static class Constants
{
    /// <summary>
    /// 引擎相关常量
    /// </summary>
    public static class Engine
    {
        /// <summary>连续失败次数阈值，超过后触发ESC重置</summary>
        public const int MaxConsecutiveFailures = 5;
        
        /// <summary>异常后的等待时间(ms)</summary>
        public const int ErrorRecoveryDelayMs = 1000;
        
        /// <summary>暂停状态检查间隔(ms)</summary>
        public const int PauseCheckIntervalMs = 100;
        
        /// <summary>图像队列等待超时(ms)</summary>
        public const int ImageQueueTimeoutMs = 200;
        
        /// <summary>截屏间隔(ms)</summary>
        public const int CaptureIntervalMs = 10;
        
        /// <summary>引擎停止等待超时(秒)</summary>
        public const int StopTimeoutSeconds = 3;
        
        /// <summary>帧采样步长</summary>
        public const int FrameSampleStride = 16;
        
        /// <summary>未变化帧阈值</summary>
        public const int UnchangedFrameThreshold = 10;
        
        /// <summary>战斗状态检测间隔(循环次数)</summary>
        public const int CombatDetectionInterval = 10;
        
        /// <summary>内存清理间隔(循环次数)</summary>
        public const int MemoryCleanupInterval = 50;
        
        /// <summary>窗口位置更新间隔(循环次数)</summary>
        public const int WindowPositionUpdateInterval = 100;
    }
    
    /// <summary>
    /// 检测相关常量
    /// </summary>
    public static class Detection
    {
        /// <summary>模板缓存最大数量</summary>
        public const int MaxTemplateCacheSize = 50;
        
        /// <summary>并行检测最小技能数</summary>
        public const int ParallelDetectionThreshold = 3;
        
        /// <summary>并行检测最大并发数</summary>
        public const int MaxParallelDegree = 4;
        
        /// <summary>读条检测间隔(ms)</summary>
        public const int CastDetectionIntervalMs = 50;
        
        /// <summary>引导技能最大时间(ms)</summary>
        public const int MaxChannelDurationMs = 10000;
        
        /// <summary>内存清理阈值(MB)</summary>
        public const int MemoryCleanupThresholdMb = 150;
    }
    
    /// <summary>
    /// UI相关常量
    /// </summary>
    public static class UI
    {
        /// <summary>日志最大条数</summary>
        public const int MaxLogMessages = 500;
        
        /// <summary>内存监控间隔(秒)</summary>
        public const int MemoryMonitorIntervalSeconds = 2;
        
        /// <summary>CD更新间隔(ms)</summary>
        public const int CooldownUpdateIntervalMs = 500;
        
        /// <summary>区域高亮显示时间(秒)</summary>
        public const int RegionHighlightDurationSeconds = 3;
        
        /// <summary>模板测试高亮时间(秒)</summary>
        public const int TemplateTestHighlightSeconds = 5;
    }
    
    /// <summary>
    /// 虚拟键码
    /// </summary>
    public static class VirtualKeys
    {
        /// <summary>ESC键</summary>
        public const int Escape = 27;
    }
}
