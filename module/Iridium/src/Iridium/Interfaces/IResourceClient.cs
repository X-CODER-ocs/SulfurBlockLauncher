using Iridium.Download;
using Iridium.Enums;
using Iridium.Models.Resources;

namespace Iridium.Interfaces;

/// <summary>
/// Common surface shared by resource platform clients (Modrinth / CurseForge).
/// Returns Iridium domain models only, so a caller can drive either platform
/// interchangeably for Minecraft resource search / lookup.
/// </summary>
public interface IResourceClient {
    ResourceApiSource ResourceApiSource { get; }

    Task<ResourceSearchPage<ResourceHit>> SearchAsync(ResourceSearchOptions options, CancellationToken cancellationToken = default);

    Task<ResourceProject?> GetProjectAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResourceProject>> GetProjectsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResourceCategory>> GetCategoriesAsync(ResourceType type, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetGameVersionsAsync(CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<ResourceFile>> GetProjectFilesAsync(
        string projectId,
        string? gameVersion = null,
        ResourceLoaderType loader = ResourceLoaderType.Any,
        CancellationToken cancellationToken = default);
}
