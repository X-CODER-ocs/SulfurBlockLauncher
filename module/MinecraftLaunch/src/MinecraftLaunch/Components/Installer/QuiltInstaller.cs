using Flurl.Http;
using MinecraftLaunch.Base.Enums;
using MinecraftLaunch.Base.Models.Game;
using MinecraftLaunch.Base.Models.Network;
using MinecraftLaunch.Components.Downloader;
using MinecraftLaunch.Components.Parser;
using MinecraftLaunch.Extensions;
using MinecraftLaunch.Utilities;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace MinecraftLaunch.Components.Installer;

public sealed class QuiltInstaller : InstallerBase {
    public string CustomId { get; init; }
    public QuiltInstallEntry Entry { get; init; }
    public override string MinecraftFolder { get; init; }
    public MinecraftEntry InheritedMinecraft { get; init; }

    public static QuiltInstaller Create(string mcFolder, QuiltInstallEntry installEntry, string customId = default) {
        return new QuiltInstaller {
            CustomId = customId,
            Entry = installEntry,
            MinecraftFolder = mcFolder,
        };
    }

    public static async Task<IEnumerable<QuiltInstallEntry>> EnumerableQuiltAsync(string mcVersion, CancellationToken cancellationToken = default) {
        await using var json = await HttpUtil.Request($"https://meta.quiltmc.org/v3/versions/loader/{mcVersion}")
            .GetStreamAsync(cancellationToken: cancellationToken);

        var entries = await JsonSerializer.DeserializeAsync(json,QuiltInstallEntryContext.Default.IEnumerableQuiltInstallEntry, cancellationToken);
        return entries.Select(entry => entry with { RequestedMcVersion = mcVersion });
    }

    public override async Task<MinecraftEntry> InstallAsync(CancellationToken cancellationToken = default) {
        ModifiedMinecraftEntry entry = default;
        MinecraftEntry inheritedEntry = default;

        ReportProgress(InstallStep.Started, 0.0d, TaskStatus.WaitingToRun, 1, 1);

        try {
            inheritedEntry = ParseMinecraft(cancellationToken);

            var jsonFile = await DownloadVersionJsonAsync(inheritedEntry, cancellationToken);
            entry = ParseModifiedMinecraft(jsonFile, inheritedEntry, cancellationToken);
            await CompleteQuiltLibrariesAsync(entry, cancellationToken);
        } catch (Exception ex) {
            ReportProgress(InstallStep.Interrupted, 1.0d, TaskStatus.Faulted, 1, 1);
            ReportCompleted(false, ex);
            throw;
        }

        ReportProgress(InstallStep.RanToCompletion, 1.0d, TaskStatus.RanToCompletion, 1, 1);
        ReportCompleted(true);
        return entry;
    }

    /// <summary>提前下载加载器版本配置，使其可与原版资源下载并行。</summary>
    public Task PreloadAsync(CancellationToken cancellationToken = default) =>
        DownloadProfileAsync(CustomId ?? $"quilt-loader-{Entry.Loader.Version}_{Entry.McVersion}", cancellationToken);

    #region Privates

    private MinecraftEntry ParseMinecraft(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ReportProgress(InstallStep.ParseMinecraft, 0.10d, TaskStatus.Running, 1, 0);

        if (InheritedMinecraft is not null) {
            return InheritedMinecraft;
        }

        var inheritedMinecraft = new MinecraftParser(MinecraftFolder).GetMinecrafts()
            .FirstOrDefault(x => x.Version.VersionId == Entry.McVersion);

        ReportProgress(InstallStep.ParseMinecraft, 0.15d, TaskStatus.Running, 1, 1);
        return inheritedMinecraft ?? throw new InvalidOperationException("The corresponding version's parent was not found."); ;
    }

    private async Task<FileInfo> DownloadVersionJsonAsync(MinecraftEntry entry, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ReportProgress(InstallStep.DownloadVersionJson, 0.20d, TaskStatus.Running, 1, 0);

        if (CustomId is { } customId)
        {
            var cachedProfile = new FileInfo(Path.Combine(MinecraftFolder, "versions", customId, $"{customId}.json"));
            if (cachedProfile.Exists)
            {
                ReportProgress(InstallStep.DownloadVersionJson, 0.45d, TaskStatus.Running, 1, 1);
                return cachedProfile;
            }
        }

        string requestUrl = $"https://meta.quiltmc.org/v3/versions/loader/{Entry.McVersion}/{Entry.BuildVersion}/profile/json";
        requestUrl = DownloadManager.BmclApi.TryFindUrl(requestUrl);

        await using var jsonStream = await HttpUtil.Request(requestUrl).GetStreamAsync(HttpCompletionOption.ResponseContentRead, cancellationToken);
        using var doc = await JsonDocument.ParseAsync(jsonStream, cancellationToken: cancellationToken);
        string entryId = CustomId ??
            doc.RootElement.GetPropertyNullable("id"u8)?.GetString() ??
            $"quilt-loader-{Entry.Loader.Version}_{entry.Id}";

        var jsonFile = new FileInfo(Path
            .Combine(MinecraftFolder, "versions", entryId, $"{entryId}.json"));

        if (jsonFile.Exists) {
            ReportProgress(InstallStep.DownloadVersionJson, 0.45d, TaskStatus.Running, 1, 1);
            return jsonFile;
        }

        if (!jsonFile.Directory!.Exists)
            jsonFile.Directory.Create();

        await using var output = File.OpenWrite(jsonFile.FullName);
        await JsonSerializer.SerializeAsync(output, doc, JsonDocumentSerializeContext.Default.JsonDocument,
            cancellationToken);

        ReportProgress(InstallStep.DownloadVersionJson, 0.45d, TaskStatus.Running, 1, 1);
        return jsonFile;
    }

    private async Task<FileInfo> DownloadProfileAsync(string instanceId, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ReportProgress(InstallStep.DownloadVersionJson, 0.20d, TaskStatus.Running, 1, 0);

        var jsonFile = new FileInfo(Path.Combine(MinecraftFolder, "versions", instanceId, $"{instanceId}.json"));
        if (jsonFile.Exists) {
            ReportProgress(InstallStep.DownloadVersionJson, 0.45d, TaskStatus.Running, 1, 1);
            return jsonFile;
        }

        var requestUrl = DownloadManager.BmclApi.TryFindUrl(
            $"https://meta.quiltmc.org/v3/versions/loader/{Entry.McVersion}/{Entry.BuildVersion}/profile/json");
        await using var jsonStream = await HttpUtil.Request(requestUrl).GetStreamAsync(HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!jsonFile.Directory!.Exists) jsonFile.Directory.Create();
        await using var output = File.OpenWrite(jsonFile.FullName);
        await jsonStream.CopyToAsync(output, cancellationToken);
        ReportProgress(InstallStep.DownloadVersionJson, 0.45d, TaskStatus.Running, 1, 1);
        return jsonFile;
    }

    private ModifiedMinecraftEntry ParseModifiedMinecraft(FileInfo file, MinecraftEntry inheritedEntry,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var entry = MinecraftParser.Parse(file.Directory, [inheritedEntry], out var _) as ModifiedMinecraftEntry;

        return entry ?? throw new InvalidOperationException("An incorrect modified entry was encountered");
    }

    private async Task CompleteQuiltLibrariesAsync(MinecraftEntry minecraft, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ReportProgress(InstallStep.DownloadLibraries, 0.5d, TaskStatus.Running, 0, 0);

        var resourceDownloader = new MinecraftResourceDownloader(minecraft) {
            SourceRootDirectories = SourceRootDirectories
        };

        resourceDownloader.CopyProgressChanged += (_, x)
            => ReportProgress(InstallStep.CopyLibraries, x.Percentage.ToPercentage(0.5d, 0.75d),
                TaskStatus.Running, x.TotalCount, x.CompletedCount);

        resourceDownloader.ProgressChanged += (_, x)
            => ReportProgress(InstallStep.DownloadLibraries, x.ToPercentage().ToPercentage(0.75d, 0.95d),
                TaskStatus.Running, resourceDownloader.TotalCount,
                    x.CompletedCount, x.Speed, true);

        var result = await resourceDownloader.VerifyAndDownloadDependenciesAsync(cancellationToken: cancellationToken);
        if (result.Failed.Any())
            throw new IOException($"Failed to download {result.Failed.Count()} Quilt dependencies.");
    }

    #endregion
}
