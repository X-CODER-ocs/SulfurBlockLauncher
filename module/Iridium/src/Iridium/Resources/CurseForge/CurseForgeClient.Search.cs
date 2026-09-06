using System.Text.Json;
using Iridium.Enums;
using Iridium.Extensions;
using Iridium.Models.Resources;
using CurseForgeJsonContext = Iridium.Resources.CurseForge.CurseForgeJsonContext;

namespace Iridium.Resources.CurseForge;

public partial class CurseForgeClient {
    public async Task<ResourceSearchPage<ResourceHit>> SearchAsync(ResourceSearchOptions options, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(options);
        
        var url = BaseUrl.AppendPathSegments("mods", "search")
            .SetQueryParam("gameId", options.CurseForgeGameId)
            .SetQueryParam("sortOrder", options.SortOrder == SortOrder.Asc ? "asc" : "desc")
            .SetQueryParam("sortField", options.Sort.ToCurseForgeSortField())
            .SetQueryParam("index", Math.Max(0, (options.Page - 1) * options.PageSize))
            .SetQueryParam("pageSize", Math.Clamp(options.PageSize, 1, 50));

        // classId 表仅对 Minecraft Java（gameId 432）有效，其他版本（如基岩版 78022）不附加 classId 过滤。
        if (options.CurseForgeGameId == MinecraftGameId && options.Type.ToCurseForgeClassId() is { } classId)
            url = url.SetQueryParam("classId", classId);

        var tags = options.Tags
            .Select(tag => tag.CurseForgeId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .Take(10)
            .ToArray();

        url = tags.Length switch {
            1 => url.SetQueryParam("categoryId", tags[0]),
            > 1 => url.SetQueryParam("categoryIds",
                JsonSerializer.Serialize(tags, CurseForgeJsonContext.Default.Int32Array)),
            _ => url
        };

        if (!string.IsNullOrWhiteSpace(options.GameVersion))
            url = url.SetQueryParam("gameVersion", options.GameVersion);
        
        if (options.Loader.ToCurseForgeLoaderType() is { } loader)
            url = url.SetQueryParam("modLoaderType", loader);
        
        if (!string.IsNullOrWhiteSpace(options.Query))
            url = url.SetQueryParam("searchFilter", options.Query);

        var response = await GetJsonAsync(url,
            CurseForgeJsonContext.Default.CurseForgePagedResponseListCurseForgeProject, cancellationToken);
        
        var hits = (response?.Data ?? [])
            .Select(project => project.ToResourceHit(options.Type)).ToArray();
        
        return new ResourceSearchPage<ResourceHit>(hits,
            response?.Pagination?.TotalCount ?? 0, options.Page, options.PageSize);
    }
}
