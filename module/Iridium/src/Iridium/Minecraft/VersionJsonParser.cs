using System.Text.Json;
using Iridium.Enums;
using Iridium.Models.Minecraft;

namespace Iridium.Minecraft;

public static class VersionJsonParser {
    public static MinecraftArguments? MapArguments(JsonElement root) {
        if (!root.TryGetProperty("arguments", out var arguments) || arguments.ValueKind != JsonValueKind.Object)
            return null;

        return new MinecraftArguments {
            Game = MapArguments(arguments, "game"),
            Jvm = MapArguments(arguments, "jvm")
        };
    }

    /// <summary>
    /// Maps a single <c>arguments.game</c> / <c>arguments.jvm</c> list, resolving the rules
    /// on each item into a <see cref="MinecraftArgument"/>. Callers merge per-layer results
    /// when walking an inheritance chain.
    /// </summary>
    public static List<MinecraftArgument> MapArguments(JsonElement arguments, string key) {
        if (!arguments.TryGetProperty(key, out var list) || list.ValueKind != JsonValueKind.Array)
            return [];

        var result = new List<MinecraftArgument>();
        foreach (var item in list.EnumerateArray()) {
            if (item.ValueKind == JsonValueKind.String) {
                if (item.GetString() is { Length: > 0 } value)
                    result.Add(new MinecraftArgument { Values = [value] });
            } else if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("value", out var valueElement)) {
                var values = new List<string>();
                if (valueElement.ValueKind == JsonValueKind.Array) {
                    foreach (var element in valueElement.EnumerateArray())
                        if (element.GetString() is { Length: > 0 } value)
                            values.Add(value);
                } else if (valueElement.GetString() is { Length: > 0 } value)
                    values.Add(value);

                if (values.Count > 0)
                    result.Add(new MinecraftArgument { Values = values, Rules = MapRules(item) });
            }
        }

        return result;
    }

    /// <summary>
    /// Adds a library while merging version manifests. Unconditional duplicates collapse to
    /// the higher version (mirrors Prism's applyLibrary); platform-rule constrained variants
    /// are all kept so the matching rules can be evaluated at launch time (e.g. jna 5.13.0 for
    /// mac + jna 5.12.1 for windows).
    /// </summary>
    public static void AddLibrary(List<MinecraftLibrary> libraries, MinecraftLibrary library) {
        var key = GetArtifactKey(library.Name);

        // Rule-constrained library: keep it alongside any unconditional or other-rule
        // variants; the launcher filters by platform rules when building the classpath.
        if (library.Rules is { Count: > 0 }) {
            if (!libraries.Any(existing => SameArtifact(existing, key) && (existing.Rules?.Count ?? 0) == 0))
                libraries.Add(library);
            return;
        }

        for (var i = 0; i < libraries.Count; i++) {
            if (!string.Equals(GetArtifactKey(libraries[i].Name), key, StringComparison.Ordinal))
                continue;

            // Existing entry is platform-specific; keep the unconditional one too.
            if (libraries[i].Rules is { Count: > 0 })
                return;

            if (CompareVersions(GetLibraryVersion(library.Name), GetLibraryVersion(libraries[i].Name)) > 0)
                libraries[i] = library;

            return;
        }

        libraries.Add(library);
    }

    private static bool SameArtifact(MinecraftLibrary library, string key) =>
        string.Equals(GetArtifactKey(library.Name), key, StringComparison.Ordinal);

    private static string GetArtifactKey(string name) {
        var parts = name.Split(':');
        if (parts.Length < 2)
            return name;

        // group:artifact[:classifier] -- natives like lwjgl-...:3.3.1:natives-linux must not
        // be collapsed into the plain artifact.
        var key = $"{parts[0]}:{parts[1]}";
        if (parts.Length >= 4)
            key += $":{parts[3]}";

        return key;
    }

    private static string GetLibraryVersion(string name) {
        var parts = name.Split(':');
        return parts.Length >= 3 ? parts[2] : string.Empty;
    }

    private static int CompareVersions(string a, string b) {
        var aParts = a.Split(['.', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        var bParts = b.Split(['.', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        var count = Math.Max(aParts.Length, bParts.Length);

        for (var i = 0; i < count; i++) {
            var x = i < aParts.Length ? aParts[i] : string.Empty;
            var y = i < bParts.Length ? bParts[i] : string.Empty;
            if (x == y)
                continue;

            if (int.TryParse(x, out var xi) && int.TryParse(y, out var yi)) {
                var numeric = xi.CompareTo(yi);
                if (numeric != 0)
                    return numeric;
            } else {
                var ordinal = string.CompareOrdinal(x, y);
                if (ordinal != 0)
                    return ordinal;
            }
        }

        return 0;
    }
    
    public static int? MapJavaVersion(JsonElement root) {
        if (!root.TryGetProperty("javaVersion", out var javaVersion) ||
            !javaVersion.TryGetProperty("majorVersion", out var major) ||
            !major.TryGetInt32(out var value))
            return null;

        return value;
    }

    public static MinecraftVersionType MapType(JsonElement root) {
        if (!root.TryGetProperty("type", out var type) || type.GetString() is not { } value)
            return MinecraftVersionType.Release;

        return value switch {
            "snapshot" => MinecraftVersionType.Snapshot,
            "old_beta" => MinecraftVersionType.OldBeta,
            "old_alpha" => MinecraftVersionType.OldAlpha,
            _ => MinecraftVersionType.Release
        };
    }

    public static DateTime? MapReleaseTime(JsonElement root) {
        if (!root.TryGetProperty("releaseTime", out var releaseTime) || releaseTime.GetString() is not { } value)
            return null;

        return DateTime.TryParse(value, out var parsed) ? parsed : null;
    }

    public static MinecraftEntry MapEntry(JsonElement root, string fallbackId) {
        var id = root.TryGetProperty("id", out var idElement) && idElement.GetString() is { Length: > 0 } value
            ? value
            : fallbackId;

        var libraries = root.TryGetProperty("libraries", out var librariesElement)
            ? MapLibraries(librariesElement)
            : [];

        var (assetId, assetUrl) = MapAssetIndex(root);

        return new MinecraftEntry {
            Id = id,
            Name = id,
            RequiredJavaVersion = MapJavaVersion(root),
            MainClass = root.TryGetProperty("mainClass", out var mainClass) ? mainClass.GetString() : null,
            MinecraftArguments = root.TryGetProperty("minecraftArguments", out var minecraftArguments) ? minecraftArguments.GetString() : null,
            Arguments = MapArguments(root),
            Jar = root.TryGetProperty("jar", out var jar) ? jar.GetString() : null,
            AssetIndex = assetId,
            AssetIndexUrl = assetUrl,
            ClientDownload = MapClientDownload(root),
            Libraries = libraries,
            InheritsFrom = root.TryGetProperty("inheritsFrom", out var inheritsFrom) ? inheritsFrom.GetString() : null,
            Type = MapType(root),
            ReleaseTime = MapReleaseTime(root)
        };
    }

    private static (AssetIndex? Id, string? Url) MapAssetIndex(JsonElement root) {
        if (!root.TryGetProperty("assetIndex", out var assetIndex) || assetIndex.ValueKind != JsonValueKind.Object)
            return (null, null);

        AssetIndex? id = null;
        if (assetIndex.TryGetProperty("id", out var assetId) && assetId.GetString() is { Length: > 0 } assetIndexId) {
            var sha1 = assetIndex.TryGetProperty("sha1", out var sha1Element) ? sha1Element.GetString() : null;
            var size = assetIndex.TryGetProperty("size", out var sizeElement) ? sizeElement.GetInt64() : 0L;
            id = new AssetIndex(assetIndexId, sha1, size);
        }

        var url = assetIndex.TryGetProperty("url", out var urlElement) ? urlElement.GetString() : null;
        return (id, url);
    }

    public static MinecraftFileDownload? MapClientDownload(JsonElement root) {
        if (!root.TryGetProperty("downloads", out var downloads) ||
            downloads.ValueKind != JsonValueKind.Object ||
            !downloads.TryGetProperty("client", out var client) ||
            client.ValueKind != JsonValueKind.Object)
            return null;

        var url = client.TryGetProperty("url", out var urlElement) ? urlElement.GetString() : null;
        if (string.IsNullOrEmpty(url))
            return null;

        return new MinecraftFileDownload {
            Url = url,
            Size = client.TryGetProperty("size", out var sizeElement) ? sizeElement.GetInt64() : 0L,
            Sha1 = client.TryGetProperty("sha1", out var sha1Element) ? sha1Element.GetString() : null
        };
    }

    public static IReadOnlyList<MinecraftLibrary> MapLibraries(JsonElement libraries) {
            if (libraries.ValueKind != JsonValueKind.Array)
                return [];
    
            var result = new List<MinecraftLibrary>();
            var enumerable = libraries.EnumerateArray()
                .Where(library => library.ValueKind == JsonValueKind.Object);
            
            foreach (var library in enumerable) {
                if (!library.TryGetProperty("name", out var nameElement) || nameElement.GetString() is not { Length: > 0 } name)
                    continue;

                // Modrinth/Forge metas mark tooling jars (e.g. ForgeAutoRenamingTool) as
                // excluded from the classpath; including them causes JPMS split-package errors.
                if (library.TryGetProperty("include_in_classpath", out var includeInClasspath) &&
                    includeInClasspath.ValueKind == JsonValueKind.False)
                    continue;

                var (url, path, size, sha1) = MapArtifact(library);

                result.Add(new MinecraftLibrary {
                    Name = name,
                    Url = url,
                    Path = path,
                    Size = size,
                    Sha1 = sha1,
                    ClassifierUrls = MapClassifiers(library),
                    Rules = MapRules(library),
                    Natives = MapNatives(library)
                });
            }
    
            return result;
        }

    private static (string? Url, string? Path, long Size, string? Sha1) MapArtifact(JsonElement element) {
        if (element.TryGetProperty("downloads", out var downloads) &&
            downloads.ValueKind == JsonValueKind.Object &&
            downloads.TryGetProperty("artifact", out var artifact) &&
            artifact.ValueKind == JsonValueKind.Object)
        {
            var url = artifact.TryGetProperty("url", out var urlElement)
                ? urlElement.GetString()
                : null;

            var path = artifact.TryGetProperty("path", out var pathElement)
                ? pathElement.GetString()
                : null;

            var size = artifact.TryGetProperty("size", out var sizeElement)
                ? sizeElement.GetInt64()
                : 0L;

            var sha1 = artifact.TryGetProperty("sha1", out var sha1Element)
                ? sha1Element.GetString()
                : null;

            return (url, path, size, sha1);
        }

        // Legacy metas (Forge 1.7.10 etc.) put a repository base on the library itself
        // instead of downloads.artifact; the artifact then lives at <url><maven-path>.
        if (element.TryGetProperty("url", out var baseUrlElement) &&
            baseUrlElement.GetString() is { Length: > 0 } baseUrl &&
            element.TryGetProperty("name", out var nameElement) &&
            nameElement.GetString() is { Length: > 0 } name &&
            MavenPathParser.GetRelativePath(name) is { } relative)
        {
            return (JoinUrl(baseUrl, relative), relative, 0L, null);
        }

        return (null, null, 0L, null);
    }

    private static Dictionary<string, string>? MapClassifiers(JsonElement element) {
        if (!element.TryGetProperty("downloads", out var downloads) ||
            downloads.ValueKind != JsonValueKind.Object ||
            !downloads.TryGetProperty("classifiers", out var classifiers) ||
            classifiers.ValueKind != JsonValueKind.Object)
            return null;

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var classifier in classifiers.EnumerateObject()) {
            if (classifier.Value.TryGetProperty("url", out var urlElement) &&
                urlElement.GetString() is { Length: > 0 } url)
                result[classifier.Name] = url;
        }

        return result.Count > 0 ? result : null;
    }

    private static string JoinUrl(string baseUrl, string relativePath) {
        var normalized = baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";
        return normalized + relativePath;
    }
    
    public static IReadOnlyList<string> MapTweakers(JsonElement root) {
        if (!root.TryGetProperty("tweakers", out var tweakers) || tweakers.ValueKind != JsonValueKind.Array)
            return [];

        var result = new List<string>();
        foreach (var tweaker in tweakers.EnumerateArray())
            if (tweaker.GetString() is { Length: > 0 } value)
                result.Add(value);

        return result;
    }

    private static Dictionary<string, string>? MapNatives(JsonElement element) {
            if (!element.TryGetProperty("natives", out var natives) || natives.ValueKind != JsonValueKind.Object)
                return null;
    
            var result = new Dictionary<string, string>();
            foreach (var native in natives.EnumerateObject())
                result[native.Name] = native.Value.GetString() ?? string.Empty;
    
            return result;
    }
    
    private static List<CompatibilityRule>? MapRules(JsonElement element) {
            if (!element.TryGetProperty("rules", out var rules) || rules.ValueKind != JsonValueKind.Array || rules.GetArrayLength() == 0)
                return null;
    
            var result = new List<CompatibilityRule>();
            var allRules = rules.EnumerateArray().Where(rule => rule.ValueKind == JsonValueKind.Object);
            
            foreach (var rule in allRules) {
                var action = rule.TryGetProperty("action", out var actionElement) && actionElement.GetString() == "disallow"
                    ? CompatibilityRuleAction.Disallow
                    : CompatibilityRuleAction.Allow;
    
                string? osName = null;
                string? osVersion = null;
                string? osArch = null;
                if (rule.TryGetProperty("os", out var os) && os.ValueKind == JsonValueKind.Object) {
                    osName = os.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                    osVersion = os.TryGetProperty("version", out var versionElement) ? versionElement.GetString() : null;
                    osArch = os.TryGetProperty("arch", out var archElement) ? archElement.GetString() : null;
                }
    
                IReadOnlyDictionary<string, bool>? features = null;
                if (rule.TryGetProperty("features", out var featureElement) && featureElement.ValueKind == JsonValueKind.Object) {
                    var featuresDict = new Dictionary<string, bool>();
                    foreach (var feature in featureElement.EnumerateObject())
                        featuresDict[feature.Name] = feature.Value.ValueKind == JsonValueKind.True;
                    
                    features = featuresDict;
                }
    
                result.Add(new CompatibilityRule {
                    Action = action,
                    OsName = osName,
                    OsVersion = osVersion,
                    OsArch = osArch,
                    Features = features
                });
            }
    
            return result.Count > 0 ? result : null;
        }
}