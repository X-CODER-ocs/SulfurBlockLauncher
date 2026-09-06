using System.Text.Json.Serialization;

namespace Iridium.Resources.CurseForge;

public record CurseForgeProject {
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("mainFileId")] public long? MainFileId { get; init; }
    [JsonPropertyName("downloadCount")] public long? DownloadCount { get; init; }
    
    [JsonPropertyName("gameId")] public int? GameId { get; init; }
    [JsonPropertyName("status")] public int? Status { get; init; }
    [JsonPropertyName("classId")] public int? ClassId { get; init; }
    
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("slug")] public string? Slug { get; init; }
    [JsonPropertyName("summary")] public string? Summary { get; init; }
    [JsonPropertyName("primaryLanguage")] public string? PrimaryLanguage { get; init; }
    
    [JsonPropertyName("isFeatured")] public bool? IsFeatured { get; init; }
    [JsonPropertyName("allowModDistribution")] public bool? AllowModDistribution { get; init; }

    [JsonPropertyName("dateCreated")] public DateTime? DateCreated { get; init; }
    [JsonPropertyName("dateModified")] public DateTime? DateModified { get; init; }
    [JsonPropertyName("dateReleased")] public DateTime? DateReleased { get; init; }
    
    [JsonPropertyName("logo")] public CurseForgeAsset? Logo { get; init; }

    [JsonPropertyName("authors")] public IReadOnlyList<CurseForgeAuthor> Authors { get; init; } = [];
    [JsonPropertyName("latestFiles")] public IReadOnlyList<CurseForgeFile> LatestFiles { get; init; } = [];
    [JsonPropertyName("screenshots")] public IReadOnlyList<CurseForgeAsset> Screenshots { get; init; } = [];
    [JsonPropertyName("categories")] public IReadOnlyList<CurseForgeCategory> Categories { get; init; } = [];
    [JsonPropertyName("latestFilesIndexes")] public IReadOnlyList<CurseForgeFileIndex> LatestFilesIndexes { get; init; } = [];
    [JsonPropertyName("gameVersionLatestFiles")] public IReadOnlyList<CurseForgeFileIndex> GameVersionLatestFiles { get; init; } = [];
    
    [JsonPropertyName("links")] public CurseForgeLinks? Links { get; init; }
}

public record CurseForgeLinks {
    [JsonPropertyName("wikiUrl")] public string? WikiUrl { get; init; }
    [JsonPropertyName("issuesUrl")] public string? IssuesUrl { get; init; }
    [JsonPropertyName("sourceUrl")] public string? SourceUrl { get; init; }
    [JsonPropertyName("websiteUrl")] public string? WebsiteUrl { get; init; }
}

public record CurseForgeAuthor {
    [JsonPropertyName("id")] public long Id { get; init; }
    
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
}

public record CurseForgeAsset {
    [JsonPropertyName("id")] public long Id { get; init; }
    
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("thumbnailUrl")] public string? ThumbnailUrl { get; init; }
}

public record CurseForgeFileIndex {
    [JsonPropertyName("fileId")] public long? FileId { get; init; }

    [JsonPropertyName("gameVersion")] public string? GameVersion { get; init; }
    [JsonPropertyName("filename")] public string? FileName { get; init; }

    [JsonPropertyName("modLoader")] public int? ModLoader { get; init; }
    [JsonPropertyName("releaseType")] public int? ReleaseType { get; init; }
    [JsonPropertyName("gameVersionTypeId")] public int? GameVersionTypeId { get; init; }
}
