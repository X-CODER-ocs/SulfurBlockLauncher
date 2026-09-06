using SulfurLauncher.Core.Const;
using SulfurLauncher.Views.Pages;
using Tio.Avalonia.Standard.Tab.Entries;
using TioUi.Shared;

namespace SulfurLauncher;

public partial class App
{
    private void ThemeMirage_OnClick(object? sender, EventArgs e)
    {
        Data.ConfigEntry.Theme = Theme.Mirage;
    }

    private void ThemeDark_OnClick(object? sender, EventArgs e)
    {
        Data.ConfigEntry.Theme = Theme.Dark;
    }

    private void ThemeLight_OnClick(object? sender, EventArgs e)
    {
        Data.ConfigEntry.Theme = Theme.Light;
    }

    private void ThemeDefault_OnClick(object? sender, EventArgs e)
    {
        Data.ConfigEntry.Theme = Theme.System;
    }

    private void OpenSetting_OnClick(object? sender, EventArgs e)
    {
        if (UiProperty.TabWindow is not { } window) return;
        var tabEntry = new TabEntry(window, new SettingPage());
        window.CreateTab(tabEntry);
        window.SelectTab(tabEntry);
        window.Activate();
    }
}