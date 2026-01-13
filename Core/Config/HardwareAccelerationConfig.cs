using CommunityToolkit.Mvvm.ComponentModel;
using ShineProCS.Core.Recognition.ONNX;

namespace ShineProCS.Core.Config;

/// <summary>
/// 硬件加速配置
/// 简化版本：仅支持 CPU 和 DirectML (GPU) 两种推理模式
/// </summary>
[Serializable]
public partial class HardwareAccelerationConfig : ObservableObject
{
    /// <summary>
    /// 推理使用的设备类型
    /// - Cpu: CPU 推理，兼容性最好
    /// - DirectML: GPU 推理，Windows 通用 GPU 加速
    /// 默认使用 CPU
    /// </summary>
    [ObservableProperty]
    private InferenceDeviceType _inferenceDevice = InferenceDeviceType.Cpu;

    /// <summary>
    /// DirectML GPU 设备 ID
    /// 默认为 0（使用系统默认 GPU）
    /// 多 GPU 系统可以指定具体设备
    /// </summary>
    [ObservableProperty]
    private int _gpuDevice = 0;

    /// <summary>
    /// 是否强制 OCR 使用 CPU 推理
    /// 在某些环境上使用 GPU 进行 OCR 推理会导致性能下降
    /// （比如很多使用 DirectML 推理的情况下）
    /// 默认关闭，启用后 OCR 将独立使用 CPU，即使其他任务启用了 GPU
    /// </summary>
    [ObservableProperty]
    private bool _cpuOcr = false;
}
