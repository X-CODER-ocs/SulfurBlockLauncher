using Iridium.Models.Minecraft;

namespace Iridium.Extension.Minecraft.Layout;

/// <summary>
/// CurseForge keeps its shared metadata under an <c>Install/</c> directory: version
/// manifests/jars in <c>Install/versions/&lt;id&gt;</c>, libraries in
/// <c>Install/libraries</c> and assets in <c>Install/assets</c>. Each instance (game
/// directory) lives under <c>Instances/&lt;name&gt;</c> with its native libraries in a
/// local <c>natives</c> folder.
/// </summary>
public sealed class CurseForgeLayout : SharedMetadataLayout {
    public override string Format => "CurseForge";

    protected override string InstanceFolder => "Instances";

    protected override string MetadataFolder => "Install";

    public override string GetNativesDirectory(MinecraftEntry entry) => Path.Combine(entry.InstancePath, "natives");
}
