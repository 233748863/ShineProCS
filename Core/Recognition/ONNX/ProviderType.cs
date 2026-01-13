namespace ShineProCS.Core.Recognition.ONNX;

/// <summary>
/// ONNX Runtime 执行提供程序类型
/// 简化版本：仅支持 CPU 和 DirectML 两种 Provider
/// </summary>
public enum ProviderType
{
    /// <summary>
    /// CPU 执行提供程序 - 默认选项，兼容性最好
    /// </summary>
    Cpu = 0,
    
    /// <summary>
    /// DirectML 执行提供程序 - Windows GPU 加速
    /// 支持大多数 Windows 设备上的 GPU（包括 NVIDIA、AMD、Intel）
    /// </summary>
    Dml = 1
}
