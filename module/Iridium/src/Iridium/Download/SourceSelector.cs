using System.Collections.Concurrent;
using System.Diagnostics;
using Flurl.Http;
using Iridium.Enums;
using Iridium.Resources;

namespace Iridium.Download;

public static class SourceSelector {
    private static readonly ConcurrentDictionary<string, CachedProbe> ProbeCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ProbeLocks = new(StringComparer.OrdinalIgnoreCase);

    private static SourceSelectionMode _mode = SourceSelectionMode.Auto;
    private static TimeSpan _probeTimeout = TimeSpan.FromSeconds(3);
    private static TimeSpan _probeTtl = TimeSpan.FromMinutes(5);
    private static int _maxAttempts = 4;

    public static SourceSelectionMode Mode => _mode;

    public static TimeSpan ProbeTimeout => _probeTimeout;

    public static TimeSpan ProbeTtl => _probeTtl;

    public static int MaxAttempts => _maxAttempts;

    public static DownloadSource GameFileMirrorSource { get; set; } = DownloadSource.BmclApi;

    public static IResourceMirror? ResourceMirror { get; set; }

    private static SourceSelectionMode _modrinthResourceMode = SourceSelectionMode.Auto;
    private static SourceSelectionMode _curseForgeResourceMode = SourceSelectionMode.Auto;

    public static void Configure(
        SourceSelectionMode mode,
        TimeSpan? probeTimeout = null,
        TimeSpan? probeTtl = null,
        int? maxAttempts = null) {
        _mode = mode;
        if (probeTimeout is { } timeout)
            _probeTimeout = timeout;
        if (probeTtl is { } ttl)
            _probeTtl = ttl;
        if (maxAttempts is { } attempts)
            _maxAttempts = Math.Max(1, attempts);
    }

    public static void ConfigureResourceMirror(ResourceSource source, SourceSelectionMode mode) {
        if (source == ResourceSource.Modrinth)
            _modrinthResourceMode = mode;
        else if (source == ResourceSource.CurseForge)
            _curseForgeResourceMode = mode;
    }

    public static async Task<IReadOnlyList<string>> OrderUrlsAsync(
        string primary,
        string? mirror,
        CancellationToken cancellationToken = default,
        SourceSelectionMode? mode = null) {
        if (string.IsNullOrWhiteSpace(mirror) ||
            string.Equals(primary, mirror, StringComparison.OrdinalIgnoreCase))
            return [primary];

        return (mode ?? _mode) switch {
            SourceSelectionMode.OfficialOnly => [primary],
            SourceSelectionMode.OfficialPreferred => [primary, mirror],
            SourceSelectionMode.MirrorPreferred => [mirror, primary],
            _ => await OrderByLatencyAsync(primary, mirror, cancellationToken)
        };
    }

    public static SourceSelectionMode GetResourceMode(string url) {
        if (url.Contains("modrinth", StringComparison.OrdinalIgnoreCase))
            return _modrinthResourceMode;
        if (url.Contains("forgecdn", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("curseforge", StringComparison.OrdinalIgnoreCase))
            return _curseForgeResourceMode;
        return _mode;
    }

    private static async Task<IReadOnlyList<string>> OrderByLatencyAsync(
        string primary,
        string mirror,
        CancellationToken cancellationToken) {
        var primaryProbeTask = ProbeAsync(primary, cancellationToken);
        var mirrorProbeTask = ProbeAsync(mirror, cancellationToken);
        await Task.WhenAll(primaryProbeTask, mirrorProbeTask);
        var primaryProbe = await primaryProbeTask;
        var mirrorProbe = await mirrorProbeTask;

        if (primaryProbe.LatencyMs is null && mirrorProbe.LatencyMs is null)
            return [primary, mirror];
        if (primaryProbe.LatencyMs is null)
            return [mirror, primary];
        if (mirrorProbe.LatencyMs is null)
            return [primary, mirror];

        return primaryProbe.LatencyMs <= mirrorProbe.LatencyMs
            ? [primary, mirror]
            : [mirror, primary];
    }

    private static async Task<CachedProbe> ProbeAsync(string url, CancellationToken cancellationToken) {
        var host = GetHost(url);
        if (ProbeCache.TryGetValue(host, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
            return cached;

        var probeLock = ProbeLocks.GetOrAdd(host, static _ => new SemaphoreSlim(1, 1));
        await probeLock.WaitAsync(cancellationToken);
        try {
            if (ProbeCache.TryGetValue(host, out cached) && cached.ExpiresAt > DateTime.UtcNow)
                return cached;

            cached = await ProbeHostAsync(url, cancellationToken);
            ProbeCache[host] = cached;
            return cached;
        } finally {
            probeLock.Release();
        }
    }

    private static async Task<CachedProbe> ProbeHostAsync(string url, CancellationToken cancellationToken) {
        long? latency = null;
        try {
            var stopwatch = Stopwatch.StartNew();
            using var response = await url
                .AllowAnyHttpStatus()
                .WithTimeout(_probeTimeout)
                .HeadAsync(HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            stopwatch.Stop();

            if (response.ResponseMessage.IsSuccessStatusCode)
                latency = stopwatch.ElapsedMilliseconds;
        } catch {
        }

        return new CachedProbe(latency, DateTime.UtcNow + _probeTtl);
    }

    private static string GetHost(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url;

    private readonly record struct CachedProbe(long? LatencyMs, DateTime ExpiresAt);
}
