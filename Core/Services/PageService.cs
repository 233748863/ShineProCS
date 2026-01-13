using Microsoft.Extensions.DependencyInjection;
using ShineProCS.Core.Interfaces;
using Wpf.Ui;

namespace ShineProCS.Core.Services;

/// <summary>
/// 页面服务实现
/// 管理页面导航和页面实例
/// 参考 BetterGI 的页面服务设计
/// 需求: 2.1, 2.3, 2.4, 2.5
/// </summary>
public class PageService : IPageService
{
    private IServiceProvider? _serviceProvider;
    private INavigationService? _navigationService;
    private Type? _currentPageType;
    private readonly Dictionary<Type, string> _pageTypeToNavigationItemMap = new();
    
    public PageService()
    {
    }
    
    public PageService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    /// <summary>
    /// 获取当前页面类型
    /// 需求: 2.4 - 保持页面状态
    /// </summary>
    public Type? CurrentPageType => _currentPageType;
    
    /// <summary>
    /// 页面类型到导航项的映射
    /// 用于验证导航一致性
    /// </summary>
    public IReadOnlyDictionary<Type, string> PageTypeToNavigationItemMap => _pageTypeToNavigationItemMap;
    
    /// <summary>
    /// 设置服务提供者
    /// 用于延迟初始化场景
    /// </summary>
    public void SetServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _navigationService = serviceProvider.GetService<INavigationService>();
    }
    
    /// <summary>
    /// 注册页面类型与导航项的映射
    /// </summary>
    public void RegisterPageMapping(Type pageType, string navigationItemName)
    {
        ArgumentNullException.ThrowIfNull(pageType);
        ArgumentNullException.ThrowIfNull(navigationItemName);
        
        _pageTypeToNavigationItemMap[pageType] = navigationItemName;
    }
    
    /// <summary>
    /// 获取指定类型的页面实例
    /// </summary>
    public T GetPage<T>() where T : class
    {
        if (_serviceProvider == null)
        {
            throw new InvalidOperationException("服务提供者未初始化，请先调用 SetServiceProvider");
        }
        
        return _serviceProvider.GetRequiredService<T>();
    }
    
    /// <summary>
    /// 获取指定类型的页面实例
    /// </summary>
    public object? GetPage(Type pageType)
    {
        if (_serviceProvider == null)
        {
            throw new InvalidOperationException("服务提供者未初始化，请先调用 SetServiceProvider");
        }
        
        return _serviceProvider.GetService(pageType);
    }
    
    /// <summary>
    /// 导航到指定页面
    /// 需求: 2.1 - 点击导航项时跳转到对应页面
    /// </summary>
    public bool Navigate<T>() where T : class
    {
        return Navigate(typeof(T));
    }
    
    /// <summary>
    /// 导航到指定页面
    /// 需求: 2.1 - 点击导航项时跳转到对应页面
    /// </summary>
    public bool Navigate(Type pageType)
    {
        if (_navigationService == null)
        {
            return false;
        }
        
        var result = _navigationService.Navigate(pageType);
        if (result)
        {
            _currentPageType = pageType;
        }
        
        return result;
    }
}
