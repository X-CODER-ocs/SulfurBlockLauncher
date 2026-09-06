using Flurl;
using Iridium.Download;
using Iridium.Enums;
using Iridium.Resources;
using Iridium.Interfaces;

namespace Iridium.Resources.Modrinth;

public sealed partial class ModrinthClient : IResourceClient {
    public ResourceApiSource ResourceApiSource { get; }
    
    private Url BaseUrl => ResourceApiSource.GetApi(ResourceApiType.Modrinth);

    public ModrinthClient(ResourceApiSource? source = null) {
        ResourceApiSource = source ?? ResourceApiSource.Official;
    }
}
