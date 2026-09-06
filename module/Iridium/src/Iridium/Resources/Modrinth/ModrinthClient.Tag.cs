using Iridium.Enums;
using Iridium.Extensions;
using Iridium.Utilities;
using Iridium.Models.Resources;
using ModrinthTagContext = Iridium.Resources.Modrinth.ModrinthTagContext;

namespace Iridium.Resources.Modrinth;

public partial class ModrinthClient {
    public async Task<IReadOnlyList<ResourceCategory>> GetCategoriesAsync(ResourceType type, CancellationToken cancellationToken = default) {
        var projectType = type.ToModrinthProjectType();
        var url = BaseUrl.AppendPathSegments("tag", "category");
        
        var result = await HttpHelper.GetJsonAsync(url,
            ModrinthTagContext.Default.IReadOnlyListModrinthCategory,
            cancellationToken);

        return result?
            .Where(category => string.Equals(category.ProjectType, projectType, StringComparison.OrdinalIgnoreCase))
            .Select(category => category.ToResourceCategory(type))
            .ToArray() ?? [];
    }

    public async Task<IReadOnlyList<string>> GetGameVersionsAsync(CancellationToken cancellationToken = default) {
        var url = BaseUrl.AppendPathSegments("tag", "game_version");
        
        var result = await HttpHelper.GetJsonAsync(url,
            ModrinthTagContext.Default.IReadOnlyListModrinthGameVersion,
            cancellationToken);

        return result?
            .Select(version => version.Version)
            .Where(static version => version is not null)
            .Cast<string>()
            .ToArray() ?? [];
    }

    public async Task<IReadOnlyList<ResourceLoaderType>> GetLoadersAsync(CancellationToken cancellationToken = default) {
        var url = BaseUrl.AppendPathSegments("tag", "loader");
        
        var result = await HttpHelper.GetJsonAsync(url,
            ModrinthTagContext.Default.IReadOnlyListModrinthLoader,
            cancellationToken);

        return result?
            .Select(loader => loader.Name.ToResourceLoaderType())
            .Where(static loader => loader.HasValue)
            .Select(static loader => loader!.Value)
            .Distinct()
            .ToArray() ?? [];
    }
}
