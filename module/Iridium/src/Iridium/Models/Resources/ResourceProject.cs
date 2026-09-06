using Iridium.Enums;

namespace Iridium.Models.Resources;


public sealed record ResourceProject {
    public required ResourceSource Source { get; init; }
    
    public required string Id { get; init; }

    public long Downloads { get; init; }
    public long Follows { get; init; }
    
    public string? LicenseId { get; init; }
    public string? WebsiteUrl { get; init; }
    public string? Slug { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Body { get; init; }
    public string? IconUrl { get; init; }
    public string? Author { get; init; }
    
    public ResourceType Type { get; init; }

    public DateTime? DateCreated { get; init; }
    public DateTime? DateModified { get; init; }

    public IReadOnlyList<string> GameVersions { get; init; } = [];
    public IReadOnlyList<ResourceScreenshotInfo> Screenshots { get; init; } = [];

    public IReadOnlyList<ResourceCategory> Categories { get; init; } = [];
    public IReadOnlyList<ResourceLoaderType> Loaders { get; init; } = [];
}

public sealed record ResourceScreenshotInfo(string Url, string? Title, string? FullUrl = null);
