using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShineProCS.Models;

/// <summary>
/// 公共CD检测模式枚举
/// </summary>
public enum GcdDetectionMode
{
    /// <summary>
    /// 自动模式：有颜色配置时使用颜色检测，否则使用亮度检测
    /// </summary>
    Auto = 0,
    
    /// <summary>
    /// 颜色检测模式：使用配置的GlobalCdColor进行颜色匹配
    /// </summary>
    Color = 1,
    
    /// <summary>
    /// 亮度检测模式：使用GlobalCdBrightnessThreshold进行亮度检测
    /// </summary>
    Brightness = 2
}

/// <summary>
/// 应用程序全局设置模型
/// </summary>
public partial class AppSettings : ObservableObject
{
    #region 检测区域配置
    
    /// <summary>
    /// 主检测区域 [X, Y, Width, Height]
    /// </summary>
    [ObservableProperty] private int[] _detectionRegion = [0, 0, 100, 100];
    
    /// <summary>
    /// 自身蓝条检测区域 [X, Y, Width, Height]
    /// </summary>
    [ObservableProperty] private int[] _manaBarRegion = [0, 0, 100, 20];
    
    /// <summary>
    /// 自身血条检测区域 [X, Y, Width, Height]
    /// </summary>
    [ObservableProperty] private int[] _healthBarRegion = [0, 0, 100, 20];
    
    /// <summary>
    /// 目标血条检测区域 [X, Y, Width, Height]
    /// 用于治疗/辅助技能检测目标HP
    /// </summary>
    [ObservableProperty] private int[] _targetHealthBarRegion = [0, 0, 100, 20];
    
    /// <summary>
    /// 公共CD检测点 [X, Y]
    /// </summary>
    [ObservableProperty] private int[] _globalCdPoint = [0, 0];
    
    /// <summary>
    /// 公共CD进行中的颜色 [R, G, B]
    /// 当检测点颜色匹配此颜色时，表示正在公共CD中
    /// </summary>
    [ObservableProperty] private int[] _globalCdColor = [255, 255, 255];
    
    /// <summary>
    /// 公共CD颜色容差
    /// </summary>
    [ObservableProperty] private int _globalCdColorTolerance = 30;
    
    #endregion
    
    #region 引擎配置
    
    /// <summary>
    /// 是否启用智能模式
    /// </summary>
    [ObservableProperty] private bool _enableSmartMode = true;
    
    /// <summary>
    /// 主循环间隔（毫秒）
    /// </summary>
    [ObservableProperty] private int _loopInterval = 100;
    
    /// <summary>
    /// 日志级别 (0=调试, 1=信息, 2=警告, 3=错误)
    /// </summary>
    [ObservableProperty] private int _logLevel = 1;
    
    /// <summary>
    /// 是否显示悬浮窗
    /// </summary>
    [ObservableProperty] private bool _enableOverlay = true;
    
    /// <summary>
    /// 目标游戏窗口标题
    /// </summary>
    [ObservableProperty] private string _gameWindowTitle = "";
    
    /// <summary>
    /// 是否启用WGC截图模式
    /// </summary>
    [ObservableProperty] private bool _enableWgcCapture = true;
    
    /// <summary>
    /// 图像队列容量（2-10）
    /// </summary>
    [ObservableProperty] private int _imageQueueCapacity = 3;
    
    #endregion
    
    #region Buff库
    
    /// <summary>
    /// Buff库 - 存储所有可检测的Buff/Debuff配置
    /// </summary>
    [ObservableProperty] private ObservableCollection<BuffConfig> _buffLibrary = [];
    
    #endregion
    
    #region 技能组配置
    
    /// <summary>
    /// 技能组集合 - 用于定义具有共享条件的技能组
    /// </summary>
    [ObservableProperty] private ObservableCollection<SkillGroupConfig> _skillGroups = [];
    
    #endregion
    
    #region 颜色检测阈值配置（HSV空间）
    
    [ObservableProperty] private int _healthHueMin = 0;
    [ObservableProperty] private int _healthHueMax = 10;
    [ObservableProperty] private int _healthSatMin = 100;
    [ObservableProperty] private int _healthValMin = 100;
    [ObservableProperty] private int _healthGreenHueMin = 35;
    [ObservableProperty] private int _healthGreenHueMax = 85;
    [ObservableProperty] private int _manaHueMin = 100;
    [ObservableProperty] private int _manaHueMax = 130;
    [ObservableProperty] private int _manaSatMin = 100;
    [ObservableProperty] private int _manaValMin = 100;
    [ObservableProperty] private int _globalCdBrightnessThreshold = 120;
    [ObservableProperty] private int _skillBrightnessThreshold = 80;
    [ObservableProperty] private int _buffBrightnessThreshold = 50;
    
    #endregion
    
    #region 血条/蓝条采样检测配置（需求 3.1-3.5）
    
    /// <summary>
    /// 红色血条 R 通道最小值
    /// 需求 3.3: 可配置的颜色阈值
    /// </summary>
    [ObservableProperty] private int _healthRedMinR = 150;
    
    /// <summary>
    /// 红色血条 R-G 最小差值
    /// 需求 3.3: 可配置的颜色阈值
    /// </summary>
    [ObservableProperty] private int _healthRedRGDiff = 30;
    
    /// <summary>
    /// 红色血条 R-B 最小差值
    /// 需求 3.3: 可配置的颜色阈值
    /// </summary>
    [ObservableProperty] private int _healthRedRBDiff = 30;
    
    /// <summary>
    /// 绿色血条 G 通道最小值
    /// 需求 3.4: 同时识别红色和绿色配色方案
    /// </summary>
    [ObservableProperty] private int _healthGreenMinG = 100;
    
    /// <summary>
    /// 蓝条 B 通道最小值
    /// 需求 3.3: 可配置的颜色阈值
    /// </summary>
    [ObservableProperty] private int _manaBlueMinB = 100;
    
    /// <summary>
    /// 蓝条 B-G 容差
    /// 需求 3.3: 可配置的颜色阈值
    /// </summary>
    [ObservableProperty] private int _manaBlueBGTolerance = 30;
    
    /// <summary>
    /// HP/MP 检测最大连续失败次数
    /// 需求 3.5: 超过此值后使用缓存值
    /// </summary>
    [ObservableProperty] private int _barDetectionMaxFailures = 5;
    
    #endregion
    
    #region 悬浮窗配置
    
    [ObservableProperty] private double _overlayLeft = 10;
    [ObservableProperty] private double _overlayTop = 10;
    [ObservableProperty] private double _overlayOpacity = 1.0;
    
    #endregion
    
    #region 遮罩窗口配置
    
    /// <summary>
    /// 是否启用遮罩窗口
    /// 需求: 21.1 - 遮罩窗口覆盖在游戏窗口上方
    /// </summary>
    [ObservableProperty] private bool _enableMaskWindow = false;
    
    /// <summary>
    /// 是否在遮罩窗口上显示识别结果
    /// </summary>
    [ObservableProperty] private bool _maskDisplayRecognitionResults = true;
    
    /// <summary>
    /// 遮罩窗口显示日志框
    /// 需求: 21.2 - 显示实时日志信息
    /// </summary>
    [ObservableProperty] private bool _maskShowLogBox = true;
    
    /// <summary>
    /// 遮罩窗口显示状态指示
    /// </summary>
    [ObservableProperty] private bool _maskShowStatus = true;
    
    /// <summary>
    /// 遮罩窗口方位提示（东南西北）
    /// </summary>
    [ObservableProperty] private bool _maskDirectionsEnabled = false;
    
    /// <summary>
    /// 遮罩窗口UID遮盖
    /// </summary>
    [ObservableProperty] private bool _maskUidCoverEnabled = false;
    
    /// <summary>
    /// 遮罩窗口显示FPS
    /// </summary>
    [ObservableProperty] private bool _maskShowFps = false;
    
    /// <summary>
    /// 遮罩窗口作为游戏子窗体
    /// </summary>
    [ObservableProperty] private bool _maskUseSubform = false;
    
    /// <summary>
    /// 遮罩窗口文本透明度 (0.0-1.0)
    /// 需求: 21.5 - 支持透明度调节
    /// </summary>
    [ObservableProperty] private double _maskTextOpacity = 1.0;
    
    /// <summary>
    /// 遮罩窗口日志框位置 X
    /// 需求: 21.4 - 支持拖拽移动
    /// </summary>
    [ObservableProperty] private double _maskLogBoxLeft = 20;
    
    /// <summary>
    /// 遮罩窗口日志框位置 Y
    /// </summary>
    [ObservableProperty] private double _maskLogBoxTop = 800;
    
    /// <summary>
    /// 遮罩窗口日志框宽度
    /// 需求: 21.4 - 支持调整大小
    /// </summary>
    [ObservableProperty] private double _maskLogBoxWidth = 477;
    
    /// <summary>
    /// 遮罩窗口日志框高度
    /// </summary>
    [ObservableProperty] private double _maskLogBoxHeight = 188;
    
    #endregion
    
    #region 全局快捷键配置
    
    [ObservableProperty] private uint _hotkeyStartStopModifier = 2; // Ctrl
    [ObservableProperty] private uint _hotkeyStartStopKey = 118; // F7
    [ObservableProperty] private uint _hotkeyPauseModifier = 2; // Ctrl
    [ObservableProperty] private uint _hotkeyPauseKey = 119; // F8
    [ObservableProperty] private bool _enableGlobalHotkeys = true;
    
    #endregion
    
    #region 输入驱动配置
    
    /// <summary>
    /// 输入驱动类型 (0=Win32软件模拟, 1=GhostBox硬件驱动)
    /// </summary>
    [ObservableProperty] private InputDriverType _inputDriverType = InputDriverType.Win32;
    
    #endregion
    
    #region GhostBox 输入延迟配置（需求 6.1, 6.2, 6.6）
    
    /// <summary>
    /// 是否启用随机延迟
    /// 需求 6.1: 支持可配置的输入延迟随机化
    /// </summary>
    [ObservableProperty] private bool _enableRandomDelay = true;
    
    /// <summary>
    /// 按键最小延迟（毫秒）
    /// 需求 6.2: 在配置范围内添加随机延迟
    /// </summary>
    [ObservableProperty] private int _keyPressMinDelayMs = 30;
    
    /// <summary>
    /// 按键最大延迟（毫秒）
    /// 需求 6.2: 在配置范围内添加随机延迟
    /// </summary>
    [ObservableProperty] private int _keyPressMaxDelayMs = 80;
    
    /// <summary>
    /// 最小按键间隔（毫秒）
    /// 需求 6.6: 强制执行最小按键间隔
    /// </summary>
    [ObservableProperty] private int _minInterKeyDelayMs = 20;
    
    /// <summary>
    /// 鼠标移动是否使用贝塞尔曲线
    /// 需求 6.3: 支持贝塞尔曲线鼠标移动以模拟人类动作
    /// </summary>
    [ObservableProperty] private bool _useBezierMouseMove = true;
    
    /// <summary>
    /// 贝塞尔曲线路径点数量
    /// 需求 6.3: 控制鼠标移动的平滑度
    /// </summary>
    [ObservableProperty] private int _bezierMouseSteps = 20;
    
    #endregion
    
    #region GhostBox 重连配置（需求 6.4, 6.5）
    
    /// <summary>
    /// 是否启用自动重连
    /// 需求 6.5: 支持自动重连尝试
    /// </summary>
    [ObservableProperty] private bool _enableAutoReconnect = true;
    
    /// <summary>
    /// 重连间隔（毫秒）
    /// 需求 6.5: 重试间隔可配置
    /// </summary>
    [ObservableProperty] private int _reconnectRetryIntervalMs = 2000;
    
    /// <summary>
    /// 最大重试次数（0 = 无限）
    /// 需求 6.5: 支持自动重连尝试
    /// </summary>
    [ObservableProperty] private int _reconnectMaxRetries = 5;
    
    #endregion
    
    #region 硬件加速配置（需求 5.1, 5.5）
    
    /// <summary>
    /// 推理设备类型 (0=CPU, 1=DirectML GPU)
    /// 需求 5.1: 支持两种推理设备类型
    /// </summary>
    [ObservableProperty] private int _inferenceDeviceType = 0;
    
    /// <summary>
    /// GPU 设备 ID（多 GPU 系统可指定）
    /// 需求 5.1: DirectML 设备 ID
    /// </summary>
    [ObservableProperty] private int _gpuDeviceId = 0;
    
    /// <summary>
    /// 是否强制 OCR 使用 CPU
    /// 需求 5.5: 允许强制 OCR 使用 CPU
    /// </summary>
    [ObservableProperty] private bool _cpuOcr = false;
    
    #endregion
    

    

    
    #region 高级配置
    
    /// <summary>
    /// 帧变化检测阈值 (0=禁用检测, 默认15)
    /// 需求 1.1: 可配置的帧变化检测阈值
    /// </summary>
    [ObservableProperty] private int _frameChangeThreshold = 15;
    
    /// <summary>
    /// 帧采样步长 (默认16，值越大采样越稀疏，性能越好但精度越低)
    /// 需求 1.1: 可配置的采样步长减少计算量
    /// </summary>
    [ObservableProperty] private int _frameSampleStride = 16;
    
    /// <summary>
    /// 公共CD检测模式 (0=自动, 1=颜色, 2=亮度)
    /// </summary>
    [ObservableProperty] private int _globalCdDetectionMode = 0;
    
    /// <summary>
    /// 联动技能优先级加成
    /// </summary>
    [ObservableProperty] private int _comboSkillPriorityBonus = 50;
    
    /// <summary>
    /// 模板缓存大小
    /// </summary>
    [ObservableProperty] private int _templateCacheSize = 50;
    
    /// <summary>
    /// 是否启用模板匹配缩放优化
    /// 需求 1.4: 可选地将图像缩放以加速匹配
    /// </summary>
    [ObservableProperty] private bool _enableTemplateScaling = true;
    
    /// <summary>
    /// 模板匹配缩放比例 (0.25-1.0, 默认0.5)
    /// 需求 1.4: 可配置的缩放比例
    /// 值越小匹配越快但精度越低
    /// </summary>
    [ObservableProperty] private double _templateScaleFactor = 0.5;
    
    /// <summary>
    /// 缩放后的相似度阈值调整值 (默认0.05)
    /// 缩放后匹配精度略有下降，阈值降低此值进行补偿
    /// </summary>
    [ObservableProperty] private double _templateScaleThresholdAdjust = 0.05;
    
    /// <summary>
    /// 最小缩放后尺寸 (像素)
    /// 如果缩放后尺寸小于此值，则不进行缩放
    /// </summary>
    [ObservableProperty] private int _templateMinScaledSize = 16;
    
    /// <summary>
    /// 是否启用 Buff 多尺度模板匹配
    /// 需求 2.3: 支持多尺度匹配以适应不同 UI 缩放
    /// 启用后会在 0.8-1.2 倍缩放范围内搜索最佳匹配
    /// </summary>
    [ObservableProperty] private bool _enableMultiScaleBuffMatch = false;
    
    #endregion
}
