using IFormatProvider = Iridium.Interfaces.IFormatProvider;
using Iridium.Extension.Minecraft.Layout;
using Iridium.Extension.Minecraft.Parsing;
using Iridium.Minecraft;
using Iridium.Models.Minecraft;
using Microsoft.Data.Sqlite;

namespace Iridium.Extension.Minecraft.Formats;

/// <summary>
/// Scans Modrinth App installations. Instance metadata (profile path, game version and
/// loader) is stored in the launcher's <c>app.db</c> SQLite database; the resolved version
/// manifest lives in <c>meta/versions/&lt;id&gt;</c> as a fully merged (or inherited)
/// Mojang-style JSON.
/// </summary>
public sealed class ModrinthProvider : IFormatProvider {
    public string Id => "Modrinth";

    public int Priority => 60;

    public bool CanResolve(DirectoryInfo root) =>
        File.Exists(Path.Combine(root.FullName, "app.db"));

    public async ValueTask<MinecraftContext?> GetAsync(DirectoryInfo root, string instanceId, CancellationToken ct = default) {
        // Modrinth profiles are enumerated through the launcher database; a single profile
        // directory carries no metadata of its own, so resolve via enumeration + filter.
        var instances = await GetMinecraftsAsync(root, ct);
        return instances.FirstOrDefault(c => string.Equals(c.Entry.Id, instanceId, StringComparison.Ordinal));
    }

    public async ValueTask<IReadOnlyList<MinecraftContext>> GetMinecraftsAsync(DirectoryInfo root, CancellationToken ct = default) {
        var databasePath = Path.Combine(root.FullName, "app.db");
        if (!File.Exists(databasePath))
            return [];

        var contexts = new List<MinecraftContext>();
        try {
            await using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;Pooling=False");
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                                   SELECT i.path, i.name, c.game_version, c.loader, c.loader_version
                                   FROM instances i
                                   LEFT JOIN instance_content_sets c ON c.id = i.applied_content_set_id
                                   WHERE i.install_stage = 'installed'
                                   """;
            await using var reader = await command.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct)) {
                var profilePath = reader.IsDBNull(0) ? null : reader.GetString(0);
                var displayName = reader.IsDBNull(1) ? null : reader.GetString(1);
                var gameVersion = reader.IsDBNull(2) ? null : reader.GetString(2);
                var loaderVersion = reader.IsDBNull(4) ? null : reader.GetString(4);

                if (string.IsNullOrWhiteSpace(profilePath) || string.IsNullOrWhiteSpace(gameVersion))
                    continue;

                var profileDir = ResolveProfilePath(root.FullName, profilePath);
                if (!Directory.Exists(profileDir))
                    continue;

                var versionId = ResolveVersionId(gameVersion, loaderVersion);
                var entry = await ParseAsync(profileDir, displayName, gameVersion, versionId, root.FullName, ct);
                if (entry is null && versionId != gameVersion)
                    entry = await ParseAsync(profileDir, displayName, gameVersion, gameVersion, root.FullName, ct);
                if (entry is not null)
                    contexts.Add(Wrap(new DirectoryInfo(profileDir), entry));
            }
        } catch (SqliteException) {
            return [];
        } catch (IOException) {
            return [];
        }

        return contexts;
    }

    private static MinecraftContext Wrap(DirectoryInfo dir, MinecraftEntry entry) => new() {
        Format = "Modrinth",
        Layout = new ModrinthLayout(),
        Entry = entry,
    };

    private async Task<MinecraftEntry?> ParseAsync(
        string profileDir,
        string? displayName,
        string gameVersion,
        string versionId,
        string launcherRoot,
        CancellationToken ct) {
        var metadataRoot = Path.Combine(launcherRoot, "meta");
        var merger = new VersionManifestMerger(id => Path.Combine(metadataRoot, "versions", id, $"{id}.json"));
        var merged = await merger.MergeAsync(versionId, ct);
        if (merged is null)
            return null;

        // The Modrinth metadata is fully merged, but some version manifests still carry
        // an inheritsFrom; loaders are detected from the manifest libraries themselves.
        var id = Path.GetFileName(profileDir);

        return new MinecraftEntry {
            Id = id,
            Name = displayName ?? id,
            MinecraftVersion = gameVersion,
            VersionId = versionId,
            InstancePath = profileDir,
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
            Loaders = merged.Loaders
        };
    }

    private static string ResolveProfilePath(string launcherRoot, string profilePath) {
        if (Path.IsPathRooted(profilePath))
            return profilePath;

        var profilesRoot = Path.Combine(launcherRoot, "profiles");
        var directPath = Path.Combine(launcherRoot, profilePath);
        if (Path.GetFullPath(directPath).StartsWith(Path.GetFullPath(profilesRoot) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            return directPath;

        return Path.Combine(profilesRoot, profilePath);
    }

    private static string ResolveVersionId(string gameVersion, string? loaderVersion) {
        if (string.IsNullOrWhiteSpace(loaderVersion))
            return gameVersion;

        // Modrinth stores loader installs under <game>-<loaderVersion> (e.g. 1.20.1-47.4.10).
        return $"{gameVersion}-{loaderVersion}";
    }
}
