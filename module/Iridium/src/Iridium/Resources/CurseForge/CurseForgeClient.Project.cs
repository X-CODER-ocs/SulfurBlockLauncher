using Iridium.Extensions;
using Iridium.Models.Resources;
using Iridium.Resources.CurseForge;
using CurseForgeJsonContext = Iridium.Resources.CurseForge.CurseForgeJsonContext;

namespace Iridium.Resources.CurseForge;

public partial class CurseForgeClient {
    public async Task<ResourceProject?> GetProjectAsync(string id, CancellationToken cancellationToken = default) {
        var url = BaseUrl.AppendPathSegments("mods", ParseId(id));
        var response = await GetJsonOrNullAsync(url,
            CurseForgeJsonContext.Default.CurseForgeResponseCurseForgeProject, cancellationToken);
        
        return response?.Data?.ToResourceProject();
    }

    public async Task<IReadOnlyList<ResourceProject>> GetProjectsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default) {
        var values = ids.Select(ParseId).Distinct().ToArray();
        if (values.Length == 0)
            return [];

        var projects = new List<CurseForgeProject>();
        foreach (var batch in values.Chunk(MaxBatchSize)) {
            var response = await PostJsonAsync(BaseUrl.AppendPathSegments("mods"),
                new CurseForgeModsRequest { ModIds = batch },
                CurseForgeJsonContext.Default.CurseForgeModsRequest,
                CurseForgeJsonContext.Default.CurseForgeResponseListCurseForgeProject, cancellationToken);
            
            if (response?.Data is { } data)
                projects.AddRange(data);
        }

        return [.. projects.Select(project => project.ToResourceProject())];
    }

    public async Task<IReadOnlyList<ResourceProject>> GetFeaturedAsync(CancellationToken cancellationToken = default) {
        var url = BaseUrl.AppendPathSegments("mods", "featured");
        var response = await PostJsonAsync(url,
            new CurseForgeFeaturedRequest { GameId = MinecraftGameId, ExcludedModIds = [0] },
            CurseForgeJsonContext.Default.CurseForgeFeaturedRequest,
            CurseForgeJsonContext.Default.CurseForgeResponseCurseForgeFeaturedResult, cancellationToken);
        
        if (response?.Data is not { } result)
            return [];

        var seen = new HashSet<long>();
        var projects = result.Popular
            .Concat(result.Featured)
            .Where(project => seen.Add(project.Id))
            .ToList();

        return [.. projects.Select(project => project.ToResourceProject())];
    }
}
