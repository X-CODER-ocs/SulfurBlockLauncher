using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using SulfurLauncher.Localization;
using Tio.Avalonia.Standard.Modules.DiskIO;
using Tio.Avalonia.Standard.Tab.Gateway;

namespace SulfurLauncher.Core.Module;

public static class DesktopShortcutUi
{
    public static async Task CreateAsync(TopLevel? topLevel, Func<Task<string>> create)
    {
        if (topLevel == null)
            return;

        try
        {
            var shortcutPath = await create();
            topLevel.Notice(string.Format(CommonLanguageManager.Instance.desktop_shortcutCreatedNotice.CurrentValue(), shortcutPath), NotificationType.Success);
        }
        catch (Exception ex)
        {
            Logger.Error(ex);
            topLevel.Notice(string.Format(CommonLanguageManager.Instance.desktop_shortcutCreateFailedNotice.CurrentValue(), ex.Message), NotificationType.Error);
        }
    }
}