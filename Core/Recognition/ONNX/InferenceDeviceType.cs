namespace ShineProCS.Core.Recognition.ONNX;

/// <summary>
/// 推理设备类型
/// 简化版本：仅支持 CPU 和 DirectML (GPU) 两种模式
/// </summary>
public enum InferenceDeviceType
{
    /// <summary>
    /// CPU 推理 - 兼容性最好，适用于所有设备
    /// </summary>
    Cpu = 0,
    
    /// <summary>
    /// DirectML GPU 推理 - Windows 通用 GPU 加速，无需额外驱动
    /// 如果 GPU 推理失败会自动回退到 CPU
    /// </summary>
    DirectML = 1
}
