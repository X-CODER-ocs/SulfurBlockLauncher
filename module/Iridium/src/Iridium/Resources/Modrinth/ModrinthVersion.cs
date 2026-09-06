using System.Text.Json.Serialization;

namespace Iridium.Resources.Modrinth;

public sealed record ModrinthVersion {
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("project_id")] public string? ProjectId { get; init; }
    [JsonPropertyName("author_id")] public string? AuthorId { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("version_number")] public string? VersionNumber { get; init; }
    [JsonPropertyName("changelog")] public string? Changelog { get; init; }
    [JsonPropertyName("changelog_url")] public string? ChangelogUrl { get; init; }
    [JsonPropertyName("version_type")] public string? VersionType { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
    
    [JsonPropertyName("featured")] public bool Featured { get; init; }

    [JsonPropertyName("downloads")] public long Downloads { get; init; }
    
    [JsonPropertyName("date_published")] public DateTime? DatePublished { get; init; }
    
    [JsonPropertyName("loaders")] public IReadOnlyList<string> Loaders { get; init; } = [];
    [JsonPropertyName("files")] public IReadOnlyList<ModrinthFile> Files { get; init; } = [];
    [JsonPropertyName("game_versions")] public IReadOnlyList<string> GameVersions { get; init; } = [];
    [JsonPropertyName("dependencies")] public IReadOnlyList<ModrinthDependency> Dependencies { get; init; } = [];
}

[JsonSerializable(typeof(ModrinthVersion))]
[JsonSerializable(typeof(IReadOnlyList<string>))]
[JsonSerializable(typeof(IReadOnlyList<ModrinthVersion>))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, ModrinthVersion?>))]
public sealed partial class ModrinthVersionContext : JsonSerializerContext;
