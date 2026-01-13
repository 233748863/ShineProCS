namespace ShineProCS.Core.Recognition.ONNX;

/// <summary>
/// 推理设备类型
/// </summary>
public enum InferenceDeviceType
{
    /// <summary>
    /// CPU 推理
    /// </summary>
    Cpu,
    
    /// <summary>
    /// GPU DirectML 推理 (Windows)
    /// </summary>
    GpuDirectMl,
    
    /// <summary>
    /// GPU 自动选择 (TensorRT > DML > CUDA > CPU)
    /// </summary>
    Gpu,
    
    /// <summary>
    /// OpenVINO 推理 (Intel GPU/CPU)
    /// </summary>
    OpenVino
}
