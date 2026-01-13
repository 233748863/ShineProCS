using CommunityToolkit.Mvvm.ComponentModel;

namespace ShineProCS.Core.Config;

/// <summary>
/// PaddleOCR 模型配置枚举
/// </summary>
public enum PaddleOcrModelConfig
{
    /// <summary>
    /// V4 自动选择（中英文优先使用 V4）
    /// </summary>
    V4Auto,
    
    /// <summary>
    /// V5 自动选择
    /// </summary>
    V5Auto,
    
    /// <summary>
    /// V4 中文模型
    /// </summary>
    V4,
    
    /// <summary>
    /// V4 英文模型
    /// </summary>
    V4En,
    
    /// <summary>
    /// V5 中文模型
    /// </summary>
    V5,
    
    /// <summary>
    /// V5 拉丁文模型
    /// </summary>
    V5Latin,
    
    /// <summary>
    /// V5 斯拉夫文模型
    /// </summary>
    V5Eslav,
    
    /// <summary>
    /// V5 韩文模型
    /// </summary>
    V5Korean
}

/// <summary>
/// OCR 配置
/// </summary>
[Serializable]
public partial class OcrConfig : ObservableObject
{
    /// <summary>
    /// PaddleOCR 模型配置
    /// </summary>
    [ObservableProperty]
    private PaddleOcrModelConfig _paddleOcrModelConfig = PaddleOcrModelConfig.V4Auto;

    /// <summary>
    /// 游戏语言文化信息名称（如 zh-CN, en-US, ja-JP）
    /// </summary>
    [ObservableProperty]
    private string _gameCultureInfoName = "zh-CN";
}
