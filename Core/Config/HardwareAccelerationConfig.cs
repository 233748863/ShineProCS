using CommunityToolkit.Mvvm.ComponentModel;
using ShineProCS.Core.Recognition.ONNX;

namespace ShineProCS.Core.Config;

/// <summary>
/// 硬件加速配置
/// </summary>
[Serializable]
public partial class HardwareAccelerationConfig : ObservableObject
{
    /// <summary>
    /// 推理使用的设备。默认 CPU
    /// </summary>
    [ObservableProperty]
    private InferenceDeviceType _inferenceDevice = InferenceDeviceType.Cpu;

    /// <summary>
    /// 是否强制 OCR 使用 CPU 推理。
    /// 在某些环境上使用 GPU 进行 OCR 推理会导致性能下降（比如很多使用 DirectML 推理的情况下）。
    /// 默认关闭。
    /// </summary>
    [ObservableProperty]
    private bool _cpuOcr = false;

    #region 一般 GPU 加速设置

    /// <summary>
    /// 强制指定 GPU 设备，默认为 0（使用默认设备）
    /// </summary>
    [ObservableProperty]
    private int _gpuDevice = 0;

    /// <summary>
    /// 附加 PATH，用 ; 分割。默认为空。
    /// </summary>
    [ObservableProperty]
    private string _additionalPath = "";

    /// <summary>
    /// 是否输出优化后的模型文件到缓存。
    /// 注意：在不支持的执行器上使用会导致异常。默认关闭。
    /// </summary>
    [ObservableProperty]
    private bool _optimizedModel = false;

    #endregion

    #region CUDA 设置

    /// <summary>
    /// 强制指定 CUDA 设备，默认为 0（使用默认设备）
    /// </summary>
    [ObservableProperty]
    private int _cudaDevice = 0;

    /// <summary>
    /// 自动附加 CUDA 的 PATH。一般情况下用这个就足够了。默认关闭。
    /// </summary>
    [ObservableProperty]
    private bool _autoAppendCudaPath = false;

    #endregion

    #region TensorRT 缓存设置

    /// <summary>
    /// 启用 TensorRT 缓存。默认开启。
    /// 不开的话使用 TensorRT 每次加载模型会卡爆。
    /// </summary>
    [ObservableProperty]
    private bool _enableTensorRtCache = true;

    /// <summary>
    /// 嵌入式引擎缓存。将引擎缓存嵌入到模型中。默认开启。
    /// 关闭它可能会提高性能（如果不爆炸的话）。
    /// </summary>
    [ObservableProperty]
    private bool _embedTensorRtCache = true;

    #endregion

    #region OpenVINO 设置

    /// <summary>
    /// OpenVINO 设备参数
    /// </summary>
    [ObservableProperty]
    private string _openVinoDevice = "AUTO:GPU,CPU";

    /// <summary>
    /// 启用 OpenVINO 缓存。默认关闭。
    /// </summary>
    [ObservableProperty]
    private bool _enableOpenVinoCache = false;

    #endregion
}
