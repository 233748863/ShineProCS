using CommunityToolkit.Mvvm.ComponentModel;

namespace ShineProCS.Core.Config;

/// <summary>
/// 遮罩窗口配置
/// 移植自 BetterGI
/// </summary>
[Serializable]
public partial class MaskWindowConfig : ObservableObject
{
    /// <summary>
    /// 是否启用遮罩窗口
    /// </summary>
    [ObservableProperty]
    private bool _maskEnabled = true;

    /// <summary>
    /// 是否在遮罩窗口上显示识别结果
    /// </summary>
    [ObservableProperty]
    private bool _displayRecognitionResultsOnMask = true;

    /// <summary>
    /// 显示日志窗口
    /// </summary>
    [ObservableProperty]
    private bool _showLogBox = true;

    /// <summary>
    /// 显示状态指示
    /// </summary>
    [ObservableProperty]
    private bool _showStatus = true;

    /// <summary>
    /// 方位提示是否启用（东南西北）
    /// </summary>
    [ObservableProperty]
    private bool _directionsEnabled = false;

    /// <summary>
    /// UID遮盖是否启用
    /// </summary>
    [ObservableProperty]
    private bool _uidCoverEnabled = false;

    /// <summary>
    /// 显示FPS
    /// </summary>
    [ObservableProperty]
    private bool _showFps = false;

    /// <summary>
    /// 作为游戏子窗体
    /// </summary>
    [ObservableProperty]
    private bool _useSubform = false;

    /// <summary>
    /// 遮罩文本透明度 (0.0-1.0)
    /// </summary>
    [ObservableProperty]
    private double _textOpacity = 1.0;

    /// <summary>
    /// 日志框位置 X
    /// </summary>
    [ObservableProperty]
    private double _logBoxLeft = 20;

    /// <summary>
    /// 日志框位置 Y
    /// </summary>
    [ObservableProperty]
    private double _logBoxTop = 800;

    /// <summary>
    /// 日志框宽度
    /// </summary>
    [ObservableProperty]
    private double _logBoxWidth = 477;

    /// <summary>
    /// 日志框高度
    /// </summary>
    [ObservableProperty]
    private double _logBoxHeight = 188;

    /// <summary>
    /// 状态栏位置 X
    /// </summary>
    [ObservableProperty]
    private double _statusBarLeft = 20;

    /// <summary>
    /// 状态栏位置 Y
    /// </summary>
    [ObservableProperty]
    private double _statusBarTop = 775;

    /// <summary>
    /// 1080p下UID遮盖的位置与大小
    /// </summary>
    public static readonly System.Windows.Rect UidCoverRightBottomRect = new(1920 - 1685, 1080 - 1053, 178, 22);
}
