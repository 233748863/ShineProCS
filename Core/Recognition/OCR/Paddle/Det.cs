using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using ShineProCS.Core.Recognition.OCR.Engine;
using ShineProCS.Core.Recognition.ONNX;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;

namespace ShineProCS.Core.Recognition.OCR.Paddle;

/// <summary>
/// PaddleOCR 文本检测模型（BetterGI 风格）
/// </summary>
public class Det(BgiOnnxModel model, OcrVersionConfig config, BgiOnnxFactory bgiOnnxFactory) : IDisposable
{
    private readonly InferenceSession _session = bgiOnnxFactory.CreateInferenceSession(model, true);

    /// <summary>
    /// 最大图像尺寸
    /// </summary>
    public int? MaxSize { get; set; } = 960;

    /// <summary>
    /// 膨胀大小
    /// </summary>
    public int? DilatedSize { get; set; } = 2;

    /// <summary>
    /// 文本框分数阈值
    /// </summary>
    public float? BoxScoreThreshold { get; set; } = 0.7f;

    /// <summary>
    /// 二值化阈值
    /// </summary>
    public float? BoxThreshold { get; set; } = 0.3f;

    /// <summary>
    /// 最小文本框尺寸
    /// </summary>
    public int MinSize { get; set; } = 3;

    /// <summary>
    /// 文本框扩展比例
    /// </summary>
    public float UnclipRatio { get; set; } = 2.0f;

    ~Det()
    {
        lock (_session)
        {
            _session.Dispose();
        }
    }

    public void Dispose()
    {
        lock (_session)
        {
            _session.Dispose();
        }
        GC.SuppressFinalize(this);
    }


    /// <summary>
    /// 运行文本检测
    /// </summary>
    public RotatedRect[] Run(Mat src)
    {
        using var pred = RunRaw(src, out var resizedSize);
        using Mat cbuf = new();
        using var roi = pred[0, resizedSize.Height, 0, resizedSize.Width];
        roi.ConvertTo(cbuf, MatType.CV_8UC1, 255);
        
        using Mat dilated = new();
        using var binary = BoxThreshold != null
            ? cbuf.Threshold((int)(BoxThreshold * 255), 255, ThresholdTypes.Binary)
            : cbuf;
            
        if (DilatedSize != null)
        {
            using var ones = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(DilatedSize.Value, DilatedSize.Value));
            Cv2.Dilate(binary, dilated, ones);
        }
        else
        {
            Cv2.CopyTo(binary, dilated);
        }

        var contours = dilated.FindContoursAsArray(RetrievalModes.List, ContourApproximationModes.ApproxSimple);
        var scaleRate = 1.0 * src.Width / resizedSize.Width;

        var rects = contours
            .Where(x => BoxScoreThreshold == null || GetScore(x, pred) > BoxScoreThreshold)
            .Select(Cv2.MinAreaRect)
            .Where(x => x.Size.Width > MinSize && x.Size.Height > MinSize)
            .Select(rect =>
            {
                var minEdge = Math.Min(rect.Size.Width, rect.Size.Height);
                Size2f newSize = new(
                    (rect.Size.Width + UnclipRatio * minEdge) * scaleRate,
                    (rect.Size.Height + UnclipRatio * minEdge) * scaleRate);
                RotatedRect largerRect = new(rect.Center * scaleRate, newSize, rect.Angle);
                return largerRect;
            })
            .OrderBy(v => v.Center.Y)
            .ThenBy(v => v.Center.X)
            .ToArray();

        return rects;
    }

    /// <summary>
    /// 运行原始推理
    /// </summary>
    public Mat RunRaw(Mat src, out Size resizedSize)
    {
        var padded = src.Channels() switch
        {
            4 => src.CvtColor(ColorConversionCodes.BGRA2BGR),
            1 => src.CvtColor(ColorConversionCodes.GRAY2BGR),
            3 => src,
            var x => throw new Exception($"不支持的图像通道数: {x}，允许: (1/3/4)")
        };

        using (var resized = MatResize(padded, MaxSize))
        {
            resizedSize = new Size(resized.Width, resized.Height);
            padded = MatPadding32(resized);
        }

        using (var _ = padded)
        {
            var inputTensor = OcrUtils.NormalizeToTensorDnn(
                padded, 
                config.NormalizeImage.Scale,
                config.NormalizeImage.Mean, 
                config.NormalizeImage.Std, 
                out var owner);
                
            using (owner)
            {
                lock (_session)
                {
                    using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run([
                        NamedOnnxValue.CreateFromTensor(_session.InputNames[0], inputTensor)
                    ]);
                    
                    var output = results[0];
                    if (output.ElementType is not TensorElementType.Float)
                        throw new Exception($"输出张量类型错误: {output.ElementType}");

                    if (output.ValueType is not OnnxValueType.ONNX_TYPE_TENSOR)
                        throw new Exception($"输出值类型错误: {output.ValueType}");
                        
                    var outputTensor = output.AsTensor<float>();
                    return OcrUtils.Tensor2Mat(outputTensor);
                }
            }
        }
    }

    /// <summary>
    /// 将图像填充到 32 的倍数
    /// </summary>
    private static Mat MatPadding32(Mat src)
    {
        var size = src.Size();
        Size newSize = new(
            32 * Math.Ceiling(1.0 * size.Width / 32),
            32 * Math.Ceiling(1.0 * size.Height / 32));
        return src.CopyMakeBorder(0, newSize.Height - size.Height, 0, newSize.Width - size.Width, 
            BorderTypes.Constant, Scalar.Black);
    }

    /// <summary>
    /// 按比例缩放图像，保持长边不超过 maxSize
    /// </summary>
    private static Mat MatResize(Mat src, int? maxSize)
    {
        if (maxSize == null) return src.Clone();

        var size = src.Size();
        var longEdge = Math.Max(size.Width, size.Height);
        var scaleRate = 1.0 * maxSize.Value / longEdge;
        return scaleRate < 1.0 ? src.Resize(default, scaleRate, scaleRate) : src.Clone();
    }

    /// <summary>
    /// 计算轮廓分数
    /// </summary>
    private static float GetScore(Point[] contour, Mat pred)
    {
        var width = pred.Width;
        var height = pred.Height;
        var boxX = contour.Select(v => v.X).ToArray();
        var boxY = contour.Select(v => v.Y).ToArray();

        var xmin = Math.Clamp(boxX.Min(), 0, width - 1);
        var xmax = Math.Clamp(boxX.Max(), 0, width - 1);
        var ymin = Math.Clamp(boxY.Min(), 0, height - 1);
        var ymax = Math.Clamp(boxY.Max(), 0, height - 1);

        var rootPoints = contour
            .Select(v => new Point(v.X - xmin, v.Y - ymin))
            .ToArray();
            
        using Mat mask = new(ymax - ymin + 1, xmax - xmin + 1, MatType.CV_8UC1, Scalar.Black);
        mask.FillPoly([rootPoints], new Scalar(1));

        using var croppedMat = pred[ymin, ymax + 1, xmin, xmax + 1];
        var score = (float)croppedMat.Mean(mask).Val0;

        return score;
    }

    /// <summary>
    /// 获取配置名称
    /// </summary>
    public string GetConfigName => config.Name;
}
