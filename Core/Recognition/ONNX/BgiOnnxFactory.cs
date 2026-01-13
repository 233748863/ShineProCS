using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using ShineProCS.Core.Config;
using ShineProCS.Core.Recognition.YOLO;

namespace ShineProCS.Core.Recognition.ONNX;

/// <summary>
/// ONNX 会话工厂（简化版）
/// 仅支持 CPU 和 DirectML 两种执行提供程序
/// GPU 推理失败时自动回退到 CPU
/// </summary>
public class BgiOnnxFactory
{
    private readonly ILogger<BgiOnnxFactory> _logger;

    /// <summary>
    /// 当前使用的 Provider 类型列表
    /// </summary>
    public ProviderType[] ProviderTypes { get; }
    
    /// <summary>
    /// DirectML 设备 ID
    /// </summary>
    public int DmlDeviceId { get; }
    
    /// <summary>
    /// 是否强制 OCR 使用 CPU
    /// </summary>
    public bool CpuOcr { get; }

    /// <summary>
    /// 创建 ONNX 工厂实例
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="config">硬件加速配置，为 null 时使用默认配置</param>
    public BgiOnnxFactory(ILogger<BgiOnnxFactory> logger, HardwareAccelerationConfig? config = null)
    {
        _logger = logger;
        config ??= new HardwareAccelerationConfig();

        DmlDeviceId = config.GpuDevice;
        CpuOcr = config.CpuOcr;
        ProviderTypes = GetProviderTypes(config.InferenceDevice);

        _logger.LogDebug(
            "[ONNX] 初始化完成 - Provider: {Providers}, 设备类型: {Device}, GPU设备ID: {GpuId}, OCR强制CPU: {CpuOcr}",
            string.Join(",", ProviderTypes.Select(Enum.GetName!)),
            config.InferenceDevice,
            DmlDeviceId,
            CpuOcr);
    }

    /// <summary>
    /// 根据推理设备类型获取对应的 Provider 列表
    /// DirectML 模式下会自动添加 CPU 作为回退选项
    /// </summary>
    /// <param name="deviceType">推理设备类型</param>
    /// <returns>Provider 类型数组</returns>
    private ProviderType[] GetProviderTypes(InferenceDeviceType deviceType)
    {
        switch (deviceType)
        {
            case InferenceDeviceType.Cpu:
                // 纯 CPU 模式
                return [ProviderType.Cpu];

            case InferenceDeviceType.DirectML:
                // DirectML GPU 模式，尝试初始化 GPU，失败则回退到 CPU
                return TryInitializeDirectML() 
                    ? [ProviderType.Dml, ProviderType.Cpu]  // GPU + CPU 回退
                    : [ProviderType.Cpu];                   // 仅 CPU

            default:
                throw new InvalidEnumArgumentException(
                    nameof(deviceType), 
                    (int)deviceType, 
                    typeof(InferenceDeviceType));
        }
    }

    /// <summary>
    /// 尝试初始化 DirectML Provider
    /// </summary>
    /// <returns>初始化成功返回 true，失败返回 false</returns>
    private bool TryInitializeDirectML()
    {
        SessionOptions? testSession = null;
        try
        {
            testSession = new SessionOptions();
            testSession.AppendExecutionProvider_DML(DmlDeviceId);
            _logger.LogDebug("[ONNX] DirectML 初始化成功，GPU 设备 ID: {DeviceId}", DmlDeviceId);
            return true;
        }
        catch (Exception e)
        {
            _logger.LogWarning(
                "[ONNX] DirectML 初始化失败，自动回退到 CPU 模式。错误: {Error}", 
                e.Message);
            return false;
        }
        finally
        {
            testSession?.Dispose();
        }
    }

    /// <summary>
    /// 根据模型创建 YOLO 预测器
    /// </summary>
    /// <param name="model">ONNX 模型配置</param>
    /// <returns>YOLO 预测器实例</returns>
    public BgiYoloPredictor CreateYoloPredictor(BgiOnnxModel model)
    {
        _logger.LogDebug("[YOLO] 创建预测器，模型: {ModelName}", model.Name);
        return new BgiYoloPredictor(model, model.ModalPath, CreateSessionOptions(model, false));
    }

    /// <summary>
    /// 根据模型创建 ONNX 推理会话
    /// </summary>
    /// <param name="model">ONNX 模型配置</param>
    /// <param name="ocr">是否用于 OCR，为 true 且 CpuOcr 启用时强制使用 CPU</param>
    /// <returns>推理会话实例</returns>
    public InferenceSession CreateInferenceSession(BgiOnnxModel model, bool ocr = false)
    {
        _logger.LogDebug("[ONNX] 创建推理会话，模型: {ModelName}, OCR模式: {IsOcr}", model.Name, ocr);
        
        // 如果是 OCR 且配置了强制 CPU，则使用 CPU Provider
        ProviderType[]? forcedProvider = null;
        if (CpuOcr && ocr)
        {
            forcedProvider = [ProviderType.Cpu];
            _logger.LogDebug("[ONNX] OCR 模式强制使用 CPU");
        }

        return new InferenceSession(model.ModalPath, CreateSessionOptions(model, false, forcedProvider));
    }

    /// <summary>
    /// 创建 SessionOptions，配置执行提供程序
    /// </summary>
    /// <param name="model">ONNX 模型配置</param>
    /// <param name="genCache">是否生成缓存（保留参数以兼容接口，当前未使用）</param>
    /// <param name="forcedProvider">强制使用的 Provider 列表，为 null 时使用默认配置</param>
    /// <returns>配置好的 SessionOptions</returns>
    private SessionOptions CreateSessionOptions(
        BgiOnnxModel model, 
        bool genCache, 
        ProviderType[]? forcedProvider = null)
    {
        var sessionOptions = new SessionOptions();
        var providers = forcedProvider ?? ProviderTypes;

        foreach (var providerType in providers)
        {
            try
            {
                switch (providerType)
                {
                    case ProviderType.Dml:
                        // DirectML 配置
                        // 禁用内存模式并使用顺序执行以提高稳定性
                        sessionOptions.AppendExecutionProvider_DML(DmlDeviceId);
                        sessionOptions.EnableMemoryPattern = false;
                        sessionOptions.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
                        _logger.LogDebug("[ONNX] 已添加 DirectML Provider，设备 ID: {DeviceId}", DmlDeviceId);
                        break;

                    case ProviderType.Cpu:
                        // CPU 配置
                        sessionOptions.AppendExecutionProvider_CPU();
                        
                        // 针对 OCR 模型优化线程配置
                        if (model.Name.Contains("PpOcr") || model.Name.Contains("Yap"))
                        {
                            sessionOptions.IntraOpNumThreads = 2;
                            sessionOptions.InterOpNumThreads = 1;
                            _logger.LogDebug("[ONNX] OCR 模型使用优化线程配置");
                        }
                        _logger.LogDebug("[ONNX] 已添加 CPU Provider");
                        break;

                    default:
                        throw new InvalidEnumArgumentException(
                            nameof(providerType), 
                            (int)providerType, 
                            typeof(ProviderType));
                }
            }
            catch (Exception e)
            {
                _logger.LogError(
                    "[ONNX] 无法加载 Provider {Provider}，跳过。错误: {Error}",
                    Enum.GetName(providerType), 
                    e.Message);
            }
        }

        return sessionOptions;
    }
}
