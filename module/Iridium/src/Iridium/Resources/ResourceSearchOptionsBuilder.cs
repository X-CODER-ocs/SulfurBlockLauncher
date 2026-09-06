using Iridium.Enums;
using Iridium.Models.Resources;

namespace Iridium.Resources;

/// <summary>
/// Fluent builder for <see cref="ResourceSearchOptions"/>. Category selection
/// accepts the compile-time category enums (ModCategory, ...) which are mapped
/// to <see cref="ResourceCategory"/> by the generated, reflection-free
/// <c>ToResourceCategory()</c> extensions.
/// </summary>
public sealed class ResourceSearchOptionsBuilder {
    private ResourceSource _source = ResourceSource.All;
    private int _page = 1;
    private int _pageSize = 40;
    private int _curseForgeGameId = 432;
    private string? _query;
    private string? _gameVersion;
    private SortOrder _sortOrder = SortOrder.Desc;
    private ResourceType _type = ResourceType.Mod;
    private ResourceSort _sort = ResourceSort.Relevance;
    private ResourceLoaderType _loader = ResourceLoaderType.Any;
    private readonly List<ResourceCategory> _tags = [];

    public ResourceSearchOptionsBuilder Source(ResourceSource source) { _source = source; return this; }

    public ResourceSearchOptionsBuilder Page(int page) { _page = page; return this; }

    public ResourceSearchOptionsBuilder PageSize(int pageSize) { _pageSize = pageSize; return this; }

    public ResourceSearchOptionsBuilder CurseForgeGameId(int gameId) { _curseForgeGameId = gameId; return this; }

    public ResourceSearchOptionsBuilder Query(string query) { _query = query; return this; }

    public ResourceSearchOptionsBuilder GameVersion(string gameVersion) { _gameVersion = gameVersion; return this; }

    public ResourceSearchOptionsBuilder Ordering(SortOrder sortOrder) { _sortOrder = sortOrder; return this; }

    public ResourceSearchOptionsBuilder Type(ResourceType type) { _type = type; return this; }

    public ResourceSearchOptionsBuilder Sort(ResourceSort sort) { _sort = sort; return this; }

    public ResourceSearchOptionsBuilder Loader(ResourceLoaderType loader) { _loader = loader; return this; }

    public ResourceSearchOptionsBuilder Categories(params ModCategory[] categories) {
        foreach (var category in categories)
            _tags.Add(category.ToResourceCategory());
        
        return this;
    }

    public ResourceSearchOptionsBuilder Categories(params ModpackCategory[] categories) {
        foreach (var category in categories)
            _tags.Add(category.ToResourceCategory());
        
        return this;
    }

    public ResourceSearchOptionsBuilder Categories(params ResourcePackCategory[] categories) {
        foreach (var category in categories)
            _tags.Add(category.ToResourceCategory());
        
        return this;
    }

    public ResourceSearchOptionsBuilder Categories(params ShaderCategory[] categories) {
        foreach (var category in categories)
            _tags.Add(category.ToResourceCategory());
        
        return this;
    }

    public ResourceSearchOptionsBuilder Categories(params DataPackCategory[] categories) {
        foreach (var category in categories)
            _tags.Add(category.ToResourceCategory());
        
        return this;
    }

    public ResourceSearchOptionsBuilder Categories(params WorldCategory[] categories) {
        foreach (var category in categories)
            _tags.Add(category.ToResourceCategory());
        
        return this;
    }

    public ResourceSearchOptions Build() => new() {
        Source = _source,
        Page = _page,
        PageSize = _pageSize,
        CurseForgeGameId = _curseForgeGameId,
        Query = _query,
        GameVersion = _gameVersion,
        SortOrder = _sortOrder,
        Type = _type,
        Sort = _sort,
        Loader = _loader,
        Tags = _tags
    };
}
