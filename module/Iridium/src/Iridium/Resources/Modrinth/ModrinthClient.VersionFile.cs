using Iridium.Enums;
using Iridium.Extensions;
using Iridium.Utilities;
using Iridium.Models.Resources;
using Iridium.Resources.Modrinth;

using ModrinthRequestContext = Iridium.Resources.Modrinth.ModrinthRequestContext;
using ModrinthVersionContext = Iridium.Resources.Modrinth.ModrinthVersionContext;

namespace Iridium.Resources.Modrinth;

public partial class ModrinthClient {
    public async Task<ResourceFile?> GetFileByHashAsync(
        string hash,
        HashAlgorithm algorithm = HashAlgorithm.Sha1,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        var url = BaseUrl
            .AppendPathSegments("version_file", hash)
            .SetQueryParam("algorithm", ModrinthRequestBuilder.ToAlgorithm(algorithm));

        var result = await HttpHelper.GetJsonOrNullAsync(url,
            ModrinthVersionContext.Default.ModrinthVersion,
            cancellationToken);

        return result?.ToResourceFile();
    }

    public async Task<IReadOnlyDictionary<string, ResourceFile?>> GetFilesByHashesAsync(
        IEnumerable<string> hashes,
        HashAlgorithm algorithm = HashAlgorithm.Sha1,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(hashes);

        var values = hashes
            .Where(static hash => !string.IsNullOrWhiteSpace(hash))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (values.Length == 0)
            return new Dictionary<string, ResourceFile?>();

        var request = new ModrinthVersionFileListRequest {
            Hashes = values,
            Algorithm = ModrinthRequestBuilder.ToAlgorithm(algorithm)
        };

        var url = BaseUrl.AppendPathSegment("version_files");
        
        var result = await HttpHelper.PostJsonAsync(url,
            request,
            ModrinthRequestContext.Default.ModrinthVersionFileListRequest,
            ModrinthVersionContext.Default.IReadOnlyDictionaryStringModrinthVersion,
            cancellationToken);

        return result is null
            ? new Dictionary<string, ResourceFile?>()
            : result.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value?.ToResourceFile(),
                StringComparer.Ordinal);
    }

    public async Task<ResourceFile?> GetLatestFileByHashAsync(
        string hash,
        IEnumerable<string> loaders,
        IEnumerable<string> gameVersions,
        IEnumerable<string>? versionTypes = null,
        HashAlgorithm algorithm = HashAlgorithm.Sha1,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        ArgumentNullException.ThrowIfNull(loaders);
        ArgumentNullException.ThrowIfNull(gameVersions);

        var request = new ModrinthUpdateRequest {
            Loaders = [.. loaders],
            GameVersions = [.. gameVersions],
            VersionTypes = versionTypes?.ToArray()
        };

        var url = BaseUrl
            .AppendPathSegments("version_file", hash, "update")
            .SetQueryParam("algorithm", ModrinthRequestBuilder.ToAlgorithm(algorithm));

        var result = await HttpHelper.PostJsonOrNullAsync(url,
            request,
            ModrinthRequestContext.Default.ModrinthUpdateRequest,
            ModrinthVersionContext.Default.ModrinthVersion,
            cancellationToken);

        return result?.ToResourceFile();
    }

    public async Task<IReadOnlyDictionary<string, ResourceFile?>> GetLatestFilesByHashesAsync(
        IEnumerable<string> hashes,
        IEnumerable<string> loaders,
        IEnumerable<string> gameVersions,
        IEnumerable<string>? versionTypes = null,
        HashAlgorithm algorithm = HashAlgorithm.Sha1,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(hashes);
        ArgumentNullException.ThrowIfNull(loaders);
        ArgumentNullException.ThrowIfNull(gameVersions);

        var values = hashes
            .Where(static hash => !string.IsNullOrWhiteSpace(hash))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (values.Length == 0)
            return new Dictionary<string, ResourceFile?>();

        var request = new ModrinthVersionFileUpdateRequest {
            Hashes = values,
            Algorithm = ModrinthRequestBuilder.ToAlgorithm(algorithm),
            Loaders = [.. loaders],
            GameVersions = [.. gameVersions],
            VersionTypes = versionTypes?.ToArray()
        };

        var url = BaseUrl.AppendPathSegments("version_files", "update");
        
        var result = await HttpHelper.PostJsonAsync(url,
            request,
            ModrinthRequestContext.Default.ModrinthVersionFileUpdateRequest,
            ModrinthVersionContext.Default.IReadOnlyDictionaryStringModrinthVersion,
            cancellationToken);

        return result is null
            ? new Dictionary<string, ResourceFile?>()
            : result.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value?.ToResourceFile(),
                StringComparer.Ordinal);
    }
}
