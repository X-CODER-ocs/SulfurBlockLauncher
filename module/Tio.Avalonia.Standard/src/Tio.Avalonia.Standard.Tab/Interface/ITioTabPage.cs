using Tio.Avalonia.Standard.Tab.Entries;
using System.Threading.Tasks;

namespace Tio.Avalonia.Standard.Tab.Interface;

public interface ITioTabPage
{
    public PageInfo PageInfo { get; init; }
    public TabEntry HostTab { get; set; }

    public Task<bool> RequestCloseAsync()
    {
        return Task.FromResult(true);
    }

    public void OnClose()
    {
    }
}
