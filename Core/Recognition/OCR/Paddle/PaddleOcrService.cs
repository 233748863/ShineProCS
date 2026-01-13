using System.Diagnostics;
using System.Globalization;
using System.IO;
using OpenCvSharp;
using ShineProCS.Core.Config;
using ShineProCS.Core.Recognition.OCR.Engine;
using ShineProCS.Core.Recognition.ONNX;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using Size = OpenCvSharp.Size;
using YamlScalar = YamlDotNet.Core.Events.Scalar;

namespace ShineProCS.Core.Recognition.OCR.Paddle;

/// <summary>
/// PaddleOCR 服务（BetterGI 风格）
/// 使用 ONNX Runtime 直接推理，支持多种 GPU 加速
/// </summary>
public class PaddleOcrService : IOcrService, IDisposable
{
    private readonly Det _localDetModel;
    private readonly Rec _localRecModel;

    /// <summary>
    /// PaddleOCR 模型类型定义
    /// </summary>
    public record PaddleOcrModelType(
        BgiOnnxModel DetectionModel,
        OcrVersionConfig DetectionVersion,
        BgiOnnxModel RecognitionModel,
        OcrVersionConfig RecognitionVersion,
        Func<IReadOnlyList<string>> RecLabel,
        string PreHeatImagePath
    )
    {
        public static string TestImagePath = Global.Absolute(@"Assets\Models\PaddleOCR\test_pp_ocr.png");
        public static string TestNumberImagePath = Global.Absolute(@"Assets\Models\PaddleOCR\test_pp_ocr_number.png");

        private static readonly Func<BgiOnnxModel, IReadOnlyList<string>> DefaultRecLabelFunc =
            recModel =>
            {
                const string modelConfigFileName = "inference.yml";
                var configFilePath = Path.Combine(
                    Path.GetDirectoryName(recModel.ModalPath) ??
                    throw new InvalidOperationException("无法获取模型目录"),
                    modelConfigFileName);

                if (!File.Exists(configFilePath))
                    throw new FileNotFoundException(
                        $"PaddleOCR 配置文件 {modelConfigFileName} 未找到: {configFilePath}");

                using var reader = new StreamReader(configFilePath);
                var parser = new Parser(reader);

                // 遍历 YAML 查找 PostProcess:character_dict
                while (parser.MoveNext())
                {
                    if (parser.Current is not YamlScalar { Value: "PostProcess" }) continue;
                    parser.MoveNext(); // 应该是 MappingStart
                    while (parser.MoveNext())
                    {
                        if (parser.Current is not YamlScalar { Value: "character_dict" }) continue;
                        parser.MoveNext(); // 应该是 SequenceStart
                        var result = new List<string>();
                        while (parser.MoveNext())
                        {
                            switch (parser.Current)
                            {
                                case SequenceEnd:
                                    return result;
                                case YamlScalar charScalar:
                                    result.Add(charScalar.Value);
                                    break;
                            }
                        }
                    }
                }

                throw new InvalidOperationException("未在 YAML 的 PostProcess 部分找到 character_dict。");
            };


        private static PaddleOcrModelType Create(
            BgiOnnxModel detectionModel,
            OcrVersionConfig detectionVersion,
            BgiOnnxModel recognitionModel,
            OcrVersionConfig recognitionVersion,
            string? preHeatImagePath = null,
            Func<IReadOnlyList<string>>? recLabel = null
        )
        {
            return new PaddleOcrModelType(
                detectionModel,
                detectionVersion,
                recognitionModel,
                recognitionVersion,
                recLabel ?? (() => DefaultRecLabelFunc(recognitionModel)),
                preHeatImagePath ?? TestImagePath);
        }

        public (Det, Rec) Build(BgiOnnxFactory onnxFactory)
        {
            return (
                new Det(DetectionModel, DetectionVersion, onnxFactory),
                new Rec(RecognitionModel, RecLabel(), RecognitionVersion, onnxFactory));
        }

        #region 预定义模型类型

        public static readonly PaddleOcrModelType V4 = Create(
            BgiOnnxModel.PaddleOcrDetV4,
            OcrVersionConfig.PpOcrV4,
            BgiOnnxModel.PaddleOcrRecV4,
            OcrVersionConfig.PpOcrV4);

        public static readonly PaddleOcrModelType V4En = Create(
            BgiOnnxModel.PaddleOcrDetV4,
            OcrVersionConfig.PpOcrV4,
            BgiOnnxModel.PaddleOcrRecV4En,
            OcrVersionConfig.PpOcrV4,
            TestNumberImagePath);

        public static readonly PaddleOcrModelType V5 = Create(
            BgiOnnxModel.PaddleOcrDetV5,
            OcrVersionConfig.PpOcrV5,
            BgiOnnxModel.PaddleOcrRecV5,
            OcrVersionConfig.PpOcrV5);

        public static readonly PaddleOcrModelType V5Latin = Create(
            BgiOnnxModel.PaddleOcrDetV5,
            OcrVersionConfig.PpOcrV5,
            BgiOnnxModel.PaddleOcrRecV5Latin,
            OcrVersionConfig.PpOcrV5);

        public static readonly PaddleOcrModelType V5Eslav = Create(
            BgiOnnxModel.PaddleOcrDetV5,
            OcrVersionConfig.PpOcrV5,
            BgiOnnxModel.PaddleOcrRecV5Eslav,
            OcrVersionConfig.PpOcrV5);

        public static readonly PaddleOcrModelType V5Korean = Create(
            BgiOnnxModel.PaddleOcrDetV5,
            OcrVersionConfig.PpOcrV5,
            BgiOnnxModel.PaddleOcrRecV5Korean,
            OcrVersionConfig.PpOcrV5);

        #endregion

        #region 语言自动选择

        /// <summary>
        /// 根据文化信息自动选择模型（V5 优先）
        /// </summary>
        public static PaddleOcrModelType? FromCultureInfo(CultureInfo cultureInfo)
        {
            HashSet<string> eslavLangs = new(StringComparer.OrdinalIgnoreCase) { "ru", "be", "uk" };
            HashSet<string> latinLangs = new(StringComparer.OrdinalIgnoreCase)
            {
                "af", "az", "bs", "cs", "cy", "da", "de", "es", "et", "fr", "ga", "hr", "hu", "id", "is", "it", "ku",
                "la", "lt", "lv", "mi", "ms", "mt", "nl", "no", "oc", "pi", "pl", "pt", "ro", "rs_latin", "sk", "sl",
                "sq", "sv", "sw", "tl", "tr", "uz", "vi", "french", "german"
            };
            HashSet<string> ocrV5Langs = new(StringComparer.OrdinalIgnoreCase) { "zh", "chi", "zho", "en", "japan", "jp" };

            List<string> names =
            [
                cultureInfo.EnglishName.ToLowerInvariant(),
                cultureInfo.Name.ToLowerInvariant(),
                cultureInfo.ThreeLetterISOLanguageName.ToLowerInvariant(),
                cultureInfo.TwoLetterISOLanguageName.ToLowerInvariant()
            ];

            foreach (var name in names)
            {
                if (name.Equals("korean") || name.Equals("ko"))
                    return V5Korean;

                if (eslavLangs.Contains(name))
                    return V5Eslav;

                if (latinLangs.Contains(name))
                    return V5Latin;

                if (ocrV5Langs.Contains(name))
                    return V5;
            }

            return null;
        }

        /// <summary>
        /// 根据文化信息自动选择模型（中英文优先使用 V4）
        /// </summary>
        public static PaddleOcrModelType? FromCultureInfoV4(CultureInfo cultureInfo)
        {
            var v5 = FromCultureInfo(cultureInfo);
            if (v5 == V5)
            {
                List<string> names =
                [
                    cultureInfo.EnglishName.ToLowerInvariant(),
                    cultureInfo.Name.ToLowerInvariant(),
                    cultureInfo.ThreeLetterISOLanguageName.ToLowerInvariant(),
                    cultureInfo.TwoLetterISOLanguageName.ToLowerInvariant()
                ];

                foreach (var name in names)
                {
                    if (name.Equals("en"))
                        return V4En;
                    if (name.Equals("zh-hant") || name.Equals("zh-tw") || name.Equals("zh-hk"))
                        return V5;
                }

                return V4;
            }

            return v5;
        }

        #endregion
    }


    /// <summary>
    /// 创建 PaddleOCR 服务实例
    /// </summary>
    public PaddleOcrService(BgiOnnxFactory bgiOnnxFactory, PaddleOcrModelType modelType)
    {
        var (modelsDet, modelsRec) = modelType.Build(bgiOnnxFactory);
        _localDetModel = modelsDet;
        _localRecModel = modelsRec;

        // 预热模型
        if (File.Exists(modelType.PreHeatImagePath))
        {
            using var preHeatImageMat = Cv2.ImRead(modelType.PreHeatImagePath);
            if (preHeatImageMat != null && !preHeatImageMat.Empty())
            {
                var preHeatResult = RunAll(preHeatImageMat, 1);
                Debug.WriteLine(
                    $"PaddleOcrService 预热完成，使用模型: {modelType.DetectionModel.Name} 和 {modelType.RecognitionModel.Name}，结果: {preHeatResult.Text}");
            }
        }
        else
        {
            Debug.WriteLine($"预热图片未找到: {modelType.PreHeatImagePath}，跳过预热");
        }
    }

    /// <summary>
    /// 执行 OCR 识别，返回文本
    /// </summary>
    public string Ocr(Mat mat)
    {
        return OcrResult(mat).Text;
    }

    /// <summary>
    /// 执行 OCR 识别，返回完整结果
    /// </summary>
    public OCR.OcrResult OcrResult(Mat mat)
    {
        if (mat.Channels() == 4)
        {
            using var mat3 = mat.CvtColor(ColorConversionCodes.BGRA2BGR);
            return _OcrResult(mat3);
        }

        return _OcrResult(mat);
    }

    /// <summary>
    /// 不使用检测器直接识别（适用于已裁剪的文本区域）
    /// </summary>
    public string OcrWithoutDetector(Mat mat)
    {
        var startTime = Stopwatch.GetTimestamp();
        var str = _localRecModel.Run(mat).Text;
        var time = Stopwatch.GetElapsedTime(startTime);
        Debug.WriteLine($"PaddleOcrWithoutDetector 耗时 {time.TotalMilliseconds}ms 结果: {str}");
        return str;
    }

    private OCR.OcrResult _OcrResult(Mat mat)
    {
        var startTime = Stopwatch.GetTimestamp();
        var result = RunAll(mat);
        var time = Stopwatch.GetElapsedTime(startTime);
        Debug.WriteLine($"PaddleOcr 耗时 {time.TotalMilliseconds}ms 结果: {result.Text}");
        return result;
    }

    /// <summary>
    /// 执行完整的检测+识别流程
    /// </summary>
    private OCR.OcrResult RunAll(Mat src, int recognizeBatchSize = 0)
    {
        var rects = _localDetModel.Run(src);
        Mat[] mats = rects.Select(rect =>
        {
            var roi = src[GetCropedRect(rect.BoundingRect(), src.Size())];
            return roi;
        }).ToArray();

        try
        {
            return new OCR.OcrResult(_localRecModel.Run(mats, recognizeBatchSize)
                .Select((result, i) => new OCR.OcrResultRegion(rects[i], result.Text, result.Score))
                .ToArray());
        }
        finally
        {
            foreach (var mat in mats) mat.Dispose();
        }
    }

    /// <summary>
    /// 获取裁剪后的矩形区域，确保不超出图像边界
    /// </summary>
    private static Rect GetCropedRect(Rect rect, Size size)
    {
        return Rect.FromLTRB(
            Math.Clamp(rect.Left, 0, size.Width),
            Math.Clamp(rect.Top, 0, size.Height),
            Math.Clamp(rect.Right, 0, size.Width),
            Math.Clamp(rect.Bottom, 0, size.Height));
    }

    /// <summary>
    /// 获取配置名称
    /// </summary>
    public (string DetConfigName, string RecConfigName) GetConfigName =>
        (_localDetModel.GetConfigName, _localRecModel.GetConfigName);

    public void Dispose()
    {
        _localDetModel.Dispose();
        _localRecModel.Dispose();
        GC.SuppressFinalize(this);
    }
}
