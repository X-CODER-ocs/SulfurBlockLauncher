using System.Text.Json;
using Iridium.Enums;
using IFormatProvider = Iridium.Interfaces.IFormatProvider;
using Iridium.Extension.Minecraft.Layout;
using Iridium.Extension.Minecraft.Parsing;
using Iridium.Minecraft;
using Iridium.Models.Minecraft;

namespace Iridium.Extension.Minecraft.Formats;

/// <summary>
/// Scans CurseForge launcher installations. Each instance under
/// <c>Instances/&lt;name&gt;</c> carries a <c>minecraftinstance.json</c> declaring the game
/// version and base mod loader (e.g. <c>forge-47.4.1</c>); the loader's version manifest
/// lives in <c>Install/versions</c> and may inherit from the vanilla manifest.
/// </summary>
public sealed class CurseForgeProvider : IFormatProvider {
    public string Id => "CurseForge";

    public int Priority => 70;

    public bool CanResolve(DirectoryInfo root) =>
        Directory.Exists(Path.Combine(root.FullName, "Instances")) &&
        Directory.Exists(Path.Combine(root.FullName, "Install"));

    public async ValueTask<MinecraftContext?> GetAsync(DirectoryInfo root, string instanceId, CancellationToken ct = default) {
        var instanceDir = Path.Combine(root.FullName, "Instances", instanceId);
        if (!Directory.Exists(instanceDir))
            return null;

        var entry = await ParseAsync(instanceDir, root.FullName, ct);
        return entry is null ? null : Wrap(new DirectoryInfo(instanceDir), entry);
    }

    public async ValueTask<IReadOnlyList<MinecraftContext>> GetMinecraftsAsync(DirectoryInfo root, CancellationToken ct = default) {
        var instancesRoot = Path.Combine(root.FullName, "Instances");
        if (!Directory.Exists(instancesRoot))
            return [];

        var contexts = new List<MinecraftContext>();
        foreach (var dir in Directory.EnumerateDirectories(instancesRoot)) {
            var entry = await ParseAsync(dir, root.FullName, ct);
            if (entry is not null)
                contexts.Add(Wrap(new DirectoryInfo(dir), entry));
        }

        return contexts;
    }

    private static MinecraftContext Wrap(DirectoryInfo dir, MinecraftEntry entry) => new() {
        Format = "CurseForge",
        Layout = new CurseForgeLayout(),
        Entry = entry,
    };

    private async Task<MinecraftEntry?> ParseAsync(string instanceDir, string launcherRoot, CancellationToken ct) {
        var metadataPath = Path.Combine(instanceDir, "minecraftinstance.json");
        if (!File.Exists(metadataPath))
            return null;

        var json = await File.ReadAllTextAsync(metadataPath, ct);
        using var document = JsonDocument.Parse(json);
        var meta = document.RootElement;

        if (meta.TryGetProperty("isValid", out var valid) && !valid.GetBoolean())
            return null;
        if (meta.TryGetProperty("isEnabled", out var enabled) && !enabled.GetBoolean())
            return null;

        var gameVersion = meta.TryGetProperty("gameVersion", out var gameVersionElement)
            ? gameVersionElement.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(gameVersion))
            return null;

        var versionId = gameVersion;
        var loader = "vanilla";
        string? loaderVersion = null;
        if (meta.TryGetProperty("baseModLoader", out var baseLoader) && baseLoader.ValueKind == JsonValueKind.Object) {
            if (baseLoader.TryGetProperty("name", out var loaderNameElement) && loaderNameElement.GetString() is { Length: > 0 } loaderName)
                versionId = loaderName;
            loaderVersion = baseLoader.TryGetProperty("forgeVersion", out var forgeVersionElement)
                ? forgeVersionElement.GetString()
                : null;
            loader = versionId;
        }

        var installRoot = Path.Combine(launcherRoot, "Install");
        var merger = new VersionManifestMerger(id => Path.Combine(installRoot, "versions", id, $"{id}.json"));
        var merged = await merger.MergeAsync(versionId, ct);
        if (merged is null)
            return null;

        var id = Path.GetFileName(instanceDir);
        var name = meta.TryGetProperty("name", out var nameElement)
            ? nameElement.GetString() ?? id
            : id;

        return new MinecraftEntry {
            Id = id,
            Name = name,
            MinecraftVersion = merged.MinecraftVersion.Length > 0 ? merged.MinecraftVersion : versionId,
            InstancePath = instanceDir,
            InheritsFrom = merged.InheritsFrom,
            MainClass = merged.MainClass,
            Arguments = merged.Arguments,
            MinecraftArguments = merged.MinecraftArguments,
            Libraries = merged.Libraries,
            RequiredJavaVersion = merged.RequiredJavaVersion,
            AssetIndex = merged.AssetIndex,
            AssetIndexUrl = merged.AssetIndexUrl,
            ClientDownload = merged.ClientDownload,
            Type = merged.Type,
            ReleaseTime = merged.ReleaseTime,
            Loaders = merged.Loaders.Count > 0
                ? merged.Loaders
                : ParseLoader(loader, loaderVersion)
        };
    }

    private static IReadOnlyList<MinecraftLoader> ParseLoader(string loader, string? loaderVersion) {
        if (loader.Contains("neoforge", StringComparison.OrdinalIgnoreCase))
            return [new MinecraftLoader { Type = LoaderType.NeoForge, Version = loaderVersion ?? string.Empty }];
        if (loader.Contains("forge", StringComparison.OrdinalIgnoreCase))
            return [new MinecraftLoader { Type = LoaderType.Forge, Version = loaderVersion ?? string.Empty }];
        if (loader.Contains("fabric", StringComparison.OrdinalIgnoreCase))
            return [new MinecraftLoader { Type = LoaderType.Fabric, Version = loaderVersion ?? string.Empty }];
        if (loader.Contains("quilt", StringComparison.OrdinalIgnoreCase))
            return [new MinecraftLoader { Type = LoaderType.Quilt, Version = loaderVersion ?? string.Empty }];

        return [];
    }
}
