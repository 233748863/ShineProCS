using CommunityToolkit.Mvvm.ComponentModel;

namespace ShineProCS.Models;

/// <summary>
/// Buff配置模型
/// 定义单个Buff/Debuff的检测配置
/// </summary>
public partial class BuffConfig : ObservableObject
{
    /// <summary>
    /// Buff唯一标识名称
    /// </summary>
    [ObservableProperty] private string _name = "";
    
    /// <summary>
    /// Buff显示名称（用于UI显示）
    /// </summary>
    [ObservableProperty] private string _displayName = "";
    
    /// <summary>
    /// Buff图标检测区域 [X, Y, Width, Height]
    /// </summary>
    [ObservableProperty] private int[] _iconRegion = [0, 0, 0, 0];
    
    /// <summary>
    /// Buff图标模板图片路径
    /// </summary>
    [ObservableProperty] private string _templatePath = "";
    
    /// <summary>
    /// 模板匹配相似度阈值 (0.0-1.0)
    /// </summary>
    [ObservableProperty] private double _similarityThreshold = 0.8;
    
    /// <summary>
    /// 是否为Debuff（负面效果）
    /// </summary>
    [ObservableProperty] private bool _isDebuff;
    
    /// <summary>
    /// 备注说明
    /// </summary>
    [ObservableProperty] private string _description = "";
    
    /// <summary>
    /// 是否启用此Buff检测
    /// </summary>
    [ObservableProperty] private bool _enabled = true;
    
    /// <summary>
    /// 检查是否已配置检测区域
    /// </summary>
    public bool HasRegion => IconRegion.Any(v => v > 0);
    
    /// <summary>
    /// 检查是否已配置模板
    /// </summary>
    public bool HasTemplate => !string.IsNullOrEmpty(TemplatePath) && System.IO.File.Exists(TemplatePath);
    
    /// <summary>
    /// 配置状态文本
    /// </summary>
    public string ConfigStatus => HasRegion && HasTemplate ? "✓ 已配置" : HasRegion ? "⚠ 缺少模板" : "⚠ 未配置";
}
