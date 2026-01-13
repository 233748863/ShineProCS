using OpenCvSharp;

namespace ShineProCS.Core.Recognition.OCR;

/// <summary>
/// OCR 文字识别服务接口（BetterGI 风格）
/// </summary>
public interface IOcrService
{
    /// <summary>
    /// 识别图像中的所有文字，返回纯文本
    /// </summary>
    /// <param name="mat">输入图像（推荐三通道 BGR）</param>
    /// <returns>识别的文本</returns>
    string Ocr(Mat mat);

    /// <summary>
    /// 识别图像中的所有文字，返回完整结果
    /// </summary>
    /// <param name="mat">输入图像（推荐三通道 BGR）</param>
    /// <returns>包含位置信息的识别结果</returns>
    OcrResult OcrResult(Mat mat);

    /// <summary>
    /// 不使用检测器直接识别（适用于已裁剪的文本区域）
    /// </summary>
    /// <param name="mat">输入图像</param>
    /// <returns>识别的文本</returns>
    string OcrWithoutDetector(Mat mat);
}
