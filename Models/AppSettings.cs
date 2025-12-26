using CommunityToolkit.Mvvm.ComponentModel;

namespace ShineProCS.Models;

/// <summary>
/// 应用程序全局设置模型
/// 包含检测区域、颜色阈值、气劲配置等所有可配置项
/// </summary>
public partial class AppSettings : ObservableObject
{
    #region 检测区域配置
    
    /// <summary>
    /// 主检测区域 [X, Y, Width, Height]
    /// 用于技能图标和状态检测的屏幕区域
    /// </summary>
    [ObservableProperty] private int[] _detectionRegion = [0, 0, 100, 100];
    
    /// <summary>
    /// 蓝条检测区域 [X, Y, Width, Height]
    /// 用于检测角色MP百分比
    /// </summary>
    [ObservableProperty] private int[] _manaBarRegion = [0, 0, 100, 20];
    
    /// <summary>
    /// 血条检测区域 [X, Y, Width, Height]
    /// 用于检测角色HP百分比
    /// </summary>
    [ObservableProperty] private int[] _healthBarRegion = [0, 0, 100, 20];
    
    /// <summary>
    /// 公共CD检测点 [X, Y]
    /// 用于检测技能公共冷却状态
    /// </summary>
    [ObservableProperty] private int[] _globalCdPoint = [0, 0];
    
    #endregion
    
    #region 引擎配置
    
    /// <summary>
    /// 是否启用智能模式
    /// 智能模式会优先选择有联动配置的技能
    /// </summary>
    [ObservableProperty] private bool _enableSmartMode = true;
    
    /// <summary>
    /// 主循环间隔（毫秒）
    /// 控制技能检测和释放的频率，建议50-200ms
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
    /// 用于WGC截图模式定位窗口
    /// </summary>
    [ObservableProperty] private string _gameWindowTitle = "";
    
    /// <summary>
    /// 是否启用WGC截图模式
    /// WGC模式性能更好，但需要Windows 10 1903+
    /// </summary>
    [ObservableProperty] private bool _enableWgcCapture = true;
    
    #endregion
    
    #region 气劲配置
    
    /// <summary>
    /// 千枝技能名称（用于技能匹配）
    /// </summary>
    [ObservableProperty] private string _qianZhiSkillName = "千枝绽蕊";
    
    /// <summary>
    /// 千枝Buff名称（用于状态检测）
    /// </summary>
    [ObservableProperty] private string _qianZhiBuffName = "千枝态";
    
    /// <summary>
    /// 千枝技能按键码（虚拟键码）
    /// </summary>
    [ObservableProperty] private int _qianZhiKeyCode = 87;
    
    /// <summary>
    /// 赤芍技能名称
    /// </summary>
    [ObservableProperty] private string _chiShaoSkillName = "赤芍寒香";
    
    /// <summary>
    /// 七情技能名称
    /// </summary>
    [ObservableProperty] private string _qiQingSkillName = "七情和合";
    
    /// <summary>
    /// 七情Buff名称
    /// </summary>
    [ObservableProperty] private string _qiQingBuffName = "七情态";
    
    #endregion
    
    #region 队列配置
    
    /// <summary>
    /// 图像队列容量（2-10）
    /// 控制截屏缓冲区大小，影响内存占用和响应延迟
    /// </summary>
    [ObservableProperty] private int _imageQueueCapacity = 3;
    
    #endregion
    
    #region 颜色检测阈值配置（HSV空间）
    
    /// <summary>
    /// 血条检测 - 红色色相最小值 (0-180)
    /// </summary>
    [ObservableProperty] private int _healthHueMin = 0;
    
    /// <summary>
    /// 血条检测 - 红色色相最大值 (0-180)
    /// </summary>
    [ObservableProperty] private int _healthHueMax = 10;
    
    /// <summary>
    /// 血条检测 - 饱和度最小值 (0-255)
    /// </summary>
    [ObservableProperty] private int _healthSatMin = 100;
    
    /// <summary>
    /// 血条检测 - 明度最小值 (0-255)
    /// </summary>
    [ObservableProperty] private int _healthValMin = 100;
    
    /// <summary>
    /// 血条检测 - 绿色色相最小值（部分游戏血条是绿色）
    /// </summary>
    [ObservableProperty] private int _healthGreenHueMin = 35;
    
    /// <summary>
    /// 血条检测 - 绿色色相最大值
    /// </summary>
    [ObservableProperty] private int _healthGreenHueMax = 85;
    
    /// <summary>
    /// 蓝条检测 - 蓝色色相最小值
    /// </summary>
    [ObservableProperty] private int _manaHueMin = 100;
    
    /// <summary>
    /// 蓝条检测 - 蓝色色相最大值
    /// </summary>
    [ObservableProperty] private int _manaHueMax = 130;
    
    /// <summary>
    /// 蓝条检测 - 饱和度最小值
    /// </summary>
    [ObservableProperty] private int _manaSatMin = 100;
    
    /// <summary>
    /// 蓝条检测 - 明度最小值
    /// </summary>
    [ObservableProperty] private int _manaValMin = 100;
    
    /// <summary>
    /// 公共CD检测 - 亮度阈值
    /// 高于此值认为正在读条
    /// </summary>
    [ObservableProperty] private int _globalCdBrightnessThreshold = 120;
    
    /// <summary>
    /// 技能图标亮度阈值
    /// 高于此值认为技能可用
    /// </summary>
    [ObservableProperty] private int _skillBrightnessThreshold = 80;
    
    /// <summary>
    /// Buff图标亮度阈值
    /// 高于此值认为Buff存在
    /// </summary>
    [ObservableProperty] private int _buffBrightnessThreshold = 50;
    
    #endregion
    
    #region 悬浮窗配置
    
    /// <summary>
    /// 悬浮窗X坐标
    /// </summary>
    [ObservableProperty] private double _overlayLeft = 10;
    
    /// <summary>
    /// 悬浮窗Y坐标
    /// </summary>
    [ObservableProperty] private double _overlayTop = 10;
    
    /// <summary>
    /// 悬浮窗透明度 (0.0-1.0)
    /// </summary>
    [ObservableProperty] private double _overlayOpacity = 1.0;
    
    #endregion
    
    #region 全局快捷键配置
    
    /// <summary>
    /// 启动/停止引擎快捷键 - 修饰键 (0=无, 1=Alt, 2=Ctrl, 4=Shift)
    /// </summary>
    [ObservableProperty] private uint _hotkeyStartStopModifier = 2; // Ctrl
    
    /// <summary>
    /// 启动/停止引擎快捷键 - 按键码
    /// </summary>
    [ObservableProperty] private uint _hotkeyStartStopKey = 118; // F7
    
    /// <summary>
    /// 暂停/恢复引擎快捷键 - 修饰键
    /// </summary>
    [ObservableProperty] private uint _hotkeyPauseModifier = 2; // Ctrl
    
    /// <summary>
    /// 暂停/恢复引擎快捷键 - 按键码
    /// </summary>
    [ObservableProperty] private uint _hotkeyPauseKey = 119; // F8
    
    /// <summary>
    /// 切换七情模式快捷键 - 修饰键
    /// </summary>
    [ObservableProperty] private uint _hotkeyQiQingModifier = 2; // Ctrl
    
    /// <summary>
    /// 切换七情模式快捷键 - 按键码
    /// </summary>
    [ObservableProperty] private uint _hotkeyQiQingKey = 120; // F9
    
    /// <summary>
    /// 是否启用全局快捷键
    /// </summary>
    [ObservableProperty] private bool _enableGlobalHotkeys = true;
    
    #endregion
}
