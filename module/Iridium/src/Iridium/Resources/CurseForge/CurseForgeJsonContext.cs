using System.Text.Json.Serialization;

namespace Iridium.Resources.CurseForge;

public record CurseForgeModsRequest {
    [JsonPropertyName("modIds")] public required long[] ModIds { get; init; }
}

public record CurseForgeFeaturedRequest {
    [JsonPropertyName("gameId")] public required int GameId { get; init; }
    [JsonPropertyName("excludedModIds")] public required int[] ExcludedModIds { get; init; }
}

public record CurseForgeFilesRequest {
    [JsonPropertyName("fileIds")] public required long[] FileIds { get; init; }
}

public record CurseForgeFingerprintRequest {
    [JsonPropertyName("fingerprints")] public required uint[] Fingerprints { get; init; }
}

[JsonSerializable(typeof(CurseForgeResponse<CurseForgeProject>))]
[JsonSerializable(typeof(CurseForgeResponse<List<CurseForgeProject>>))]
[JsonSerializable(typeof(CurseForgeResponse<CurseForgeFile>))]
[JsonSerializable(typeof(CurseForgeResponse<List<CurseForgeFile>>))]
[JsonSerializable(typeof(CurseForgeResponse<List<CurseForgeCategory>>))]
[JsonSerializable(typeof(CurseForgeResponse<List<CurseForgeGameVersion>>))]
[JsonSerializable(typeof(CurseForgeResponse<CurseForgeFeaturedResult>))]
[JsonSerializable(typeof(CurseForgeResponse<string>))]
[JsonSerializable(typeof(CurseForgePagedResponse<List<CurseForgeProject>>))]
[JsonSerializable(typeof(CurseForgePagedResponse<List<CurseForgeFile>>))]
[JsonSerializable(typeof(CurseForgeFingerprintResult))]
[JsonSerializable(typeof(CurseForgeModsRequest))]
[JsonSerializable(typeof(CurseForgeFeaturedRequest))]
[JsonSerializable(typeof(CurseForgeFilesRequest))]
[JsonSerializable(typeof(CurseForgeFingerprintRequest))]
public sealed partial class CurseForgeJsonContext : JsonSerializerContext;
