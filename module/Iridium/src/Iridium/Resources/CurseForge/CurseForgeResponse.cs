using System.Text.Json.Serialization;
using Iridium.JsonConverters;

namespace Iridium.Resources.CurseForge;

public record CurseForgeResponse<T> {
    [JsonPropertyName("data")] public T? Data { get; init; }
}

public record CurseForgePagedResponse<T> {
    [JsonPropertyName("data")] public T? Data { get; init; }
    [JsonPropertyName("pagination")] public CurseForgePagination? Pagination { get; init; }
}

public record CurseForgePagination {
    [JsonPropertyName("index")] public int? Index { get; init; }
    [JsonPropertyName("pageSize")] public int? PageSize { get; init; }
    [JsonPropertyName("resultCount")] public int? ResultCount { get; init; }
    [JsonPropertyName("totalCount")] public int? TotalCount { get; init; }
}


public record CurseForgeFeaturedResult {
    [JsonPropertyName("popular")] public IReadOnlyList<CurseForgeProject> Popular { get; init; } = [];
    [JsonPropertyName("featured")] public IReadOnlyList<CurseForgeProject> Featured { get; init; } = [];
}


public record CurseForgeFingerprintResult {
    [JsonPropertyName("data")] public CurseForgeFingerprintData? Data { get; init; }
}

public record CurseForgeFingerprintData {
    [JsonPropertyName("isMatch")] public bool IsMatch { get; init; }
    
    [JsonPropertyName("exactMatches")] public IReadOnlyList<CurseForgeFingerprintMatch> ExactMatches { get; init; } = [];
    [JsonPropertyName("partialMatches")] public IReadOnlyList<CurseForgeFingerprintMatch> PartialMatches { get; init; } = [];
    
    [JsonPropertyName("exactFingerprints")]
    [JsonConverter(typeof(TolerantUIntListConverter))]
    public IReadOnlyList<uint> ExactFingerprints { get; init; } = [];
    
    [JsonPropertyName("partialMatchFingerprints")]
    [JsonConverter(typeof(TolerantUIntListConverter))]
    public IReadOnlyList<uint> PartialMatchFingerprints { get; init; } = [];
    
    [JsonPropertyName("installedFingerprints")]
    [JsonConverter(typeof(TolerantUIntListConverter))]
    public IReadOnlyList<uint> InstalledFingerprints { get; init; } = [];
    
    [JsonPropertyName("unmatchedFingerprints")]
    [JsonConverter(typeof(TolerantUIntListConverter))]
    public IReadOnlyList<uint> UnmatchedFingerprints { get; init; } = [];
}

public record CurseForgeFingerprintMatch {
    [JsonPropertyName("id")] public long Id { get; init; }
    
    [JsonPropertyName("file")] public CurseForgeFile? File { get; init; }
    [JsonPropertyName("project")] public CurseForgeProject? Project { get; init; }

    [JsonPropertyName("latestFiles")] public IReadOnlyList<CurseForgeFile> LatestFiles { get; init; } = [];
    
    [JsonPropertyName("unmatchedFingerprints")]
    [JsonConverter(typeof(TolerantUIntListConverter))]
    public IReadOnlyList<uint> UnmatchedFingerprints { get; init; } = [];
}
