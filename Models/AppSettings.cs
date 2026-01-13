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
    
    #region 自动拾取配置
    
    /// <summary>
    /// 是否启用自动拾取
    /// </summary>
    [ObservableProperty] private bool _enableAutoPick = false;
    
    /// <summary>
    /// 自动拾取检测区域 [X, Y, Width, Height]
    /// </summary>
    [ObservableProperty] private int[] _autoPickRegion = [0, 0, 200, 100];
    
    /// <summary>
    /// 自动拾取触发间隔（毫秒）
    /// </summary>
    [ObservableProperty] private int _autoPickInterval = 200;
    
    /// <summary>
    /// 自动拾取按键（默认 F 键，虚拟键码 70）
    /// </summary>
    [ObservableProperty] private int _autoPickKeyCode = 70;
    
    /// <summary>
    /// 自动拾取白名单（为空时拾取所有）
    /// </summary>
    [ObservableProperty] private ObservableCollection<string> _autoPickWhitelist = [];
    
    /// <summary>
    /// 自动拾取黑名单
    /// </summary>
    [ObservableProperty] private ObservableCollection<string> _autoPickBlacklist = [];
    
    /// <summary>
    /// 自动拾取 OCR 置信度阈值
    /// </summary>
    [ObservableProperty] private float _autoPickConfidenceThreshold = 0.6f;
    
    /// <summary>
    /// 自动拾取关键词列表（用于检测拾取提示）
    /// </summary>
    [ObservableProperty] private ObservableCollection<string> _autoPickKeywords = ["拾取", "F", "获取", "采集"];
    
    #endregion
    
    #region 自动剧情跳过配置
    
    /// <summary>
    /// 是否启用自动剧情跳过
    /// </summary>
    [ObservableProperty] private bool _enableAutoSkip = false;
    
    /// <summary>
    /// 自动剧情跳过检测区域 [X, Y, Width, Height]
    /// 用于检测对话框和选项界面
    /// </summary>
    [ObservableProperty] private int[] _autoSkipRegion = [0, 0, 400, 200];
    
    /// <summary>
    /// 自动剧情跳过触发间隔（毫秒）
    /// </summary>
    [ObservableProperty] private int _autoSkipInterval = 300;
    
    /// <summary>
    /// 对话跳过按键（默认鼠标左键点击，-1表示鼠标点击）
    /// </summary>
    [ObservableProperty] private int _autoSkipKeyCode = -1;
    
    /// <summary>
    /// 选项自动选择模式 (0=第一个选项, 1=最后一个选项, 2=随机选项)
    /// </summary>
    [ObservableProperty] private int _autoSkipOptionMode = 0;
    
    /// <summary>
    /// 对话检测关键词列表
    /// </summary>
    [ObservableProperty] private ObservableCollection<string> _autoSkipDialogKeywords = ["对话", "继续", "跳过", "下一步"];
    
    /// <summary>
    /// 选项检测关键词列表
    /// </summary>
    [ObservableProperty] private ObservableCollection<string> _autoSkipOptionKeywords = ["选项", "选择", "确认", "同意"];
    
    /// <summary>
    /// 自动剧情跳过 OCR 置信度阈值
    /// </summary>
    [ObservableProperty] private float _autoSkipConfidenceThreshold = 0.6f;
    
    /// <summary>
    /// 是否使用模板匹配检测对话框（替代OCR）
    /// </summary>
    [ObservableProperty] private bool _autoSkipUseTemplateMatch = false;
    
    /// <summary>
    /// 对话框模板图片路径
    /// </summary>
    [ObservableProperty] private string _autoSkipDialogTemplatePath = "";
    
    /// <summary>
    /// 选项界面模板图片路径
    /// </summary>
    [ObservableProperty] private string _autoSkipOptionTemplatePath = "";
    
    /// <summary>
    /// 模板匹配阈值
    /// </summary>
    [ObservableProperty] private double _autoSkipTemplateThreshold = 0.8;
    
    /// <summary>
    /// 是否启用后台运行模式
    /// 启用后，即使游戏窗口失去焦点也能继续操作
    /// </summary>
    [ObservableProperty] private bool _autoSkipRunBackground = false;
    
    #endregion
    
    #region 自动秘境配置
    
    /// <summary>
    /// 是否启用自动秘境
    /// </summary>
    [ObservableProperty] private bool _enableAutoDomain = false;
    
    /// <summary>
    /// 是否启用自动钓鱼
    /// </summary>
    [ObservableProperty] private bool _enableAutoFishing = false;
    
    /// <summary>
    /// 是否启用快速传送
    /// </summary>
    [ObservableProperty] private bool _enableQuickTeleport = false;
    
    /// <summary>
    /// 秘境刷取次数（0 表示无限制，直到体力耗尽）
    /// 需求: 19.4 - 支持配置刷取次数
    /// </summary>
    [ObservableProperty] private int _autoDomainRunCount = 1;
    
    /// <summary>
    /// 每次秘境的超时时间（秒）
    /// </summary>
    [ObservableProperty] private int _autoDomainTimeoutSeconds = 300;
    
    /// <summary>
    /// 战斗超时时间（秒）
    /// </summary>
    [ObservableProperty] private int _autoDomainCombatTimeoutSeconds = 180;
    
    /// <summary>
    /// 是否自动使用体力恢复道具
    /// </summary>
    [ObservableProperty] private bool _autoDomainUseResinRecovery = false;
    
    /// <summary>
    /// 是否自动领取奖励
    /// </summary>
    [ObservableProperty] private bool _autoDomainAutoClaimReward = true;
    
    /// <summary>
    /// 领取奖励后的等待时间（毫秒）
    /// </summary>
    [ObservableProperty] private int _autoDomainRewardClaimDelayMs = 2000;
    
    /// <summary>
    /// 秘境入口检测区域 [X, Y, Width, Height]
    /// 需求: 19.6 - 支持自动识别秘境入口
    /// </summary>
    [ObservableProperty] private int[] _autoDomainEntranceRegion = [0, 0, 400, 300];
    
    /// <summary>
    /// 古树检测区域 [X, Y, Width, Height]
    /// 需求: 19.6 - 支持自动识别古树位置
    /// </summary>
    [ObservableProperty] private int[] _autoDomainTreeRegion = [0, 0, 400, 300];
    
    /// <summary>
    /// 体力检测区域 [X, Y, Width, Height]
    /// 需求: 19.5 - 体力检测
    /// </summary>
    [ObservableProperty] private int[] _autoDomainResinRegion = [0, 0, 200, 50];
    
    /// <summary>
    /// 奖励界面检测区域 [X, Y, Width, Height]
    /// </summary>
    [ObservableProperty] private int[] _autoDomainRewardRegion = [0, 0, 600, 400];
    
    /// <summary>
    /// 秘境入口检测关键词
    /// </summary>
    [ObservableProperty] private ObservableCollection<string> _autoDomainEntranceKeywords = ["秘境", "进入", "挑战", "开始"];
    
    /// <summary>
    /// 战斗结束检测关键词
    /// </summary>
    [ObservableProperty] private ObservableCollection<string> _autoDomainCombatEndKeywords = ["挑战成功", "完成", "奖励", "古树", "领取"];
    
    /// <summary>
    /// 最低体力要求
    /// </summary>
    [ObservableProperty] private int _autoDomainMinResin = 20;
    
    /// <summary>
    /// 是否在秘境完成后继续挑战
    /// </summary>
    [ObservableProperty] private bool _autoDomainContinueChallenge = true;
    
    #endregion
    
    #region 高级配置
    
    /// <summary>
    /// 帧变化检测阈值 (0=禁用检测, 默认15)
    /// </summary>
    [ObservableProperty] private int _frameChangeThreshold = 15;
    
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
    
    #endregion
}
