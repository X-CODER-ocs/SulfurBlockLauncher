using System.Text.Json;
using IFormatProvider = Iridium.Interfaces.IFormatProvider;
using Iridium.Enums;
using Iridium.Minecraft.Layout;
using Iridium.Models.Minecraft;

namespace Iridium.Minecraft.Formats;

/// <summary>
/// Scans the traditional launcher layout where every version lives under a
/// <c>versions/</c> directory ({root}/versions/{id}/{id}.json).
/// </summary>
public sealed class StandardMinecraftProvider : IFormatProvider {
    private const int MaxScanConcurrency = 2;

    public string Id => "Standard";

    public int Priority => 100;

    public bool CanResolve(DirectoryInfo root) =>
        Directory.Exists(Path.Combine(root.FullName, "versions"));

    public async ValueTask<MinecraftContext?> GetAsync(DirectoryInfo root, string instanceId, CancellationToken ct = default) {
        var dir = new DirectoryInfo(Path.Combine(root.FullName, "versions", instanceId));
        if (!dir.Exists)
            return null;

        var entry = await ParseAsync(dir, root.FullName, ct);
        return entry is null ? null : Wrap(dir, entry);
    }

    public async ValueTask<IReadOnlyList<MinecraftContext>> GetMinecraftsAsync(DirectoryInfo root, CancellationToken ct = default) {
        var versionsDir = new DirectoryInfo(Path.Combine(root.FullName, "versions"));
        if (!versionsDir.Exists)
            return [];

        var directories = versionsDir.EnumerateDirectories().ToArray();
        var contexts = new MinecraftContext?[directories.Length];
        await Parallel.ForEachAsync(Enumerable.Range(0, directories.Length),
            new ParallelOptions { MaxDegreeOfParallelism = MaxScanConcurrency, CancellationToken = ct },
            async (index, cancellationToken) => {
                var dir = directories[index];
                var entry = await ParseAsync(dir, root.FullName, cancellationToken);
                if (entry is not null)
                    contexts[index] = Wrap(dir, entry);
            });

        return contexts.OfType<MinecraftContext>().ToArray();
    }

    private static MinecraftContext Wrap(DirectoryInfo dir, MinecraftEntry entry) => new() {
        Format = "Standard",
        Layout = new StandardLayout(),
        Entry = entry,
    };

    private async Task<MinecraftEntry?> ParseAsync(DirectoryInfo dir, string launcherRoot, CancellationToken ct) {
        var jsonPath = Path.Combine(dir.FullName, $"{dir.Name}.json");
        if (!File.Exists(jsonPath))
            return null;

        if (File.Exists(Path.Combine(dir.FullName, ".pclignore")))
            return null;

        var json = await File.ReadAllTextAsync(jsonPath, ct);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var entry = await ResolveInheritedEntryAsync(dir.Name, root, launcherRoot, ct);
        return entry with {
            MinecraftVersion = await ResolveVersionAsync(entry.Id, root, launcherRoot, ct),
            RequiredJavaVersion = await ResolveJavaVersionAsync(root, launcherRoot, ct),
            InstancePath = dir.FullName,
            Loaders = ModLoaderDetector.DetectFromLibraries(
                root.TryGetProperty("libraries", out var librariesElement) ? librariesElement : default)
        };
    }

    private async Task<MinecraftEntry> ResolveInheritedEntryAsync(
        string fallbackId,
        JsonElement root,
        string launcherRoot,
        CancellationToken ct,
        int depth = 0) {
        var entry = VersionJsonParser.MapEntry(root, fallbackId);
        if (depth >= 16 || entry.InheritsFrom is not { Length: > 0 } parentId)
            return entry;

        var parentJsonPath = Path.Combine(launcherRoot, "versions", parentId, $"{parentId}.json");
        if (!File.Exists(parentJsonPath))
            return entry;

        var parentJson = await File.ReadAllTextAsync(parentJsonPath, ct);
        using var parentDocument = JsonDocument.Parse(parentJson);
        var parent = await ResolveInheritedEntryAsync(parentId, parentDocument.RootElement, launcherRoot, ct, depth + 1);

        var libraries = parent.Libraries.ToList();
        foreach (var library in entry.Libraries)
            VersionJsonParser.AddLibrary(libraries, library);

        var arguments = MergeArguments(parent.Arguments, entry.Arguments);
        return entry with {
            MainClass = entry.MainClass ?? parent.MainClass,
            MinecraftArguments = entry.MinecraftArguments ?? parent.MinecraftArguments,
            Arguments = arguments,
            Libraries = libraries,
            AssetIndex = entry.AssetIndex ?? parent.AssetIndex,
            AssetIndexUrl = entry.AssetIndexUrl ?? parent.AssetIndexUrl,
            ClientDownload = entry.ClientDownload ?? parent.ClientDownload,
            Jar = entry.Jar ?? parent.Jar,
            RequiredJavaVersion = entry.RequiredJavaVersion ?? parent.RequiredJavaVersion,
            Type = entry.Type == MinecraftVersionType.Release && parent.Type != MinecraftVersionType.Release
                ? parent.Type
                : entry.Type,
            ReleaseTime = entry.ReleaseTime ?? parent.ReleaseTime
        };
    }

    private async Task<string> ResolveVersionAsync(
        string fallbackId,
        JsonElement root,
        string launcherRoot,
        CancellationToken ct,
        int depth = 0) {
        if (depth > 8)
            return fallbackId;

        // Modded versions (Forge/Fabric/OptiFine...) inherit from the vanilla version JSON.
        if (root.TryGetProperty("inheritsFrom", out var inherits) && inherits.GetString() is { Length: > 0 } parentId) {
            var parentJsonPath = Path.Combine(launcherRoot, "versions", parentId, $"{parentId}.json");

            if (File.Exists(parentJsonPath)) {
                var parentJson = await File.ReadAllTextAsync(parentJsonPath, ct);
                using var parentDocument = JsonDocument.Parse(parentJson);
                return await ResolveVersionAsync(parentId, parentDocument.RootElement, launcherRoot, ct, depth + 1);
            }
        }

        // PCL writes the real Minecraft version into clientVersion.
        if (root.TryGetProperty("clientVersion", out var clientVersion) &&
            clientVersion.GetString() is { Length: > 0 } pclVersion)
            return pclVersion;

        // HMCL stores the real Minecraft version in the "game" patch.
        if (root.TryGetProperty("patches", out var patches) && patches.ValueKind == JsonValueKind.Array) {
            var patchesEnumerable = patches.EnumerateArray().Where(patch => patch.ValueKind == JsonValueKind.Object);

            foreach (var patch in patchesEnumerable) {
                if (!patch.TryGetProperty("id", out var patchId) || patchId.GetString() != "game")
                    continue;

                if (patch.TryGetProperty("version", out var patchVersion) && patchVersion.GetString() is { Length: > 0 } hmclVersion)
                    return hmclVersion;
            }
        }

        return fallbackId;
    }

    private async Task<int?> ResolveJavaVersionAsync(
        JsonElement root,
        string launcherRoot,
        CancellationToken ct,
        int depth = 0) {
        if (depth > 8)
            return null;

        // The declared javaVersion is authoritative; loaders (Forge etc.) often inherit it
        // from the vanilla version they extend, so keep walking the chain while it is absent.
        if (VersionJsonParser.MapJavaVersion(root) is { } required)
            return required;

        if (root.TryGetProperty("inheritsFrom", out var inherits) && inherits.GetString() is { Length: > 0 } parentId) {
            var parentJsonPath = Path.Combine(launcherRoot, "versions", parentId, $"{parentId}.json");

            if (File.Exists(parentJsonPath)) {
                var parentJson = await File.ReadAllTextAsync(parentJsonPath, ct);
                using var parentDocument = JsonDocument.Parse(parentJson);
                return await ResolveJavaVersionAsync(parentDocument.RootElement, launcherRoot, ct, depth + 1);
            }
        }

        return null;
    }

    private static MinecraftArguments? MergeArguments(MinecraftArguments? parent, MinecraftArguments? child) {
        if (parent is null) return child;
        if (child is null) return parent;

        return new MinecraftArguments {
            Game = [.. parent.Game, .. child.Game],
            Jvm = [.. parent.Jvm, .. child.Jvm]
        };
    }
}
