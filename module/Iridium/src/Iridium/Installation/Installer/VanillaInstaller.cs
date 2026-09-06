using System.Text.Json;
using Flurl.Http;
using Iridium.Download;
using Iridium.Models.Installation;
using Iridium.Minecraft;
using Iridium.Models.Minecraft;
using Iridium.Installation.Tasks;

namespace Iridium.Installation.Installer;

/// <summary>
/// A concrete Minecraft installer. It owns the Minecraft-specific inputs (target, download
/// source) and expresses its flow as a plain generic <see cref="InstallTask"/>; the per-call
/// version is passed through the <see cref="IVersionManifestEntry"/> interface so any
/// implementation can be installed.
/// </summary>
public sealed class VanillaInstaller : InstallerBase<IVersionManifestEntry> {
    private const string VersionManifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json";

    private readonly MinecraftTarget _target;
    private readonly DownloadSource _source;

    public static readonly InstallStepKey DownloadVersion = nameof(DownloadVersion);
    public static readonly InstallStepKey ResolveVersion = nameof(ResolveVersion);
    public static readonly InstallStepKey DownloadResources = nameof(DownloadResources);
    public static readonly InstallStepKey ReconstructAssets = nameof(ReconstructAssets);

    public VanillaInstaller(MinecraftTarget target, DownloadSource? source = null) {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _source = source ?? DownloadSource.Official;
    }

    public static async Task<IReadOnlyList<VersionManifestEntry>?> GetVersionsAsync(CancellationToken ct = default) {
            await using var stream = await VersionManifestUrl
                .GetStreamAsync(HttpCompletionOption.ResponseContentRead, ct);
    
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    
            var versions = document.RootElement
                .GetProperty("versions")
                .Deserialize<IEnumerable<VersionManifestEntry>>(
                    VersionManifestEntryContext.Default.IEnumerableVersionManifestEntry);
    
            ArgumentNullException.ThrowIfNull(versions);
            return [.. versions];
        }
    
    public override async Task<IInstallResult> InstallAsync(IVersionManifestEntry version, int maxConcurrency = 32, CancellationToken ct = default) {
        var result = await RunTaskAsync(CreateTask(version), maxConcurrency, ct).ConfigureAwait(false);

        var resolved = result.State.Get<MinecraftContext>("resolved-context");
        var entry = resolved?.Entry
            ?? result.State.Get<MinecraftEntry>("seed-entry")
            ?? new MinecraftEntry { InstancePath = _target.Root.FullName };

        return new MinecraftInstallResult {
            Minecraft = resolved,
            VersionJsonPath = result.State.Get<string>("version-json-path") ?? string.Empty,
            ClientJarPath = _target.Layout.GetVersionJarPath(entry),
            Failures = result.Failures,
            Elapsed = result.Elapsed
        };
    }

    protected override InstallTask CreateTask(IVersionManifestEntry version) =>
        InstallTask.Define(task => task
            .Do(DownloadVersion, "Download Version", (state, report, ct) => DownloadVersionAsync(version, _target, state, report, ct))
            .Then(ResolveVersion, "Resolve Version", (state, report, ct) => ResolveVersionAsync(_target, state, report, ct))
            .Then(DownloadResources, "Download Resources", (state, report, ct) => DownloadResourcesAsync(_source, state, report, ct))
            .Then(ReconstructAssets, "Reconstruct Assets", ReconstructAssetsAsync));
    
    private static async ValueTask DownloadVersionAsync(
        IVersionManifestEntry version,
        MinecraftTarget target,
        InstallState state,
        Action<long, long> report,
        CancellationToken ct) {
        report(0, 1);

        var layout = target.Layout;
        var instancePath = Path.Combine(
            target.Root.FullName,
            layout.GetInstanceDirectory(version.Id));

        var seed = new MinecraftEntry {
            Id = version.Id,
            Name = version.Id,
            InstancePath = instancePath
        };
        var jsonPath = layout.GetVersionJsonPath(seed);

        state.Set("seed-entry", seed);
        state.Set("version-json-path", jsonPath);

        await using var input = await version.Url
            .GetStreamAsync(HttpCompletionOption.ResponseContentRead, ct);

        var jsonFile = new FileInfo(jsonPath);
        jsonFile.Directory?.Create();

        await using var output = new FileStream(
            jsonFile.FullName,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        await input.CopyToAsync(output, ct);

        report(1, 1);
    }

    private static async ValueTask ResolveVersionAsync(
        MinecraftTarget target,
        InstallState state,
        Action<long, long> report,
        CancellationToken ct) {
        report(0, 1);

        var seed = state.Get<MinecraftEntry>("seed-entry")
            ?? throw new InvalidOperationException("Seed entry not found in install state.");
        var jsonPath = state.Get<string>("version-json-path")
            ?? throw new InvalidOperationException("Version JSON path not found in install state.");

        var json = await File.ReadAllTextAsync(jsonPath, ct);
        using var document = JsonDocument.Parse(json);
        var entry = VersionJsonParser.MapEntry(document.RootElement, seed.Id) with {
            InstancePath = seed.InstancePath,
            MinecraftVersion = seed.Id
        };

        state.Set("resolved-context", new MinecraftContext {
            Format = target.Format,
            Layout = target.Layout,
            Entry = entry
        });

        report(1, 1);
    }

    private static async ValueTask DownloadResourcesAsync(
        DownloadSource source,
        InstallState state,
        Action<long, long> report,
        CancellationToken ct) {
        var mc = state.Get<MinecraftContext>("resolved-context")
            ?? throw new InvalidOperationException("Resolved context not found in install state.");

        var maxConcurrency = state.Get<int>(InstallState.DownloadConcurrencyKey);
        using var downloader = new ResourceDownloader(DefaultDownloader.Default, source, mc.Layout, maxConcurrency > 0 ? maxConcurrency : null);
        downloader.ProgressChanged += (_, args) =>
            report(args.CompletedCount, args.TotalCount);

        var result = await downloader.DownloadAsync(mc.Entry, ct);
        if (result.FailCount > 0)
            throw new InvalidOperationException(
                $"Some dependent files encountered errors during download. FailCount: {result.FailCount}");
    }

    private static async ValueTask ReconstructAssetsAsync(InstallState state, Action<long, long> report, CancellationToken ct) {
        report(0, 1);

        var mc = state.Get<MinecraftContext>("resolved-context")
            ?? throw new InvalidOperationException("Resolved context not found in install state.");

        await new AssetsReconstructor(mc.Layout)
            .ReconstructAsync(mc.Entry, mc.Layout.GetGameDirectory(mc.Entry), ct);

        report(1, 1);
    }
}