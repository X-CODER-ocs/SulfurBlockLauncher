using Avalonia.Controls;
using SulfurLauncher.Core.Classes.Entries;
using SulfurLauncher.Core.Module.Widgets;

namespace SulfurLauncher.Views.Widgets;

public abstract class IWidgetContent : UserControl
{
    public WidgetCellSize Size { get; protected set; } = new(1, 1);
    public WidgetKind Kind { get; internal set; }

    public virtual void Initialize(WidgetLayoutData layout)
    {
    }

    public virtual void PerformClick()
    {
    }
}

public interface IWidgetContextMenuProvider
{
    IReadOnlyList<MenuItem> CreateContextMenuItems(Action saveLayout);
}

public interface IWidgetPersistenceAware
{
    void SetSaveLayoutAction(Action saveLayout);
}
