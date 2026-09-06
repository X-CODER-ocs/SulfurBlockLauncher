using System.Security.Cryptography;
using System.Text.Json;
using Iridium.Minecraft;
using Iridium.Models.Download;
using Iridium.Models.Minecraft;
using Iridium.Interfaces;
using Iridium.Enums;

namespace Iridium.Download;

public sealed class ResourceDownloader : IDisposable {
    private readonly DownloadSource _source;
    private readonly IMinecraftLayout? _layout;
    private readonly DefaultDownloader _downloader;
    private readonly int? _maxConcurrency;
    private readonly Action<ResourceDownloadProgressChangedEventArgs> _forwardProgress;
    private readonly bool _ownsDownloader;

    private int _disposed;

    public event EventHandler<ResourceDownloadProgressChangedEventArgs>? ProgressChanged;

    internal ResourceDownloader(DefaultDownloader downloader, DownloadSource source, IMinecraftLayout layout, int? maxConcurrency) {
        ArgumentNullException.ThrowIfNull(downloader);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(layout);

        _source = source;
        _layout = layout;
        _downloader = downloader;
        _maxConcurrency = maxConcurrency;
        _ownsDownloader = false;
        _forwardProgress = ForwardProgress;
    }

    public ResourceDownloader(DownloadSource source, IMinecraftLayout layout, int maxConcurrency = 4) {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(layout);
    
            _source = source;
            _layout = layout;
            _ownsDownloader = true;
            _forwardProgress = ForwardProgress;
            _downloader = new DefaultDownloader(Math.Max(1, maxConcurrency));
        }
    
    public ResourceDownloader(DefaultDownloader downloader, DownloadSource source, IMinecraftLayout layout)
        : this(downloader, source, layout, null) {
    }

    public async Task<DownloadResponse> DownloadAsync(MinecraftEntry entry, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(entry);

        var layout = _layout ?? throw new InvalidOperationException("A layout is required.");
        var files = ResolveFiles(entry, layout);

        DownloadFileEntry? assetIndex = null;
        var assetIndexPos = -1;

        for (var i = 0; i < files.Count; i++) {
            if (files[i].Type != DownloadFileType.AssetIndex)
                continue;

            assetIndex = files[i];
            assetIndexPos = i;
            break;
        }

        if (assetIndex is not null) {
            var indexResult = await _downloader.DownloadManyAsync([
                    BuildRequest(assetIndex)
                ], _maxConcurrency, _forwardProgress, cancellationToken)
                .ConfigureAwait(false);

            if (indexResult.FailCount > 0)
                return indexResult;

            files.RemoveAt(assetIndexPos);
        }

        var assetsRoot = layout.GetAssetsRoot(entry);
        var assetIndexId = entry.AssetIndex?.Id ?? entry.Id;
        var assetIndexPath = Path.Combine(assetsRoot, "indexes", $"{assetIndexId}.json");

        if (File.Exists(assetIndexPath)) {
            await using var assetStream = new FileStream(
                assetIndexPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            using var assetDoc = await JsonDocument.ParseAsync(assetStream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

        if (assetDoc.RootElement.TryGetProperty("objects", out var objects)) {
            foreach (var asset in objects.EnumerateObject()) {
                var hash = asset.Value.GetProperty("hash")
                    .GetString()!;

                var size = asset.Value.TryGetProperty("size", out var sizeElement)
                    ? sizeElement.GetInt64()
                    : 0L;

                var assetPath = Path.Combine(assetsRoot, "objects", hash[..2], hash);

                // Asset indexes contain tens of thousands of immutable, content-addressed files.
                // Re-hashing every existing asset on each launch reads the entire asset store.
                if (!NeedsDownload(assetPath, size, hash, verifyHash: false))
                    continue;

                var assetEntry = new DownloadFileEntry {
                    Type = DownloadFileType.Asset,
                    Hash = hash
                };

                files.Add(new DownloadFileEntry {
                    Type = DownloadFileType.Asset,
                    LocalPath = assetPath,
                    Hash = hash,
                    Sha1 = hash,
                    Size = size,
                    Url = _source.GetUrl(assetEntry)
                });
            }
        }
        }

        if (files.Count == 0)
            return new DownloadResponse {
                SuccessCount = 0
            };

        var downloadRequests = new List<DownloadRequest>(files.Count);
        downloadRequests.AddRange(files.Select(BuildRequest));

        return await _downloader.DownloadManyAsync(downloadRequests, _maxConcurrency, _forwardProgress, cancellationToken)
            .ConfigureAwait(false);
    }

private static DownloadRequest BuildRequest(DownloadFileEntry file) {
        var request = new DownloadRequest {
            Url = file.Url,
            LocalPath = file.LocalPath,
            Size = file.Size,
            Sha1 = file.Sha1
        };

        if (file.Type != DownloadFileType.AssetIndex && IsMojangHosted(file.Url)) {
            var mirrorUrl = SourceSelector.GameFileMirrorSource.GetUrl(file);
            if (!string.Equals(file.Url, mirrorUrl, StringComparison.OrdinalIgnoreCase))
                request = request with { AlternateUrls = [mirrorUrl] };
        }

        return request;
    }

    private List<DownloadFileEntry> ResolveFiles(MinecraftEntry entry, IMinecraftLayout layout) {
        var files = new List<DownloadFileEntry>(entry.Libraries.Count + 64);
        var librariesRoot = layout.GetLibrariesRoot(entry);
        var assetsRoot = layout.GetAssetsRoot(entry);

        var versionJarPath = layout.GetVersionJarPath(entry);

        if (entry.ClientDownload is { Url.Length: > 0 } client &&
            NeedsDownload(versionJarPath, client.Size, client.Sha1)) {
            files.Add(new DownloadFileEntry {
                Type = DownloadFileType.ClientJar,
                LocalPath = versionJarPath,
                Url = client.Url,
                Size = client.Size,
                Sha1 = client.Sha1,
                VersionId = entry.Id
            });
        }

        foreach (var library in EnumerateLibraries(entry)) {
            if (library.Natives is { Count: > 0 } natives) {
                AddNativeLibraryDownload(files, librariesRoot, library, natives);
                continue;
            }

            var mavenPath = ResolveLibraryPath(librariesRoot, library);

            if (mavenPath is null)
                continue;

            if (!VersionArgumentRuleParser.IsActive(library.Rules, []))
                continue;

            if (!NeedsDownload(mavenPath, library.Size, library.Sha1))
                continue;

            var relativePath = Path
                .GetRelativePath(librariesRoot, mavenPath)
                .Replace(Path.DirectorySeparatorChar, '/');

            var libEntry = new DownloadFileEntry {
                Type = DownloadFileType.Library,
                LibraryPath = relativePath
            };

            files.Add(new DownloadFileEntry {
                Type = DownloadFileType.Library,
                LocalPath = mavenPath,
                Url = ResolveLibraryUrl(library.Url, libEntry),
                Size = library.Size,
                Sha1 = library.Sha1
            });
        }

        var assetIndexId = entry.AssetIndex?.Id ?? entry.Id;
        var assetIndexPath = Path.Combine(assetsRoot, "indexes", $"{assetIndexId}.json");

        if (entry.AssetIndexUrl is { Length: > 0 } url &&
            NeedsDownload(assetIndexPath, entry.AssetIndex?.Size ?? 0, entry.AssetIndex?.Sha1)) {
            files.Add(new DownloadFileEntry {
                Type = DownloadFileType.AssetIndex,
                LocalPath = assetIndexPath,
                Url = url,
                Size = entry.AssetIndex?.Size ?? 0,
                Sha1 = entry.AssetIndex?.Sha1,
                VersionId = assetIndexId
            });
        }

        return files;
    }

    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        if (_ownsDownloader)
            _downloader.Dispose();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    
    private static bool NeedsDownload(string localPath, long size, string? sha1, bool verifyHash = true) {
        if (!File.Exists(localPath))
            return true;

        var info = new FileInfo(localPath);
        if (size > 0 && info.Length != size)
            return true;

        if (verifyHash && !string.IsNullOrEmpty(sha1) && !FileSha1Matches(localPath, sha1))
            return true;

        return false;
    }

    private static bool FileSha1Matches(string path, string expected) {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.SequentialScan);

        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(stream);
        return string.Equals(Convert.ToHexStringLower(hash), expected, StringComparison.Ordinal);
    }

    private void ForwardProgress(ResourceDownloadProgressChangedEventArgs args) => ProgressChanged?.Invoke(this, args);

    private static IEnumerable<MinecraftLibrary> EnumerateLibraries(MinecraftEntry entry) {
        foreach (var library in entry.Libraries)
            yield return library;

        foreach (var mavenFile in entry.MavenFiles)
            yield return mavenFile;
    }

    private void AddNativeLibraryDownload(
        List<DownloadFileEntry> files,
        string librariesRoot,
        MinecraftLibrary library,
        IReadOnlyDictionary<string, string> natives) {
        if (!VersionArgumentRuleParser.IsActive(library.Rules, []))
            return;

        if (VersionArgumentRuleParser.GetNativeClassifier(natives) is not { } classifier)
            return;

        // The metadata only ships natives for the platforms it declares (e.g. twitch is
        // Windows/macOS only), so don't invent a download for an undeclared classifier.
        if (library.ClassifierUrls is not null && !library.ClassifierUrls.ContainsKey(classifier))
            return;

        var nativeName = $"{library.Name}:{classifier}";
        var nativePath = MavenPathParser.Resolve(librariesRoot, nativeName);
        if (nativePath is null || !NeedsDownload(nativePath, 0, null))
            return;

        var relativePath = Path
            .GetRelativePath(librariesRoot, nativePath)
            .Replace(Path.DirectorySeparatorChar, '/');

        var libEntry = new DownloadFileEntry {
            Type = DownloadFileType.Library,
            LibraryPath = relativePath
        };

        var classifierUrl = library.ClassifierUrls is not null &&
            library.ClassifierUrls.TryGetValue(classifier, out var url)
                ? url
                : null;

        files.Add(new DownloadFileEntry {
            Type = DownloadFileType.Library,
            LocalPath = nativePath,
            Url = ResolveLibraryUrl(classifierUrl, libEntry)
        });
    }

    private static string? ResolveLibraryPath(string librariesRoot, MinecraftLibrary library) {
        if (library.Path is { Length: > 0 } relative)
            return Path.Combine(librariesRoot, relative.Replace('/', Path.DirectorySeparatorChar));

        return MavenPathParser.Resolve(librariesRoot, library.Name);
    }

    private string ResolveLibraryUrl(string? metadataUrl, DownloadFileEntry file) {
        // Third-party metadata (Forge etc.) pins its own download host; use it verbatim.
        // Mojang-hosted artifacts keep flowing through the DownloadSource so mirrors
        // (BmclApi) can rewrite them.
        if (metadataUrl is { Length: > 0 } && !IsMojangHosted(metadataUrl))
            return metadataUrl;

        return _source.GetUrl(file);
    }

    private static bool IsMojangHosted(string url) {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        return uri.Host is "libraries.minecraft.net" or "resources.download.minecraft.net"
            || uri.Host.EndsWith(".mojang.com", StringComparison.Ordinal);
    }
}
