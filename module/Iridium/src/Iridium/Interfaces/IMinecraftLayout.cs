using Iridium.Models.Minecraft;

namespace Iridium.Interfaces;

public interface IMinecraftLayout {
    /// <summary>
    /// Format identity of this layout, e.g. "Standard", "Prism", "CurseForge". The layout is
    /// the single source of the Minecraft format: it defines both the directory layout and the
    /// format it belongs to.
    /// </summary>
    string Format { get; }

    /// <summary>
    /// Root-relative path of an instance directory, e.g. "versions/1.21".
    /// </summary>
    string GetInstanceDirectory(string id);

    string GetInstanceRoot(MinecraftEntry entry);
    string GetGameDirectory(MinecraftEntry entry);
    string GetLibrariesRoot(MinecraftEntry entry);
    string GetAssetsRoot(MinecraftEntry entry);
    string GetNativesDirectory(MinecraftEntry entry);
    string GetVersionJarPath(MinecraftEntry entry);
    string GetVersionJsonPath(MinecraftEntry entry);
}
