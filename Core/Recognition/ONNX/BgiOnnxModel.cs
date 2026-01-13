using System.Collections.Immutable;
using System.IO;
using ShineProCS.Core.Config;

namespace ShineProCS.Core.Recognition.ONNX;

/// <summary>
/// ONNX 模型定义（BetterGI 风格）
/// </summary>
public class BgiOnnxModel
{
    /// <summary>
    /// 模型使用的缓存文件的相对目录
    /// </summary>
    public static readonly string ModelCacheRelativePath = Path.Combine("Cache", Global.Version, "Model");

    private static readonly List<BgiOnnxModel> RegisteredModels = [];
    
    /// <summary>
    /// 模型名称
    /// </summary>
    public string Name { get; private init; }
    
    /// <summary>
    /// 模型相对路径
    /// </summary>
    public string ModelRelativePath { get; private init; }
    
    /// <summary>
    /// 模型绝对路径
    /// </summary>
    public string ModalPath => Global.Absolute(ModelRelativePath);
    
    /// <summary>
    /// 缓存相对路径
    /// </summary>
    public string CacheRelativePath { get; private init; }
    
    /// <summary>
    /// 缓存绝对路径
    /// </summary>
    public string CachePath => Global.Absolute(CacheRelativePath);

    #region 模型注册

    /// <summary>
    /// PaddleOCR V4 检测模型
    /// </summary>
    public static readonly BgiOnnxModel PaddleOcrDetV4 =
        Register("PpOcrDetV4", @"Assets\Model\PaddleOCR\Det\V4\PP-OCRv4_mobile_det_infer\slim.onnx");

    /// <summary>
    /// PaddleOCR V5 检测模型
    /// </summary>
    public static readonly BgiOnnxModel PaddleOcrDetV5 =
        Register("PpOcrDetV5", @"Assets\Model\PaddleOCR\Det\V5\PP-OCRv5_mobile_det_infer\slim.onnx");

    /// <summary>
    /// PaddleOCR V4 中文识别模型
    /// </summary>
    public static readonly BgiOnnxModel PaddleOcrRecV4 =
        Register("PpOcrRecV4", @"Assets\Model\PaddleOCR\Rec\V4\PP-OCRv4_mobile_rec_infer\slim.onnx");

    /// <summary>
    /// PaddleOCR V4 英文/数字识别模型
    /// </summary>
    public static readonly BgiOnnxModel PaddleOcrRecV4En =
        Register("PpOcrRecV4En", @"Assets\Model\PaddleOCR\Rec\V4\en_PP-OCRv4_mobile_rec_infer\slim.onnx");

    /// <summary>
    /// PaddleOCR V5 中文识别模型
    /// </summary>
    public static readonly BgiOnnxModel PaddleOcrRecV5 =
        Register("PpOcrRecV5", @"Assets\Model\PaddleOCR\Rec\V5\PP-OCRv5_mobile_rec_infer\slim.onnx");

    /// <summary>
    /// PaddleOCR V5 拉丁文识别模型
    /// </summary>
    public static readonly BgiOnnxModel PaddleOcrRecV5Latin =
        Register("PpOcrRecV5Latin", @"Assets\Model\PaddleOCR\Rec\V5\latin_PP-OCRv5_mobile_rec_infer\slim.onnx");

    /// <summary>
    /// PaddleOCR V5 斯拉夫文识别模型
    /// </summary>
    public static readonly BgiOnnxModel PaddleOcrRecV5Eslav =
        Register("PpOcrRecV5Eslav", @"Assets\Model\PaddleOCR\Rec\V5\eslav_PP-OCRv5_mobile_rec_infer\slim.onnx");

    /// <summary>
    /// PaddleOCR V5 韩文识别模型
    /// </summary>
    public static readonly BgiOnnxModel PaddleOcrRecV5Korean =
        Register("PpOcrRecV5Korean", @"Assets\Model\PaddleOCR\Rec\V5\korean_PP-OCRv5_mobile_rec_infer\slim.onnx");

    #endregion

    #region Yap 模型注册

    /// <summary>
    /// Yap 文字识别模型
    /// </summary>
    public static readonly BgiOnnxModel YapModelTraining =
        Register("YapModelTraining", @"Assets\Model\Yap\model_training.onnx");

    #endregion

    #region YOLO 模型注册

    /// <summary>
    /// 钓鱼模型
    /// </summary>
    public static readonly BgiOnnxModel BgiFish =
        Register("BgiFish", @"Assets\Model\Fish\bgi_fish.onnx");

    /// <summary>
    /// 秘境中古树
    /// </summary>
    public static readonly BgiOnnxModel BgiTree =
        Register("BgiTree", @"Assets\Model\Domain\bgi_tree.onnx");

    /// <summary>
    /// 用于捡东西等的大世界模型
    /// </summary>
    public static readonly BgiOnnxModel BgiWorld =
        Register("BgiWorld", @"Assets\Model\World\bgi_world.onnx");

    /// <summary>
    /// 角色识别
    /// </summary>
    public static readonly BgiOnnxModel BgiAvatarSide =
        Register("BgiAvatarSide", @"Assets\Model\Common\avatar_side_classify_sim.onnx");

    #endregion

    /// <summary>
    /// 创建 ONNX 模型定义
    /// </summary>
    /// <param name="name">模型名称</param>
    /// <param name="modelRelativePath">模型相对路径</param>
    /// <param name="cacheRelativePath">缓存相对路径</param>
    protected BgiOnnxModel(string name, string modelRelativePath, string cacheRelativePath)
    {
        Name = name;
        ModelRelativePath = modelRelativePath;
        CacheRelativePath = cacheRelativePath;
    }

    /// <summary>
    /// 检查模型文件是否存在
    /// </summary>
    public static bool IsModelExist(BgiOnnxModel model)
    {
        return File.Exists(model.ModalPath);
    }

    /// <summary>
    /// 获取所有已注册的模型
    /// </summary>
    public static ImmutableList<BgiOnnxModel> GetAll()
    {
        return RegisteredModels.ToImmutableList();
    }

    private static BgiOnnxModel Register(string name, string modelRelativePath)
    {
        return Register(name, modelRelativePath, Path.Combine(ModelCacheRelativePath, name));
    }

    private static BgiOnnxModel Register(string name, string modelRelativePath, string cacheRelativePath)
    {
        var model = new BgiOnnxModel(name, modelRelativePath, cacheRelativePath);
        
        // 确保缓存目录存在
        var cachePath = model.CachePath;
        if (!Directory.Exists(cachePath))
        {
            try
            {
                Directory.CreateDirectory(cachePath);
            }
            catch
            {
                // 忽略创建目录失败
            }
        }

        RegisteredModels.Add(model);
        return model;
    }
}
