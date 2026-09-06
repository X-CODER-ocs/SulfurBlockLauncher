using Iridium.Extensions;
using Iridium.Utilities;
using Iridium.Models.Resources;
using ModrinthSearchResultContext = Iridium.Resources.Modrinth.ModrinthSearchResultContext;

namespace Iridium.Resources.Modrinth;

public partial class ModrinthClient {
    public async Task<ResourceSearchPage<ResourceHit>> SearchAsync(
        ResourceSearchOptions options,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(options);

        var pageSize = Math.Clamp(options.PageSize, 1, 100);
        var page = Math.Max(1, options.Page);
        var offset = (page - 1) * options.PageSize;

        var url = BaseUrl
            .AppendPathSegment("search")
            .SetQueryParam("limit", pageSize)
            .SetQueryParam("index", options.Sort.ToModrinthIndex())
            .SetQueryParam("facets", ModrinthRequestBuilder.BuildFacets(options));

        if (offset > 0)
            url = url.SetQueryParam("offset", offset);

        if (!string.IsNullOrWhiteSpace(options.Query))
            url = url.SetQueryParam("query", options.Query);

        var result = await HttpHelper.GetJsonAsync(url,
            ModrinthSearchResultContext.Default.ModrinthSearchResult,
            cancellationToken);

        if (result is null)
            return new ResourceSearchPage<ResourceHit>([], 0, page, pageSize);

        var hits = result.Hits?
            .Select(hit => hit.ToResourceHit(options.Type))
            .ToArray() ?? [];

        return new ResourceSearchPage<ResourceHit>(hits, (int)result.TotalHits, page, pageSize);
    }
}
