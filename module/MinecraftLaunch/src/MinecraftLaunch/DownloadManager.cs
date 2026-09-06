using MinecraftLaunch.Base.Enums;
using MinecraftLaunch.Base.Interfaces;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace MinecraftLaunch;

public static class DownloadManager {
    private static readonly ConcurrentDictionary<string, SourceHealth> SourceHealthMap = new();
    private static readonly ConcurrentDictionary<string, DownloadResourceType> RouteTypes = new();
    private static readonly ConcurrentDictionary<string, string> AlternateRoutes = new();
    private static readonly ConcurrentDictionary<string, byte> MirrorRoutes = new();
    private static long _automaticSequence;

    public static string CurseforgeApiKey { get; set; } = string.Empty;
    public static int MaxThread { get; set; } = 16;
    public static int MaxFragment { get; set; } = 16;
    public static int MaxRetryCount { get; set; } = 8;
    public static bool IsEnableFragment { get; set; } = true;

    public static DownloadSourceMode MinecraftMetadataSource { get; set; } = DownloadSourceMode.Auto;
    public static DownloadSourceMode MinecraftFileSource { get; set; } = DownloadSourceMode.Auto;

    public static readonly IDownloadMirror BmclApi = new BmclApiSource();

    public static IReadOnlyList<string> ResolveUrls(string sourceUrl) {
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var source) || source.Scheme != Uri.UriSchemeHttps)
            return [sourceUrl];

        var type = Classify(source);
        if (AlternateRoutes.TryGetValue(sourceUrl, out var alternate) && RouteTypes.TryGetValue(sourceUrl, out type)) {
            var knownMode = GetMode(type);
            if (knownMode == DownloadSourceMode.OfficialOnly)
                return MirrorRoutes.ContainsKey(sourceUrl) ? [alternate] : [sourceUrl];
            return [sourceUrl, alternate];
        }
        RouteTypes[sourceUrl] = type;
        var mode = GetMode(type);
        var mirror = CreateMirrorUrl(source, type);
        if (mirror is null || mode == DownloadSourceMode.OfficialOnly)
            return [sourceUrl];
        RouteTypes[mirror] = type;
        AlternateRoutes[sourceUrl] = mirror;
        AlternateRoutes[mirror] = sourceUrl;
        MirrorRoutes[mirror] = 0;

        if (mode == DownloadSourceMode.OfficialPreferred)
            return [sourceUrl, mirror];
        if (mode == DownloadSourceMode.MirrorPreferred)
            return [mirror, sourceUrl];

        var officialHealth = GetHealth(sourceUrl);
        var mirrorHealth = GetHealth(mirror);
        if (officialHealth.HasSamples || mirrorHealth.HasSamples) {
            if (officialHealth.Failures != mirrorHealth.Failures)
                return officialHealth.Failures < mirrorHealth.Failures ? [sourceUrl, mirror] : [mirror, sourceUrl];
            if (officialHealth.AverageDuration != mirrorHealth.AverageDuration)
                return officialHealth.AverageDuration <= mirrorHealth.AverageDuration ? [sourceUrl, mirror] : [mirror, sourceUrl];
        }

        // Concurrent installations sample both routes immediately; later requests use the faster route.
        return Interlocked.Increment(ref _automaticSequence) % 2 == 0 ? [sourceUrl, mirror] : [mirror, sourceUrl];
    }

    internal static void RecordSuccess(string url, TimeSpan duration) {
        var health = SourceHealthMap.GetOrAdd(GetHealthKey(url), static _ => new SourceHealth());
        lock (health) {
            health.Successes++;
            health.Failures = 0;
            health.AverageDuration = health.Successes == 1
                ? duration
                : TimeSpan.FromTicks((long)(health.AverageDuration.Ticks * 0.75 + duration.Ticks * 0.25));
        }
    }

    internal static void RecordFailure(string url) {
        var health = SourceHealthMap.GetOrAdd(GetHealthKey(url), static _ => new SourceHealth());
        lock (health) health.Failures++;
    }

    internal static void RecordTransferSuccess(string url, long bytes, TimeSpan duration) {
        if (bytes > 0)
            RecordSuccess(url, TimeSpan.FromTicks(Math.Max(1, duration.Ticks / bytes)));
    }

    internal static bool IsFileTransfer(string url) {
        var type = GetResourceType(url);
        if (type == DownloadResourceType.MinecraftFile) return true;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        return type switch {
            DownloadResourceType.Modrinth => uri.Host.Equals("cdn.modrinth.com", StringComparison.OrdinalIgnoreCase),
            DownloadResourceType.CurseForge => !uri.Host.Equals("api.curseforge.com", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static SourceHealth GetHealth(string url) =>
        SourceHealthMap.TryGetValue(GetHealthKey(url), out var health) ? health : SourceHealth.Empty;

    private static DownloadResourceType GetResourceType(string url) => RouteTypes.TryGetValue(url, out var type)
        ? type
        : Uri.TryCreate(url, UriKind.Absolute, out var uri) ? Classify(uri) : DownloadResourceType.Other;

    private static string GetHealthKey(string url) {
        var authority = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Authority.ToLowerInvariant() : url;
        return $"{GetResourceType(url)}:{authority}";
    }

    private static DownloadSourceMode GetMode(DownloadResourceType type) => type switch {
        DownloadResourceType.MinecraftMetadata => MinecraftMetadataSource,
        DownloadResourceType.MinecraftFile => MinecraftFileSource,
        _ => DownloadSourceMode.OfficialOnly
    };

    private static DownloadResourceType Classify(Uri uri) {
        var host = uri.Host.ToLowerInvariant();
        if (host is "api.modrinth.com" or "cdn.modrinth.com") return DownloadResourceType.Modrinth;
        if (host is "api.curseforge.com" or "edge.forgecdn.net" or "media.forgecdn.net" or
            "mediafiles.forgecdn.net" or "mediafilez.forgecdn.net") return DownloadResourceType.CurseForge;
        if (host == "piston-meta.mojang.com")
            return uri.AbsolutePath.Contains("java-runtime", StringComparison.OrdinalIgnoreCase)
                ? DownloadResourceType.MinecraftFile : DownloadResourceType.MinecraftMetadata;
        if (host is "launchermeta.mojang.com" or "launcher.mojang.com")
            return DownloadResourceType.MinecraftMetadata;
        if (host == "piston-data.mojang.com" || host == "resources.download.minecraft.net" ||
            host == "libraries.minecraft.net" || host == "maven.minecraftforge.net" ||
            host == "files.minecraftforge.net" || host == "maven.fabricmc.net" ||
            host == "meta.fabricmc.net" || host == "maven.neoforged.net")
            return DownloadResourceType.MinecraftFile;
        return DownloadResourceType.Other;
    }

    private static string CreateMirrorUrl(Uri source, DownloadResourceType type) {
        var host = source.Host.ToLowerInvariant();
        string baseUrl = null;
        string path = source.AbsolutePath;
        if (type is DownloadResourceType.MinecraftMetadata or DownloadResourceType.MinecraftFile) {
            baseUrl = host switch {
                "resources.download.minecraft.net" => "https://bmclapi2.bangbang93.com/assets",
                "libraries.minecraft.net" or "maven.minecraftforge.net" or "maven.fabricmc.net" => "https://bmclapi2.bangbang93.com/maven",
                "files.minecraftforge.net" => "https://bmclapi2.bangbang93.com/maven",
                "maven.neoforged.net" => "https://bmclapi2.bangbang93.com/maven",
                "meta.fabricmc.net" => "https://bmclapi2.bangbang93.com/fabric-meta",
                _ => "https://bmclapi2.bangbang93.com"
            };
            if (host == "files.minecraftforge.net" && path.StartsWith("/maven/", StringComparison.OrdinalIgnoreCase))
                path = path[6..];
            if (host == "maven.neoforged.net" && path.StartsWith("/releases/", StringComparison.OrdinalIgnoreCase))
                path = path[9..];
        }
        return baseUrl is null ? null : baseUrl.TrimEnd('/') + "/" + path.TrimStart('/') + source.Query;
    }

    private sealed class SourceHealth {
        public static readonly SourceHealth Empty = new();
        public int Successes;
        public int Failures;
        public TimeSpan AverageDuration = TimeSpan.MaxValue;
        public bool HasSamples => Successes > 0 || Failures > 0;
    }
}

public sealed class BmclApiSource : IDownloadMirror {
    public string TryFindUrl(string sourceUrl) => sourceUrl;
}
