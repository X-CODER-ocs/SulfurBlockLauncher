using System.Text.Json;
using System.Globalization;
using MinecraftLaunch.Base.Interfaces;
using System.Text.Json.Serialization;
using MinecraftLaunch.Base.Models.SHA1;

namespace MinecraftLaunch.Base.Models.Game;

public class AssstIndex : MinecraftDependency, IDownloadDependency, IVerifiableDependency {
    long? IVerifiableDependency.Size => Size;

    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("url")] public string Url { get; set; }
    [JsonPropertyName("sha1")] public Sha1Data? Sha1 { get; set; }
    [JsonPropertyName("size")] public long? Size { get; set; }

    [JsonIgnore] public override string FilePath => Path.Combine("assets", "indexes", $"{Id}.json");
}

public record MinecraftJsonEntry {
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; }
    [JsonPropertyName("assets")] public string Assets { get; set; }
    [JsonPropertyName("mainClass")] public string MainClass { get; set; }
    [JsonPropertyName("arguments")] public JsonElement Arguments { get; set; }
    /// <summary> ValueType is Array </summary>
    [JsonPropertyName("libraries")] public JsonElement Libraries { get; set; }
    [JsonPropertyName("inheritsFrom")] public string InheritsFrom { get; set; }
    [JsonPropertyName("jar")] public string Jar { get; set; }
    [JsonPropertyName("javaVersion")] public JsonElement JavaVersion { get; set; }
    [JsonPropertyName("releaseTime")]
    [JsonConverter(typeof(FlexibleDateTimeConverter))]
    public DateTime ReleaseTime { get; set; }
    [JsonPropertyName("assetIndex")] public AssstIndexJsonEntry AssetIndex { get; set; }
    [JsonPropertyName("minecraftArguments")] public string MinecraftArguments { get; set; }
}

public record AssstIndexJsonEntry {
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("size")] public int Size { get; set; }
    [JsonPropertyName("url")] public string Url { get; set; }
    [JsonPropertyName("sha1")] public Sha1Data Sha1 { get; set; }
    [JsonPropertyName("totalSize")] public int TotalSize { get; set; }
}

public record OptifineMinecraftEntry {
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("time")] public string Time { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; }
    [JsonPropertyName("mainClass")] public string MainClass { get; set; }
    [JsonPropertyName("releaseTime")] public string ReleaseTime { get; set; }
    [JsonPropertyName("inheritsFrom")] public string InheritsFrom { get; set; }
    [JsonPropertyName("libraries")] public IEnumerable<OptifineMinecraftLibrary> Libraries { get; set; }
    [JsonPropertyName("minecraftArguments")] public string MinecraftArguments { get; set; }
}

public record struct OptifineMinecraftLibrary {
    [JsonPropertyName("name")] public string Name { get; set; }
}

/// <summary>
/// Some third-party launchers write non-RFC 3339 release times into version JSON.
/// Release time is display metadata, so an invalid value must not prevent instance discovery.
/// </summary>
public sealed class FlexibleDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String &&
            DateTime.TryParse(reader.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value))
            return value;

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var unixMilliseconds))
            return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds).UtcDateTime;

        return DateTime.MinValue;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}

[JsonSerializable(typeof(MinecraftJsonEntry))]
[JsonSerializable(typeof(OptifineMinecraftEntry))]
public sealed partial class MinecraftJsonEntryContext : JsonSerializerContext;

[JsonSerializable(typeof(JsonDocument))]
[JsonSerializable(typeof(JsonElement))]
public sealed partial class JsonDocumentSerializeContext: JsonSerializerContext;
