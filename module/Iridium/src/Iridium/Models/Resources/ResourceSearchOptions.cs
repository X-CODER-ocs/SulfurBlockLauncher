using Iridium.Enums;

namespace Iridium.Models.Resources;

public sealed class ResourceSearchOptions {
    public ResourceSource Source { get; init; } = ResourceSource.All;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 40;

    public int CurseForgeGameId { get; init; } = 432;
    
    public string? Query { get; init; }
    public string? GameVersion { get; init; }

    public SortOrder SortOrder { get; init; } = SortOrder.Desc;
    public ResourceType Type { get; init; } = ResourceType.Mod;
    public ResourceSort Sort { get; init; } = ResourceSort.Relevance;
    public ResourceLoaderType Loader { get; init; } = ResourceLoaderType.Any;
    public ResourceEnvironment Environment { get; init; } = ResourceEnvironment.Any;
    
    public IReadOnlyList<ResourceCategory> Tags { get; init; } = [];
    public IReadOnlyList<ResourceCategory> ExcludedTags { get; init; } = [];
}
