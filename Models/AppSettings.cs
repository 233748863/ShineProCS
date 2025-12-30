using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShineProCS.Models;

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
    
    #region 全局快捷键配置
    
    [ObservableProperty] private uint _hotkeyStartStopModifier = 2; // Ctrl
    [ObservableProperty] private uint _hotkeyStartStopKey = 118; // F7
    [ObservableProperty] private uint _hotkeyPauseModifier = 2; // Ctrl
    [ObservableProperty] private uint _hotkeyPauseKey = 119; // F8
    [ObservableProperty] private bool _enableGlobalHotkeys = true;
    
    #endregion
}
