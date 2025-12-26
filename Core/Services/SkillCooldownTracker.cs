using System.Collections.Concurrent;
using ShineProCS.Models;

namespace ShineProCS.Core.Services;

/// <summary>
/// 技能冷却追踪器
/// 记录每个技能的实际CD时间，智能预测可用时间
/// </summary>
public class SkillCooldownTracker
{
    /// <summary>
    /// 技能CD记录
    /// </summary>
    public class CooldownRecord
    {
        /// <summary>
        /// 技能名称
        /// </summary>
        public string SkillName { get; set; } = "";
        
        /// <summary>
        /// 最后使用时间
        /// </summary>
        public DateTime LastUsedTime { get; set; }
        
        /// <summary>
        /// 预计可用时间
        /// </summary>
        public DateTime EstimatedReadyTime { get; set; }
        
        /// <summary>
        /// 历史CD时间列表（用于计算平均值）
        /// </summary>
        public List<double> CooldownHistory { get; } = [];
        
        /// <summary>
        /// 平均CD时间（秒）
        /// </summary>
        public double AverageCooldown => CooldownHistory.Count > 0 
            ? CooldownHistory.TakeLast(10).Average() 
            : 0;
        
        /// <summary>
        /// 总使用次数
        /// </summary>
        public int TotalUseCount { get; set; }
        
        /// <summary>
        /// 剩余CD时间（秒）
        /// </summary>
        public double RemainingCooldown
        {
            get
            {
                var remaining = (EstimatedReadyTime - DateTime.Now).TotalSeconds;
                return remaining > 0 ? remaining : 0;
            }
        }
        
        /// <summary>
        /// 是否预计已就绪
        /// </summary>
        public bool IsEstimatedReady => DateTime.Now >= EstimatedReadyTime;
    }

    private readonly ConcurrentDictionary<string, CooldownRecord> _records = new();
    private readonly object _lock = new();

    /// <summary>
    /// CD变化事件
    /// </summary>
    public event Action<string, CooldownRecord>? CooldownChanged;

    /// <summary>
    /// 记录技能使用
    /// </summary>
    /// <param name="skillName">技能名称</param>
    /// <param name="configuredCooldown">配置的CD时间（秒），0表示未配置</param>
    public void RecordSkillUse(string skillName, double configuredCooldown = 0)
    {
        var now = DateTime.Now;
        
        var record = _records.GetOrAdd(skillName, _ => new CooldownRecord { SkillName = skillName });
        
        lock (_lock)
        {
            // 如果有上次使用记录，计算实际CD
            if (record.LastUsedTime != default)
            {
                var actualCooldown = (now - record.LastUsedTime).TotalSeconds;
                
                // 只记录合理范围内的CD（0.5秒 - 300秒）
                if (actualCooldown >= 0.5 && actualCooldown <= 300)
                {
                    record.CooldownHistory.Add(actualCooldown);
                    
                    // 保留最近20条记录
                    while (record.CooldownHistory.Count > 20)
                        record.CooldownHistory.RemoveAt(0);
                }
            }
            
            record.LastUsedTime = now;
            record.TotalUseCount++;
            
            // 预测下次可用时间
            var estimatedCd = configuredCooldown > 0 
                ? configuredCooldown 
                : (record.AverageCooldown > 0 ? record.AverageCooldown : 1);
            
            record.EstimatedReadyTime = now.AddSeconds(estimatedCd);
        }
        
        CooldownChanged?.Invoke(skillName, record);
    }

    /// <summary>
    /// 记录技能就绪（视觉检测到可用）
    /// </summary>
    /// <param name="skillName">技能名称</param>
    public void RecordSkillReady(string skillName)
    {
        if (!_records.TryGetValue(skillName, out var record)) return;
        
        lock (_lock)
        {
            // 如果预计时间还没到但已经就绪，更新预计时间
            if (record.EstimatedReadyTime > DateTime.Now)
            {
                var actualCooldown = (DateTime.Now - record.LastUsedTime).TotalSeconds;
                
                // 记录实际CD
                if (actualCooldown >= 0.5 && actualCooldown <= 300)
                {
                    record.CooldownHistory.Add(actualCooldown);
                    while (record.CooldownHistory.Count > 20)
                        record.CooldownHistory.RemoveAt(0);
                }
                
                record.EstimatedReadyTime = DateTime.Now;
            }
        }
        
        CooldownChanged?.Invoke(skillName, record);
    }

    /// <summary>
    /// 获取技能CD记录
    /// </summary>
    public CooldownRecord? GetRecord(string skillName)
    {
        return _records.TryGetValue(skillName, out var record) ? record : null;
    }

    /// <summary>
    /// 获取所有CD记录
    /// </summary>
    public IReadOnlyDictionary<string, CooldownRecord> GetAllRecords() => _records;

    /// <summary>
    /// 获取预计最快就绪的技能
    /// </summary>
    /// <param name="enabledSkills">启用的技能列表</param>
    /// <returns>最快就绪的技能名称</returns>
    public string? GetNextReadySkill(IEnumerable<string> enabledSkills)
    {
        var enabledSet = enabledSkills.ToHashSet();
        
        return _records
            .Where(r => enabledSet.Contains(r.Key))
            .OrderBy(r => r.Value.EstimatedReadyTime)
            .FirstOrDefault().Key;
    }

    /// <summary>
    /// 获取技能统计信息
    /// </summary>
    public SkillStatistics GetStatistics(string skillName)
    {
        if (!_records.TryGetValue(skillName, out var record))
            return new SkillStatistics { SkillName = skillName };
        
        lock (_lock)
        {
            return new SkillStatistics
            {
                SkillName = skillName,
                TotalUseCount = record.TotalUseCount,
                AverageCooldown = record.AverageCooldown,
                MinCooldown = record.CooldownHistory.Count > 0 ? record.CooldownHistory.Min() : 0,
                MaxCooldown = record.CooldownHistory.Count > 0 ? record.CooldownHistory.Max() : 0,
                LastUsedTime = record.LastUsedTime
            };
        }
    }

    /// <summary>
    /// 清除所有记录
    /// </summary>
    public void Clear()
    {
        _records.Clear();
    }

    /// <summary>
    /// 清除指定技能的记录
    /// </summary>
    public void ClearSkill(string skillName)
    {
        _records.TryRemove(skillName, out _);
    }
}

/// <summary>
/// 技能统计信息
/// </summary>
public class SkillStatistics
{
    public string SkillName { get; set; } = "";
    public int TotalUseCount { get; set; }
    public double AverageCooldown { get; set; }
    public double MinCooldown { get; set; }
    public double MaxCooldown { get; set; }
    public DateTime LastUsedTime { get; set; }
}
