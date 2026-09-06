using Flurl.Http;
using MinecraftLaunch.Base.EventArgs;
using MinecraftLaunch.Base.Interfaces;
using MinecraftLaunch.Base.Models.Game;
using MinecraftLaunch.Base.Models.Network;
using MinecraftLaunch.Extensions;
using MinecraftLaunch.Utilities;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;

namespace MinecraftLaunch.Components.Downloader;

public sealed class MinecraftResourceDownloader {
    private readonly MinecraftEntry _entry;
    private readonly DefaultDownloader _downloader;
    private readonly List<MinecraftDependency> _dependencies = [];

    public event EventHandler<ResourceDownloadProgressChangedEventArgs> ProgressChanged;
    public event EventHandler<ResourceCopyProgressChangedEventArgs> CopyProgressChanged;

    internal int TotalCount { get; set; }
    public bool AllowVerifyAssets { get; init; } = true;
    public bool AllowInheritedDependencies { get; init; } = true;
    
    public IEnumerable<string> SourceRootDirectories { get; set; } = [];

    /// <summary>验证后可复制的本地文件清单。</summary>
    public IReadOnlyList<ResourceCopyItem> CopyItems { get; private set; } = [];

    private readonly List<MinecraftDependency> _dependenciesToDownload = [];

    /// <summary>验证后仍需要下载的依赖项（复制失败的本地文件也会落入此清单）。</summary>
    public IReadOnlyList<MinecraftDependency> DependenciesToDownload => _dependenciesToDownload;

    public MinecraftResourceDownloader(MinecraftEntry entry, IEnumerable<MinecraftDependency> extraDependencies = null) {
        if (extraDependencies is not null)
            _dependencies.AddRange(extraDependencies);

        _entry = entry;
        _downloader = new();
    }

    /// <summary>
    /// 验证、复制本地资源并下载剩余依赖（一次性调用全部阶段）。
    /// </summary>
    public async Task<GroupDownloadResult> VerifyAndDownloadDependenciesAsync(int fileVerificationParallelism = 10, CancellationToken cancellationToken = default) {
        await VerifyDependenciesAsync(fileVerificationParallelism, cancellationToken);
        CopyDependencies(fileVerificationParallelism, cancellationToken);
        return await DownloadDependenciesAsync(cancellationToken);
    }

    /// <summary>
    /// 第一阶段：收集并验证所有依赖，同时找出可从本地资源目录复制的文件。
    /// </summary>
    public Task VerifyDependenciesAsync(int fileVerificationParallelism = 10, CancellationToken cancellationToken = default) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fileVerificationParallelism);

        #region 1.1 Libraries & Inherited Libraries

        var (libs, nativeLibs) = _entry.GetRequiredLibraries();
        _dependencies.AddRange(libs);
        _dependencies.AddRange(nativeLibs);

        if (AllowInheritedDependencies
            && _entry is ModifiedMinecraftEntry modInstance
            && modInstance.HasInheritance) {
            (libs, nativeLibs) = modInstance.InheritedMinecraft.GetRequiredLibraries();
            _dependencies.AddRange(libs);
            _dependencies.AddRange(nativeLibs);
        }

        #endregion

        #region 1.2 Client.jar

        var jar = _entry.GetJarElement();
        if (jar != null) {
            _dependencies.Add(jar);
        }

        #endregion

        #region 1.3 AssetIndex & Assets

        if (AllowVerifyAssets) {
            var assetIndex = _entry.GetAssetIndex();

            // 验证 AssetIndex 文件
            if (!VerifyDependency(assetIndex, cancellationToken)) {
                var assetIndexDirectory = Path.Combine(assetIndex.MinecraftFolderPath, "assets", "indexes");
                Directory.CreateDirectory(assetIndexDirectory);
                return DownloadAssetIndexAsync(assetIndex, cancellationToken)
                    .ContinueWith(ContinueVerify, cancellationToken,
                        TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }

            // 添加资源文件到依赖列表
            _dependencies.AddRange(_entry.GetRequiredAssets());
        }

        #endregion

        return VerifyAndPlanDependenciesAsync(fileVerificationParallelism, cancellationToken);

        async Task ContinueVerify(Task previous) {
            await previous.ConfigureAwait(false);
            _dependencies.AddRange(_entry.GetRequiredAssets());
            await VerifyAndPlanDependenciesAsync(fileVerificationParallelism, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 第二阶段：将可复制的本地资源文件复制到目标目录，并上报复制进度。
    /// 复制失败的依赖项会被追加到 <see cref="DependenciesToDownload"/>，由下载阶段兜底。
    /// </summary>
    public void CopyDependencies(int fileVerificationParallelism = 4, CancellationToken cancellationToken = default) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fileVerificationParallelism);

        var items = CopyItems;
        if (items.Count == 0)
            return;

        var totalBytes = items.Sum(item => GetDependencySize(item.Dependency));
        var copiedBytes = 0L;
        var completedCount = 0;
        ConcurrentBag<MinecraftDependency> copyFailed = [];

        Parallel.ForEach(items, new ParallelOptions {
            MaxDegreeOfParallelism = fileVerificationParallelism,
            CancellationToken = cancellationToken
        }, item => {
            var target = item.TargetPath;
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(item.SourcePath, target, true);
                Interlocked.Add(ref copiedBytes, GetDependencySize(item.Dependency));
            } catch (Exception exception) {
                Debug.WriteLine($"从本地资源目录复制文件失败：{item.SourcePath} -> {target}{Environment.NewLine}{exception}");
                copyFailed.Add(item.Dependency);
            }

            var finished = Interlocked.Increment(ref completedCount);
            CopyProgressChanged?.Invoke(this, new ResourceCopyProgressChangedEventArgs {
                TotalCount = items.Count,
                CompletedCount = finished,
                TotalBytes = totalBytes,
                CopiedBytes = Interlocked.Read(ref copiedBytes),
                CurrentFile = item.Dependency.FilePath
            });
        });

        if (!copyFailed.IsEmpty)
            _dependenciesToDownload.AddRange(copyFailed);
    }

    /// <summary>
    /// 第三阶段：下载验证后仍缺失的依赖，并上报下载进度。
    /// </summary>
    public Task<GroupDownloadResult> DownloadDependenciesAsync(CancellationToken cancellationToken = default) {
        TotalCount = DependenciesToDownload.Count;
        var downloadItems = DependenciesToDownload
            .OfType<IDownloadDependency>()
            .Select(dep => new DownloadRequest(dep.Url, dep.FullPath, dep.Size ?? 0))
            .ToList();

        Debug.WriteLine(_dependencies.Where(x => x is FabricLibrary).Count());

        var groupDownloadRequest = new GroupDownloadRequest(downloadItems);
        groupDownloadRequest.ProgressChanged += args
            => ProgressChanged?.Invoke(this, args);

        return _downloader.DownloadManyAsync(groupDownloadRequest, cancellationToken);
    }

    #region Privates

    private async Task DownloadAssetIndexAsync(AssstIndex assetIndex, CancellationToken cancellationToken) {
        var assetIndexDirectory = Path.Combine(assetIndex.MinecraftFolderPath, "assets", "indexes");
        Directory.CreateDirectory(assetIndexDirectory);
        await using var assetIndexStream = await HttpUtil.Request(assetIndex.Url)
            .GetStreamAsync(HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await using var assetIndexFile = File.Create(Path.Combine(assetIndexDirectory, $"{assetIndex.Id}.json"));
        await assetIndexStream.CopyToAsync(assetIndexFile, cancellationToken);
    }

    private async Task VerifyAndPlanDependenciesAsync(int fileVerificationParallelism, CancellationToken cancellationToken) {
        // 2. 验证依赖项
        ConcurrentBag<MinecraftDependency> invalidDeps = [];
        await Task.Run(() => Parallel.ForEach(_dependencies, new ParallelOptions {
            MaxDegreeOfParallelism = fileVerificationParallelism,
            CancellationToken = cancellationToken
        }, dep => {
            if (!VerifyDependency(dep, cancellationToken)) {
                invalidDeps.Add(dep);
            }
        }), cancellationToken);

        // 3. 找出可从本地资源目录复制的文件，其余进入下载清单
        ConcurrentBag<ResourceCopyItem> copyItems = [];
        ConcurrentBag<MinecraftDependency> dependenciesToDownload = [];
        await Task.Run(() => Parallel.ForEach(invalidDeps, new ParallelOptions {
            MaxDegreeOfParallelism = fileVerificationParallelism,
            CancellationToken = cancellationToken
        }, dep => {
            cancellationToken.ThrowIfCancellationRequested();
            if (dep is IDownloadDependency
                && FindSourceFile(dep, cancellationToken) is { } sourcePath) {
                copyItems.Add(new ResourceCopyItem(dep, sourcePath));
                return;
            }
            dependenciesToDownload.Add(dep);
        }), cancellationToken);

        CopyItems = copyItems.ToArray();
        _dependenciesToDownload.AddRange(dependenciesToDownload);
    }

    private string FindSourceFile(MinecraftDependency dependency, CancellationToken cancellationToken) {
        if (dependency is not IVerifiableDependency verifiableDependency)
            return null;

        foreach (var sourceRoot in SourceRootDirectories) {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(sourceRoot))
                continue;

            var candidate = Path.Combine(sourceRoot, dependency.FilePath);
            if (!File.Exists(candidate))
                continue;

            if (!VerifyFileContent(candidate, verifiableDependency))
                continue;

            return candidate;
        }
        return null;
    }

    /// <summary>
    /// 尝试从给定的本地资源根目录复制依赖项到目标目录（校验内容匹配后才复制）。
    /// </summary>
    public static bool TryCopyDependencyFromSources(MinecraftDependency dependency, IEnumerable<string> sourceRoots,
        CancellationToken cancellationToken = default) {
        if (dependency is not IVerifiableDependency verifiableDependency)
            return false;

        foreach (var sourceRoot in sourceRoots) {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(sourceRoot))
                continue;

            var candidate = Path.Combine(sourceRoot, dependency.FilePath);
            if (!File.Exists(candidate))
                continue;

            if (!VerifyFileContent(candidate, verifiableDependency))
                continue;

            var target = dependency.FullPath;
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(candidate, target, true);
                return true;
            } catch (Exception exception) {
                Debug.WriteLine($"从本地资源目录复制文件失败：{candidate} -> {target}{Environment.NewLine}{exception}");
            }
        }
        return false;
    }

    private static bool VerifyDependency(MinecraftDependency dep, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        Debug.WriteLineIf(dep is FabricLibrary, dep.FullPath);
        if (!File.Exists(dep.FullPath))
            return false;

        if (dep is not IVerifiableDependency verifiableDependency)
            return true;

        return VerifyFileContent(dep.FullPath, verifiableDependency);
    }

    private static bool VerifyFileContent(string filePath, IVerifiableDependency verifiableDependency) {
        if (verifiableDependency.Sha1 is { } sha1) {
            using var fileStream = File.OpenRead(filePath);
            var sha1Bytes = (Span<byte>)stackalloc byte[20];
            SHA1.HashData(fileStream, sha1Bytes);
            return sha1Bytes.SequenceEqual(sha1);
        }

        if (verifiableDependency.Size is long size)
            return new FileInfo(filePath).Length == size;

        return false;
    }

    private static long GetDependencySize(MinecraftDependency dependency) => dependency switch {
        IVerifiableDependency verifiable => verifiable.Size ?? 0,
        IDownloadDependency download => download.Size ?? 0,
        _ => 0
    };

    #endregion
}
