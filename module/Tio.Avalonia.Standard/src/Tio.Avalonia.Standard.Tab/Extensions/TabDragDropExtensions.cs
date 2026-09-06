using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Tio.Avalonia.Standard.Tab.Behavior;
using Tio.Avalonia.Standard.Tab.Interface;

namespace Tio.Avalonia.Standard.Tab.Extensions;

/// <summary>
/// 为标签页控件提供拖拽功能的扩展方法
/// </summary>
public static class TabDragDropExtensions
{
    // 使用弱引用表：容器被回收后条目会自动消失，避免静态字典长期持有窗口与其下所有标签页。
    private static readonly ConditionalWeakTable<Control, TabDragDropBehavior> Behaviors = new();

    /// <summary>
    /// 为标签页容器启用拖拽重新排序功能
    /// </summary>
    /// <param name="container">标签页容器控件（通常是 SelectionList）</param>
    /// <param name="window">标签页窗口</param>
    public static void EnableTabDragDrop(this Control container, TioTabWindowBase window)
    {
        // 如果已经启用，先禁用旧的
        container.DisableTabDragDrop();

        var behavior = new TabDragDropBehavior();
        behavior.Attach(container, window);
        Behaviors.Add(container, behavior);
    }

    /// <summary>
    /// 禁用标签页容器的拖拽功能
    /// </summary>
    /// <param name="container">标签页容器控件</param>
    public static void DisableTabDragDrop(this Control container)
    {
        if (!Behaviors.TryGetValue(container, out var behavior))
            return;

        behavior.Detach(container);
        Behaviors.Remove(container);
    }
}
