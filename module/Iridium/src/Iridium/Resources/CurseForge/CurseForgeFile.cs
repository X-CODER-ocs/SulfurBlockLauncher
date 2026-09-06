using System.Text.Json.Serialization;

namespace Iridium.Resources.CurseForge;

public record CurseForgeFile {
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("modId")] public long? ModId { get; init; }
    [JsonPropertyName("alternateFileId")] public long? AlternateFileId { get; init; }
    [JsonPropertyName("fileLength")] public long? FileLength { get; init; }
    [JsonPropertyName("downloadCount")] public long? DownloadCount { get; init; }
    
    [JsonPropertyName("gameId")] public int? GameId { get; init; }
    [JsonPropertyName("releaseType")] public int? ReleaseType { get; init; }
    [JsonPropertyName("fileStatus")] public int? FileStatus { get; init; }
    
    [JsonPropertyName("fileFingerprint")] public uint? FileFingerprint { get; init; }
    
    [JsonPropertyName("isAvailable")] public bool? IsAvailable { get; init; }
    [JsonPropertyName("isServerPack")] public bool? IsServerPack { get; init; }

    [JsonPropertyName("fileName")] public string? FileName { get; init; }
    [JsonPropertyName("displayName")] public string? DisplayName { get; init; }
    [JsonPropertyName("downloadUrl")] public string? DownloadUrl { get; init; }
    
    [JsonPropertyName("fileDate")] public DateTime? FileDate { get; init; }

    [JsonPropertyName("gameVersions")] public IReadOnlyList<string> GameVersions { get; init; } = [];
    [JsonPropertyName("hashes")] public IReadOnlyList<CurseForgeFileHash> Hashes { get; init; } = [];
    [JsonPropertyName("modules")] public IReadOnlyList<CurseForgeFileModule> Modules { get; init; } = [];
    [JsonPropertyName("dependencies")] public IReadOnlyList<CurseForgeDependency> Dependencies { get; init; } = [];
    [JsonPropertyName("sortableGameVersions")] public IReadOnlyList<CurseForgeSortableGameVersion> SortableGameVersions { get; init; } = [];
}

public record CurseForgeFileHash {
    [JsonPropertyName("value")] public string? Value { get; init; }
    [JsonPropertyName("algo")] public int? Algo { get; init; }
}

public record CurseForgeDependency {
    [JsonPropertyName("modId")] public long? ModId { get; init; }
    [JsonPropertyName("relationType")] public int? RelationType { get; init; }
}

public record CurseForgeFileModule {
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("fingerprint")] public uint? Fingerprint { get; init; }
}

public record CurseForgeSortableGameVersion {
    [JsonPropertyName("gameVersionTypeId")] public int? GameVersionTypeId { get; init; }
    
    [JsonPropertyName("gameVersion")] public string? GameVersion { get; init; }
    [JsonPropertyName("gameVersionName")] public string? GameVersionName { get; init; }
}
