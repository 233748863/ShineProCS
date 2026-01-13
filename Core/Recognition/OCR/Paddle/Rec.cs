using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using ShineProCS.Core.Recognition.OCR.Engine;
using ShineProCS.Core.Recognition.ONNX;

namespace ShineProCS.Core.Recognition.OCR.Paddle;

/// <summary>
/// PaddleOCR 文本识别模型（BetterGI 风格）
/// </summary>
public class Rec(
    BgiOnnxModel model,
    IReadOnlyList<string> labels,
    OcrVersionConfig config,
    BgiOnnxFactory bgiOnnxFactory) : IDisposable
{
    private readonly InferenceSession _session = bgiOnnxFactory.CreateInferenceSession(model, true);

    public void Dispose()
    {
        lock (_session)
        {
            _session.Dispose();
        }
        GC.SuppressFinalize(this);
    }

    ~Rec()
    {
        lock (_session)
        {
            _session.Dispose();
        }
    }

    /// <summary>
    /// 批量运行 OCR 识别
    /// </summary>
    public OCR.OcrRecognizerResult[] Run(Mat[] srcs, int batchSize = 0)
    {
        if (srcs.Length == 0) return [];

        var chooseBatchSize = batchSize != 0 ? batchSize : Math.Min(8, Environment.ProcessorCount);

        return srcs
            .Select((x, i) => (mat: x, i))
            .OrderBy(x => x.mat.Width)
            .Chunk(chooseBatchSize)
            .Select(x => (result: RunMulti(x.Select(x1 => x1.mat).ToArray()), ids: x.Select(x1 => x1.i).ToArray()))
            .SelectMany(x => x.result.Zip(x.ids, (result, i) => (result, i)))
            .OrderBy(x => x.i)
            .Select(x => x.result)
            .ToArray();
    }

    /// <summary>
    /// 运行单张图像识别
    /// </summary>
    public OCR.OcrRecognizerResult Run(Mat src)
    {
        return RunMulti([src]).Single();
    }


    /// <summary>
    /// 批量识别多张图像
    /// </summary>
    private OCR.OcrRecognizerResult[] RunMulti(Mat[] srcs)
    {
        if (srcs.Length == 0) return [];

        for (var i = 0; i < srcs.Length; ++i)
        {
            var src = srcs[i];
            if (src.Empty())
                throw new ArgumentException($"src[{i}] 图像为空，输入图像错误？");
        }

        var modelHeight = config.Shape.Height;
        var maxWidth = (int)Math.Ceiling(srcs.Max(src =>
        {
            var size = src.Size();
            return 1.0 * size.Width / size.Height * modelHeight;
        }));
        
        List<IMemoryOwner<float>> owners = [];
        (int[], float[])[] resultTensors;
        
        try
        {
            resultTensors = srcs
                .Select(src =>
                {
                    using var channel3 = src.Channels() switch
                    {
                        4 => src.CvtColor(ColorConversionCodes.BGRA2BGR),
                        1 => src.CvtColor(ColorConversionCodes.GRAY2BGR),
                        3 => src,
                        var x => throw new Exception($"不支持的图像通道数: {x}，允许: (1/3/4)")
                    };
                    
                    var result = OcrUtils.ResizeNormImg(channel3, new OcrShape(3, maxWidth, modelHeight), out var owner);
                    lock (owners)
                    {
                        owners.Add(owner);
                    }
                    return result;
                })
                .Select(inputTensor =>
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
                            
                        var tensor = output.AsTensor<float>();
                        return (tensor.Dimensions.ToArray(), tensor.ToArray());
                    }
                })
                .ToArray();
        }
        finally
        {
            owners.ForEach(x => x.Dispose());
        }

        return resultTensors.SelectMany(resultTensor =>
        {
            var resultArray = resultTensor.Item2;
            var resultShape = resultTensor.Item1;
            GCHandle dataHandle = default;
            
            try
            {
                dataHandle = GCHandle.Alloc(resultArray, GCHandleType.Pinned);
                var dataPtr = dataHandle.AddrOfPinnedObject();
                var labelCount = resultShape[2];
                var charCount = resultShape[1];

                return Enumerable.Range(0, resultShape[0])
                    .Select(i =>
                    {
                        StringBuilder sb = new();
                        var lastIndex = 0;
                        float score = 0;
                        
                        for (var n = 0; n < charCount; ++n)
                        {
                            using var mat = Mat.FromPixelData(1, labelCount, MatType.CV_32FC1,
                                dataPtr + (n + i * charCount) * labelCount * sizeof(float));
                            var maxIdx = new int[2];
                            mat.MinMaxIdx(out _, out var maxVal, [], maxIdx);

                            if (maxIdx[1] > 0 && !(n > 0 && maxIdx[1] == lastIndex))
                            {
                                score += (float)maxVal;
                                sb.Append(OcrUtils.GetLabelByIndex(maxIdx[1], labels));
                            }

                            lastIndex = maxIdx[1];
                        }

                        return new OCR.OcrRecognizerResult(sb.ToString(), score / sb.Length);
                    })
                    .ToArray();
            }
            finally
            {
                dataHandle.Free();
            }
        }).ToArray();
    }

    /// <summary>
    /// 获取配置名称
    /// </summary>
    public string GetConfigName => config.Name;
}
