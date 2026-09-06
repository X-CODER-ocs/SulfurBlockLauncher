using System.Text.Json.Serialization;

namespace Iridium.Resources.Modrinth;

public record ModrinthCategory {
    [JsonPropertyName("icon")] public string? Icon { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("header")] public string? Header { get; init; }
    [JsonPropertyName("project_type")] public string? ProjectType { get; init; }
}

public record ModrinthLoader {
    [JsonPropertyName("icon")] public string? Icon { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    
    [JsonPropertyName("supported_project_types")] public IReadOnlyList<string> SupportedProjectTypes { get; init; } = [];
}


public record ModrinthGameVersion {
    [JsonPropertyName("version")] public string? Version { get; init; }
    [JsonPropertyName("version_type")] public string? VersionType { get; init; }
    
    [JsonPropertyName("major")] public bool Major { get; init; }
    
    [JsonPropertyName("date")] public DateTime? Date { get; init; }
}

[JsonSerializable(typeof(IReadOnlyList<ModrinthLoader>))]
[JsonSerializable(typeof(IReadOnlyList<ModrinthCategory>))]
[JsonSerializable(typeof(IReadOnlyList<ModrinthGameVersion>))]
public sealed partial class ModrinthTagContext : JsonSerializerContext;
