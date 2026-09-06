using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SulfurLauncher.Core.Const;
using SulfurLauncher.Core.Minecraft.Classes;
using SulfurLauncher.Core.Module.AggregatedSearch;
using SulfurLauncher.Localization;
using SulfurLauncher.ViewModels;
using SulfurLauncher.Views.Components.Operations.OpenFile;
using Tio.Avalonia.Standard.Tab.Gateway;
using TioUi.Common;
using TioUi.Common.Extensions;
using TioUi.Controls;
using NewMinecraftFolderViewModel = SulfurLauncher.Views.Components.Operations.OpenFile.NewMinecraftFolderViewModel;

namespace SulfurLauncher.Views.Pages.SettingPages;

[AggregatedSearchPage("pages_gameFolder", "pages_gameFolderPath", "GameFolder")]
public partial class GameFolder : Dsc
{
    public GameFolder()
    {
        InitializeComponent();
        DataContext = this;
    }

    private async void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        var options = new OverlayDialogOptions
        {
            Mode = DialogMode.None,
            Buttons = DialogButton.None,
            CanLightDismiss = false,
            CanDragMove = true,
            IsCloseButtonVisible = false,
            CanResize = false,
            VerticalOffset = 110,
            VerticalAnchor = VerticalPosition.Top
        };

        var result = await OverlayDialog
            .ShowCustomAsync<NewMinecraftFolder, NewMinecraftFolderViewModel, MinecraftFolderEntry>(
                new NewMinecraftFolderViewModel(Data.ConfigEntry.MinecraftFolders.Select(x
                    => x.FolderPath).ToList()), (sender as Control)!.TryGetHostId(), options);

        if (result == null) return;
        Data.ConfigEntry.MinecraftFolders.Add(result);
    }

    private void Button1_OnClick(object? sender, RoutedEventArgs e)
    {
        var folder = (sender as Control).Tag as MinecraftFolderEntry;
        if (folder == null)
            return;
        var restoresDefaultFolder = folder.SupportsInstallation &&
                                    Data.ConfigEntry.InstallableMinecraftFolders.Count() == 1;
        Data.ConfigEntry.MinecraftFolders.Remove(folder);
        if (restoresDefaultFolder)
            Dispatcher.UIThread.Post(() => this.GetTopLevel().Notice(
                CommonLanguageManager.Instance.gameFolder_keepAtLeastOne.CurrentValue(), NotificationType.Warning));
    }
}