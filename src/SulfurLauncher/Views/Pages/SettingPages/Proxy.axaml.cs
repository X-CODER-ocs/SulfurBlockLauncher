using SulfurLauncher.Core.Module.AggregatedSearch;
using SulfurLauncher.Core.Services;
using SulfurLauncher.ViewModels;

namespace SulfurLauncher.Views.Pages.SettingPages;

[AggregatedSearchPage("pages_proxy", "pages_proxyPath", "Proxy")]
public partial class Proxy : Dsc
{
    public Proxy()
    {
        InitializeComponent();
        DataContext = this;
    }

    public object DefaultAgent => $"SulfurLauncher/{AppVersionService.Instance.Version.VersionTitle}";
}