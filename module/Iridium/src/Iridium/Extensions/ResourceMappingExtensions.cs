using Iridium.Enums;
using Iridium.Resources;
using Iridium.Models.Resources;
using Iridium.Resources.CurseForge;
using Iridium.Resources.Modrinth;

namespace Iridium.Extensions;

public static class ResourceMappingExtensions {
    public static ResourceHit ToResourceHit(this ModrinthSearchHit hit, ResourceType type) {
        var slug = hit.Slug;
        return new ResourceHit {
            Source = ResourceSource.Modrinth,
            Id = hit.ProjectId ?? slug ?? string.Empty,
            Slug = slug,
            Title = hit.Title,
            Summary = hit.Description,
            IconUrl = hit.IconUrl,
            Author = hit.Author,
            Type = type,
            Downloads = hit.Downloads,
            Follows = hit.Follows,
            DateCreated = hit.DateCreated,
            DateModified = hit.DateModified,
            Categories = [
                .. hit.Categories.Select(category => new ResourceCategory {
                    Type = type, Name = category, ModrinthSlug = category
                })
            ],
            GameVersions = hit.Versions,
            Loaders = hit.Categories.Select(ToResourceLoaderType)
                .Where(loader => loader.HasValue).Select(loader => loader!.Value).Distinct().ToArray(),
            Screenshots = hit.Gallery,
            WebsiteUrl = slug is null ? null : ResourceUrlHelper.BuildModrinthWebsiteUrl(type.ToModrinthProjectType(), slug)
        };
    }

    public static ResourceProject ToResourceProject(this ModrinthProject project) {
        var type = ParseModrinthProjectType(project.ProjectType);
        return new ResourceProject {
            Source = ResourceSource.Modrinth,
            Id = project.Id ?? string.Empty,
            Slug = project.Slug,
            Title = project.Title,
            Description = project.Description,
            Body = project.Body,
            IconUrl = project.IconUrl,
            Type = type,
            Downloads = project.Downloads,
            Follows = project.Followers,
            DateCreated = project.Published,
            DateModified = project.Updated,
            Categories = project.Categories.Select(category => new ResourceCategory {
                Type = type, Name = category, ModrinthSlug = category
            }).ToArray(),
            GameVersions = project.GameVersions,
            Loaders = project.Loaders.Select(ToResourceLoaderType)
                .Where(loader => loader.HasValue).Select(loader => loader!.Value).Distinct().ToArray(),
            Screenshots = project.Gallery
                .Where(gallery => gallery.Url is not null)
                .Select(gallery => new ResourceScreenshotInfo(gallery.Url!, gallery.Title, gallery.RawUrl)).ToArray(),
            LicenseId = project.License?.Id,
            WebsiteUrl = project.Slug is null ? null : ResourceUrlHelper.BuildModrinthWebsiteUrl(type.ToModrinthProjectType(), project.Slug)
        };
    }

    public static ResourceFile ToResourceFile(this ModrinthVersion version) {
        var primary = version.Files.FirstOrDefault(file => file.Primary) ?? version.Files.FirstOrDefault();
        return new ResourceFile {
            Source = ResourceSource.Modrinth,
            Id = version.Id ?? string.Empty,
            ProjectId = version.ProjectId ?? string.Empty,
            Name = version.Name,
            VersionNumber = version.VersionNumber,
            Changelog = version.Changelog,
            ReleaseType = ParseModrinthReleaseType(version.VersionType),
            Published = version.DatePublished,
            Downloads = version.Downloads,
            GameVersions = version.GameVersions,
            Loaders = version.Loaders.Select(ToResourceLoaderType)
                .Where(loader => loader.HasValue).Select(loader => loader!.Value).Distinct().ToArray(),
            PrimaryFile = primary?.ToResourceFileEntry(),
            Files = version.Files.Select(file => file.ToResourceFileEntry()).ToArray(),
            Dependencies = version.Dependencies.Select(dependency => dependency.ToResourceDependency()).ToArray()
        };
    }

    public static ResourceFileEntry ToResourceFileEntry(this ModrinthFile file) => new() {
        FileName = file.FileName,
        Url = file.Url,
        Size = file.Size,
        Sha1 = file.Hashes?.Sha1,
        Sha512 = file.Hashes?.Sha512,
        IsPrimary = file.Primary
    };

    public static ResourceDependency ToResourceDependency(this ModrinthDependency dependency) => new() {
        ProjectId = dependency.ProjectId,
        VersionId = dependency.VersionId,
        FileName = dependency.FileName,
        Type = dependency.DependencyType?.ToLowerInvariant() switch {
            "required" => DependencyType.Required,
            "optional" => DependencyType.Optional,
            "embedded" => DependencyType.Embedded,
            "incompatible" => DependencyType.Incompatible,
            _ => DependencyType.Unknown
        }
    };

    public static ResourceCategory ToResourceCategory(this ModrinthCategory category, ResourceType type) => new() {
        Type = type,
        Name = category.Name ?? string.Empty,
        DisplayName = category.Name,
        ModrinthSlug = category.Name
    };

    public static ResourceHit ToResourceHit(this CurseForgeProject project, ResourceType type) {
        return new ResourceHit {
            Source = ResourceSource.CurseForge,
            Id = project.Id.ToString(),
            Slug = project.Slug,
            Title = project.Name,
            Summary = project.Summary,
            IconUrl = project.Logo?.ThumbnailUrl ?? project.Logo?.Url,
            Author = project.Authors.FirstOrDefault()?.Name,
            Type = type,
            Downloads = project.DownloadCount ?? 0,
            DateCreated = project.DateCreated,
            DateModified = project.DateModified,
            Categories = project.Categories.Select(category => category.ToResourceCategory(type)).ToArray(),
            GameVersions = project.LatestFilesIndexes.Select(index => index.GameVersion)
                .Where(version => version is not null).Cast<string>().Distinct().ToArray(),
            Loaders = project.LatestFilesIndexes.Select(index => index.ModLoader)
                .Where(loader => loader.HasValue)
                .Select(loader => ParseCurseForgeLoader(loader!.Value))
                .Where(loader => loader.HasValue).Select(loader => loader!.Value).Distinct().ToArray(),
            Screenshots = project.Screenshots.Select(screenshot => screenshot.Url ?? screenshot.ThumbnailUrl)
                .Where(url => url is not null).Cast<string>().ToArray(),
            WebsiteUrl = project.Links?.WebsiteUrl
        };
    }

    public static ResourceProject ToResourceProject(this CurseForgeProject project) {
        var type = ParseCurseForgeProjectType(project.ClassId ?? 0);
        return new ResourceProject {
            Source = ResourceSource.CurseForge,
            Id = project.Id.ToString(),
            Slug = project.Slug,
            Title = project.Name,
            Description = project.Summary,
            IconUrl = project.Logo?.ThumbnailUrl ?? project.Logo?.Url,
            Author = project.Authors.FirstOrDefault()?.Name,
            Type = type,
            Downloads = project.DownloadCount ?? 0,
            DateCreated = project.DateCreated,
            DateModified = project.DateModified,
            Categories = project.Categories.Select(category => category.ToResourceCategory(type)).ToArray(),
            GameVersions = project.LatestFilesIndexes.Select(index => index.GameVersion)
                .Where(version => version is not null).Cast<string>().Distinct().ToArray(),
            Loaders = project.LatestFilesIndexes.Select(index => index.ModLoader)
                .Where(loader => loader.HasValue)
                .Select(loader => ParseCurseForgeLoader(loader!.Value))
                .Where(loader => loader.HasValue).Select(loader => loader!.Value).Distinct().ToArray(),
            Screenshots = project.Screenshots
                .Select(screenshot => (screenshot.Url ?? screenshot.ThumbnailUrl, Title: null as string))
                .Where(item => !string.IsNullOrWhiteSpace(item.Item1))
                .Select(item => new ResourceScreenshotInfo(item.Item1!, item.Title)).ToArray(),
            WebsiteUrl = project.Links?.WebsiteUrl
        };
    }

    public static ResourceFile ToResourceFile(this CurseForgeFile file) {
        var sha1 = file.Hashes.FirstOrDefault(hash => hash.Algo == 1)?.Value;
        var md5 = file.Hashes.FirstOrDefault(hash => hash.Algo == 2)?.Value;
        var url = file.DownloadUrl;
        if (string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(file.FileName))
            url = ResourceUrlHelper.BuildCurseForgeCdnUrl(file.Id, file.FileName);

        var entry = new ResourceFileEntry {
            FileName = file.FileName,
            Url = url,
            Size = file.FileLength ?? 0,
            Sha1 = sha1,
            Md5 = md5,
            IsPrimary = true
        };

        return new ResourceFile {
            Source = ResourceSource.CurseForge,
            Id = file.Id.ToString(),
            ProjectId = file.ModId?.ToString() ?? string.Empty,
            Name = file.DisplayName,
            VersionNumber = file.DisplayName,
            ReleaseType = ParseCurseForgeReleaseType(file.ReleaseType),
            Published = file.FileDate,
            Downloads = file.DownloadCount ?? 0,
            GameVersions = file.GameVersions,
            Loaders = file.GameVersions.Select(ToResourceLoaderType)
                .Where(loader => loader.HasValue).Select(loader => loader!.Value).Distinct().ToArray(),
            PrimaryFile = entry,
            Files = [entry],
            Dependencies = file.Dependencies.Select(dependency => dependency.ToResourceDependency()).ToArray()
        };
    }

    public static ResourceDependency ToResourceDependency(this CurseForgeDependency dependency) => new() {
        ProjectId = dependency.ModId?.ToString() ?? string.Empty,
        Type = dependency.RelationType switch {
            1 => DependencyType.Embedded,
            2 => DependencyType.Optional,
            3 => DependencyType.Required,
            4 => DependencyType.Tool,
            5 => DependencyType.Incompatible,
            6 => DependencyType.Include,
            _ => DependencyType.Unknown
        }
    };

    public static ResourceCategory ToResourceCategory(this CurseForgeCategory category, ResourceType type) => new() {
        Type = type,
        Name = category.Slug ?? string.Empty,
        DisplayName = category.Name,
        CurseForgeId = category.Id
    };

    public static string ToModrinthProjectType(this ResourceType type) => type switch {
        ResourceType.Mod => "mod",
        ResourceType.Modpack => "modpack",
        ResourceType.ResourcePack => "resourcepack",
        ResourceType.Shader => "shader",
        ResourceType.DataPack => "datapack",
        ResourceType.Plugin => "plugin",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public static string? ToModrinthLoader(this ResourceLoaderType loader) => loader switch {
        ResourceLoaderType.Vanilla => "vanilla",
        ResourceLoaderType.Forge => "forge",
        ResourceLoaderType.Fabric => "fabric",
        ResourceLoaderType.Quilt => "quilt",
        ResourceLoaderType.NeoForge => "neoforge",
        ResourceLoaderType.LiteLoader => "liteloader",
        ResourceLoaderType.OptiFine => "optifine",
        ResourceLoaderType.Canvas => "canvas",
        ResourceLoaderType.Iris => "iris",
        ResourceLoaderType.LegacyFabric => "legacy-fabric",
        ResourceLoaderType.Paper => "paper",
        ResourceLoaderType.Purpur => "purpur",
        ResourceLoaderType.Spigot => "spigot",
        ResourceLoaderType.Bukkit => "bukkit",
        ResourceLoaderType.Velocity => "velocity",
        ResourceLoaderType.Waterfall => "waterfall",
        ResourceLoaderType.BungeeCord => "bungeecord",
        _ => null
    };

    public static string ToModrinthIndex(this ResourceSort sort) => sort switch {
        ResourceSort.Downloads or ResourceSort.TotalDownloads => "downloads",
        ResourceSort.Follows => "follows",
        ResourceSort.Newest or ResourceSort.ReleasedDate => "newest",
        ResourceSort.Updated or ResourceSort.LastUpdated => "updated",
        _ => "relevance"
    };

    public static int? ToCurseForgeClassId(this ResourceType type) => type switch {
        ResourceType.Mod => 6,
        ResourceType.Modpack => 4471,
        ResourceType.ResourcePack => 12,
        ResourceType.Shader => 6552,
        ResourceType.DataPack => 6945,
        ResourceType.World => 17,
        ResourceType.Plugin => 5,
        _ => null
    };

    public static int? ToCurseForgeLoaderType(this ResourceLoaderType loader) => loader switch {
        ResourceLoaderType.Forge => 1,
        ResourceLoaderType.LiteLoader => 3,
        ResourceLoaderType.Fabric => 4,
        ResourceLoaderType.Quilt => 5,
        ResourceLoaderType.NeoForge => 6,
        ResourceLoaderType.Canvas => 8,
        ResourceLoaderType.Iris => 9,
        ResourceLoaderType.OptiFine => 10,
        ResourceLoaderType.Vanilla => 11,
        _ => null
    };

    public static int ToCurseForgeSortField(this ResourceSort sort) => sort switch {
        ResourceSort.Relevance => 1,
        ResourceSort.Popularity => 2,
        ResourceSort.Updated or ResourceSort.LastUpdated => 3,
        ResourceSort.Name => 4,
        ResourceSort.Author => 5,
        ResourceSort.Downloads or ResourceSort.TotalDownloads => 6,
        ResourceSort.Newest or ResourceSort.ReleasedDate => 11,
        ResourceSort.Follows => 12,
        ResourceSort.Rating => 13,
        _ => 1
    };
    
    public static ResourceLoaderType? ToResourceLoaderType(this string? loader) =>
        loader?.Trim().ToLowerInvariant() switch {
            "vanilla" => ResourceLoaderType.Vanilla,
            "forge" => ResourceLoaderType.Forge,
            "fabric" => ResourceLoaderType.Fabric,
            "quilt" => ResourceLoaderType.Quilt,
            "neoforge" => ResourceLoaderType.NeoForge,
            "liteloader" => ResourceLoaderType.LiteLoader,
            "optifine" => ResourceLoaderType.OptiFine,
            "canvas" => ResourceLoaderType.Canvas,
            "iris" => ResourceLoaderType.Iris,
            "legacy-fabric" => ResourceLoaderType.LegacyFabric,
            "paper" => ResourceLoaderType.Paper,
            "purpur" => ResourceLoaderType.Purpur,
            "spigot" => ResourceLoaderType.Spigot,
            "bukkit" => ResourceLoaderType.Bukkit,
            "velocity" => ResourceLoaderType.Velocity,
            "waterfall" => ResourceLoaderType.Waterfall,
            "bungeecord" => ResourceLoaderType.BungeeCord,
            _ => null
        };

    private static ResourceLoaderType? ParseCurseForgeLoader(int? loader) => loader switch {
        1 => ResourceLoaderType.Forge,
        3 => ResourceLoaderType.LiteLoader,
        4 => ResourceLoaderType.Fabric,
        5 => ResourceLoaderType.Quilt,
        6 => ResourceLoaderType.NeoForge,
        8 => ResourceLoaderType.Canvas,
        9 => ResourceLoaderType.Iris,
        10 => ResourceLoaderType.OptiFine,
        11 => ResourceLoaderType.Vanilla,
        _ => null
    };

    private static ResourceType ParseModrinthProjectType(string? projectType) =>
        projectType?.ToLowerInvariant() switch {
            "modpack" => ResourceType.Modpack,
            "resourcepack" => ResourceType.ResourcePack,
            "shader" => ResourceType.Shader,
            "datapack" => ResourceType.DataPack,
            "plugin" => ResourceType.Plugin,
            _ => ResourceType.Mod
        };

    private static ResourceType ParseCurseForgeProjectType(int classId) => classId switch {
        4471 => ResourceType.Modpack,
        12 => ResourceType.ResourcePack,
        6552 => ResourceType.Shader,
        6945 => ResourceType.DataPack,
        17 => ResourceType.World,
        5 => ResourceType.Plugin,
        _ => ResourceType.Mod
    };

    private static ReleaseType ParseModrinthReleaseType(string? type) =>
        type?.ToLowerInvariant() switch {
            "beta" => ReleaseType.Beta,
            "alpha" => ReleaseType.Alpha,
            _ => ReleaseType.Release
        };

    private static ReleaseType ParseCurseForgeReleaseType(int? type) => type switch {
        2 => ReleaseType.Beta,
        3 => ReleaseType.Alpha,
        _ => ReleaseType.Release
    };
}
