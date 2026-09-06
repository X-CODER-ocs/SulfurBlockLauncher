using System.Text.Json.Serialization;

namespace Iridium.Resources.Modrinth;

public record ModrinthVersionFileListRequest {
    [JsonPropertyName("hashes")] public required string[] Hashes { get; init; }
    
    [JsonPropertyName("algorithm")] public required string Algorithm { get; init; }
}

public record ModrinthVersionFileUpdateRequest {
    [JsonPropertyName("hashes")] public required string[] Hashes { get; init; }
    [JsonPropertyName("algorithm")] public required string Algorithm { get; init; }
    [JsonPropertyName("loaders")] public required string[] Loaders { get; init; }
    [JsonPropertyName("game_versions")] public required string[] GameVersions { get; init; }
    [JsonPropertyName("version_types")] public string[]? VersionTypes { get; init; }
}

public record ModrinthUpdateRequest {
    [JsonPropertyName("loaders")] public required string[] Loaders { get; init; }
    [JsonPropertyName("version_types")] public string[]? VersionTypes { get; init; }
    [JsonPropertyName("game_versions")] public required string[] GameVersions { get; init; }
}

[JsonSerializable(typeof(ModrinthUpdateRequest))]
[JsonSerializable(typeof(ModrinthVersionFileListRequest))]
[JsonSerializable(typeof(ModrinthVersionFileUpdateRequest))]
public sealed partial class ModrinthRequestContext : JsonSerializerContext;
