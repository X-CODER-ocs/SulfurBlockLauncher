using System.Text.Json;
using Iridium.Enums;
using Iridium.Extensions;
using Iridium.Utilities;
using Iridium.Models.Resources;
using ModrinthSearchResultContext = Iridium.Resources.Modrinth.ModrinthSearchResultContext;
using ModrinthVersionContext = Iridium.Resources.Modrinth.ModrinthVersionContext;

namespace Iridium.Resources.Modrinth;

public partial class ModrinthClient {
    public Task<IReadOnlyList<ResourceFile>> GetProjectFilesAsync(
        string projectId, 
        string? gameVersion = null,
        ResourceLoaderType loader = ResourceLoaderType.Any,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        var loaders = loader.ToModrinthLoader() is { } loaderSlug
            ? new[] { loaderSlug }
            : null;

        var gameVersions = gameVersion is not null
            ? new[] { gameVersion }
            : null;

        return GetProjectVersionsAsync(projectId, loaders, gameVersions, null, cancellationToken);
    }

    public async Task<IReadOnlyList<ResourceFile>> GetProjectVersionsAsync(
        string projectId,
        IEnumerable<string>? loaders = null,
        IEnumerable<string>? gameVersions = null,
        IEnumerable<string>? versionTypes = null,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        var url = BaseUrl.AppendPathSegments("project", projectId, "version");

        if (loaders is not null) {
            var values = loaders
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            if (values.Length > 0)
                url = url.SetQueryParam("loaders", 
                    JsonSerializer.Serialize(values, ModrinthSearchResultContext.Default.StringArray));
        }

        if (gameVersions is not null) {
            var values = gameVersions
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            if (values.Length > 0)
                url = url.SetQueryParam("game_versions", 
                    JsonSerializer.Serialize(values, ModrinthSearchResultContext.Default.StringArray));
        }

        if (versionTypes is not null) {
            var values = versionTypes
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            if (values.Length > 0)
                url = url.SetQueryParam("version_types", JsonSerializer.Serialize(
                    values, ModrinthSearchResultContext.Default.StringArray));
        }

        var result = await HttpHelper.GetJsonAsync(url,
            ModrinthVersionContext.Default.IReadOnlyListModrinthVersion,
            cancellationToken);

        return result?
            .Select(version => version.ToResourceFile())
            .ToArray() ?? [];
    }

    public async Task<ResourceFile?> GetFileAsync(string versionId, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionId);

        var result = await HttpHelper.GetJsonOrNullAsync(
            BaseUrl.AppendPathSegments("version", versionId),
            ModrinthVersionContext.Default.ModrinthVersion,
            cancellationToken);

        return result?.ToResourceFile();
    }

    public async Task<ResourceFile?> GetFileByIdOrNumberAsync(string projectId, string idOrNumber, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idOrNumber);

        var url = BaseUrl.AppendPathSegments("project", projectId, "version", idOrNumber);

        var result = await HttpHelper.GetJsonOrNullAsync(url,
            ModrinthVersionContext.Default.ModrinthVersion,
            cancellationToken);

        return result?.ToResourceFile();
    }

    public async Task<IReadOnlyList<ResourceFile>> GetVersionsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(ids);

        var values = ids
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (values.Length == 0)
            return [];

        var url = BaseUrl
            .AppendPathSegment("versions")
            .SetQueryParam("ids",
                JsonSerializer.Serialize(values, ModrinthSearchResultContext.Default.StringArray));

        var result = await HttpHelper.GetJsonAsync(url,
            ModrinthVersionContext.Default.IReadOnlyListModrinthVersion,
            cancellationToken);

        return result?
            .Select(version => version.ToResourceFile())
            .ToArray() ?? [];
    }
}
