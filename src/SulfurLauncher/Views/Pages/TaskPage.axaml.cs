using Avalonia.Media;
using SulfurLauncher.Core.Module.AggregatedSearch;
using SulfurLauncher.Localization;
using SulfurLauncher.ViewModels;
using Tio.Avalonia.Standard.Tab.Entries;
using Tio.Avalonia.Standard.Tab.Interface;

using SulfurLauncher.Module;
namespace SulfurLauncher.Views.Pages;

[AggregatedSearchPage("pages_tasks", "pages_tasksPath", "Task")]
public partial class TaskPage : Dsc, ITioTabPage
{
    public TaskPage()
    {
        InitializeComponent();
        DataContext = this;
    }

    public PageInfo PageInfo { get; init; } = new()
    {
        Title = CommonLanguageManager.Instance.titleBar_tasks.CurrentValue(),
        IconGlyph = "\ue62a", IconFont = IconResources.FontFamilyName
    };

    public TabEntry HostTab { get; set; }

    public void OnClose()
    {
        DataContext = null;
    }
}