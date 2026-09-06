using Iridium.Installation.Tasks;
using Iridium.Minecraft;

namespace Iridium.Models.Installation;

/// <summary>
/// Minecraft-specific install result: the resolved context and the key artifact paths. The
/// install target is intentionally not included — the installer already owns it; callers get
/// everything through the polymorphic <see cref="IInstallResult"/>.
/// </summary>
public sealed record MinecraftInstallResult : IInstallResult {
    /// <summary>
    /// The resolved Minecraft context after a successful install, or <c>null</c> when the
    /// install did not reach the resolve stage.
    /// </summary>
    public MinecraftContext? Minecraft { get; init; }

    public string VersionJsonPath { get; init; } = string.Empty;
    public string ClientJarPath { get; init; } = string.Empty;

    public TimeSpan Elapsed { get; init; }
    public IReadOnlyList<Exception> Failures { get; init; } = [];
    public bool IsSuccess => Failures.Count == 0;
}