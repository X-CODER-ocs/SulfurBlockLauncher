using Iridium.Minecraft;
namespace Iridium.Interfaces;

/// <summary>
/// A concrete Minecraft format provider. It is the only component allowed to recognize
/// a format: detection, instance location and parsing all live behind this interface.
/// Consumers of <see cref="MinecraftContext"/> must never branch on the format id
/// themselves.
/// </summary>
public interface IFormatProvider {
    string Id { get; }

    int Priority { get; }

    /// <summary>
    /// Coarse detection: whether <paramref name="root"/> is a Game Root of this format
    /// (e.g. a directory containing <c>versions/</c>, <c>instances/</c>, ...).
    /// </summary>
    bool CanResolve(DirectoryInfo root);

    /// <summary>
    /// Locates the instance identified by <paramref name="instanceId"/> within the Game
    /// Root and resolves it into a <see cref="MinecraftContext"/>. Returns <c>null</c>
    /// when the root does not belong to this format or the instance does not exist.
    /// </summary>
    ValueTask<MinecraftContext?> GetAsync(DirectoryInfo root, string instanceId, CancellationToken ct = default);

    /// <summary>Enumerates all instances under a Game Root of this format.</summary>
    ValueTask<IReadOnlyList<MinecraftContext>> GetMinecraftsAsync(DirectoryInfo root, CancellationToken ct = default);
}
