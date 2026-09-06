using MinecraftLaunch.Base.Enums;
using MinecraftLaunch.Base.Interfaces;
using MinecraftLaunch.Base.Models.Game;
using MinecraftLaunch.Base.Models.Network;
using MinecraftLaunch.Components.Downloader;
using MinecraftLaunch.Extensions;
using System.IO.Compression;
using System.Text.Json;

namespace MinecraftLaunch.Components.Installer.Modpack;

public sealed class ModrinthModpackInstaller : InstallerBase {
    public string ModpackPath { get; init; }
    public MinecraftEntry Minecraft { get; init; }
    /// <summary>在游戏实例创建前预下载整合包文件时使用的目标目录。</summary>
    public string WorkingPath { get; init; }
    public override string MinecraftFolder { get; init; }
    public ModrinthModpackInstallEntry Entry { get; init; }

    public static ModrinthModpackInstallEntry ParseModpackInstallEntry(string modpackPath) {
        using var zipArchive = ZipFile.OpenRead(modpackPath);
        using var json = zipArchive?.GetEntry("modrinth.index.json")?.Open()
            ?? throw new ArgumentException("Not found modrinth.index.json");

        return JsonSerializer.Deserialize(json,ModrinthModpackInstallEntryContext.Default.ModrinthModpackInstallEntry)
            ?? throw new InvalidOperationException("Failed to parse modrinth.index.json");
    }

    public static async Task<IInstallEntry?> ParseModLoaderEntryAsync(ModrinthModpackInstallEntry modpack, CancellationToken cancellationToken = default) {
        if (modpack.Dependencies.TryGetValue("fabric-loader", out var modpackDependency1))
            return (await FabricInstaller.EnumerableFabricAsync(modpack.McVersion, cancellationToken: cancellationToken))
                .First(x => x.BuildVersion.Equals(modpackDependency1));
        else if (modpack.Dependencies.TryGetValue("quilt-loader", out var dependency1))
            return (await QuiltInstaller.EnumerableQuiltAsync(modpack.McVersion, cancellationToken))
                .First(x => x.BuildVersion.Equals(dependency1));
        else if (modpack.Dependencies.TryGetValue("forge", out var modpackDependency))
            return (await ForgeInstaller.EnumerableForgeAsync(modpack.McVersion, false, cancellationToken))
                .First(x => x.ForgeVersion.Equals(modpackDependency));
        else if (modpack.Dependencies.TryGetValue("neoforge", out var dependency))
            return (await ForgeInstaller.EnumerableForgeAsync(modpack.McVersion, true, cancellationToken))
                .First(x => x.ForgeVersion.Equals(dependency));
        else
            return null;
    }

    public static ModrinthModpackInstaller Create(string mcFolder, string modpackPath, ModrinthModpackInstallEntry installEntry, MinecraftEntry entry) {
        return new ModrinthModpackInstaller {
            Entry = installEntry,
            ModpackPath = modpackPath,
            MinecraftFolder = mcFolder,
            Minecraft = entry
        };
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
            var downloadRequests = ParseModFiles(cancellationToken);

            var result = await DownloadModsAsync(downloadRequests, cancellationToken);
            if (result.Failed.Any())
                throw new IOException($"Failed to download {result.Failed.Count()} modpack files.");
            await ExtractModpackAsync(cancellationToken);
        } catch (Exception ex) {
            ReportCompleted(false, ex);
            throw;
        }

        ReportProgress(InstallStep.RanToCompletion, 1.0d, TaskStatus.RanToCompletion, 1, 1);
        ReportCompleted(true);
    }

    #region Privates

    private IEnumerable<DownloadRequest> ParseModFiles(CancellationToken cancellationToken)
    {
        const double minProgress = 0.1d;
        const double maxProgress = 0.45d;
        var fileArray = Entry.Files.ToArray();
        var constTotalCount = fileArray.Length;
        ReportProgress(
            step: InstallStep.ParseDownloadUrls,
            progress: 0.1d,
            status: TaskStatus.Running,
            totalCount: constTotalCount, 
            finshedCount: 0);
        double count = 0;
        var versionPath = GetWorkingPath();
        return fileArray.Select(fileItem =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            // 非多线程且同个闭包可见性好,无需原子操作
            ReportProgress(
                step: InstallStep.ParseDownloadUrls,
                // ReSharper disable once AccessToModifiedClosure
                progress: (++count / constTotalCount).ToPercentage(minProgress, maxProgress),
                status: TaskStatus.Running,
                totalCount: constTotalCount,
                finshedCount: 0);
            if (!fileItem.Downloads.Any()) return null;
            if (string.IsNullOrEmpty(fileItem.Path)) return null;
            var filePath = Path.Combine(versionPath, fileItem.Path);
            return new DownloadRequest(fileItem.Downloads.First(), filePath);
        }).Where(static x => x is not null);
    }

    private Task<GroupDownloadResult> DownloadModsAsync(IEnumerable<DownloadRequest> downloadRequests, CancellationToken cancellationToken) {
        List<Task> downloadTasks = [];

        var groupRequest = new GroupDownloadRequest(downloadRequests);

        groupRequest.ProgressChanged = args => {
            ReportProgress(InstallStep.DownloadMods, args.Percentage.ToPercentage(0.45d, 0.7d),
                TaskStatus.Running, args.TotalCount, args.CompletedCount, args.Speed, true);
        };

        return new DefaultDownloader()
            .DownloadManyAsync(groupRequest, cancellationToken);
    }

    private async Task ExtractModpackAsync(CancellationToken cancellationToken) {
      
        ReportProgress(InstallStep.ExtractModpack, 0.85d, TaskStatus.Running, 0, 0); // 此处未开始解析,返回0

        const string decompressPrefix = "overrides";

        var totalCount = CountExtractableEntries(decompressPrefix);
        if (totalCount == 0)
        {
            ReportProgress(InstallStep.ExtractModpack, 1.0d, TaskStatus.RanToCompletion, 0, 0);
            return;
        }
        var count = 0;
        await ModPackUtils.ExtractSingleThreadAsync(
            srcZipPath: ModpackPath,
            overridesPrefix: decompressPrefix,
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
