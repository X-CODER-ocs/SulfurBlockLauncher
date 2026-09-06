using Avalonia.Controls;
using SulfurLauncher.Core.Module.AggregatedSearch;
using SulfurLauncher.Localization;
using SulfurLauncher.ViewModels;
using Tio.Avalonia.Standard.Tab.Gateway;
using TioUi.Common.Extensions;

namespace SulfurLauncher.Views.Pages.SettingPages;

[AggregatedSearchPage("pages_notifications", "pages_notificationsPath", "Notification")]
public partial class Notification : Dsc
{
    public Notification()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void SelectingItemsControl_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        (sender as Control)!.GetTopLevel().Notice(CommonLanguageManager.Instance.notification_test.CurrentValue());
    }
}