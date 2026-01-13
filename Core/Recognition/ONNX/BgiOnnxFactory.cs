using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.Win32;
using ShineProCS.Core.Config;
using ShineProCS.Core.Recognition.YOLO;

namespace ShineProCS.Core.Recognition.ONNX;

/// <summary>
/// ONNX 会话工厂（BetterGI 风格）
/// 支持 TensorRT、CUDA、DirectML、OpenVINO、CPU 等多种执行提供程序
/// </summary>
public class BgiOnnxFactory
{
    private readonly ILogger<BgiOnnxFactory> _logger;

    /// <summary>
    /// 缓存模型路径。如果一开始使用缓存就一直使用缓存文件，如果没有使用缓存就一直使用原始模型路径。
    /// 这样能避免并发加载模型问题。
    /// </summary>
    private readonly ConcurrentDictionary<BgiOnnxModel, string?> _cachedModelPaths = new();

    public ProviderType[] ProviderTypes { get; }
    public int DmlDeviceId { get; }
    public int CudaDeviceId { get; }
    public bool OptimizedModel { get; }
    public bool TrtUseEmbedMode { get; }
    public string OpenVinoDevice { get; }
    public bool EnableCache { get; }
    public bool CpuOcr { get; }
    public bool OpenVinoCache { get; }

    /// <summary>
    /// 创建 ONNX 工厂实例
    /// </summary>
    public BgiOnnxFactory(ILogger<BgiOnnxFactory> logger, HardwareAccelerationConfig? config = null)
    {
        _logger = logger;
        config ??= new HardwareAccelerationConfig();

        if (config.AutoAppendCudaPath) AppendCudaPath();

        if (!string.IsNullOrWhiteSpace(config.AdditionalPath))
            AppendPath(config.AdditionalPath.Split(Path.PathSeparator));

        OptimizedModel = config.OptimizedModel;
        CudaDeviceId = config.CudaDevice;
        DmlDeviceId = config.GpuDevice;
        TrtUseEmbedMode = config.EmbedTensorRtCache;
        EnableCache = config.EnableTensorRtCache;
        CpuOcr = config.CpuOcr;
        OpenVinoDevice = config.OpenVinoDevice;
        OpenVinoCache = config.EnableOpenVinoCache;
        ProviderTypes = GetProviderType(config.InferenceDevice);

        _logger.LogDebug(
            "[ONNX] 启用的 Provider: {Device}, 初始化参数: InferenceDevice={InferenceDevice}, OptimizedModel={OptimizedModel}, CudaDeviceId={CudaDeviceId}, DmlDeviceId={DmlDeviceId}, EmbedTensorRtCache={EmbedTensorRtCache}, EnableTensorRtCache={EnableTensorRtCache}, CpuOcr={CpuOcr}",
            string.Join(",", ProviderTypes.Select(Enum.GetName!)),
            config.InferenceDevice,
            OptimizedModel,
            CudaDeviceId,
            DmlDeviceId,
            TrtUseEmbedMode,
            EnableCache,
            CpuOcr);
    }


    /// <summary>
    /// 根据 InferenceDeviceType 选择 Provider
    /// </summary>
    private ProviderType[] GetProviderType(InferenceDeviceType inferenceDeviceType)
    {
        switch (inferenceDeviceType)
        {
            case InferenceDeviceType.Cpu:
                return [ProviderType.Cpu];

            case InferenceDeviceType.GpuDirectMl:
                // 只用 DML 不加 CPU 的话在很多场景下性能很差
                return [ProviderType.Dml, ProviderType.Cpu];

            case InferenceDeviceType.Gpu:
            {
                List<ProviderType> list = [];
                SessionOptions? testSession = null;
                var hasGpu = false;

                // TensorRT 优先（比纯 CUDA 效果好很多）
                if (!hasGpu && CudaDeviceId >= 0)
                {
                    try
                    {
                        testSession = SessionOptions.MakeSessionOptionWithTensorrtProvider(CudaDeviceId);
                        list.Add(ProviderType.TensorRt);
                        hasGpu = true;
                    }
                    catch (Exception e)
                    {
                        _logger.LogDebug("[init] 无法加载 TensorRT，可能不支持，跳过。({Err})", e.Message);
                    }
                    finally
                    {
                        testSession?.Dispose();
                    }
                }

                // DML 次之（比纯 CUDA 稳定性强）
                if (!hasGpu && DmlDeviceId >= 0)
                {
                    try
                    {
                        testSession = new SessionOptions();
                        testSession.AppendExecutionProvider_DML(DmlDeviceId);
                        list.Add(ProviderType.Dml);
                        hasGpu = true;
                    }
                    catch (Exception e)
                    {
                        _logger.LogDebug("[init] 无法加载 DML，可能不支持，跳过。({Err})", e.Message);
                    }
                    finally
                    {
                        testSession?.Dispose();
                    }
                }

                // CUDA 优先级较低
                if (!hasGpu && CudaDeviceId >= 0)
                {
                    try
                    {
                        testSession = SessionOptions.MakeSessionOptionWithCudaProvider(CudaDeviceId);
                        list.Add(ProviderType.Cuda);
                        hasGpu = true;
                    }
                    catch (Exception e)
                    {
                        _logger.LogDebug("[init] 无法加载 CUDA，可能不支持，跳过。({Err})", e.Message);
                    }
                    finally
                    {
                        testSession?.Dispose();
                    }
                }

                if (!hasGpu) _logger.LogWarning("[init] GPU 自动选择失败，回退到 CPU 处理");

                // 无论如何都要加入 CPU
                list.Add(ProviderType.Cpu);
                return list.ToArray();
            }

            case InferenceDeviceType.OpenVino:
            {
                List<ProviderType> list = [];
                SessionOptions? testSession = null;

                try
                {
                    testSession = new SessionOptions();
                    testSession.AppendExecutionProvider("OpenVINO", GetOpenVinoProviderConfig(null));
                    testSession.GraphOptimizationLevel = GraphOptimizationLevel.ORT_DISABLE_ALL;
                    list.Add(ProviderType.OpenVino);
                }
                catch (Exception e)
                {
                    _logger.LogDebug("[init] 无法加载 OpenVINO，可能不支持，跳过。({Err})", e.Message);
                }
                finally
                {
                    testSession?.Dispose();
                }

                list.Add(ProviderType.Cpu);
                return list.ToArray();
            }

            default:
                throw new InvalidEnumArgumentException("无效的推理设备");
        }
    }

    /// <summary>
    /// 自动嗅探并修改 PATH 以加载 CUDA
    /// </summary>
    private void AppendCudaPath()
    {
        var cudaVersion =
            Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\NVIDIA Corporation\GPU Computing Toolkit\CUDA",
                "FirstVersionInstalled", null)?.ToString() ?? "v12.8";
        string[] filePrefix = ["cudnn", "nvrtc", "cudart", "nvinfer", "cublas", "onnx"];
        string[] environmentVariableNames = ["PATH", "CUDA_PATH", "CUDNN_PATH", "LD_LIBRARY_PATH"];

        var validPaths = environmentVariableNames.SelectMany(s => Environment
                .GetEnvironmentVariable(s, EnvironmentVariableTarget.Process)?
                .Split(Path.PathSeparator) ?? []).Distinct()
            .SelectMany<string, string>(s =>
                [s, Path.Combine(s, cudaVersion), Path.Combine(s, "bin"), Path.Combine(s, "lib")])
            .SelectMany<string, string>(
                s => cudaVersion.StartsWith("v", StringComparison.InvariantCultureIgnoreCase)
                    ? [s, Path.Combine(s, cudaVersion), Path.Combine(s, cudaVersion[1..])]
                    : [s, Path.Combine(s, cudaVersion)])
            .SelectMany<string, string>(s =>
            {
                var architecture = Enum.GetName(RuntimeInformation.ProcessArchitecture);
                if (architecture is null) return [s];

                return
                [
                    s, Path.Combine(s, architecture), Path.Combine(s, architecture.ToLowerInvariant()),
                    Path.Combine(s, architecture.ToUpperInvariant())
                ];
            })
            .Where(basePath => !string.IsNullOrWhiteSpace(basePath))
            .Distinct()
            .Where(d =>
            {
                try { return Directory.Exists(d); }
                catch { return false; }
            })
            .SelectMany(s =>
                filePrefix.SelectMany(se =>
                {
                    try
                    {
                        return Directory.GetFiles(s, $"{se}*.dll")
                            .Select(Path.GetDirectoryName)
                            .Where(x => x != null)!;
                    }
                    catch { return []; }
                }))
            .Distinct();

        AppendPath(validPaths.ToArray()!);
    }

    /// <summary>
    /// 将附加的 PATH 应用进来
    /// </summary>
    private void AppendPath(string[] extraPath)
    {
        if (extraPath.Length <= 0) return;

        var pathVariables = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process)
            ?.Split(Path.PathSeparator).ToList() ?? [];
        pathVariables.AddRange(extraPath);

        if (pathVariables.Count <= 0)
        {
            _logger.LogWarning("[GpuAuto] SetCudaPath: No valid paths found.");
            return;
        }

        var updatedPath = string.Join(Path.PathSeparator, pathVariables.Distinct());
        _logger.LogDebug("[GpuAuto] 修改进程 PATH 为: {UpdatedPath}", updatedPath);
        Environment.SetEnvironmentVariable("PATH", updatedPath, EnvironmentVariableTarget.Process);
    }


    /// <summary>
    /// 根据模型创建一个 YoloPredictor
    /// </summary>
    /// <param name="model">模型</param>
    /// <returns>BgiYoloPredictor</returns>
    public BgiYoloPredictor CreateYoloPredictor(BgiOnnxModel model)
    {
        _logger.LogDebug("[YOLO] 创建 YOLO 预测器，模型: {ModelName}", model.Name);
        
        if (!EnableCache)
            return new BgiYoloPredictor(model, model.ModalPath, CreateSessionOptions(model, false));

        var cached = GetCached(model);
        return cached == null
            ? new BgiYoloPredictor(model, model.ModalPath, CreateSessionOptions(model, true))
            : new BgiYoloPredictor(model, cached, CreateSessionOptions(model, false));
    }

    /// <summary>
    /// 根据模型创建一个 ONNX 运行时的 InferenceSession
    /// </summary>
    /// <param name="model">模型</param>
    /// <param name="ocr">是否是用于 OCR 的模型，默认 false</param>
    /// <returns>InferenceSession</returns>
    public InferenceSession CreateInferenceSession(BgiOnnxModel model, bool ocr = false)
    {
        _logger.LogDebug("[ONNX] 创建推理会话，模型: {ModelName}", model.Name);
        ProviderType[]? providerTypes = null;
        if (CpuOcr && ocr) providerTypes = [ProviderType.Cpu];

        if (!EnableCache)
            return new InferenceSession(model.ModalPath, CreateSessionOptions(model, false, providerTypes));

        var cached = GetCached(model, providerTypes);
        return cached == null
            ? new InferenceSession(model.ModalPath, CreateSessionOptions(model, true, providerTypes))
            : new InferenceSession(cached, CreateSessionOptions(model, false, providerTypes));
    }

    /// <summary>
    /// 获取带有缓存的模型（目前只支持 TensorRT）
    /// </summary>
    private string? GetCached(BgiOnnxModel model, ProviderType[]? forcedProvider = null)
    {
        var providerTypes = forcedProvider ?? ProviderTypes;
        if (!providerTypes.Contains(ProviderType.TensorRt)) return null;

        var result = _cachedModelPaths.GetOrAdd(model, _GetCached);
        if (result is null) return result;

        if (File.Exists(result)) return result;

        _logger.LogWarning("[ONNX] 模型 {Model} 的缓存文件可能已被删除，使用原始模型文件。", model.Name);
        return null;
    }

    private string? _GetCached(BgiOnnxModel model)
    {
        if (model.ModelRelativePath.StartsWith(BgiOnnxModel.ModelCacheRelativePath) &&
            model.ModelRelativePath.EndsWith("_ctx.onnx"))
            return model.ModalPath;

        var ctxA = Path.Combine(model.CachePath, "trt", "_ctx.onnx");
        if (File.Exists(ctxA))
        {
            _logger.LogDebug("[ONNX] 模型 {Model} 命中 TRT 匿名缓存文件: {Path}", model.Name, ctxA);
            return ctxA;
        }

        var ctxB = Path.Combine(model.CachePath, "trt",
            Path.GetFileNameWithoutExtension(model.ModalPath) + "_ctx.onnx");
        if (File.Exists(ctxB))
        {
            _logger.LogDebug("[ONNX] 模型 {Model} 命中 TRT 命名缓存文件: {Path}", model.Name, ctxB);
            return ctxB;
        }

        _logger.LogDebug("[ONNX] 没有找到模型 {Model} 的模型缓存文件。", model.Name);
        return null;
    }

    /// <summary>
    /// 通过模型路径生成 SessionOptions
    /// </summary>
    private SessionOptions CreateSessionOptions(BgiOnnxModel model, bool genCache, ProviderType[]? forcedProvider = null)
    {
        var sessionOptions = new SessionOptions();
        foreach (var type in forcedProvider is null || forcedProvider.Length == 0 ? ProviderTypes : forcedProvider)
        {
            try
            {
                switch (type)
                {
                    case ProviderType.Dml:
                        sessionOptions.AppendExecutionProvider_DML(DmlDeviceId);
                        sessionOptions.EnableMemoryPattern = false;
                        sessionOptions.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
                        break;

                    case ProviderType.Cpu:
                        sessionOptions.AppendExecutionProvider_CPU();
                        if (model.Name.Contains("PpOcr") || model.Name.Contains("Yap"))
                        {
                            sessionOptions.IntraOpNumThreads = 2;
                            sessionOptions.InterOpNumThreads = 1;
                        }
                        break;

                    case ProviderType.Dnnl:
                        sessionOptions.AppendExecutionProvider_Dnnl();
                        break;

                    case ProviderType.OpenVino:
                        sessionOptions.AppendExecutionProvider("OpenVINO",
                            GetOpenVinoProviderConfig(OpenVinoCache ? model.CachePath : null));
                        sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_DISABLE_ALL;
                        break;

                    case ProviderType.TensorRt:
                        using (var options = new OrtTensorRTProviderOptions())
                        {
                            options.UpdateOptions(GetTrtProviderConfig(genCache ? model.CachePath : null));
                            sessionOptions.AppendExecutionProvider_Tensorrt(options);
                        }
                        break;

                    case ProviderType.Cuda:
                        using (var options = new OrtCUDAProviderOptions())
                        {
                            options.UpdateOptions(GetCudaProviderConfig());
                            sessionOptions.AppendExecutionProvider_CUDA();
                        }
                        break;

                    default:
                        throw new InvalidEnumArgumentException("无效的推理设备");
                }
            }
            catch (Exception e)
            {
                _logger.LogError("无法加载指定的 ONNX Provider {Provider}，跳过。请检查推理设备配置是否正确。({Err})",
                    Enum.GetName(type), e.Message);
            }
        }

        if (!OptimizedModel) return sessionOptions;
        if (!genCache) return sessionOptions;

        var optPath = Path.Combine(model.CachePath, "optimized");
        if (!Directory.Exists(optPath)) Directory.CreateDirectory(optPath);
        sessionOptions.OptimizedModelFilePath = Path.Combine(optPath, Path.GetFileName(model.ModalPath));
        return sessionOptions;
    }

    /// <summary>
    /// 获取 TensorRT 的配置
    /// </summary>
    /// <param name="cacheFolder">缓存生成的目录</param>
    /// <returns>TRT 配置</returns>
    private Dictionary<string, string> GetTrtProviderConfig(string? cacheFolder)
    {
        if (cacheFolder is null)
        {
            // 不使用缓存目录
            return new Dictionary<string, string>
            {
                ["device_id"] = CudaDeviceId.ToString()
            };
        }

        var result = new Dictionary<string, string>
        {
            ["trt_engine_cache_enable"] = "1",
            ["trt_dump_ep_context_model"] = "1",
            ["trt_ep_context_file_path"] = Path.Combine(cacheFolder, "trt"),
            ["trt_timing_cache_enable"] = "1",
            ["trt_timing_cache_path"] = Global.Absolute(Path.Combine(BgiOnnxModel.ModelCacheRelativePath, "trt_timing")),
            ["device_id"] = CudaDeviceId.ToString()
        };

        if (TrtUseEmbedMode)
        {
            result["trt_ep_context_embed_mode"] = "1";
        }
        else
        {
            result["trt_ep_context_embed_mode"] = "0";
            result["trt_engine_cache_path"] = ".\\";
        }

        // 确保 TRT 上下文文件路径存在
        if (!Directory.Exists(result["trt_ep_context_file_path"]))
        {
            _logger.LogDebug("[ONNX] TensorRT 上下文文件路径不存在，创建目录: {Path}", result["trt_ep_context_file_path"]);
            try
            {
                Directory.CreateDirectory(result["trt_ep_context_file_path"]);
            }
            catch (Exception e)
            {
                _logger.LogError("无法创建 TensorRT 上下文文件路径: {Path}，请检查权限。({Err})",
                    result["trt_ep_context_file_path"], e.Message);
                result.Remove("trt_ep_context_file_path");
            }
        }

        // 确保 TRT 计时缓存路径存在
        if (!Directory.Exists(result["trt_timing_cache_path"]))
        {
            _logger.LogDebug("[ONNX] TensorRT 计时缓存路径不存在，创建目录: {Path}", result["trt_timing_cache_path"]);
            try
            {
                Directory.CreateDirectory(result["trt_timing_cache_path"]);
            }
            catch (Exception e)
            {
                _logger.LogError("无法创建 TensorRT 计时缓存路径: {Path}，请检查权限。({Err})",
                    result["trt_timing_cache_path"], e.Message);
                result.Remove("trt_timing_cache_path");
            }
        }

        return result;
    }

    /// <summary>
    /// 获取 CUDA Provider 的配置
    /// </summary>
    /// <returns>CUDA 配置</returns>
    private Dictionary<string, string> GetCudaProviderConfig()
    {
        return new Dictionary<string, string>
        {
            ["device_id"] = CudaDeviceId.ToString()
        };
    }

    /// <summary>
    /// 获取 OpenVINO Provider 的配置
    /// </summary>
    /// <param name="cacheFolder">缓存目录</param>
    /// <returns>OpenVINO 配置</returns>
    private Dictionary<string, string> GetOpenVinoProviderConfig(string? cacheFolder)
    {
        var result = new Dictionary<string, string>();

        if (!string.IsNullOrWhiteSpace(OpenVinoDevice))
        {
            result["device_type"] = OpenVinoDevice;
        }

        if (!string.IsNullOrWhiteSpace(cacheFolder))
        {
            result["cache_dir"] = Path.Combine(cacheFolder, "openvino");
            if (!Directory.Exists(result["cache_dir"]))
            {
                try
                {
                    Directory.CreateDirectory(result["cache_dir"]);
                }
                catch (Exception e)
                {
                    _logger.LogError("无法创建 OpenVINO 缓存目录: {Path}，请检查权限。({Err})",
                        result["cache_dir"], e.Message);
                    result.Remove("cache_dir");
                }
            }
        }

        result["enable_opencl_throttling"] = "true";
        return result;
    }
}