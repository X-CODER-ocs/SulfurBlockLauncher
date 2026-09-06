using Iridium.Enums;
using Iridium.Extensions;
using Iridium.Models.Resources;
using Iridium.Resources.CurseForge;
using CurseForgeJsonContext = Iridium.Resources.CurseForge.CurseForgeJsonContext;

namespace Iridium.Resources.CurseForge;

public partial class CurseForgeClient {
    private const int MaximumConcurrentRequests = 16;
    
    public async Task<IReadOnlyList<ResourceFile>> GetProjectFilesAsync(
        string projectId,
        string? gameVersion = null,
        ResourceLoaderType loader = ResourceLoaderType.Any,
        CancellationToken cancellationToken = default) {
        var modId = ParseId(projectId);

        var firstPage = await GetPageAsync(0, cancellationToken);
        var files = new List<CurseForgeFile>(firstPage.Files);
        
        if (files.Count >= firstPage.TotalCount || files.Count < PageSize)
            return [.. files.Select(file => file.ToResourceFile())];

        var pageCount = (firstPage.TotalCount + PageSize - 1) / PageSize;
        
        foreach (var batch in Enumerable.Range(1, pageCount - 1).Chunk(MaximumConcurrentRequests)) {
            var results = await Task
                .WhenAll(batch.Select(index => GetPageAsync(index, cancellationToken)));
            
            foreach (var result in results)
                files.AddRange(result.Files);
        }

        return files.Take(firstPage.TotalCount).Select(file => file.ToResourceFile()).ToArray();
        
        async Task<(List<CurseForgeFile> Files, int TotalCount)> GetPageAsync(int index, CancellationToken token) {
            var url = BaseUrl.AppendPathSegments("mods", modId, "files")
                .SetQueryParam("index", index * PageSize)
                .SetQueryParam("pageSize", PageSize);
            
            if (!string.IsNullOrWhiteSpace(gameVersion))
                url = url.SetQueryParam("gameVersion", gameVersion);
            
            if (loader.ToCurseForgeLoaderType() is { } loaderType)
                url = url.SetQueryParam("modLoaderType", loaderType);

            var response = await GetJsonAsync(url,
                CurseForgeJsonContext.Default.CurseForgePagedResponseListCurseForgeFile, token);
            
            return (response?.Data ?? [], response?.Pagination?.TotalCount ?? 0);
        }
    }

    public async Task<ResourceFile?> GetFileAsync(long modId, long fileId, CancellationToken cancellationToken = default) {

        var url = BaseUrl.AppendPathSegments("mods", modId, "files", fileId);
        var response = await GetJsonOrNullAsync(url,
            CurseForgeJsonContext.Default.CurseForgeResponseCurseForgeFile, cancellationToken);
        return response?.Data?.ToResourceFile();
    }

    public async Task<IReadOnlyList<ResourceFile>> GetFilesByFileIdsAsync(IEnumerable<long> fileIds, CancellationToken cancellationToken = default) {
        var ids = fileIds.Distinct().ToArray();
        if (ids.Length == 0)
            return [];

        var files = new List<CurseForgeFile>();
        foreach (var batch in ids.Chunk(MaxBatchSize)) {
            var response = await PostJsonAsync(BaseUrl.AppendPathSegments("mods", "files"),
                new CurseForgeFilesRequest { FileIds = batch },
                CurseForgeJsonContext.Default.CurseForgeFilesRequest,
                CurseForgeJsonContext.Default.CurseForgeResponseListCurseForgeFile, 
                cancellationToken);
            
            if (response?.Data is { } data)
                files.AddRange(data);
        }

        return [.. files.Select(file => file.ToResourceFile())];
    }

    public async Task<string?> GetDownloadUrlAsync(long modId, long fileId, CancellationToken cancellationToken = default) {
        var url = BaseUrl.AppendPathSegments("mods", modId, "files", fileId, "download-url");
        var response = await GetJsonOrNullAsync(url,
            CurseForgeJsonContext.Default.CurseForgeResponseString, cancellationToken);
        
        return response?.Data;
    }

    public async Task<string?> GetChangelogAsync(long modId, long fileId, CancellationToken cancellationToken = default) {
        var url = BaseUrl.AppendPathSegments("mods", modId, "files", fileId, "changelog");
        var response = await GetJsonOrNullAsync(url,
            CurseForgeJsonContext.Default.CurseForgeResponseString, cancellationToken);
        
        return response?.Data;
    }
}
