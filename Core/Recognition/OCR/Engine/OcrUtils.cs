using System.Buffers;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using Size = OpenCvSharp.Size;

namespace ShineProCS.Core.Recognition.OCR.Engine;

/// <summary>
/// OCR 工具类，提供图像预处理和张量转换功能
/// </summary>
public static class OcrUtils
{
    /// <summary>
    /// 用于 Det 模型的归一化和张量转换
    /// </summary>
    public static Tensor<float> NormalizeToTensorDnn(
        Mat src,
        float? scale,
        float[]? mean,
        float[]? std,
        out IMemoryOwner<float> tensorMemoryOwner,
        bool swapRb = false,
        bool crop = false,
        Size size = default)
    {
        scale ??= 0.00392156862745f;
        mean ??= [0.485f, 0.456f, 0.406f];
        std ??= [0.229f, 0.224f, 0.225f];
        
        var channels = src.Channels();
        if (channels != 3)
            throw new ArgumentException($"图像通道数必须为3，当前为{channels}");
        
        using var stdMat = new Mat();
        Mat[] bgr = [];
        try
        {
            bgr = src.Split();
            for (var i = 0; i < bgr.Length; ++i)
            {
                bgr[i].ConvertTo(bgr[i], MatType.CV_32FC1, 1 / std[i],
                    (0.0 - mean[i]) / std[i] / (float)scale);
            }
            Cv2.Merge(bgr, stdMat);
        }
        finally
        {
            foreach (var channel in bgr) channel.Dispose();
        }

        using var blob = CvDnn.BlobFromImage(
            stdMat,
            (double)scale,
            size,
            default,
            swapRb,
            crop
        );

        var total = (int)blob.Total();
        tensorMemoryOwner = MemoryPool<float>.Shared.Rent(total);
        blob.AsSpan<float>().CopyTo(tensorMemoryOwner.Memory.Span);
        
        return new DenseTensor<float>(
            tensorMemoryOwner.Memory[..total],
            [1, channels, stdMat.Rows, stdMat.Cols]
        );
    }


    /// <summary>
    /// 用于 Rec 模型的图像调整和归一化
    /// </summary>
    public static Tensor<float> ResizeNormImg(
        Mat img,
        OcrShape imageShape,
        out IMemoryOwner<float> tensorMemoryOwner,
        bool padding = true,
        InterpolationFlags interpolation = InterpolationFlags.Linear)
    {
        var imgH = imageShape.Height;
        var imgW = imageShape.Width;
        var h = img.Height;
        var w = img.Width;

        using var resizedImage = new Mat();
        if (!padding)
        {
            Cv2.Resize(img, resizedImage, new Size(imgW, imgH), 0, 0, interpolation);
        }
        else
        {
            var ratio = w / (double)h;
            var resizedW = Math.Ceiling(imgH * ratio) > imgW ? imgW : (int)Math.Ceiling(imgH * ratio);
            Cv2.Resize(img, resizedImage, new Size(resizedW, imgH), 0, 0, interpolation);
        }

        // 归一化到 [-1, 1]
        using var blob = CvDnn.BlobFromImage(
            resizedImage,
            2 / 255f,
            default,
            new Scalar(127.5, 127.5, 127.5),
            false,
            false
        );

        var total = blob.Total();
        tensorMemoryOwner = MemoryPool<float>.Shared.Rent((int)total);
        blob.AsSpan<float>().CopyTo(tensorMemoryOwner.Memory.Span);
        
        return new DenseTensor<float>(
            tensorMemoryOwner.Memory[..(int)total],
            [1, resizedImage.Channels(), resizedImage.Rows, resizedImage.Cols]
        );
    }

    /// <summary>
    /// 根据索引获取标签
    /// </summary>
    public static string GetLabelByIndex(int i, IReadOnlyList<string> labels)
    {
        return i switch
        {
            var x when x > 0 && x <= labels.Count => labels[x - 1],
            var x when x == labels.Count + 1 => " ",
            _ => throw new Exception(
                $"无法获取标签: 索引 {i} 超出范围 {labels.Count}，OCR 模型或标签不匹配？")
        };
    }

    /// <summary>
    /// 将张量转换为 Mat
    /// </summary>
    public static Mat Tensor2Mat(Tensor<float> tensor)
    {
        var dimensions = tensor.Dimensions;
        if (dimensions.Length != 4 || dimensions[0] != 1 || dimensions[1] != 1)
            throw new ArgumentException($"张量形状错误: {string.Join(",", dimensions.ToArray())}");
        
        if (tensor is not DenseTensor<float> denseTensor)
            return Mat.FromPixelData(dimensions[2], dimensions[3], MatType.CV_32FC1, tensor.ToArray());
        
        var mat = new Mat(new Size(dimensions[3], dimensions[2]), MatType.CV_32FC1);
        denseTensor.Buffer.Span.CopyTo(mat.AsSpan<float>());
        return mat;
    }
}
