using System.Diagnostics;
using System.Text.Json;
using Compunet.YoloSharp;
using Compunet.YoloSharp.Data;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;
using ShineProCS.Core.GameTask.Model.Area;
using ShineProCS.Core.Recognition.ONNX;
using ShineProCS.Core.View.Drawable;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// 使用别名避免命名冲突
using YoloSharpPredictor = Compunet.YoloSharp.YoloPredictor;
using YoloSharpOptions = Compunet.YoloSharp.YoloPredictorOptions;

namespace ShineProCS.Core.Recognition.YOLO;

/// <summary>
/// BetterGI 风格的 YOLO 预测器
/// 使用 YoloSharp 库进行目标检测
/// </summary>
public class BgiYoloPredictor : IDisposable
{
    private readonly BgiOnnxModel _model;
    private readonly ILogger? _logger;
    private readonly Lazy<YoloSharpPredictor> _lazyPredictor;

    /// <summary>
    /// 使用 BgiOnnxFactory 创建这个类的实例
    /// </summary>
    /// <param name="onnxModel">模型</param>
    /// <param name="modelPath">实际要加载的模型文件的绝对路径，在使用模型缓存的场景下可能有差别</param>
    /// <param name="sessionOptions">sessionOptions</param>
    /// <param name="logger">日志记录器（可选）</param>
    internal BgiYoloPredictor(BgiOnnxModel onnxModel, string modelPath, SessionOptions sessionOptions, ILogger? logger = null)
    {
        _model = onnxModel;
        _logger = logger;
        _lazyPredictor = new Lazy<YoloSharpPredictor>(() => 
            new YoloSharpPredictor(modelPath,
                new YoloSharpOptions
                {
                    SessionOptions = sessionOptions
                }));
    }

    /// <summary>
    /// 获取 YoloSharp 预测器
    /// </summary>
    public YoloSharpPredictor Predictor => _lazyPredictor.Value;

    /// <summary>
    /// 模型名称
    /// </summary>
    public string ModelName => _model.Name;

    /// <summary>
    /// 检测图像中的目标（使用 ImageRegion，与 BetterGI 原版一致）
    /// </summary>
    /// <param name="region">图像区域</param>
    /// <returns>类别-矩形框字典</returns>
    public Dictionary<string, List<Rect>> Detect(ImageRegion region)
    {
        var result = Predictor.Detect(region.CacheImage);

        var dict = new Dictionary<string, List<Rect>>();
        foreach (var box in result)
        {
            if (!dict.TryGetValue(box.Name.Name, out var value))
            {
                dict[box.Name.Name] = [new Rect(box.Bounds.X, box.Bounds.Y, box.Bounds.Width, box.Bounds.Height)];
            }
            else
            {
                value.Add(new Rect(box.Bounds.X, box.Bounds.Y, box.Bounds.Width, box.Bounds.Height));
            }
        }

        Debug.WriteLine("YOLO识别结果:" + JsonSerializer.Serialize(dict));

        // 在遮罩窗口上绘制检测结果（与 BetterGI 原版一致）
        var list = result
            .Select(box => new Rect(box.Bounds.X, box.Bounds.Y, box.Bounds.Width, box.Bounds.Height))
            .Select(rect => region.ToRectDrawable(rect, _model.Name)).ToList();

        VisionContext.Instance().DrawContent.PutOrRemoveRectList(_model.Name, list);

        return dict;
    }

    /// <summary>
    /// 检测图像中的目标（使用 ImageSharp Image）
    /// </summary>
    /// <param name="image">输入图像 (ImageSharp Image)</param>
    /// <param name="drawOnWindow">是否在遮罩窗口上绘制结果</param>
    /// <returns>类别-矩形框字典</returns>
    public Dictionary<string, List<Rect>> Detect(Image<Rgb24> image, bool drawOnWindow = false)
    {
        var result = Predictor.Detect(image);

        var dict = new Dictionary<string, List<Rect>>();
        foreach (var box in result)
        {
            var rect = new Rect(box.Bounds.X, box.Bounds.Y, box.Bounds.Width, box.Bounds.Height);
            if (!dict.TryGetValue(box.Name.Name, out var value))
            {
                dict[box.Name.Name] = [rect];
            }
            else
            {
                value.Add(rect);
            }
        }

        Debug.WriteLine("YOLO识别结果:" + JsonSerializer.Serialize(dict));

        // 可选：在遮罩窗口上绘制检测结果
        if (drawOnWindow)
        {
            var list = result
                .Select(box => new Rect(box.Bounds.X, box.Bounds.Y, box.Bounds.Width, box.Bounds.Height))
                .Select(rect => new RectDrawable(new System.Windows.Rect(rect.X, rect.Y, rect.Width, rect.Height), _model.Name))
                .ToList();

            VisionContext.Instance().DrawContent.PutOrRemoveRectList(_model.Name, list);
        }

        return dict;
    }

    /// <summary>
    /// 检测图像中的目标（使用 OpenCvSharp Mat）
    /// </summary>
    /// <param name="mat">输入图像 (Mat)</param>
    /// <param name="drawOnWindow">是否在遮罩窗口上绘制结果</param>
    /// <returns>类别-矩形框字典</returns>
    public Dictionary<string, List<Rect>> Detect(Mat mat, bool drawOnWindow = false)
    {
        using var image = ConvertMatToImage(mat);
        return Detect(image, drawOnWindow);
    }

    /// <summary>
    /// 检测图像中的目标（返回详细结果）
    /// </summary>
    /// <param name="image">输入图像 (ImageSharp Image)</param>
    /// <param name="drawOnWindow">是否在遮罩窗口上绘制结果</param>
    /// <returns>检测结果列表</returns>
    public List<DetectionResult> DetectWithDetails(Image<Rgb24> image, bool drawOnWindow = false)
    {
        var result = Predictor.Detect(image);

        var detections = new List<DetectionResult>();
        foreach (var box in result)
        {
            detections.Add(new DetectionResult
            {
                ClassId = box.Name.Id,
                ClassName = box.Name.Name,
                Confidence = box.Confidence,
                BoundingBox = new Rect(box.Bounds.X, box.Bounds.Y, box.Bounds.Width, box.Bounds.Height)
            });
        }

        // 可选：在遮罩窗口上绘制检测结果
        if (drawOnWindow)
        {
            var list = detections
                .Select(d => new RectDrawable(new System.Windows.Rect(d.BoundingBox.X, d.BoundingBox.Y, d.BoundingBox.Width, d.BoundingBox.Height), _model.Name))
                .ToList();

            VisionContext.Instance().DrawContent.PutOrRemoveRectList(_model.Name, list);
        }

        return detections;
    }

    /// <summary>
    /// 检测图像中的目标（返回详细结果，使用 Mat）
    /// </summary>
    /// <param name="mat">输入图像 (Mat)</param>
    /// <param name="drawOnWindow">是否在遮罩窗口上绘制结果</param>
    /// <returns>检测结果列表</returns>
    public List<DetectionResult> DetectWithDetails(Mat mat, bool drawOnWindow = false)
    {
        using var image = ConvertMatToImage(mat);
        return DetectWithDetails(image, drawOnWindow);
    }

    /// <summary>
    /// 检测图像中的目标（返回详细结果，使用 ImageRegion）
    /// </summary>
    /// <param name="region">图像区域</param>
    /// <returns>检测结果列表</returns>
    public List<DetectionResult> DetectWithDetails(ImageRegion region)
    {
        var result = Predictor.Detect(region.CacheImage);

        var detections = new List<DetectionResult>();
        foreach (var box in result)
        {
            detections.Add(new DetectionResult
            {
                ClassId = box.Name.Id,
                ClassName = box.Name.Name,
                Confidence = box.Confidence,
                BoundingBox = new Rect(box.Bounds.X, box.Bounds.Y, box.Bounds.Width, box.Bounds.Height)
            });
        }

        // 在遮罩窗口上绘制检测结果
        var list = detections
            .Select(d => region.ToRectDrawable(d.BoundingBox, _model.Name))
            .ToList();

        VisionContext.Instance().DrawContent.PutOrRemoveRectList(_model.Name, list);

        return detections;
    }

    /// <summary>
    /// 检测图像中的目标（返回 DetectionResults）
    /// </summary>
    /// <param name="mat">输入图像 (Mat)</param>
    /// <returns>检测结果集合</returns>
    public DetectionResults DetectResults(Mat mat)
    {
        var sw = Stopwatch.StartNew();
        var detections = DetectWithDetails(mat);
        sw.Stop();

        return new DetectionResults
        {
            Detections = detections,
            InferenceTimeMs = sw.Elapsed.TotalMilliseconds
        };
    }

    /// <summary>
    /// 将 OpenCvSharp Mat 转换为 SixLabors.ImageSharp Image
    /// </summary>
    private static Image<Rgb24> ConvertMatToImage(Mat mat)
    {
        // 转换为 RGB
        using var rgbMat = new Mat();
        if (mat.Channels() == 4)
        {
            Cv2.CvtColor(mat, rgbMat, ColorConversionCodes.BGRA2RGB);
        }
        else if (mat.Channels() == 3)
        {
            Cv2.CvtColor(mat, rgbMat, ColorConversionCodes.BGR2RGB);
        }
        else
        {
            Cv2.CvtColor(mat, rgbMat, ColorConversionCodes.GRAY2RGB);
        }

        // 创建 ImageSharp 图像
        var image = new Image<Rgb24>(rgbMat.Width, rgbMat.Height);
        
        // 复制像素数据
        var data = new byte[rgbMat.Rows * rgbMat.Cols * 3];
        System.Runtime.InteropServices.Marshal.Copy(rgbMat.Data, data, 0, data.Length);

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < accessor.Width; x++)
                {
                    var idx = (y * accessor.Width + x) * 3;
                    row[x] = new Rgb24(data[idx], data[idx + 1], data[idx + 2]);
                }
            }
        });

        return image;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_lazyPredictor.IsValueCreated)
        {
            Predictor.Dispose();
        }
        GC.SuppressFinalize(this);
    }

    ~BgiYoloPredictor()
    {
        Dispose();
    }
}
