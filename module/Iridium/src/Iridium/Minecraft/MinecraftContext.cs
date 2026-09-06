using Iridium.Minecraft.Layout;
using Iridium.Models.Minecraft;
using Iridium.Interfaces;

namespace Iridium.Minecraft;

/// <summary>
/// A fully resolved, already-existing Minecraft instance: the format that produced it, the
/// layout it maps to and the parsed entry. This is the common language shared by the
/// provider and the argument parser. It never represents an uninstalled Minecraft — use
/// <see cref="MinecraftTarget"/> for an install target.
/// </summary>
public sealed record MinecraftContext {
    /// <summary>Format identifier, e.g. "Standard", "Prism", "CurseForge".</summary>
    public required string Format { get; init; }

    /// <summary>Directory mapping of this instance.</summary>
    public required IMinecraftLayout Layout { get; init; }

    /// <summary>Parsed instance data.</summary>
    public required MinecraftEntry Entry { get; init; }

    /// <summary>Format-specific metadata bag, used by providers when needed.</summary>
    public object? Metadata { get; init; }

    /// <summary>
    /// Instance Root: the physical directory of this instance within the Game Root
    /// (e.g. <c>.minecraft/versions/1.21.8</c>).
    /// </summary>
    public DirectoryInfo? Root =>
        string.IsNullOrEmpty(Entry.InstancePath) ? null : new DirectoryInfo(Entry.InstancePath);
}
