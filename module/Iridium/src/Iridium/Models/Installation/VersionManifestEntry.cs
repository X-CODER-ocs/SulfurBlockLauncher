using System.Text.Json.Serialization;

namespace Iridium.Models.Installation;

/// <summary>
/// The per-call install input a Minecraft version installer needs: an identity and the URL of
/// the version manifest. Abstracting it behind an interface lets integrations hand any
/// implementation to <c>InstallAsync</c>.
/// </summary>
public interface IVersionManifestEntry {
    string Id { get; }
    string Url { get; }
}

public sealed record VersionManifestEntry : IVersionManifestEntry {
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("time")] public DateTime Time { get; set; }
    [JsonPropertyName("releaseTime")] public DateTime ReleaseTime { get; set; }
}

[JsonSerializable(typeof(VersionManifestEntry))]
[JsonSerializable(typeof(IEnumerable<VersionManifestEntry>))]
public sealed partial class VersionManifestEntryContext : JsonSerializerContext;