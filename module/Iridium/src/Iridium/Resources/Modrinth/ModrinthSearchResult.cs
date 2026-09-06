using System.Text.Json.Serialization;

namespace Iridium.Resources.Modrinth;

public record ModrinthSearchHit {
    [JsonPropertyName("project_id")] public string? ProjectId { get; init; }
    [JsonPropertyName("project_type")] public string? ProjectType { get; init; }
    [JsonPropertyName("slug")] public string? Slug { get; init; }
    [JsonPropertyName("author")] public string? Author { get; init; }
    [JsonPropertyName("author_id")] public string? AuthorId { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("latest_version")] public string? LatestVersion { get; init; }
    [JsonPropertyName("license")] public string? License { get; init; }
    [JsonPropertyName("icon_url")] public string? IconUrl { get; init; }
    [JsonPropertyName("client_side")] public string? ClientSide { get; init; }
    [JsonPropertyName("server_side")] public string? ServerSide { get; init; }
    [JsonPropertyName("featured_gallery")] public string? FeaturedGallery { get; init; }
    [JsonPropertyName("organization")] public string? Organization { get; init; }
    [JsonPropertyName("organization_id")] public string? OrganizationId { get; init; }
    
    [JsonPropertyName("all_project_types")] public IReadOnlyList<string> AllProjectTypes { get; init; } = [];
    [JsonPropertyName("categories")] public IReadOnlyList<string> Categories { get; init; } = [];
    [JsonPropertyName("display_categories")] public IReadOnlyList<string> DisplayCategories { get; init; } = [];
    [JsonPropertyName("versions")] public IReadOnlyList<string> Versions { get; init; } = [];
    
    [JsonPropertyName("downloads")] public long Downloads { get; init; }
    [JsonPropertyName("follows")] public long Follows { get; init; }
    
    [JsonPropertyName("date_created")] public DateTime? DateCreated { get; init; }
    [JsonPropertyName("date_modified")] public DateTime? DateModified { get; init; }

    [JsonPropertyName("environment")] public IReadOnlyList<string> Environment { get; init; } = [];
    [JsonPropertyName("disclosure_types")] public IReadOnlyList<string> DisclosureTypes { get; init; } = [];

    [JsonPropertyName("gallery")] public IReadOnlyList<string> Gallery { get; init; } = [];
    
    [JsonPropertyName("color")] public int? Color { get; init; }
}

public record ModrinthSearchResult {
    [JsonPropertyName("limit")] public int Limit { get; init; }
    [JsonPropertyName("offset")] public int Offset { get; init; }
    
    [JsonPropertyName("total_hits")] public long TotalHits { get; init; }

    [JsonPropertyName("hits")] public IReadOnlyList<ModrinthSearchHit>? Hits { get; init; }
}

[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(List<List<string>>))]
[JsonSerializable(typeof(ModrinthSearchResult))]
public sealed partial class ModrinthSearchResultContext : JsonSerializerContext;
