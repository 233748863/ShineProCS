using System.Collections.Concurrent;

namespace ShineProCS.Core.View.Drawable;

/// <summary>
/// 绘制内容管理器 - 管理遮罩窗口上的所有绘制元素
/// 移植自 BetterGI
/// </summary>
public class DrawContent
{
    /// <summary>
    /// 在遮罩窗口上绘制的矩形
    /// </summary>
    public ConcurrentDictionary<string, List<RectDrawable>> RectList { get; set; } = new();

    /// <summary>
    /// 在遮罩窗口上绘制的文本
    /// </summary>
    public ConcurrentDictionary<string, List<TextDrawable>> TextList { get; set; } = new();

    /// <summary>
    /// 在遮罩窗口上绘制的线条
    /// </summary>
    public ConcurrentDictionary<string, List<LineDrawable>> LineList { get; set; } = new();

    /// <summary>
    /// 刷新遮罩窗口的回调
    /// </summary>
    public Action? RefreshAction { get; set; }

    /// <summary>
    /// 添加或更新矩形
    /// </summary>
    public virtual void PutRect(string key, RectDrawable newRect)
    {
        if (RectList.TryGetValue(key, out var prevRect))
        {
            if (prevRect.Count == 0 && newRect.Equals(prevRect[0]))
            {
                return;
            }
        }

        RectList[key] = [newRect];
        RefreshAction?.Invoke();
    }

    /// <summary>
    /// 添加或移除矩形列表
    /// </summary>
    public virtual void PutOrRemoveRectList(string key, List<RectDrawable>? list)
    {
        bool changed = false;

        if (RectList.TryGetValue(key, out var prevRect))
        {
            if (list == null)
            {
                RectList.TryRemove(key, out _);
                changed = true;
            }
            else if (prevRect.Count != list.Count)
            {
                RectList[key] = list;
                changed = true;
            }
            else
            {
                RectList[key] = list;
                changed = true;
            }
        }
        else
        {
            if (list is { Count: > 0 })
            {
                RectList[key] = list;
                changed = true;
            }
        }

        if (changed)
        {
            RefreshAction?.Invoke();
        }
    }

    /// <summary>
    /// 移除矩形
    /// </summary>
    public virtual void RemoveRect(string key)
    {
        if (RectList.TryGetValue(key, out _))
        {
            RectList.TryRemove(key, out _);
            RefreshAction?.Invoke();
        }
    }

    /// <summary>
    /// 添加或更新线条
    /// </summary>
    public virtual void PutLine(string key, LineDrawable newLine)
    {
        if (LineList.TryGetValue(key, out var prev))
        {
            if (prev.Count == 0 && newLine.Equals(prev[0]))
            {
                return;
            }
        }

        LineList[key] = [newLine];
        RefreshAction?.Invoke();
    }

    /// <summary>
    /// 移除线条
    /// </summary>
    public virtual void RemoveLine(string key)
    {
        if (LineList.TryGetValue(key, out _))
        {
            LineList.TryRemove(key, out _);
            RefreshAction?.Invoke();
        }
    }

    /// <summary>
    /// 添加或更新文本
    /// </summary>
    public virtual void PutText(string key, TextDrawable newText)
    {
        if (TextList.TryGetValue(key, out var prev))
        {
            if (prev.Count == 0 && newText.Equals(prev[0]))
            {
                return;
            }
        }

        TextList[key] = [newText];
        RefreshAction?.Invoke();
    }

    /// <summary>
    /// 移除文本
    /// </summary>
    public virtual void RemoveText(string key)
    {
        if (TextList.TryGetValue(key, out _))
        {
            TextList.TryRemove(key, out _);
            RefreshAction?.Invoke();
        }
    }

    /// <summary>
    /// 清理所有绘制内容
    /// </summary>
    public virtual void ClearAll()
    {
        if (RectList.IsEmpty && TextList.IsEmpty && LineList.IsEmpty)
        {
            return;
        }
        RectList.Clear();
        TextList.Clear();
        LineList.Clear();
        RefreshAction?.Invoke();
    }
}
