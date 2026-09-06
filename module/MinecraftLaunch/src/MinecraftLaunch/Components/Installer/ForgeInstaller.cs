using Flurl.Http;
using MinecraftLaunch.Base.Enums;
using MinecraftLaunch.Base.Interfaces;
using MinecraftLaunch.Base.Models.Game;
using MinecraftLaunch.Base.Models.Network;
using MinecraftLaunch.Components.Downloader;
using MinecraftLaunch.Components.Parser;
using MinecraftLaunch.Extensions;
using MinecraftLaunch.Utilities;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MinecraftLaunch.Components.Installer;

/// <summary>
/// Forge（Neo）通用安装器
/// </summary>
public sealed class ForgeInstaller : InstallerBase {
    public string CustomId { get; init; }
    public string JavaPath { get; init; }
    public ForgeInstallEntry Entry { get; init; }
    public override string MinecraftFolder { get; init; }
    public MinecraftEntry InheritedMinecraft { get; init; }

    public static ForgeInstaller Create(string folder, string javaPath, ForgeInstallEntry installEntry, string customId = default) {
        return new ForgeInstaller {
            CustomId = customId,
            JavaPath = javaPath,
            Entry = installEntry,
            MinecraftFolder = folder
        };
    }

    public override async Task<MinecraftEntry> InstallAsync(CancellationToken cancellationToken = default) {
        FileInfo forgePackageFile = default;
        MinecraftEntry inheritedEntry = default;
        ModifiedMinecraftEntry entry = default;

        ReportProgress(InstallStep.Started, 0.0d, TaskStatus.WaitingToRun, 1, 1);

        try
        {
            inheritedEntry = ParseMinecraft(cancellationToken);
            forgePackageFile = await DownloadForgePackageAsync(cancellationToken);

            var (package, installProfile, isLegacy) = ParseForgePackage(forgePackageFile.FullName, cancellationToken);
            using (package)
            using(var doc = installProfile){
                var forgeClientFile =
                    await WriteVersionJsonAndSomeDependenciesAsync(isLegacy, doc.RootElement, package,
                        cancellationToken);

                entry = ParseModifiedMinecraft(forgeClientFile, inheritedEntry, cancellationToken);
                await CompleteForgeDependenciesAsync(isLegacy, doc.RootElement, entry, cancellationToken);

                if (!isLegacy)
                {
                    await RunInstallProcessorAsync(forgePackageFile.FullName, doc.RootElement, entry, cancellationToken);
                }
            }

            ReportProgress(InstallStep.RanToCompletion, 1.0d, TaskStatus.RanToCompletion, 1, 1);
            ReportCompleted(true);
        }
        catch (Exception ex)
        {
            ReportProgress(InstallStep.Interrupted, 1.0d, TaskStatus.Faulted, 1, 1);
            ReportCompleted(false, ex);
            throw;
        }
        return entry;
    }

    /// <summary>提前下载加载器安装包，使其可与原版资源下载并行。</summary>
    public Task PreloadAsync(CancellationToken cancellationToken = default) => DownloadForgePackageAsync(cancellationToken);

    public static async Task<IEnumerable<ForgeInstallEntry>> EnumerableForgeAsync(string mcVersion, bool isNeoforge = false, CancellationToken cancellationToken = default) {
        var artifact = isNeoforge && mcVersion != "1.20.1" ? "neoforge" : "forge";
        var packagesUrl = isNeoforge
            ? $"https://maven.neoforged.net/releases/net/neoforged/{artifact}/maven-metadata.xml"
            : "https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml";
        await using var xml = await HttpUtil.Request(packagesUrl).GetStreamAsync(cancellationToken: cancellationToken);
        var document = await XDocument.LoadAsync(xml, LoadOptions.None, cancellationToken);
        var versions = document.Root?.Element("versioning")?.Element("versions")?.Elements("version")
            .Select(element => element.Value).Where(version => MatchesMinecraftVersion(version, mcVersion, isNeoforge))
            .ToArray() ?? [];
        IEnumerable<ForgeInstallEntry> entries = versions.Select((version, index) => new ForgeInstallEntry {
            Build = index,
            McVersion = mcVersion,
            ForgeVersion = isNeoforge && mcVersion != "1.20.1" ? version : version[(mcVersion.Length + 1)..],
            IsNeoforge = isNeoforge
        });

        // NeoForge 列表接口不返回 build 字段（全部为 0），需按版本号语义排序才能让最新版排在最前。
        entries = isNeoforge
            ? entries.OrderByDescending(entry => entry, new NeoForgeVersionComparer(mcVersion))
            : entries.OrderByDescending(entry => entry.Build);

        foreach (var entry in entries)
            entry.IsNeoforge = isNeoforge;

        return entries;

        static bool MatchesMinecraftVersion(string version, string minecraftVersion, bool neoForge) {
            if (!neoForge || minecraftVersion == "1.20.1")
                return version.StartsWith(minecraftVersion + "-", StringComparison.Ordinal);
            var expectedPrefix = minecraftVersion.StartsWith("1.", StringComparison.Ordinal)
                ? minecraftVersion[2..] + "." : minecraftVersion + ".";
            return version.StartsWith(expectedPrefix, StringComparison.Ordinal);
        }
    }

    #region Privates

    private MinecraftEntry ParseMinecraft(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ReportProgress(InstallStep.ParseMinecraft, 0.15d, TaskStatus.Running, 1, 0);

        if (InheritedMinecraft is not null) {
            return InheritedMinecraft;
        }

        var inheritedMinecraft = new MinecraftParser(MinecraftFolder).GetMinecrafts()
            .FirstOrDefault(x => x.Version.VersionId == Entry.McVersion);

        ReportProgress(InstallStep.ParseMinecraft, 0.30d, TaskStatus.Running, 1, 1);
        return inheritedMinecraft ?? throw new InvalidOperationException("The corresponding version's parent was not found."); ;
    }

    private async Task<FileInfo> DownloadForgePackageAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ReportProgress(InstallStep.DownloadPackage, 0.30d, TaskStatus.Running, 1, 0);

        string packageUrl;
        if (Entry.IsNeoforge) {
            string prefix = Entry.McVersion is "1.20.1" ? "forge" : "neoforge";
            string packageVersion = Entry.McVersion is "1.20.1" ? $"{Entry.McVersion}-{Entry.ForgeVersion}" : Entry.ForgeVersion;
            packageUrl = $"https://maven.neoforged.net/releases/net/neoforged/{prefix}/"
                + packageVersion + $"/{prefix}-{packageVersion}-installer.jar";
        } else {
            List<string> identifiers = [Entry.McVersion, Entry.ForgeVersion];
            string loaderVersion = string.Join('-', identifiers);

            packageUrl = $"https://maven.minecraftforge.net/releases/net/minecraftforge/forge/"
                + loaderVersion + $"/forge-{loaderVersion}-installer.jar";
        }

        string fileName = Entry.IsNeoforge
            ? $"neoforge-{Entry.ForgeVersion}-installer.jar"
            : $"forge-{Entry.McVersion}-{Entry.ForgeVersion}" +
                $"{(string.IsNullOrEmpty(Entry.Branch) ? string.Empty : $"-{Entry.Branch}")}" +
                $"-installer.jar";

        var packageFile = new FileInfo(Path.Combine(MinecraftFolder, fileName));
        if (packageFile.Exists) {
            ReportProgress(InstallStep.DownloadPackage, 0.45d, TaskStatus.Running, 1, 1);
            return packageFile;
        }
        var downloadRequest = new DownloadRequest(packageUrl, packageFile.FullName);

        var downloadResult = await new DefaultDownloader()
            .DownloadAsync(downloadRequest, cancellationToken);
        if (downloadResult.Type == DownloadResultType.Cancelled)
            throw new OperationCanceledException(cancellationToken);
        if (downloadResult.Type is DownloadResultType.Failed) throw downloadResult.Exception;
        ReportProgress(InstallStep.DownloadPackage, 0.45d, TaskStatus.Running, 1, 1);

        return packageFile;
    }

    private (ZipArchive package, /*注意需要释放*/ JsonDocument installProfile, bool isLegacy) ParseForgePackage(string packageFilePath, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ReportProgress(InstallStep.ParsePackage, 0.45d, TaskStatus.Running, 1, 0);

        var packageArchive = ZipFile.OpenRead(packageFilePath);
        using var install_profile_json_stream = packageArchive.GetEntry("install_profile.json")?.Open()??throw new Exception("Failed to parse install_profile.json");
        // 这里转交所有权,不释放
        var installProfileNode = JsonDocument.Parse(install_profile_json_stream);
        var isLegacyForgeVersion = installProfileNode.RootElement.TryGetProperty("install"u8,out _);

        ReportProgress(InstallStep.ParsePackage, 0.50d, TaskStatus.Running, 1, 1);
        return (packageArchive, installProfileNode, isLegacyForgeVersion);
    }

    private async Task<FileInfo> WriteVersionJsonAndSomeDependenciesAsync(bool isLegacyForgeVersion, JsonElement installProfile, ZipArchive packageArchive, CancellationToken cancellationToken) {
        string forgeVersion = $"{Entry.McVersion}-{Entry.ForgeVersion}";
        string forgeLibsFolder = Path.Combine(MinecraftFolder, "libraries\\net\\minecraftforge\\forge", forgeVersion);

        ReportProgress(InstallStep.WriteVersionJsonAndSomeDependencies, 0.50d, TaskStatus.Running, 1, 0);

        if (isLegacyForgeVersion) {
            if(!installProfile.GetProperty("install"u8).TryGetProperty("filePath"u8,out var filePath))throw new InvalidDataException("Unable to resolve location of universal file in archive");
            
            var universalFileEntry = packageArchive.GetEntry(filePath.GetString()!)
                ?? throw new FileNotFoundException("The universal file was not found in the archive");

            universalFileEntry.ExtractTo(Path.Combine(forgeLibsFolder, universalFileEntry.Name.Replace("-universal", string.Empty,StringComparison.Ordinal)));
        }

        if (packageArchive.GetEntry($"maven/net/minecraftforge/forge/{forgeVersion}/") != null)
            foreach (var entry in packageArchive.Entries.Where(x => !x.FullName.EndsWith('/') && x.FullName.StartsWith($"maven/net/minecraftforge/forge/{forgeVersion}", StringComparison.Ordinal)))
                entry.ExtractTo(Path.Combine(forgeLibsFolder, entry.Name));

        if (packageArchive.GetEntry("data/client.lzma") is { } clientData)
        {
            var clientDataMavenName = TryGetClientDataMavenName(installProfile);
            var clientDataPath = clientDataMavenName is null
                ? Path.Combine(forgeLibsFolder, $"forge-{forgeVersion}-clientdata.lzma")
                : Path.Combine(MinecraftFolder, "libraries", clientDataMavenName.FormatLibraryNameToRelativePath());
            clientData.ExtractTo(clientDataPath);
        }
        
        var jsonContent = (isLegacyForgeVersion
                              ? installProfile.GetProperty("versionInfo"u8).GetString()
                              : packageArchive.GetEntry("version.json")?.ReadAsString())
                          ?? throw new Exception("Failed to read version.json");
        // not TO DO,据测量这里的开销占比较小,不是很值得手写patch
        var jsonNode = JsonNode.Parse(jsonContent);
        
        
        string entryId = CustomId ?? $"{Entry.McVersion}-{(Entry.IsNeoforge ? "neoforge" : "forge")}-{Entry.ForgeVersion}";
        var jsonFile = new FileInfo(Path.Combine(MinecraftFolder, "versions", entryId, $"{entryId}.json"));

        if (!jsonFile.Directory!.Exists)
            jsonFile.Directory.Create();

        jsonNode!["id"] = entryId;
        await File.WriteAllTextAsync(jsonFile.FullName, jsonNode.ToJsonString(), cancellationToken);

        ReportProgress(InstallStep.WriteVersionJsonAndSomeDependencies, 0.60d, TaskStatus.Running, 1, 1);
        return jsonFile;
    }

    private async Task CompleteForgeDependenciesAsync(bool isLegacyForgeVersion, JsonElement installProfile, MinecraftEntry minecraft, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ReportProgress(InstallStep.DownloadLibraries, 0.50d, TaskStatus.Running, 1, 0);

        string forgeVersion = $"{Entry.McVersion}-{Entry.ForgeVersion}";
        var dependencies = new List<MinecraftLibrary>();

        var libraries = minecraft.GetRequiredLibraries().Libraries.ToList();
        foreach (var lib in libraries.Where(x => x.MavenName.Equals($"net.minecraftforge:forge:{forgeVersion}")
            || x.MavenName.Equals($"net.minecraftforge:forge:{forgeVersion}:client") || x is not IDownloadDependency).ToArray())
            libraries.Remove(lib);

        dependencies.AddRange(libraries);

        if (!isLegacyForgeVersion) {
            var processorLibraries = installProfile.GetProperty("libraries"u8)
                .Deserialize(LibraryEntryContext.Default.IEnumerableLibraryEntry)?
                .Select(lib => MinecraftLibrary.ParseJsonNode(lib, MinecraftFolder))
                ?? throw new InvalidDataException();

            foreach (var item in processorLibraries)
                if (!dependencies.Contains(item))
                    dependencies.Add(item);
        }

        // 优先从本地资源目录复制，仅复制失败的进入下载清单。
        var dependenciesToDownload = new ConcurrentBag<MinecraftLibrary>();
        var totalCopyCandidates = dependencies.Count(dependency => dependency is IDownloadDependency);
        var copiedCount = 0;
        Parallel.ForEach(dependencies, new ParallelOptions {
            MaxDegreeOfParallelism = 4,
            CancellationToken = cancellationToken
        }, dependency => {
            if (dependency is IDownloadDependency
                && MinecraftResourceDownloader.TryCopyDependencyFromSources(dependency, SourceRootDirectories, cancellationToken)) {
                var finished = Interlocked.Increment(ref copiedCount);
                ReportProgress(InstallStep.CopyLibraries, (finished / (double)Math.Max(1, totalCopyCandidates))
                        .ToPercentage(0.50d, 0.60d),
                    TaskStatus.Running, totalCopyCandidates, finished);
                return;
            }
            dependenciesToDownload.Add(dependency);
        });

        var groupDownloadRequest = new GroupDownloadRequest(dependenciesToDownload.OfType<IDownloadDependency>()
            .Select(x => new DownloadRequest(DownloadManager.BmclApi.TryFindUrl(x.Url), x.FullPath)));

        groupDownloadRequest.ProgressChanged = args
            => ReportProgress(InstallStep.DownloadLibraries, args.Percentage.ToPercentage(0.60d, 0.70d), 
                    TaskStatus.Running, args.TotalCount, args.CompletedCount, args.Speed, true);

        var groupDownloadResult = await new DefaultDownloader()
            .DownloadManyAsync(groupDownloadRequest, cancellationToken);
        if (groupDownloadResult.Failed.Any())
            throw new IOException($"Failed to download {groupDownloadResult.Failed.Count()} Forge dependencies.");
    }

    private async Task RunInstallProcessorAsync(string packageFilePath, JsonElement installProfile, MinecraftEntry entry, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        Dictionary<string, Dictionary<string, string>> forgeDataDictionary = 
            installProfile.GetProperty("data"u8).Deserialize(ForgeInstallerContext.Default.DictionaryStringDictionaryStringString)
            ?? throw new Exception("Failed to parse install profile data");

        string forgeVersion = $"{Entry.McVersion}-{Entry.ForgeVersion}";

        if (forgeDataDictionary.TryGetValue("BINPATCH", out Dictionary<string, string> value)) {
            var clientDataMavenName = TryGetClientDataMavenName(installProfile);
            value["client"] = $"[{clientDataMavenName ?? $"net.minecraftforge:forge:{forgeVersion}:clientdata@lzma"}]";
            value["server"] = $"[net.minecraftforge:forge:{forgeVersion}:serverdata@lzma]";
        }

        var replaceValues = new Dictionary<string, string> {
            { "{SIDE}", "client" },
            { "{MINECRAFT_JAR}", entry.ClientJarPath },
            { "{MINECRAFT_VERSION}", Entry.McVersion },
            { "{ROOT}", MinecraftFolder },
            { "{INSTALLER}", packageFilePath },
            { "{LIBRARY_DIR}", Path.Combine(MinecraftFolder, "libraries") }
        };

        var replaceProcessorArgs = forgeDataDictionary.ToDictionary(
            kvp => $"{{{kvp.Key}}}", kvp => {
                var value = kvp.Value["client"];
                if (!value.StartsWith('[')) return value;

                return Path.Combine(MinecraftFolder, "libraries", value.TrimStart('[').TrimEnd(']')
                    .FormatLibraryNameToRelativePath());
            });
        if (!installProfile.TryGetProperty("processors"u8,out var processorsEntry)) throw new InvalidDataException("Unable to parse Forge Processors");
        var forgeProcessors = processorsEntry
            .Deserialize(ForgeInstallerContext.Default.IEnumerableForgeProcessorData)?
            .Where(x => !(x.Sides.Count == 1 && x.Sides.Contains("server")))
            .ToArray()
            ?? throw new InvalidDataException("Unable to parse Forge Processors");

        int count = 0;
        int totalCount = forgeProcessors.Length;

        ReportProgress(InstallStep.RunInstallProcessor, 0.70d, TaskStatus.Running, totalCount, count);
        foreach (var processor in forgeProcessors) {
            cancellationToken.ThrowIfCancellationRequested();

            processor.Args = processor.Args.Select(x => {
                if (x.StartsWith('['))
                    return Path.Combine(MinecraftFolder, "libraries", x.TrimStart('[').TrimEnd(']').FormatLibraryNameToRelativePath());

                return x.ReplaceFromDictionary(replaceProcessorArgs)
                    .ReplaceFromDictionary(replaceValues);
            });

            processor.Outputs = processor.Outputs.ToDictionary(
                kvp => kvp.Key.ReplaceFromDictionary(replaceProcessorArgs),
                kvp => kvp.Value.ReplaceFromDictionary(replaceProcessorArgs));

            var fileName = Path.Combine(MinecraftFolder, "libraries", processor.Jar.FormatLibraryNameToRelativePath());

            using var fileArchive = ZipFile.OpenRead(fileName);
            string mainClass = fileArchive.GetEntry("META-INF/MANIFEST.MF")?
                .ReadAsString()
                .Split("\r\n".ToCharArray())
                .FirstOrDefault(x => x.Contains("Main-Class: "))
                ?.Replace("Main-Class: ", string.Empty)
                ?? throw new InvalidDataException("Unable to find MainClass for Processor");

            string classPath = string.Join(Path.PathSeparator.ToString(), new List<string>() { fileName }
                .Concat(processor.Classpath.Select(x => Path.Combine(MinecraftFolder, "libraries", x.FormatLibraryNameToRelativePath()))));

            var args = new List<string> {
                "-cp",
                classPath,
                mainClass
            };

            args.AddRange(processor.Args);

            var startInfo = new ProcessStartInfo(JavaPath) {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = MinecraftFolder,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            foreach (var argument in args)
                startInfo.ArgumentList.Add(argument);
            using var process = Process.Start(startInfo) ?? throw new Exception("Failed to start Java");
//            TODO Maybe it is Xilu's Todo event 
//            List<string> _errorOutputs = [];
//
//            process.ErrorDataReceived += (_, arg) => {
//                if (arg.Data is not null && !string.IsNullOrEmpty(arg.Data))
//                    _errorOutputs.Add(arg.Data);
//            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Forge installation processor exited with code {process.ExitCode}.");

            var missingOutput = processor.Outputs.Keys.FirstOrDefault(path => !File.Exists(path));
            if (missingOutput is not null)
                throw new FileNotFoundException("Forge installation processor did not produce an expected output.",
                    missingOutput);

            ReportProgress(InstallStep.RunInstallProcessor, ((double)count / (double)totalCount).ToPercentage(0.75d, 0.95d),
                 TaskStatus.Running, totalCount, Interlocked.Increment(ref count));
        }

        if (replaceProcessorArgs.TryGetValue("{PATCHED}", out var patchedClientPath)
            && !File.Exists(patchedClientPath))
            throw new FileNotFoundException("Forge installation did not produce the patched client.", patchedClientPath);
    }

    private static string TryGetClientDataMavenName(JsonElement installProfile)
    {
        if (!installProfile.TryGetProperty("data"u8, out var data)
            || !data.TryGetProperty("PATCHED"u8, out var patched)
            || !patched.TryGetProperty("client"u8, out var client))
            return null;

        var patchedMavenName = client.GetString()?.Trim('[', ']');
        if (string.IsNullOrWhiteSpace(patchedMavenName))
            return null;

        var parts = patchedMavenName.Split(':');
        return parts.Length >= 3 ? $"{parts[0]}:{parts[1]}:{parts[2]}:clientdata@lzma" : null;
    }

    private static ModifiedMinecraftEntry ParseModifiedMinecraft(FileInfo file, MinecraftEntry inheritedEntry,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var entry = MinecraftParser.Parse(file.Directory, [inheritedEntry], out var _) as ModifiedMinecraftEntry;

        return entry ?? throw new InvalidOperationException("An incorrect modified entry was encountered");
    }

    #endregion
}

public record ForgeProcessorData {
    [JsonPropertyName("jar")] public string Jar { get; set; } = null!;
    [JsonPropertyName("sides")] public List<string> Sides { get; set; } = [];
    [JsonPropertyName("args")] public IEnumerable<string> Args { get; set; } = null!;
    [JsonPropertyName("classpath")] public IEnumerable<string> Classpath { get; set; } = null!;
    [JsonPropertyName("outputs")] public Dictionary<string, string> Outputs { get; set; } = [];
}

[JsonSerializable(typeof(IEnumerable<ForgeProcessorData>))]
[JsonSerializable(typeof(Dictionary<string, Dictionary<string, string>>))]
internal sealed partial class ForgeInstallerContext : JsonSerializerContext;

/// <summary>
/// 按 NeoForge 版本号语义比较（如 1.20.1-47.1.85 与 21.1.247），预发布版本排在正式版本之后。
/// </summary>
internal sealed class NeoForgeVersionComparer(string mcVersion) : IComparer<ForgeInstallEntry>
{
    public int Compare(ForgeInstallEntry left, ForgeInstallEntry right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left is null) return -1;
        if (right is null) return 1;

        var leftVersion = ParseVersion(left);
        var rightVersion = ParseVersion(right);
        int length = Math.Max(leftVersion.Components.Length, rightVersion.Components.Length);
        for (int i = 0; i < length; i++)
        {
            int comparison = (i < leftVersion.Components.Length ? leftVersion.Components[i] : 0)
                .CompareTo(i < rightVersion.Components.Length ? rightVersion.Components[i] : 0);
            if (comparison != 0) return comparison;
        }

        if (leftVersion.IsPreRelease != rightVersion.IsPreRelease)
            return leftVersion.IsPreRelease ? -1 : 1;

        return string.CompareOrdinal(left.ForgeVersion, right.ForgeVersion);
    }

    private (int[] Components, bool IsPreRelease) ParseVersion(ForgeInstallEntry entry)
    {
        var version = entry.ForgeVersion;
        if (string.IsNullOrWhiteSpace(version)) return ([], false);

        var loaderVersion = version;
        if (loaderVersion.StartsWith(mcVersion + "-", StringComparison.Ordinal))
            loaderVersion = loaderVersion[(mcVersion.Length + 1)..];

        int dashIndex = loaderVersion.IndexOf('-');
        var numericPart = dashIndex >= 0 ? loaderVersion[..dashIndex] : loaderVersion;

        var components = new List<int>();
        foreach (var part in numericPart.Split('.'))
            if (int.TryParse(part, out int number))
                components.Add(number);

        return (components.ToArray(), dashIndex >= 0);
    }
}
