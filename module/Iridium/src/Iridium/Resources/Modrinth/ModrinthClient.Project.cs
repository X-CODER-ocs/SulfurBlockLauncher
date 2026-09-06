using System.Text.Json;
using Iridium.Extensions;
using Iridium.Utilities;
using Iridium.Models.Resources;
using ModrinthProjectContext = Iridium.Resources.Modrinth.ModrinthProjectContext;
using ModrinthSearchResultContext = Iridium.Resources.Modrinth.ModrinthSearchResultContext;

namespace Iridium.Resources.Modrinth;

public partial class ModrinthClient {
    public async Task<ResourceProject?> GetProjectAsync(string id, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var result = await HttpHelper.GetJsonOrNullAsync(
            BaseUrl.AppendPathSegments("project", id),
            ModrinthProjectContext.Default.ModrinthProject,
            cancellationToken);

        return result?.ToResourceProject();
    }

    public async Task<IReadOnlyList<ResourceProject>> GetProjectsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(ids);

        var values = ids
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (values.Length == 0)
            return [];

        var url = BaseUrl
            .AppendPathSegment("projects")
            .SetQueryParam("ids", JsonSerializer.Serialize(
                values, ModrinthSearchResultContext.Default.StringArray));

        var result = await HttpHelper.GetJsonAsync(url,
            ModrinthProjectContext.Default.IReadOnlyListModrinthProject,
            cancellationToken);

        return result?
            .Select(project => project.ToResourceProject())
            .ToArray() ?? [];
    }

    public async Task<IReadOnlyList<ResourceProject>> GetRandomProjectsAsync(int count, CancellationToken cancellationToken = default) {
        var url = BaseUrl
            .AppendPathSegment("projects_random")
            .SetQueryParam("count", Math.Clamp(count, 1, 100));

        var result = await HttpHelper.GetJsonAsync(url,
            ModrinthProjectContext.Default.IReadOnlyListModrinthProject,
            cancellationToken);

        return result?
            .Select(project => project.ToResourceProject())
            .ToArray() ?? [];
    }
}
