using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using MinecraftLaunch;
using MinecraftLaunch.Base.Enums;
using MinecraftLaunch.Base.Models.Network;
using MinecraftLaunch.Components.Downloader;
using MinecraftLaunch.Utilities;
using SulfurLauncher.Core.Minecraft.Instance.Java;
using SulfurLauncher.Localization;
using SharpCompress.Compressors.Xz;

namespace SulfurLauncher.Core.Minecraft.Services;

public sealed record JavaDistribution(string Vendor, string Product, IReadOnlyList<JavaDistributionVersion> Versions)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Product) ? Vendor : $"{Vendor} · {Product}";
}

public sealed record JavaDistributionVersion(
    int MajorVersion,
    string FullVersion,
    string Vendor,
    string Product,
    string Url,
    string Sha256,
    long Size,
    string ArchiveName);

public sealed record JavaInstallProgress(
    string Stage,
    double? Fraction,
    long DownloadedBytes,
    long TotalBytes,
    double SpeedBytesPerSecond);

public delegate void JavaInstallProgressHandler(JavaInstallProgress progress);

public static class JavaDistributionService
{
    private const string FeedUrl = "https://download.jetbrains.com/jdk/feed/v1/jdks.json.xz";

    private const string MojangRuntimeIndexUrl =
        "https://piston-meta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";

    private static readonly SemaphoreSlim FeedLock = new(1, 1);
    private static IReadOnlyList<JavaDistributionVersion>? FeedCache;
    private static HttpClient Client => HttpUtil.Client;

    public static async Task<IReadOnlyList<JavaDistribution>> GetDistributionsAsync(
        CancellationToken cancellationToken = default)
    {
        var entries = await GetFeedAsync(cancellationToken);
        return entries.GroupBy(x => x.Vendor, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x =>
                new JavaDistribution(x.Key, x.First().Product, x.OrderByDescending(v => v.MajorVersion).ToList()))
            .ToList();
    }

    public static async Task<IReadOnlyList<JavaDistributionVersion>> GetVersionsAsync(string vendor,
        CancellationToken cancellationToken = default)
    {
        return (await GetFeedAsync(cancellationToken))
            .Where(x => string.Equals(x.Vendor, vendor, StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.MajorVersion).Select(x => x.First()).OrderByDescending(x => x.MajorVersion).ToList();
    }

    public static async Task<JavaDistributionVersion?> GetFastestVersionAsync(int majorVersion,
        CancellationToken cancellationToken = default)
    {
        var candidates = (await GetFeedAsync(cancellationToken)).Where(x => x.MajorVersion == majorVersion).ToList();
        if (candidates.Count == 0) return null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        var measurements = await Task.WhenAll(candidates.Select(async candidate =>
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, candidate.Url);
                request.Headers.Range = new RangeHeaderValue(0, 0);
                using var response =
                    await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                return (Candidate: candidate,
                    Elapsed: response.IsSuccessStatusCode ? Stopwatch.GetElapsedTime(started) : TimeSpan.MaxValue);
            }
            catch
            {
                return (Candidate: candidate, Elapsed: TimeSpan.MaxValue);
            }
        }));
        return measurements.OrderBy(x => x.Elapsed).First().Candidate;
    }

    public static async Task<JavaRuntimeEntry> InstallAsync(JavaDistributionVersion version, string runtimesPath,
        string temporaryPath, JavaInstallProgressHandler? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(runtimesPath);
        var baseName = SanitizeName($"{version.Vendor}-{version.MajorVersion}");
        var target = GetUniqueDirectory(Path.Combine(runtimesPath, baseName));
        var staging = target + $".{Guid.NewGuid():N}.installing";
        var urlPath = new Uri(version.Url).AbsolutePath;
        var extension = urlPath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
            ? ".tar.gz"
            : Path.GetExtension(urlPath);
        if (string.IsNullOrWhiteSpace(extension)) extension = ".zip";
        var archive = Path.Combine(temporaryPath, $"java-{Guid.NewGuid():N}{extension}");
        Directory.CreateDirectory(Path.GetDirectoryName(archive)!);
        try
        {
            await DownloadArchiveAsync(version, archive, progress, cancellationToken);
            if (!string.IsNullOrWhiteSpace(version.Sha256))
            {
                progress?.Invoke(new JavaInstallProgress(CommonLanguageManager.Instance.javaDistribution_verifyStage.CurrentValue(), null, 0, 0, 0));
                await using var hashStream = File.OpenRead(archive);
                var actual = Convert.ToHexString(await SHA256.HashDataAsync(hashStream, cancellationToken));
                if (!actual.Equals(version.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(CommonLanguageManager.Instance.javaDistribution_sha256VerificationFailed.CurrentValue());
            }

            progress?.Invoke(new JavaInstallProgress(CommonLanguageManager.Instance.javaDistribution_extractStage.CurrentValue(), null, 0, 0, 0));
            Directory.CreateDirectory(staging);
            await ExtractAsync(archive, staging, cancellationToken);
            var root = FindRuntimeRoot(staging);
            Directory.Move(root, target);
            var executable = FindJavaExecutable(target);
            await PrepareRuntimeForExecutionAsync(executable, cancellationToken);
            var runtime = await JavaRuntimeManager.FromPathAsync(executable, cancellationToken);
            if (runtime is null)
            {
                // FromPathAsync runs `java -version` to detect the runtime. On some
                // environments (e.g. macOS 27 beta with SIP/AMFI disabled) Java 9+
                // crashes on startup with SIGBUS, so detection returns null even
                // though the runtime was extracted correctly. Fall back to the
                // feed metadata so the installation does not fail.
                runtime = new JavaRuntimeEntry
                {
                    JavaPath = executable,
                    JavaType = version.Vendor,
                    JavaVersion = version.FullVersion,
                    MajorVersion = version.MajorVersion,
                    Is64Bit = true
                };
            }
            return runtime;
        }
        finally
        {
            TryDelete(archive);
            TryDelete(staging);
        }
    }

    public static async Task<JavaRuntimeEntry?> InstallMojangAsync(int majorVersion, string runtimesPath,
        JavaInstallProgressHandler? progress = null, CancellationToken cancellationToken = default)
    {
        var component = majorVersion switch
        {
            8 => "jre-legacy", 16 => "java-runtime-alpha", 17 => "java-runtime-gamma",
            21 => "java-runtime-delta", 25 => "java-runtime-epsilon", _ => null
        };
        var arm64 = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        var platform = OperatingSystem.IsWindows() ? arm64 ? "windows-arm64" : "windows-x64"
            : OperatingSystem.IsMacOS() ? arm64 ? "mac-os-arm64" : "mac-os"
            : arm64 ? null : "linux";
        if (component is null || platform is null) return null;

        progress?.Invoke(new JavaInstallProgress(CommonLanguageManager.Instance.javaDistribution_fetchMetadataStage.CurrentValue(), null, 0, 0, 0));
        using var index = await GetJsonAsync(MojangRuntimeIndexUrl, cancellationToken);
        if (!index.RootElement.TryGetProperty(platform, out var platformNode) ||
            !platformNode.TryGetProperty(component, out var components) || components.GetArrayLength() == 0)
            return null;
        var manifestUrl = components[0].GetProperty("manifest").GetProperty("url").GetString()!;
        using var manifest = await GetJsonAsync(manifestUrl, cancellationToken);

        var target = GetUniqueDirectory(Path.Combine(runtimesPath, $"Mojang-{majorVersion}"));
        var staging = target + $".{Guid.NewGuid():N}.installing";
        Directory.CreateDirectory(staging);
        var stopwatch = Stopwatch.StartNew();
        long totalBytes = 0;
        long downloadedBytes = 0;
        try
        {
            var files = new List<(string Name, JsonElement Element)>();
            foreach (var entry in manifest.RootElement.GetProperty("files").EnumerateObject())
            {
                var type = entry.Value.GetProperty("type").GetString();
                var path = SafeRuntimePath(staging, entry.Name);
                if (type == "directory")
                {
                    Directory.CreateDirectory(path);
                    continue;
                }

                if (type != "file") continue;
                totalBytes += entry.Value.GetProperty("downloads").GetProperty("raw").GetProperty("size").GetInt64();
                files.Add((entry.Name, entry.Value));
            }

            using var semaphore = new SemaphoreSlim(Math.Max(1, DownloadManager.MaxThread));
            var tasks = new List<Task>(files.Count);
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        var path = SafeRuntimePath(staging, file.Name);
                        var raw = file.Element.GetProperty("downloads").GetProperty("raw");
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        await DownloadFileVerifiedAsync(raw.GetProperty("url").GetString()!, path,
                            raw.GetProperty("sha1").GetString()!, raw.GetProperty("size").GetInt64(),
                            received =>
                            {
                                var downloaded = Interlocked.Add(ref downloadedBytes, received);
                                var elapsed = stopwatch.Elapsed.TotalSeconds;
                                progress?.Invoke(new JavaInstallProgress(CommonLanguageManager.Instance.javaDistribution_downloadStage.CurrentValue(),
                                    totalBytes > 0 ? Math.Clamp((double)downloaded / totalBytes, 0, 1) : null,
                                    downloaded, totalBytes, downloaded / Math.Max(1.0, elapsed)));
                            }, cancellationToken);
                        if (!OperatingSystem.IsWindows() &&
                            file.Element.TryGetProperty("executable", out var executable) &&
                            executable.GetBoolean())
                            File.SetUnixFileMode(path, File.GetUnixFileMode(path) | UnixFileMode.UserExecute |
                                                       UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, CancellationToken.None));
            }

            await Task.WhenAll(tasks);
            var finalElapsed = stopwatch.Elapsed.TotalSeconds;
            progress?.Invoke(new JavaInstallProgress(CommonLanguageManager.Instance.javaDistribution_completeStage.CurrentValue(), 1, downloadedBytes, totalBytes,
                downloadedBytes / Math.Max(1.0, finalElapsed)));
            Directory.Move(staging, target);
            var javaPath = FindJavaExecutable(target);
            await PrepareRuntimeForExecutionAsync(javaPath, cancellationToken);
            var mojangRuntime = await JavaRuntimeManager.FromPathAsync(javaPath, cancellationToken);
            if (mojangRuntime is not null) return mojangRuntime;

            // Same fallback as InstallAsync: if `java -version` cannot run (e.g.
            // SIGBUS on macOS 27 beta with SIP disabled), construct the entry from
            // the known component metadata instead of failing the installation.
            var fullVersion = majorVersion.ToString();
            var releaseFile = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(javaPath)!, "..", "release"));
            if (File.Exists(releaseFile))
            {
                foreach (var line in await File.ReadAllLinesAsync(releaseFile, cancellationToken))
                {
                    if (line.StartsWith("JAVA_VERSION=", StringComparison.Ordinal))
                    {
                        fullVersion = line["JAVA_VERSION=".Length..].Trim().Trim('"');
                        break;
                    }
                }
            }

            return new JavaRuntimeEntry
            {
                JavaPath = javaPath,
                JavaType = "Mojang",
                JavaVersion = fullVersion,
                MajorVersion = majorVersion,
                Is64Bit = true
            };
        }
        finally
        {
            TryDelete(staging);
        }
    }

    private static async Task<IReadOnlyList<JavaDistributionVersion>> GetFeedAsync(CancellationToken cancellationToken)
    {
        if (FeedCache is not null) return FeedCache;
        await FeedLock.WaitAsync(cancellationToken);
        try
        {
            if (FeedCache is not null) return FeedCache;
            var bytes = await Client.GetByteArrayAsync(FeedUrl, cancellationToken);
            using var input = new MemoryStream(bytes);
            Stream decoded = input;
            if (bytes.Length > 6 && bytes[0] == 0xFD && bytes[1] == 0x37 && bytes[2] == 0x7A)
                decoded = new XZStream(input);
            using var document = await JsonDocument.ParseAsync(decoded, cancellationToken: cancellationToken);
            var values = new List<JavaDistributionVersion>();
            foreach (var item in document.RootElement.GetProperty("jdks").EnumerateArray())
            {
                var vendor = item.GetProperty("vendor").GetString() ?? "Unknown";
                var product = item.TryGetProperty("product", out var productValue)
                    ? productValue.GetString() ?? ""
                    : "";
                var major = item.GetProperty("jdk_version_major").GetInt32();
                var full = item.GetProperty("jdk_version").GetString() ?? major.ToString();
                if (!item.TryGetProperty("packages", out var packages)) continue;
                foreach (var package in packages.EnumerateArray())
                {
                    if (package.GetProperty("os").GetString() != FeedOs() ||
                        package.GetProperty("arch").GetString() != FeedArch()) continue;
                    values.Add(new JavaDistributionVersion(major, full, vendor, product,
                        package.GetProperty("url").GetString()!, package.GetProperty("sha256").GetString() ?? "",
                        package.GetProperty("archive_size").GetInt64(),
                        package.GetProperty("install_folder_name").GetString() ?? $"java-{major}"));
                    break;
                }
            }

            return FeedCache = values;
        }
        finally
        {
            FeedLock.Release();
        }
    }

    private static async Task DownloadArchiveAsync(JavaDistributionVersion version, string destination,
        JavaInstallProgressHandler? progress, CancellationToken cancellationToken)
    {
        var request = new DownloadRequest(version.Url, destination, version.Size)
        {
            ProgressChanged = e => progress?.Invoke(new JavaInstallProgress(CommonLanguageManager.Instance.javaDistribution_downloadStage.CurrentValue(),
                e.TotalBytes > 0 ? Math.Clamp((double)e.DownloadedBytes / e.TotalBytes, 0, 1) : null,
                e.DownloadedBytes, e.TotalBytes, e.Speed))
        };
        var result = await new DefaultDownloader().DownloadAsync(request, cancellationToken);
        if (result.Type == DownloadResultType.Cancelled)
            throw new OperationCanceledException(cancellationToken);
        if (result.Type != DownloadResultType.Successful)
            throw result.Exception ?? new IOException(CommonLanguageManager.Instance.javaDistribution_downloadFailed.CurrentValue());
    }

    private static async Task DownloadFileVerifiedAsync(string url, string destination, string sha1, long size,
        Action<long> progressCallback, CancellationToken cancellationToken)
    {
        var maxRetries = Math.Max(1, DownloadManager.MaxRetryCount);
        Exception? lastError = null;
        for (var attempt = 1; attempt <= maxRetries && !cancellationToken.IsCancellationRequested; attempt++)
        {
            try
            {
                if (File.Exists(destination)) File.Delete(destination);
                var request = new DownloadRequest(url, destination, size)
                {
                    ProgressChanged = e => progressCallback(Math.Max(0, e.DownloadedBytes))
                };
                var result =
                    await new DefaultDownloader { MaxRetryCount = 1 }.DownloadAsync(request, cancellationToken);
                if (result.Type == DownloadResultType.Cancelled)
                    throw new OperationCanceledException(cancellationToken);
                if (result.Type != DownloadResultType.Successful)
                    throw result.Exception ?? new IOException(CommonLanguageManager.Instance.javaDistribution_runtimeFileDownloadFailed.CurrentValue());
                await using var stream = File.OpenRead(destination);
                var actual = Convert.ToHexString(await SHA1.HashDataAsync(stream, cancellationToken));
                if (actual.Equals(sha1, StringComparison.OrdinalIgnoreCase)) return;
                lastError = new InvalidDataException(CommonLanguageManager.Instance.javaDistribution_runtimeFileSha1Failed.CurrentValue());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                lastError = exception;
            }

            if (attempt < maxRetries)
                await Task.Delay(TimeSpan.FromMilliseconds(1000 * attempt), cancellationToken);
        }

        throw lastError ?? new IOException(CommonLanguageManager.Instance.javaDistribution_runtimeFileDownloadFailed.CurrentValue());
    }

    private static async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        await using var stream = await Client.GetStreamAsync(url, cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static string SafeRuntimePath(string root, string relative)
    {
        var fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(CommonLanguageManager.Instance.javaDistribution_manifestInvalidPath.CurrentValue());
        return full;
    }

    private static string FeedOs()
    {
        return OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macOS" : "linux";
    }

    private static string FeedArch()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "aarch64",
            _ => "x86_64"
        };
    }

    private static async Task ExtractAsync(string archive, string destination, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destination);

        if (archive.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            // ZipFile.ExtractToDirectory handles path traversal checks internally.
            // Use overwriteFiles: true so partial/duplicate entries do not abort extraction.
            ZipFile.ExtractToDirectory(archive, destination, overwriteFiles: true);
            return;
        }

        // Prefer the native tar command on non-Windows platforms — it is the most
        // robust for the variety of JDK .tar.gz distributions (Microsoft/Amazon/
        // Azul/Eclipse/etc.) and handles ./ prefixes, long names, permissions,
        // symlinks and multi-member gzip streams correctly.
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                await ExtractWithNativeTarAsync(archive, destination, cancellationToken);
                return;
            }
            catch
            {
                // Fall through to managed extraction below.
            }
        }

        await ExtractWithManagedTarAsync(archive, destination, cancellationToken);
    }

    private static async Task ExtractWithNativeTarAsync(string archive, string destination, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "tar",
            ArgumentList = { "-xzf", archive, "-C", destination },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start native tar process.");

        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"tar extraction failed (exit {process.ExitCode}): {error}");
        }
    }

    private static async Task ExtractWithManagedTarAsync(string archive, string destination, CancellationToken cancellationToken)
    {
        await using var file = File.OpenRead(archive);
        await using var gzip = new GZipStream(file, CompressionMode.Decompress);
        // Buffer into a seekable MemoryStream because some GZipStream versions
        // do not reliably signal end-of-stream for multi-member archives, which
        // can cause TarFile to stop early or throw on trailing data.
        await using var ms = new MemoryStream();
        await gzip.CopyToAsync(ms, cancellationToken);
        ms.Position = 0;
        TarFile.ExtractToDirectory(ms, destination, overwriteFiles: true);
    }

    private static string FindRuntimeRoot(string staging)
    {
        // Look for the directory that actually contains bin/java (or bin/java.exe)
        // rather than assuming the archive has exactly one top-level directory.
        // Some distributions ship extra top-level entries (e.g. legal/, release)
        // which would make SingleOrDefault() throw.
        var javaName = OperatingSystem.IsWindows() ? "java.exe" : "java";

        // Direct match: staging/bin/java
        if (File.Exists(Path.Combine(staging, "bin", javaName)))
            return staging;

        // Search one level deep — most JDK archives extract to a single folder.
        foreach (var dir in Directory.EnumerateDirectories(staging))
        {
            if (File.Exists(Path.Combine(dir, "bin", javaName)))
                return dir;

            // macOS JDKs use <dir>/Contents/Home/bin/java
            var nested = Path.Combine(dir, "Contents", "Home", "bin", javaName);
            if (File.Exists(nested))
                return Path.Combine(dir, "Contents", "Home");
        }

        // Fall back to the single top-level directory if present.
        return Directory.EnumerateDirectories(staging).SingleOrDefault() ?? staging;
    }

    private static string FindJavaExecutable(string root)
    {
        var binDir = Path.Combine(root, "bin");
        var candidates = OperatingSystem.IsWindows() ? new[] { "javaw.exe", "java.exe" } : new[] { "java" };
        foreach (var name in candidates)
        {
            var path = Path.Combine(binDir, name);
            if (File.Exists(path)) return path;
        }

        // Fallback: recursive search for unusual layouts.
        var found = candidates.SelectMany(name =>
                Directory.EnumerateFiles(root, name, SearchOption.AllDirectories))
            .FirstOrDefault();
        return found ?? throw new InvalidDataException(CommonLanguageManager.Instance.javaDistribution_noExecutable.CurrentValue());
    }

    private static async Task PrepareRuntimeForExecutionAsync(string executable, CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows()) return;

        // Ensure the java binary (and sibling executables) are executable.
        try
        {
            var mode = File.GetUnixFileMode(executable);
            File.SetUnixFileMode(executable, mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
        }
        catch
        {
            // chmod via SetUnixFileMode may fail on some filesystems; fall back to /bin/chmod.
            try
            {
                using var chmod = Process.Start(new ProcessStartInfo
                {
                    FileName = "chmod",
                    ArgumentList = { "+x", executable },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (chmod is not null)
                    await chmod.WaitForExitAsync(cancellationToken);
            }
            catch
            {
                // Best-effort: ignore permission errors.
            }
        }

        if (!OperatingSystem.IsMacOS()) return;

        // Strip the quarantine extended attribute so Gatekeeper does not block
        // the downloaded binary, and ad-hoc sign it so it can be launched.
        var homeDir = Directory.GetParent(Path.GetDirectoryName(executable)!)?.FullName ?? executable;
        foreach (var tool in new[] { "xattr", "codesign" })
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = tool,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                if (tool == "xattr")
                {
                    psi.ArgumentList.Add("-dr");
                    psi.ArgumentList.Add("com.apple.quarantine");
                    psi.ArgumentList.Add(homeDir);
                }
                else
                {
                    psi.ArgumentList.Add("--force");
                    psi.ArgumentList.Add("--deep");
                    psi.ArgumentList.Add("--sign");
                    psi.ArgumentList.Add("-");
                    psi.ArgumentList.Add(executable);
                }

                using var process = Process.Start(psi);
                if (process is not null)
                    await process.WaitForExitAsync(cancellationToken);
            }
            catch
            {
                // Best-effort: ignore tool errors.
            }
        }
    }

    private static string GetUniqueDirectory(string path)
    {
        for (var i = 0; Directory.Exists(path); i++)
            path = i == 0 ? path + "-1" : path[..path.LastIndexOf('-')] + $"-{i + 1}";
        return path;
    }

    private static string SanitizeName(string value)
    {
        return string.Concat(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        catch
        {
        }
    }
}