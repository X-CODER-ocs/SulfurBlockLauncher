using Iridium.Models.Minecraft;

namespace Iridium.Extension.Minecraft.Layout;

/// <summary>
/// Modrinth keeps its shared metadata under a <c>meta/</c> directory: version manifests
/// and jars in <c>meta/versions/&lt;id&gt;</c>, libraries in <c>meta/libraries</c>,
/// assets in <c>meta/assets</c> and per-profile native libraries in
/// <c>meta/natives/&lt;profile&gt;</c>. Each profile (game directory) lives directly under
/// <c>profiles/&lt;name&gt;</c>.
/// </summary>
public sealed class ModrinthLayout : SharedMetadataLayout {
    public override string Format => "Modrinth";

    protected override string InstanceFolder => "profiles";

    protected override string MetadataFolder => "meta";

    public override string GetNativesDirectory(MinecraftEntry entry) =>
        Path.Combine(GetMetadataRoot(entry), "natives", Path.GetFileName(entry.InstancePath));

    /// <summary>
    /// Modrinth stores loader installs under <c>&lt;game&gt;-&lt;loaderVersion&gt;</c>
    /// (e.g. 1.20.1-47.4.10); the entry records that id in <see cref="MinecraftEntry.VersionId"/>.
    /// </summary>
    protected override string ResolveVersionId(MinecraftEntry entry) =>
        entry.VersionId.Length > 0
            ? entry.VersionId
            : base.ResolveVersionId(entry);
}
