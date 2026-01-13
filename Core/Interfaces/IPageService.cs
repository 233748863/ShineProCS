namespace ShineProCS.Core.Interfaces;

/// <summary>
/// 页面服务接口
/// 管理页面导航和页面实例
/// 参考 BetterGI 的页面服务设计
/// 需求: 2.1, 2.3, 2.4, 2.5
/// </summary>
public interface IPageService
{
    /// <summary>
    /// 获取指定类型的页面实例
    /// </summary>
    /// <typeparam name="T">页面类型</typeparam>
    /// <returns>页面实例</returns>
    T GetPage<T>() where T : class;
    
    /// <summary>
    /// 获取指定类型的页面实例
    /// </summary>
    /// <param name="pageType">页面类型</param>
    /// <returns>页面实例，如果未找到返回 null</returns>
    object? GetPage(Type pageType);
    
    /// <summary>
    /// 设置服务提供者
    /// 用于延迟初始化场景
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    void SetServiceProvider(IServiceProvider serviceProvider);
    
    /// <summary>
    /// 导航到指定页面
    /// 需求: 2.1 - 点击导航项时跳转到对应页面
    /// </summary>
    /// <typeparam name="T">页面类型</typeparam>
    /// <returns>导航是否成功</returns>
    bool Navigate<T>() where T : class;
    
    /// <summary>
    /// 导航到指定页面
    /// 需求: 2.1 - 点击导航项时跳转到对应页面
    /// </summary>
    /// <param name="pageType">页面类型</param>
    /// <returns>导航是否成功</returns>
    bool Navigate(Type pageType);
    
    /// <summary>
    /// 获取当前页面类型
    /// 需求: 2.4 - 保持页面状态
    /// </summary>
    Type? CurrentPageType { get; }
    
    /// <summary>
    /// 页面类型到导航项的映射
    /// 用于验证导航一致性
    /// </summary>
    IReadOnlyDictionary<Type, string> PageTypeToNavigationItemMap { get; }
    
    /// <summary>
    /// 注册页面类型与导航项的映射
    /// </summary>
    /// <param name="pageType">页面类型</param>
    /// <param name="navigationItemName">导航项名称</param>
    void RegisterPageMapping(Type pageType, string navigationItemName);
}
