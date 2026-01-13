namespace ShineProCS.Core.Interfaces;

/// <summary>
/// ViewModel 接口
/// 所有 ViewModel 的基础接口，用于依赖注入和生命周期管理
/// 参考 BetterGI 的 IViewModel 设计
/// </summary>
public interface IViewModel
{
    /// <summary>
    /// 当导航到此 ViewModel 对应的页面时调用
    /// 用于初始化数据或刷新状态
    /// </summary>
    void OnNavigatedTo();
    
    /// <summary>
    /// 当从此 ViewModel 对应的页面导航离开时调用
    /// 用于清理资源或保存状态
    /// </summary>
    void OnNavigatedFrom();
}
