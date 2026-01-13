namespace ShineProCS.Core.Recognition.OCR.Engine;

/// <summary>
/// PP-OCR 版本配置
/// </summary>
public readonly record struct OcrVersionConfig(
    string Name,
    OcrImgMode Mode,
    bool ChannelFirst,
    OcrNormalizeImage NormalizeImage,
    OcrShape Shape)
{
    /// <summary>
    /// PP-OCR V3 配置
    /// </summary>
    public static readonly OcrVersionConfig PpOcrV3 = new(
        "PP-OCRv3",
        OcrImgMode.BGR,
        false,
        new OcrNormalizeImage(
            1.0f / 255.0f,
            [0.485f, 0.456f, 0.406f],
            [0.229f, 0.224f, 0.225f]
        ),
        new OcrShape(3, 320, 48));

    /// <summary>
    /// PP-OCR V4 配置
    /// </summary>
    public static readonly OcrVersionConfig PpOcrV4 = new(
        "PP-OCRv4",
        OcrImgMode.BGR,
        false,
        new OcrNormalizeImage(
            1.0f / 255.0f,
            [0.485f, 0.456f, 0.406f],
            [0.229f, 0.224f, 0.225f]
        ),
        new OcrShape(3, 320, 48));

    /// <summary>
    /// PP-OCR V5 配置
    /// </summary>
    public static readonly OcrVersionConfig PpOcrV5 = new(
        "PP-OCRv5",
        OcrImgMode.BGR,
        false,
        new OcrNormalizeImage(
            1.0f / 255.0f,
            [0.485f, 0.456f, 0.406f],
            [0.229f, 0.224f, 0.225f]
        ),
        new OcrShape(3, 320, 48));
}
