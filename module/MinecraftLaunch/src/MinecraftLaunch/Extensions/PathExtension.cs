using MinecraftLaunch.Base.Models.Game;

namespace MinecraftLaunch.Extensions;

public static class PathExtension {
    public static string ToPath(this string raw) {
        if (!Enumerable.Contains(raw, ' ')) {
            return raw;
        }
        return "\"" + raw + "\"";
    }

    /// <summary>
    /// Gets the path ofthe libraries directory.
    /// </summary>
    /// <param name="entry">The game entry.</param>
    /// <returns>The path of the libraries directory.</returns>
    public static string ToLibrariesPath(this MinecraftEntry entry) =>
        entry.LibrariesDirectoryPath ?? Path.Combine(entry.MinecraftFolderPath, "libraries");

    public static string ToAssetsPath(this MinecraftEntry entry) =>
        entry.AssetsDirectoryPath ?? Path.Combine(entry.MinecraftFolderPath, "assets");

    public static string ToNativesPath(this MinecraftEntry entry) =>
        entry.NativesDirectoryPath ?? Path.Combine(entry.MinecraftFolderPath, "versions", entry.Id, "natives");

    public static string ToWorkingPath(this MinecraftEntry entry, bool isEnableIndependency) => isEnableIndependency
        ? entry.GameDirectoryPath ?? entry.VersionDirectoryPath ?? Path.Combine(entry.MinecraftFolderPath, "versions", entry.Id)
        : entry.MinecraftFolderPath;

    public static string ToLogsPath(this MinecraftEntry entry, bool isEnableIndependency) =>
        Path.Combine(entry.ToWorkingPath(isEnableIndependency), "logs");
}
