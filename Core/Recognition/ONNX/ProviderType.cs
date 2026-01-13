namespace ShineProCS.Core.Recognition.ONNX;

/// <summary>
/// ONNX Runtime 执行提供程序类型
/// </summary>
public enum ProviderType
{
    /// <summary>
    /// TensorRT (NVIDIA GPU)
    /// </summary>
    TensorRt,
    
    /// <summary>
    /// CUDA (NVIDIA GPU)
    /// </summary>
    Cuda,
    
    /// <summary>
    /// DirectML (Windows GPU)
    /// </summary>
    Dml,
    
    /// <summary>
    /// CPU
    /// </summary>
    Cpu,
    
    /// <summary>
    /// DNNL (Intel CPU 优化)
    /// </summary>
    Dnnl,
    
    /// <summary>
    /// OpenVINO (Intel GPU/CPU)
    /// </summary>
    OpenVino
}
