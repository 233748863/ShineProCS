using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShineProCS.Models;

/// <summary>
/// 状态项 - 用于在遮罩窗口显示功能启用状态
/// 移植自 BetterGI
/// </summary>
public partial class StatusItem : ObservableObject
{
    /// <summary>
    /// 显示名称（支持图标字符）
    /// </summary>
    public string Name { get; set; }

    private INotifyPropertyChanged _sourceObject { get; set; }
    private string _propertyName { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    [ObservableProperty] 
    private bool _isEnabled;

    /// <summary>
    /// 创建状态项
    /// </summary>
    /// <param name="name">显示名称</param>
    /// <param name="sourceObject">源对象（实现 INotifyPropertyChanged）</param>
    /// <param name="propertyName">属性名称（默认为 "Enabled"）</param>
    public StatusItem(string name, INotifyPropertyChanged sourceObject, string propertyName = "Enabled")
    {
        Name = name;
        _sourceObject = sourceObject;
        _propertyName = propertyName;

        _sourceObject.PropertyChanged += OnSourcePropertyChanged;
        IsEnabled = GetSourceValue();
    }

    private bool GetSourceValue()
    {
        var property = _sourceObject.GetType().GetProperty(_propertyName);
        if (property == null) return false;
        var value = property.GetValue(_sourceObject);
        if (value == null) return false;
        return (bool)value;
    }

    private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == _propertyName)
        {
            IsEnabled = GetSourceValue();
        }
    }

    /// <summary>
    /// 取消订阅
    /// </summary>
    public void Unsubscribe()
    {
        _sourceObject.PropertyChanged -= OnSourcePropertyChanged;
    }
}
