using System.IO;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using ShineProCS.Core.Config;
using ShineProCS.Core.GameTask.Model.Area;
using ShineProCS.Core.Interfaces;
using ShineProCS.Core.Recognition.ONNX;

namespace ShineProCS.Core.Recognition.YOLO;

/// <summary>
/// YOLO 目标检测服务
/// 提供模型管理、优雅降级和检测功能
/// 支持两种模式：
/// 1. BgiYoloPredictor 模式（使用 YoloSharp，与 BetterGI 原版一致）
/// 2. YoloPredictor 模式（自定义 ONNX 推理，作为备用）
/// </summary>
public class YoloService : IYoloService
{
    private readonly ILogger<YoloService> _logger;
    private readonly INotificationService? _notificationService;
    private readonly BgiOnnxFactory _onnxFactory;
    private readonly YoloPredictor _fallbackPredictor;
    
    private BgiYoloPredictor? _bgiPredictor;
    private bool _disposed;
    private bool _modelLoadFailed;
    private bool _useBgiPredictor;

    /// <summary>
    /// 默认模型目录（与 BetterGI 原版一致）
    /// </summary>
    public static readonly string DefaultModelDirectory = Path.Combine("Assets", "Model");

    /// <inheritdoc />
    public bool IsInitialized => _useBgiPredictor ? _bgiPredictor != null : _fallbackPredictor.IsInitialized;

    /// <inheritdoc />
    public string? CurrentModelPath => _useBgiPredictor ? _bgiPredictor?.ModelName : _fallbackPredictor.CurrentModelPath;

    /// <inheritdoc />
    public IReadOnlyList<string> Labels => _fallbackPredictor.Labels;

    /// <inheritdoc />
    public float DefaultConfidenceThreshold
    {
        get => _fallbackPredictor.DefaultConfidenceThreshold;
        set => _fallbackPredictor.DefaultConfidenceThreshold = value;
    }

    /// <inheritdoc />
    public float DefaultNmsThreshold
    {
        get => _fallbackPredictor.DefaultNmsThreshold;
        set => _fallbackPredictor.DefaultNmsThreshold = value;
    }

    /// <inheritdoc />
    public bool UseGpu => _fallbackPredictor.UseGpu;

    /// <summary>
    /// 模型加载是否失败（用于优雅降级）
    /// </summary>
    public bool ModelLoadFailed => _modelLoadFailed;

    /// <summary>
    /// 是否使用 BgiYoloPredictor（YoloSharp）
    /// </summary>
    public bool UseBgiPredictor => _useBgiPredictor;

    /// <summary>
    /// 创建 YOLO 服务实例
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="predictorLogger">预测器日志记录器</param>
    /// <param name="onnxFactory">ONNX 工厂</param>
    /// <param name="notificationService">通知服务（可选）</param>
    public YoloService(
        ILogger<YoloService> logger,
        ILogger<YoloPredictor> predictorLogger,
        BgiOnnxFactory onnxFactory,
        INotificationService? notificationService = null)
    {
        _logger = logger;
        _notificationService = notificationService;
        _onnxFactory = onnxFactory;
        _fallbackPredictor = new YoloPredictor(predictorLogger, onnxFactory);
    }

    /// <summary>
    /// 使用 BgiOnnxModel 加载模型（推荐，与 BetterGI 原版一致）
    /// </summary>
    /// <param name="model">ONNX 模型定义</param>
    /// <returns>加载是否成功</returns>
    public bool LoadModel(BgiOnnxModel model)
    {
        _modelLoadFailed = false;

        // 检查模型文件是否存在
        if (!BgiOnnxModel.IsModelExist(model))
        {
            _modelLoadFailed = true;
            var message = $"YOLO 模型文件不存在: {model.ModalPath}";
            _logger.LogWarning("[YOLO] {Message}", message);
            _notificationService?.ShowWarning(message);
            return false;
        }

        try
        {
            // 释放旧的预测器
            _bgiPredictor?.Dispose();
            _bgiPredictor = null;

            // 使用 BgiOnnxFactory 创建 YoloPredictor
            _bgiPredictor = _onnxFactory.CreateYoloPredictor(model);
            _useBgiPredictor = true;

            _logger.LogInformation("[YOLO] 模型加载成功 (YoloSharp): {Name}", model.Name);
            return true;
        }
        catch (Exception ex)
        {
            _modelLoadFailed = true;
            var message = $"YOLO 模型加载失败: {ex.Message}";
            _logger.LogError(ex, "[YOLO] {Message}", message);
            _notificationService?.ShowWarning(message);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> LoadModelAsync(string modelPath, string? labelsPath = null)
    {
        _modelLoadFailed = false;
        _useBgiPredictor = false;

        // 检查模型文件是否存在
        if (!File.Exists(modelPath))
        {
            _modelLoadFailed = true;
            var message = $"YOLO 模型文件不存在: {modelPath}";
            _logger.LogWarning("[YOLO] {Message}", message);
            _notificationService?.ShowWarning(message);
            return false;
        }

        // 使用备用预测器加载自定义模型
        var result = await _fallbackPredictor.LoadModelAsync(modelPath, labelsPath);
        
        if (!result)
        {
            _modelLoadFailed = true;
            var message = "YOLO 模型加载失败，目标检测功能将不可用";
            _logger.LogWarning("[YOLO] {Message}", message);
            _notificationService?.ShowWarning(message);
        }
        else
        {
            _logger.LogInformation("[YOLO] 模型加载成功 (自定义): {Path}", modelPath);
        }

        return result;
    }

    /// <summary>
    /// 尝试加载默认模型
    /// </summary>
    /// <param name="modelName">模型文件名（不含路径）</param>
    /// <returns>加载是否成功</returns>
    public async Task<bool> TryLoadDefaultModelAsync(string modelName = "yolov8n.onnx")
    {
        var modelPath = Global.Absolute(Path.Combine(DefaultModelDirectory, modelName));
        return await LoadModelAsync(modelPath);
    }

    /// <summary>
    /// 加载预定义的 YOLO 模型
    /// </summary>
    /// <param name="modelType">模型类型</param>
    /// <returns>加载是否成功</returns>
    public bool LoadPredefinedModel(YoloModelType modelType)
    {
        var model = modelType switch
        {
            YoloModelType.Fish => BgiOnnxModel.BgiFish,
            YoloModelType.Tree => BgiOnnxModel.BgiTree,
            YoloModelType.World => BgiOnnxModel.BgiWorld,
            _ => throw new ArgumentOutOfRangeException(nameof(modelType))
        };

        return LoadModel(model);
    }

    /// <summary>
    /// 获取可用的模型列表
    /// </summary>
    /// <returns>模型文件路径列表</returns>
    public IReadOnlyList<string> GetAvailableModels()
    {
        var modelDir = Global.Absolute(DefaultModelDirectory);
        
        if (!Directory.Exists(modelDir))
        {
            return Array.Empty<string>();
        }

        return Directory.GetFiles(modelDir, "*.onnx", SearchOption.AllDirectories)
            .OrderBy(f => f)
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public DetectionResults Detect(Mat image)
    {
        if (_useBgiPredictor && _bgiPredictor != null)
        {
            return _bgiPredictor.DetectResults(image);
        }

        if (!_fallbackPredictor.IsInitialized)
        {
            if (!_modelLoadFailed)
            {
                _logger.LogDebug("[YOLO] 模型未初始化，跳过检测");
            }
            return new DetectionResults();
        }

        return _fallbackPredictor.Detect(image);
    }

    /// <inheritdoc />
    public DetectionResults Detect(Mat image, string[] classes)
    {
        if (_useBgiPredictor && _bgiPredictor != null)
        {
            // BgiYoloPredictor 返回的结果需要过滤
            var results = _bgiPredictor.DetectResults(image);
            results.Detections = results.Detections
                .Where(d => classes.Contains(d.ClassName, StringComparer.OrdinalIgnoreCase))
                .ToList();
            return results;
        }

        if (!_fallbackPredictor.IsInitialized)
        {
            if (!_modelLoadFailed)
            {
                _logger.LogDebug("[YOLO] 模型未初始化，跳过检测");
            }
            return new DetectionResults();
        }

        return _fallbackPredictor.Detect(image, classes);
    }

    /// <inheritdoc />
    public DetectionResults Detect(Mat image, float confidenceThreshold)
    {
        if (_useBgiPredictor && _bgiPredictor != null)
        {
            // BgiYoloPredictor 不支持动态置信度阈值，返回所有结果后过滤
            var results = _bgiPredictor.DetectResults(image);
            results.Detections = results.Detections
                .Where(d => d.Confidence >= confidenceThreshold)
                .ToList();
            return results;
        }

        if (!_fallbackPredictor.IsInitialized)
        {
            if (!_modelLoadFailed)
            {
                _logger.LogDebug("[YOLO] 模型未初始化，跳过检测");
            }
            return new DetectionResults();
        }

        return _fallbackPredictor.Detect(image, confidenceThreshold);
    }

    /// <summary>
    /// 使用 BgiYoloPredictor 检测（返回类别-矩形框字典）
    /// </summary>
    /// <param name="image">输入图像</param>
    /// <returns>类别-矩形框字典</returns>
    public Dictionary<string, List<Rect>> DetectAsDictionary(Mat image)
    {
        if (_useBgiPredictor && _bgiPredictor != null)
        {
            return _bgiPredictor.Detect(image);
        }

        // 使用备用预测器并转换结果
        var results = _fallbackPredictor.Detect(image);
        var dict = new Dictionary<string, List<Rect>>();
        
        foreach (var detection in results.Detections)
        {
            if (!dict.TryGetValue(detection.ClassName, out var list))
            {
                dict[detection.ClassName] = [detection.BoundingBox];
            }
            else
            {
                list.Add(detection.BoundingBox);
            }
        }

        return dict;
    }

    /// <summary>
    /// 使用 ImageRegion 检测（与 BetterGI 原版一致）
    /// </summary>
    /// <param name="region">图像区域</param>
    /// <returns>类别-矩形框字典</returns>
    public Dictionary<string, List<Rect>> Detect(ImageRegion region)
    {
        if (_useBgiPredictor && _bgiPredictor != null)
        {
            return _bgiPredictor.Detect(region);
        }

        // 使用备用预测器
        return DetectAsDictionary(region.SrcMat);
    }

    /// <summary>
    /// 使用 ImageRegion 检测（返回详细结果）
    /// </summary>
    /// <param name="region">图像区域</param>
    /// <returns>检测结果</returns>
    public DetectionResults DetectResults(ImageRegion region)
    {
        if (_useBgiPredictor && _bgiPredictor != null)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var detections = _bgiPredictor.DetectWithDetails(region);
            sw.Stop();

            return new DetectionResults
            {
                Detections = detections,
                InferenceTimeMs = sw.Elapsed.TotalMilliseconds
            };
        }

        return _fallbackPredictor.Detect(region.SrcMat);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _bgiPredictor?.Dispose();
            _fallbackPredictor.Dispose();
        }

        _disposed = true;
    }
}

/// <summary>
/// 预定义 YOLO 模型类型
/// </summary>
public enum YoloModelType
{
    /// <summary>
    /// 钓鱼检测模型
    /// </summary>
    Fish,
    
    /// <summary>
    /// 树木检测模型
    /// </summary>
    Tree,
    
    /// <summary>
    /// 世界模型（通用检测）
    /// </summary>
    World
}
