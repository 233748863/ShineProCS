using ShineProCS.Core.Interfaces;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace ShineProCS.Core.Services;

/// <summary>
/// 通知服务实现
/// 使用 WPF-UI 的 SnackbarService 显示通知
/// 参考 BetterGI 的通知服务设计
/// </summary>
public class NotificationService : INotificationService
{
    private ISnackbarService? _snackbarService;
    
    /// <summary>
    /// 通知显示时长（毫秒）
    /// </summary>
    private const int DefaultTimeout = 3000;
    
    /// <summary>
    /// 通知事件，用于在 SnackbarService 不可用时通知订阅者
    /// </summary>
    public event Action<string, string?, NotificationLevel>? NotificationReceived;
    
    public NotificationService()
    {
    }
    
    public NotificationService(ISnackbarService snackbarService)
    {
        _snackbarService = snackbarService;
    }
    
    /// <summary>
    /// 设置 SnackbarService
    /// 用于延迟初始化场景
    /// </summary>
    public void SetSnackbarService(ISnackbarService snackbarService)
    {
        _snackbarService = snackbarService;
    }
    
    /// <summary>
    /// 显示通知
    /// </summary>
    public void Show(string message, string? title = null, NotificationLevel level = NotificationLevel.Info)
    {
        // 触发事件，允许其他组件处理通知
        NotificationReceived?.Invoke(message, title, level);
        
        if (_snackbarService == null)
        {
            // 如果 SnackbarService 不可用，使用 ToastManager 作为后备
            ShowViaToastManager(message, title, level);
            return;
        }
        
        try
        {
            var appearance = GetAppearance(level);
            var icon = GetIcon(level);
            
            // 在 UI 线程上显示通知
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                _snackbarService.Show(
                    title ?? GetDefaultTitle(level),
                    message,
                    appearance,
                    icon,
                    TimeSpan.FromMilliseconds(DefaultTimeout)
                );
            });
        }
        catch
        {
            // 如果显示失败，使用 ToastManager 作为后备
            ShowViaToastManager(message, title, level);
        }
    }
    
    /// <summary>
    /// 使用 ToastManager 显示通知（后备方案）
    /// </summary>
    private void ShowViaToastManager(string message, string? title, NotificationLevel level)
    {
        try
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                switch (level)
                {
                    case NotificationLevel.Success:
                        Views.ToastManager.Success(message, title ?? "成功");
                        break;
                    case NotificationLevel.Warning:
                        Views.ToastManager.Warning(message, title ?? "警告");
                        break;
                    case NotificationLevel.Error:
                        Views.ToastManager.Error(message, title ?? "错误");
                        break;
                    default:
                        Views.ToastManager.Info(message, title ?? "提示");
                        break;
                }
            });
        }
        catch
        {
            // 忽略显示错误
        }
    }
    
    /// <summary>
    /// 显示信息通知
    /// </summary>
    public void ShowInfo(string message, string? title = null)
    {
        Show(message, title, NotificationLevel.Info);
    }
    
    /// <summary>
    /// 显示成功通知
    /// </summary>
    public void ShowSuccess(string message, string? title = null)
    {
        Show(message, title, NotificationLevel.Success);
    }
    
    /// <summary>
    /// 显示警告通知
    /// </summary>
    public void ShowWarning(string message, string? title = null)
    {
        Show(message, title, NotificationLevel.Warning);
    }
    
    /// <summary>
    /// 显示错误通知
    /// </summary>
    public void ShowError(string message, string? title = null)
    {
        Show(message, title, NotificationLevel.Error);
    }
    
    /// <summary>
    /// 获取通知外观
    /// </summary>
    private static ControlAppearance GetAppearance(NotificationLevel level)
    {
        return level switch
        {
            NotificationLevel.Success => ControlAppearance.Success,
            NotificationLevel.Warning => ControlAppearance.Caution,
            NotificationLevel.Error => ControlAppearance.Danger,
            _ => ControlAppearance.Secondary
        };
    }
    
    /// <summary>
    /// 获取通知图标
    /// </summary>
    private static IconElement? GetIcon(NotificationLevel level)
    {
        var symbol = level switch
        {
            NotificationLevel.Success => SymbolRegular.CheckmarkCircle24,
            NotificationLevel.Warning => SymbolRegular.Warning24,
            NotificationLevel.Error => SymbolRegular.ErrorCircle24,
            _ => SymbolRegular.Info24
        };
        
        return new SymbolIcon { Symbol = symbol };
    }
    
    /// <summary>
    /// 获取默认标题
    /// </summary>
    private static string GetDefaultTitle(NotificationLevel level)
    {
        return level switch
        {
            NotificationLevel.Success => "成功",
            NotificationLevel.Warning => "警告",
            NotificationLevel.Error => "错误",
            _ => "提示"
        };
    }
}
