using System.Globalization;
using Microsoft.Extensions.Logging;
using ShineProCS.Core.Config;
using ShineProCS.Core.Recognition.OCR.Paddle;
using ShineProCS.Core.Recognition.ONNX;

namespace ShineProCS.Core.Recognition.OCR;

/// <summary>
/// OCR 引擎类型
/// </summary>
public enum OcrEngineTypes
{
    Paddle
}

/// <summary>
/// OCR 工厂（BetterGI 风格）
/// 负责创建和管理 OCR 服务实例
/// </summary>
public class OcrFactory : IDisposable
{
    private readonly ILogger<OcrFactory> _logger;
    private readonly BgiOnnxFactory _onnxFactory;
    private readonly OcrConfig _config;
    private IOcrService? _paddleOcrService;

    /// <summary>
    /// 获取 PaddleOCR 服务实例（懒加载）
    /// </summary>
    public IOcrService PaddleOcr => _paddleOcrService ??= Create(OcrEngineTypes.Paddle);

    /// <summary>
    /// 创建 OCR 工厂实例
    /// </summary>
    public OcrFactory(ILogger<OcrFactory> logger, BgiOnnxFactory onnxFactory, OcrConfig? config = null)
    {
        _logger = logger;
        _onnxFactory = onnxFactory;
        _config = config ?? new OcrConfig();
    }

    /// <summary>
    /// 创建指定类型的 OCR 服务
    /// </summary>
    private IOcrService Create(OcrEngineTypes type)
    {
        var result = type switch
        {
            OcrEngineTypes.Paddle => CreatePaddleOcrInstance(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "不支持的 OCR 引擎类型")
        };
        _logger.LogDebug("创建了类型为 {Type} 的 OCR 服务", Enum.GetName(type));
        return result;
    }

    /// <summary>
    /// 获取文化信息
    /// </summary>
    private CultureInfo GetCultureInfo()
    {
        try
        {
            return new CultureInfo(_config.GameCultureInfoName);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "获取游戏文化信息失败，使用默认文化信息: zh-CN");
            return new CultureInfo("zh-CN");
        }
    }

    /// <summary>
    /// 创建 PaddleOCR 实例
    /// </summary>
    private PaddleOcrService CreatePaddleOcrInstance()
    {
        return _config.PaddleOcrModelConfig switch
        {
            PaddleOcrModelConfig.V4Auto =>
                new PaddleOcrService(_onnxFactory,
                    PaddleOcrService.PaddleOcrModelType.FromCultureInfoV4(GetCultureInfo()) ??
                    PaddleOcrService.PaddleOcrModelType.V4),

            PaddleOcrModelConfig.V5Auto =>
                new PaddleOcrService(_onnxFactory,
                    PaddleOcrService.PaddleOcrModelType.FromCultureInfo(GetCultureInfo()) ??
                    PaddleOcrService.PaddleOcrModelType.V5),

            PaddleOcrModelConfig.V5 =>
                new PaddleOcrService(_onnxFactory, PaddleOcrService.PaddleOcrModelType.V5),

            PaddleOcrModelConfig.V4 =>
                new PaddleOcrService(_onnxFactory, PaddleOcrService.PaddleOcrModelType.V4),

            PaddleOcrModelConfig.V4En =>
                new PaddleOcrService(_onnxFactory, PaddleOcrService.PaddleOcrModelType.V4En),

            PaddleOcrModelConfig.V5Korean =>
                new PaddleOcrService(_onnxFactory, PaddleOcrService.PaddleOcrModelType.V5Korean),

            PaddleOcrModelConfig.V5Latin =>
                new PaddleOcrService(_onnxFactory, PaddleOcrService.PaddleOcrModelType.V5Latin),

            PaddleOcrModelConfig.V5Eslav =>
                new PaddleOcrService(_onnxFactory, PaddleOcrService.PaddleOcrModelType.V5Eslav),

            _ => throw new ArgumentOutOfRangeException(nameof(_config.PaddleOcrModelConfig),
                _config.PaddleOcrModelConfig, "不支持的 PaddleOCR 模型配置")
        };
    }

    /// <summary>
    /// 卸载 OCR 服务
    /// </summary>
    public Task Unload()
    {
        if (_paddleOcrService is IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "卸载 OCR 服务时发生错误");
            }
        }
        _paddleOcrService = null;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Unload().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    ~OcrFactory()
    {
        Dispose();
    }
}
