using System.Diagnostics;
using MinecraftLaunch.Base.Enums;
using MinecraftLaunch.Base.Interfaces;
using MinecraftLaunch.Base.Models.Game;
using MinecraftLaunch.Base.Models.Network;
using MinecraftLaunch.Components.Downloader;
using MinecraftLaunch.Components.Provider;
using MinecraftLaunch.Extensions;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace MinecraftLaunch.Components.Installer.Modpack;

public sealed class CurseforgeModpackInstaller : InstallerBase {
    public string ModpackPath { get; init; }
    public MinecraftEntry Minecraft { get; init; }
    /// <summary>在游戏实例创建前预下载整合包文件时使用的目标目录。</summary>
    public string WorkingPath { get; init; }
    public override string MinecraftFolder { get; init; }
    public CurseforgeModpackInstallEntry Entry { get; init; }

    [Obsolete("Implemented processing method")]
    public List<long> FaildParseModProjectId { get; set; } = [];

    public static CurseforgeModpackInstaller Create(string mcFolder, string modpackPath, CurseforgeModpackInstallEntry installEntry, MinecraftEntry entry) {
        return new CurseforgeModpackInstaller {
            Minecraft = entry,
            Entry = installEntry,
            ModpackPath = modpackPath,
            MinecraftFolder = mcFolder
        };
    }

    public static CurseforgeModpackInstallEntry ParseModpackInstallEntry(string modpackPath) {
        using var zipArchive = ZipFile.OpenRead(modpackPath);
        using var json = zipArchive?.GetEntry("manifest.json")?.Open()
            ?? throw new ArgumentException("Not found manifest.json");

        var entry = JsonSerializer.Deserialize(json,CurseforgeModpackInstallEntryContext.Default.CurseforgeModpackInstallEntry)
            ?? throw new InvalidOperationException("Failed to parse manifest.json");

        return entry;
    }

    public static async IAsyncEnumerable<IInstallEntry> ParseModLoaderEntryByManifestAsync(CurseforgeModpackInstallEntry entry, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        Debug.Assert(entry.Minecraft.ModLoaders.ValueKind is JsonValueKind.Array);
        foreach (var loader in entry.Minecraft.ModLoaders.EnumerateArray()) {
            cancellationToken.ThrowIfCancellationRequested();

            (bool isPrimary, string id) = (loader.GetProperty("primary"u8).GetBoolean(), loader.GetProperty("id"u8).GetString());
            var idDatas = id.Split('-');

            var loaderVersion = idDatas.Last();
            var loaderType = idDatas.First() switch {
                "forge" => ModLoaderType.Forge,
                "fabric" => ModLoaderType.Fabric,
                "neoforge" => ModLoaderType.NeoForge,
                _ => throw new NotSupportedException("Unsupported installer type")
            };

            IInstallEntry installEntry = loaderType switch {
                ModLoaderType.Forge => (await ForgeInstaller.EnumerableForgeAsync(entry.McVersion, cancellationToken: cancellationToken))
                    .First(x => x.ForgeVersion.Equals(loaderVersion)),

                ModLoaderType.Fabric => (await FabricInstaller.EnumerableFabricAsync(entry.McVersion, cancellationToken: cancellationToken))
                    .First(x => x.BuildVersion.Equals(loaderVersion)),

                ModLoaderType.NeoForge => (await ForgeInstaller.EnumerableForgeAsync(entry.McVersion, true, cancellationToken))
                    .First(x => x.ForgeVersion.Equals(loaderVersion)),

                _ => throw new NotImplementedException()
            };

            yield return installEntry ?? throw new InvalidOperationException();
        }
    }

    public override async Task<MinecraftEntry> InstallAsync(CancellationToken cancellationToken = default) {
        await InstallFilesAsync(cancellationToken);
        return Minecraft;
    }

    /// <summary>
    /// 下载模组并释放覆盖文件。这些文件不依赖版本 JSON，可与游戏和加载器安装并行执行。
    /// </summary>
    public async Task InstallFilesAsync(CancellationToken cancellationToken = default) {
        ReportProgress(InstallStep.Started, 0.0d, TaskStatus.WaitingToRun, 1, 1);

        try {
            var modInfoGroup = (await ParseModFilesAsync(cancellationToken))
                .ToLookup(x => string.IsNullOrEmpty(x.url));

            var downloadUrls = modInfoGroup[false].Select(x => x.url).ToList();
            var invalidMods = modInfoGroup[true].Select(x => x.invalidMod).ToList();

            var redirectownloadUrls = RedirectInvalidModsAsync(invalidMods, cancellationToken);
            await foreach (var downloadUrl in redirectownloadUrls)
                downloadUrls.Add(downloadUrl);

            await DownloadModsAsync(downloadUrls, cancellationToken);
            await ExtractModpackAsync(cancellationToken);
        } catch (Exception ex) {
            ReportProgress(InstallStep.Interrupted, 1.0d, TaskStatus.Canceled, 1, 1);
            ReportCompleted(false, ex);
            throw;
        }

        ReportProgress(InstallStep.RanToCompletion, 1.0d, TaskStatus.RanToCompletion, 1, 1);
        ReportCompleted(true);
    }

    #region Privates

    [Obsolete]
    private void ParseMinecraft(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ReportProgress(InstallStep.ParseMinecraft, 0.05d, TaskStatus.Running, 1, 0);

        if (Minecraft is not null && Minecraft is ModifiedMinecraftEntry && Minecraft.Version.VersionId.Equals(Entry.McVersion)) {
            ReportProgress(InstallStep.ParseMinecraft, 0.1d, TaskStatus.Running, 1, 1);
            return;
        }

        throw new NotSupportedException("Your entry is incorrect or does not exist");
    }

    private async Task<IEnumerable<(string url, CurseforgeModpackFileEntry invalidMod)>> ParseModFilesAsync(CancellationToken cancellationToken) {
        int count = 0;
        int totalCount = Entry.ModFiles.Count();
        List<Task> requestTasks = [];
        List<(string, CurseforgeModpackFileEntry)> downloadInfoGroup = [];
        using SemaphoreSlim semaphoreSlim = new(256, 256);
        ReportProgress(InstallStep.ParseDownloadUrls, 0.1d, TaskStatus.Running, totalCount, count);

        foreach (var modpackFile in Entry.ModFiles) {
            string downloadUrl = string.Empty;

            requestTasks.Add(Task.Run(async () => {
                await semaphoreSlim.WaitAsync(cancellationToken);
                try {
                    if (!modpackFile.IsRequired) return;
                    downloadUrl = await CurseforgeProvider.GetModDownloadUrlAsync(modpackFile.ProjectId, modpackFile.FileId, cancellationToken);

                    lock (downloadInfoGroup) {
                        var progress = (double)Interlocked.Increment(ref count) / (double)totalCount;
                        ReportProgress(InstallStep.ParseDownloadUrls, progress.ToPercentage(0.1d, 0.5d),
                            TaskStatus.Running, totalCount, count);

                        downloadInfoGroup.Add(new(downloadUrl, modpackFile));
                    }
                }
                finally {
                    semaphoreSlim.Release();
                }
            }, cancellationToken));
        }

        await Task.WhenAll(requestTasks);
        return downloadInfoGroup;
    }

    private async IAsyncEnumerable<string> RedirectInvalidModsAsync(List<CurseforgeModpackFileEntry> modpacks, [EnumeratorCancellation] CancellationToken cancellationToken) {
        ReportProgress(InstallStep.RedirectInvalidMod, 0.5d, TaskStatus.Running, modpacks.Count, 0);

        var totalCount = modpacks.Count;
        var count = 0;
        var resolvedUrls = await Task.WhenAll(modpacks.Select(async modpackFile =>
        {
            var modFileName = (await CurseforgeProvider
                .GetModFileEntryAsync(modpackFile.ProjectId, modpackFile.FileId, cancellationToken))
                .GetProperty("fileName"u8).GetString();
            var url = await CurseforgeProvider.TestDownloadUrlAsync(modpackFile.FileId, modFileName, cancellationToken);
            var completed = Interlocked.Increment(ref count);
            ReportProgress(InstallStep.RedirectInvalidMod,
                ((double)completed / totalCount).ToPercentage(0.5d, 0.6d), TaskStatus.Running, totalCount, completed);
            return url;
        }));

        foreach (var url in resolvedUrls) yield return url;
    }

    private async Task DownloadModsAsync(IEnumerable<string> asyncUrls, CancellationToken cancellationToken) {
        List<Task> downloadTasks = [];
        var urls = asyncUrls.ToList();

        var modsPath = new DirectoryInfo(Path.Combine(GetWorkingPath(), "mods"));
        if (!modsPath.Exists)
            modsPath.Create();

        var groupRequest = new GroupDownloadRequest(urls
            .Select(x => new DownloadRequest(x, Path.Combine(modsPath.FullName,
                Path.GetFileName(x)))));

        groupRequest.ProgressChanged = args =>
            ReportProgress(InstallStep.DownloadMods, args.Percentage.ToPercentage(0.6d, 0.85d),
                TaskStatus.Running, args.TotalCount, args.CompletedCount, args.Speed, true);

        ReportProgress(InstallStep.DownloadMods, 0.6d, TaskStatus.Running,
            urls.Count, 0, 0, false);

        var result = await new DefaultDownloader().DownloadManyAsync(groupRequest, cancellationToken);
        if (result.Failed.Any())
            throw new IOException($"Failed to download {result.Failed.Count()} modpack files.");
    }

    private async Task ExtractModpackAsync(CancellationToken cancellationToken) {
      
        ReportProgress(InstallStep.ExtractModpack, 0.85d, TaskStatus.Running, 0, 0); // 此处未开始解析,返回0

        var totalCount = CountExtractableEntries(Entry.Overrides);
        if (totalCount == 0)
        {
            ReportProgress(InstallStep.ExtractModpack, 1.0d, TaskStatus.RanToCompletion, 0, 0);
            return;
        }
        var count = 0;
        await ModPackUtils.ExtractSingleThreadAsync(
            srcZipPath: ModpackPath,
            overridesPrefix: Entry.Overrides,
            independentAndFullWorkingPath: GetWorkingPath(),
            whenEachEntryCompleted: ReportEntryExtractingProgress,
            cancellationToken: cancellationToken);
        void ReportEntryExtractingProgress(ZipArchive zipArchive) =>
            ReportProgress(
                step: InstallStep.ExtractModpack, 
                progress: (Interlocked.Increment(ref count) / (double)totalCount).ToPercentage(0.85d, 1.0d),
                status:  TaskStatus.Running,
                totalCount: totalCount,
                finshedCount: count);

        ReportProgress(InstallStep.ExtractModpack, 1.0d, TaskStatus.RanToCompletion, totalCount, totalCount);
    }

    private string GetWorkingPath() => WorkingPath ?? Minecraft.ToWorkingPath(true);

    private int CountExtractableEntries(string prefix)
    {
        using var archive = ZipFile.OpenRead(ModpackPath);
        return archive.Entries.Count(entry => !entry.FullName.EndsWith('/') &&
            entry.FullName.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}
