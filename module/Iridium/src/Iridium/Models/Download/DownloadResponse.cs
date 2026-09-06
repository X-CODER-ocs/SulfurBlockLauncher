namespace Iridium.Models.Download;

public sealed record DownloadResponse {
    public int SuccessCount { get; init; }
    public int FailCount { get; init; }
    public IReadOnlyList<Exception> Exceptions { get; init; } = [];
}

public sealed record DownloadRequest {
    public string Url { get; init; } = string.Empty;
    public string LocalPath { get; init; } = string.Empty;

    public IReadOnlyList<string>? AlternateUrls { get; init; }

    public long Size { get; init; }

    /// <summary>
    /// Optional lower-case SHA-1 of the expected file content. When provided, the downloader
    /// verifies the result and re-downloads on mismatch; an existing local file that already
    /// matches is skipped.
    /// </summary>
    public string? Sha1 { get; init; }
    
    public FileInfo FileInfo => new(LocalPath);
    
    public Action<EventArgs>? Completed { get; init; }
    public Action<ResourceDownloadProgressChangedEventArgs>? ProgressChanged { get; init; }
}