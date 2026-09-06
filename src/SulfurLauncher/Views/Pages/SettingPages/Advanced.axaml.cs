using System.Runtime.InteropServices;
using SulfurLauncher.Core.Module.AggregatedSearch;
using SulfurLauncher.ViewModels;

namespace SulfurLauncher.Views.Pages.SettingPages;

[AggregatedSearchPage("pages_advanced", "pages_advancedPath", "Advanced")]
public partial class Advanced : Dsc
{
    public Advanced()
    {
        InitializeComponent();
        DataContext = this;
    }

    public bool IsOverlaySupported => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
}