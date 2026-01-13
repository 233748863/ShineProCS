using System.Windows;
using System.Windows.Threading;

using Application = System.Windows.Application;

namespace ShineProCS.Helpers;

/// <summary>
/// UI 调度器帮助类
/// 移植自 BetterGI
/// </summary>
public static class UIDispatcherHelper
{
    /// <summary>
    /// 在 UI 线程上执行操作
    /// </summary>
    public static void Invoke(Action action)
    {
        if (Application.Current?.Dispatcher == null)
        {
            action();
            return;
        }

        if (Application.Current.Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            Application.Current.Dispatcher.Invoke(action);
        }
    }

    /// <summary>
    /// 在 UI 线程上异步执行操作
    /// </summary>
    public static async Task InvokeAsync(Action action)
    {
        if (Application.Current?.Dispatcher == null)
        {
            action();
            return;
        }

        if (Application.Current.Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            await Application.Current.Dispatcher.InvokeAsync(action);
        }
    }

    /// <summary>
    /// 在 UI 线程上执行操作并返回结果
    /// </summary>
    public static T Invoke<T>(Func<T> func)
    {
        if (Application.Current?.Dispatcher == null)
        {
            return func();
        }

        if (Application.Current.Dispatcher.CheckAccess())
        {
            return func();
        }
        else
        {
            return Application.Current.Dispatcher.Invoke(func);
        }
    }

    /// <summary>
    /// 在 UI 线程上异步执行操作并返回结果
    /// </summary>
    public static async Task<T> InvokeAsync<T>(Func<T> func)
    {
        if (Application.Current?.Dispatcher == null)
        {
            return func();
        }

        if (Application.Current.Dispatcher.CheckAccess())
        {
            return func();
        }
        else
        {
            return await Application.Current.Dispatcher.InvokeAsync(func);
        }
    }

    /// <summary>
    /// 延迟在 UI 线程上执行操作
    /// </summary>
    public static void BeginInvoke(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        Application.Current?.Dispatcher?.BeginInvoke(action, priority);
    }
}
