using OpenCvSharp;

namespace ShineProCS.Core.Recognition.YOLO;

/// <summary>
/// YOLO 目标检测服务接口
/// </summary>
public interface IYoloService : IDisposable
{
    /// <summary>
    /// 检测图像中的所有目标
    /// </summary>
    /// <param name="image">输入图像</param>
    /// <returns>检测结果集合</returns>
    DetectionResults Detect(Mat image);

    /// <summary>
    /// 检测图像中指定类别的目标
    /// </summary>
    /// <param name="image">输入图像</param>
    /// <param name="classes">要检测的类别名称数组</param>
    /// <returns>检测结果集合</returns>
    DetectionResults Detect(Mat image, string[] classes);

    /// <summary>
    /// 检测图像中的目标，使用自定义置信度阈值
    /// </summary>
    /// <param name="image">输入图像</param>
    /// <param name="confidenceThreshold">置信度阈值 (0-1)</param>
    /// <returns>检测结果集合</returns>
    DetectionResults Detect(Mat image, float confidenceThreshold);

    /// <summary>
    /// 加载 YOLO 模型
    /// </summary>
    /// <param name="modelPath">模型文件路径 (.onnx)</param>
    /// <param name="labelsPath">标签文件路径（可选）</param>
    /// <returns>加载是否成功</returns>
    Task<bool> LoadModelAsync(string modelPath, string? labelsPath = null);

    /// <summary>
    /// 服务是否已初始化（模型已加载）
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// 当前加载的模型路径
    /// </summary>
    string? CurrentModelPath { get; }

    /// <summary>
    /// 模型支持的类别列表
    /// </summary>
    IReadOnlyList<string> Labels { get; }

    /// <summary>
    /// 默认置信度阈值
    /// </summary>
    float DefaultConfidenceThreshold { get; set; }

    /// <summary>
    /// 默认 NMS（非极大值抑制）阈值
    /// </summary>
    float DefaultNmsThreshold { get; set; }

    /// <summary>
    /// 是否使用 GPU 加速
    /// </summary>
    bool UseGpu { get; }
}
