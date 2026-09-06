using System.Text.Json.Serialization;

namespace Iridium.Resources.Modrinth;

public record ModrinthLicense {
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
    
    [JsonPropertyName("modified")] public DateTime? Modified { get; init; }
}

public record ModrinthGalleryItem {
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("raw_url")] public string? RawUrl { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }

    [JsonPropertyName("featured")] public bool Featured { get; init; }
    
    [JsonPropertyName("created")] public DateTime? Created { get; init; }
}

public record ModrinthDonationUrl {
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("platform")] public string? Platform { get; init; }
}
