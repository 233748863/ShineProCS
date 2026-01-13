using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using ShineProCS.Core.Config;
using ShineProCS.Core.Recognition.ONNX;

namespace ShineProCS.Core.Recognition.YOLO;

/// <summary>
/// YOLO 目标检测预测器
/// 支持 YOLOv5/v8/v11 ONNX 模型，集成 DirectML GPU 加速
/// </summary>
public class YoloPredictor : IYoloService
{
    private readonly ILogger<YoloPredictor> _logger;
    private readonly BgiOnnxFactory _onnxFactory;
    
    private InferenceSession? _session;
    private string? _currentModelPath;
    private List<string> _labels = new();
    private bool _disposed;
    
    // 模型输入尺寸（默认 640x640）
    private int _inputWidth = 640;
    private int _inputHeight = 640;
    
    /// <inheritdoc />
    public bool IsInitialized => _session != null;
    
    /// <inheritdoc />
    public string? CurrentModelPath => _currentModelPath;
    
    /// <inheritdoc />
    public IReadOnlyList<string> Labels => _labels.AsReadOnly();
    
    /// <inheritdoc />
    public float DefaultConfidenceThreshold { get; set; } = 0.5f;
    
    /// <inheritdoc />
    public float DefaultNmsThreshold { get; set; } = 0.45f;
    
    /// <inheritdoc />
    public bool UseGpu { get; private set; }

    /// <summary>
    /// 创建 YOLO 预测器实例
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="onnxFactory">ONNX 工厂</param>
    public YoloPredictor(ILogger<YoloPredictor> logger, BgiOnnxFactory onnxFactory)
    {
        _logger = logger;
        _onnxFactory = onnxFactory;
        UseGpu = _onnxFactory.ProviderTypes.Any(p => 
            p == ProviderType.Dml || p == ProviderType.Cuda || p == ProviderType.TensorRt);
    }

    /// <inheritdoc />
    public async Task<bool> LoadModelAsync(string modelPath, string? labelsPath = null)
    {
        try
        {
            // 检查模型文件是否存在
            if (!File.Exists(modelPath))
            {
                _logger.LogWarning("[YOLO] 模型文件不存在: {Path}", modelPath);
                return false;
            }

            // 释放旧会话
            _session?.Dispose();
            _session = null;
            _currentModelPath = null;

            // 创建模型定义
            var model = CreateModelDefinition(modelPath);
            
            // 异步加载模型
            await Task.Run(() =>
            {
                _session = _onnxFactory.CreateInferenceSession(model);
            });

            // 获取模型输入尺寸
            ParseModelInputSize();

            // 加载标签
            await LoadLabelsAsync(labelsPath, modelPath);

            _currentModelPath = modelPath;
            _logger.LogInformation("[YOLO] 模型加载成功: {Path}, 输入尺寸: {W}x{H}, 类别数: {Count}",
                modelPath, _inputWidth, _inputHeight, _labels.Count);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[YOLO] 模型加载失败: {Path}", modelPath);
            _session?.Dispose();
            _session = null;
            return false;
        }
    }


    /// <inheritdoc />
    public DetectionResults Detect(Mat image)
    {
        return Detect(image, DefaultConfidenceThreshold, DefaultNmsThreshold, null);
    }

    /// <inheritdoc />
    public DetectionResults Detect(Mat image, string[] classes)
    {
        return Detect(image, DefaultConfidenceThreshold, DefaultNmsThreshold, classes);
    }

    /// <inheritdoc />
    public DetectionResults Detect(Mat image, float confidenceThreshold)
    {
        return Detect(image, confidenceThreshold, DefaultNmsThreshold, null);
    }

    /// <summary>
    /// 执行目标检测
    /// </summary>
    /// <param name="image">输入图像</param>
    /// <param name="confidenceThreshold">置信度阈值</param>
    /// <param name="nmsThreshold">NMS 阈值</param>
    /// <param name="filterClasses">要过滤的类别（null 表示不过滤）</param>
    /// <returns>检测结果</returns>
    private DetectionResults Detect(Mat image, float confidenceThreshold, float nmsThreshold, string[]? filterClasses)
    {
        var results = new DetectionResults();
        
        if (!IsInitialized || _session == null)
        {
            _logger.LogWarning("[YOLO] 模型未初始化，无法执行检测");
            return results;
        }

        if (image.Empty())
        {
            _logger.LogWarning("[YOLO] 输入图像为空");
            return results;
        }

        var sw = Stopwatch.StartNew();

        try
        {
            // 预处理图像
            var (inputTensor, scaleX, scaleY, padX, padY) = PreprocessImage(image);

            // 执行推理
            var inputName = _session.InputNames[0];
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
            };

            using var outputs = _session.Run(inputs);
            var output = outputs.First().AsTensor<float>();

            // 后处理
            var detections = PostprocessOutput(output, image.Width, image.Height, 
                scaleX, scaleY, padX, padY, confidenceThreshold, nmsThreshold, filterClasses);

            results.Detections = detections;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[YOLO] 检测过程中发生错误");
        }

        sw.Stop();
        results.InferenceTimeMs = sw.Elapsed.TotalMilliseconds;
        
        return results;
    }

    /// <summary>
    /// 预处理图像
    /// </summary>
    private (DenseTensor<float> tensor, float scaleX, float scaleY, int padX, int padY) PreprocessImage(Mat image)
    {
        // 计算缩放比例（保持宽高比）
        var scaleX = (float)_inputWidth / image.Width;
        var scaleY = (float)_inputHeight / image.Height;
        var scale = Math.Min(scaleX, scaleY);

        var newWidth = (int)(image.Width * scale);
        var newHeight = (int)(image.Height * scale);

        // 计算填充
        var padX = (_inputWidth - newWidth) / 2;
        var padY = (_inputHeight - newHeight) / 2;

        // 缩放图像
        using var resized = new Mat();
        Cv2.Resize(image, resized, new OpenCvSharp.Size(newWidth, newHeight));

        // 创建填充后的图像
        using var padded = new Mat(_inputHeight, _inputWidth, MatType.CV_8UC3, new Scalar(114, 114, 114));
        resized.CopyTo(padded[new Rect(padX, padY, newWidth, newHeight)]);

        // 转换为 RGB
        using var rgb = new Mat();
        Cv2.CvtColor(padded, rgb, ColorConversionCodes.BGR2RGB);

        // 创建张量 [1, 3, H, W]
        var tensor = new DenseTensor<float>(new[] { 1, 3, _inputHeight, _inputWidth });
        
        // 填充张量数据（归一化到 0-1）
        var data = new byte[_inputHeight * _inputWidth * 3];
        System.Runtime.InteropServices.Marshal.Copy(rgb.Data, data, 0, data.Length);

        for (var y = 0; y < _inputHeight; y++)
        {
            for (var x = 0; x < _inputWidth; x++)
            {
                var idx = (y * _inputWidth + x) * 3;
                tensor[0, 0, y, x] = data[idx] / 255f;     // R
                tensor[0, 1, y, x] = data[idx + 1] / 255f; // G
                tensor[0, 2, y, x] = data[idx + 2] / 255f; // B
            }
        }

        return (tensor, scale, scale, padX, padY);
    }


    /// <summary>
    /// 后处理模型输出
    /// </summary>
    private List<DetectionResult> PostprocessOutput(
        Tensor<float> output,
        int originalWidth,
        int originalHeight,
        float scaleX,
        float scaleY,
        int padX,
        int padY,
        float confidenceThreshold,
        float nmsThreshold,
        string[]? filterClasses)
    {
        var detections = new List<DetectionResult>();
        var dimensions = output.Dimensions.ToArray();

        // YOLOv8/v11 输出格式: [1, 84, 8400] 或 [1, numClasses+4, numBoxes]
        // YOLOv5 输出格式: [1, 25200, 85] 或 [1, numBoxes, numClasses+5]
        
        bool isYoloV8Format = dimensions.Length == 3 && dimensions[1] < dimensions[2];
        
        if (isYoloV8Format)
        {
            // YOLOv8/v11 格式
            detections = ProcessYoloV8Output(output, originalWidth, originalHeight, 
                scaleX, scaleY, padX, padY, confidenceThreshold, filterClasses);
        }
        else
        {
            // YOLOv5 格式
            detections = ProcessYoloV5Output(output, originalWidth, originalHeight,
                scaleX, scaleY, padX, padY, confidenceThreshold, filterClasses);
        }

        // 应用 NMS
        return ApplyNms(detections, nmsThreshold);
    }

    /// <summary>
    /// 处理 YOLOv8/v11 格式输出
    /// </summary>
    private List<DetectionResult> ProcessYoloV8Output(
        Tensor<float> output,
        int originalWidth,
        int originalHeight,
        float scale,
        float _,
        int padX,
        int padY,
        float confidenceThreshold,
        string[]? filterClasses)
    {
        var detections = new List<DetectionResult>();
        var dimensions = output.Dimensions.ToArray();
        
        var numClasses = dimensions[1] - 4;
        var numBoxes = dimensions[2];

        for (var i = 0; i < numBoxes; i++)
        {
            // 找到最大类别置信度
            var maxConfidence = 0f;
            var maxClassId = 0;

            for (var c = 0; c < numClasses; c++)
            {
                var confidence = output[0, 4 + c, i];
                if (confidence > maxConfidence)
                {
                    maxConfidence = confidence;
                    maxClassId = c;
                }
            }

            if (maxConfidence < confidenceThreshold)
                continue;

            // 检查类别过滤
            var className = maxClassId < _labels.Count ? _labels[maxClassId] : $"class_{maxClassId}";
            if (filterClasses != null && !filterClasses.Contains(className, StringComparer.OrdinalIgnoreCase))
                continue;

            // 获取边界框 (cx, cy, w, h)
            var cx = output[0, 0, i];
            var cy = output[0, 1, i];
            var w = output[0, 2, i];
            var h = output[0, 3, i];

            // 转换为原始图像坐标
            var x1 = (cx - w / 2 - padX) / scale;
            var y1 = (cy - h / 2 - padY) / scale;
            var x2 = (cx + w / 2 - padX) / scale;
            var y2 = (cy + h / 2 - padY) / scale;

            // 裁剪到图像边界
            x1 = Math.Max(0, Math.Min(x1, originalWidth));
            y1 = Math.Max(0, Math.Min(y1, originalHeight));
            x2 = Math.Max(0, Math.Min(x2, originalWidth));
            y2 = Math.Max(0, Math.Min(y2, originalHeight));

            detections.Add(new DetectionResult
            {
                ClassId = maxClassId,
                ClassName = className,
                Confidence = maxConfidence,
                BoundingBox = new Rect((int)x1, (int)y1, (int)(x2 - x1), (int)(y2 - y1))
            });
        }

        return detections;
    }

    /// <summary>
    /// 处理 YOLOv5 格式输出
    /// </summary>
    private List<DetectionResult> ProcessYoloV5Output(
        Tensor<float> output,
        int originalWidth,
        int originalHeight,
        float scale,
        float _,
        int padX,
        int padY,
        float confidenceThreshold,
        string[]? filterClasses)
    {
        var detections = new List<DetectionResult>();
        var dimensions = output.Dimensions.ToArray();
        
        var numBoxes = dimensions[1];
        var numOutputs = dimensions[2];
        var numClasses = numOutputs - 5;

        for (var i = 0; i < numBoxes; i++)
        {
            var objectness = output[0, i, 4];
            if (objectness < confidenceThreshold)
                continue;

            // 找到最大类别置信度
            var maxConfidence = 0f;
            var maxClassId = 0;

            for (var c = 0; c < numClasses; c++)
            {
                var confidence = output[0, i, 5 + c] * objectness;
                if (confidence > maxConfidence)
                {
                    maxConfidence = confidence;
                    maxClassId = c;
                }
            }

            if (maxConfidence < confidenceThreshold)
                continue;

            // 检查类别过滤
            var className = maxClassId < _labels.Count ? _labels[maxClassId] : $"class_{maxClassId}";
            if (filterClasses != null && !filterClasses.Contains(className, StringComparer.OrdinalIgnoreCase))
                continue;

            // 获取边界框 (cx, cy, w, h)
            var cx = output[0, i, 0];
            var cy = output[0, i, 1];
            var w = output[0, i, 2];
            var h = output[0, i, 3];

            // 转换为原始图像坐标
            var x1 = (cx - w / 2 - padX) / scale;
            var y1 = (cy - h / 2 - padY) / scale;
            var x2 = (cx + w / 2 - padX) / scale;
            var y2 = (cy + h / 2 - padY) / scale;

            // 裁剪到图像边界
            x1 = Math.Max(0, Math.Min(x1, originalWidth));
            y1 = Math.Max(0, Math.Min(y1, originalHeight));
            x2 = Math.Max(0, Math.Min(x2, originalWidth));
            y2 = Math.Max(0, Math.Min(y2, originalHeight));

            detections.Add(new DetectionResult
            {
                ClassId = maxClassId,
                ClassName = className,
                Confidence = maxConfidence,
                BoundingBox = new Rect((int)x1, (int)y1, (int)(x2 - x1), (int)(y2 - y1))
            });
        }

        return detections;
    }


    /// <summary>
    /// 应用非极大值抑制 (NMS)
    /// </summary>
    private List<DetectionResult> ApplyNms(List<DetectionResult> detections, float nmsThreshold)
    {
        if (detections.Count == 0)
            return detections;

        // 按类别分组
        var groupedByClass = detections.GroupBy(d => d.ClassId);
        var result = new List<DetectionResult>();

        foreach (var group in groupedByClass)
        {
            var classDetections = group.OrderByDescending(d => d.Confidence).ToList();
            var keep = new List<DetectionResult>();

            while (classDetections.Count > 0)
            {
                var best = classDetections[0];
                keep.Add(best);
                classDetections.RemoveAt(0);

                classDetections = classDetections
                    .Where(d => CalculateIoU(best.BoundingBox, d.BoundingBox) < nmsThreshold)
                    .ToList();
            }

            result.AddRange(keep);
        }

        return result;
    }

    /// <summary>
    /// 计算两个边界框的 IoU（交并比）
    /// </summary>
    private static float CalculateIoU(Rect box1, Rect box2)
    {
        var x1 = Math.Max(box1.X, box2.X);
        var y1 = Math.Max(box1.Y, box2.Y);
        var x2 = Math.Min(box1.X + box1.Width, box2.X + box2.Width);
        var y2 = Math.Min(box1.Y + box1.Height, box2.Y + box2.Height);

        var intersection = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        var area1 = box1.Width * box1.Height;
        var area2 = box2.Width * box2.Height;
        var union = area1 + area2 - intersection;

        return union > 0 ? (float)intersection / union : 0;
    }

    /// <summary>
    /// 解析模型输入尺寸
    /// </summary>
    private void ParseModelInputSize()
    {
        if (_session == null) return;

        try
        {
            var inputMeta = _session.InputMetadata.First().Value;
            var dimensions = inputMeta.Dimensions;
            
            // 输入格式通常是 [batch, channels, height, width]
            if (dimensions.Length >= 4)
            {
                _inputHeight = dimensions[2] > 0 ? dimensions[2] : 640;
                _inputWidth = dimensions[3] > 0 ? dimensions[3] : 640;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[YOLO] 无法解析模型输入尺寸，使用默认值 640x640");
            _inputWidth = 640;
            _inputHeight = 640;
        }
    }

    /// <summary>
    /// 加载标签文件
    /// </summary>
    private async Task LoadLabelsAsync(string? labelsPath, string modelPath)
    {
        _labels.Clear();

        // 尝试从指定路径加载
        if (!string.IsNullOrEmpty(labelsPath) && File.Exists(labelsPath))
        {
            var lines = await File.ReadAllLinesAsync(labelsPath);
            _labels = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            _logger.LogDebug("[YOLO] 从指定路径加载标签: {Path}, 数量: {Count}", labelsPath, _labels.Count);
            return;
        }

        // 尝试从模型同目录加载
        var modelDir = Path.GetDirectoryName(modelPath);
        if (!string.IsNullOrEmpty(modelDir))
        {
            var possiblePaths = new[]
            {
                Path.Combine(modelDir, "labels.txt"),
                Path.Combine(modelDir, "classes.txt"),
                Path.Combine(modelDir, "coco.names"),
                Path.Combine(modelDir, Path.GetFileNameWithoutExtension(modelPath) + ".txt")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    var lines = await File.ReadAllLinesAsync(path);
                    _labels = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                    _logger.LogDebug("[YOLO] 从模型目录加载标签: {Path}, 数量: {Count}", path, _labels.Count);
                    return;
                }
            }
        }

        // 使用默认 COCO 标签
        _labels = GetDefaultCocoLabels();
        _logger.LogDebug("[YOLO] 使用默认 COCO 标签，数量: {Count}", _labels.Count);
    }

    /// <summary>
    /// 获取默认 COCO 数据集标签
    /// </summary>
    private static List<string> GetDefaultCocoLabels()
    {
        return new List<string>
        {
            "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat",
            "traffic light", "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat",
            "dog", "horse", "sheep", "cow", "elephant", "bear", "zebra", "giraffe", "backpack",
            "umbrella", "handbag", "tie", "suitcase", "frisbee", "skis", "snowboard", "sports ball",
            "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket",
            "bottle", "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple",
            "sandwich", "orange", "broccoli", "carrot", "hot dog", "pizza", "donut", "cake",
            "chair", "couch", "potted plant", "bed", "dining table", "toilet", "tv", "laptop",
            "mouse", "remote", "keyboard", "cell phone", "microwave", "oven", "toaster", "sink",
            "refrigerator", "book", "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush"
        };
    }

    /// <summary>
    /// 创建模型定义
    /// </summary>
    private static BgiOnnxModel CreateModelDefinition(string modelPath)
    {
        // 使用反射创建 BgiOnnxModel 实例（因为构造函数是私有的）
        var modelName = Path.GetFileNameWithoutExtension(modelPath);
        var relativePath = GetRelativePath(modelPath);
        var cacheRelativePath = Path.Combine(BgiOnnxModel.ModelCacheRelativePath, modelName);

        // 创建临时模型定义
        return new YoloOnnxModel(modelName, relativePath, cacheRelativePath);
    }

    /// <summary>
    /// 获取相对路径
    /// </summary>
    private static string GetRelativePath(string absolutePath)
    {
        var basePath = Global.StartUpPath;
        if (absolutePath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
        {
            return absolutePath.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar);
        }
        return absolutePath;
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
            _session?.Dispose();
            _session = null;
            _currentModelPath = null;
            _labels.Clear();
        }

        _disposed = true;
    }

    /// <summary>
    /// YOLO 模型定义（内部类）
    /// </summary>
    private class YoloOnnxModel : BgiOnnxModel
    {
        public YoloOnnxModel(string name, string modelRelativePath, string cacheRelativePath)
            : base(name, modelRelativePath, cacheRelativePath)
        {
        }
    }
}
