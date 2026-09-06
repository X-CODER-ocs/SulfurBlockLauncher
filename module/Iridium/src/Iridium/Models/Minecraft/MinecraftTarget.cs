using Iridium.Minecraft.Layout;
using Iridium.Interfaces;

namespace Iridium.Models.Minecraft;

public sealed record MinecraftTarget {
    public required DirectoryInfo Root { get; init; }
    public required IMinecraftLayout Layout { get; init; }

    public string Format => Layout.Format;

    public static MinecraftTarget Create(
        DirectoryInfo root,
        IMinecraftLayout? layout = null) {
        ArgumentNullException.ThrowIfNull(root);

        return new MinecraftTarget {
            Root = root,
            Layout = layout ?? new StandardLayout()
        };
    }
}