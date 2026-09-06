using Iridium.Minecraft;
namespace Iridium.Interfaces;

/// <summary>
/// Facade over the registered format providers, bound to a single Game Root. It locates
/// instances by logical instance id and delegates format detection, instance location and
/// parsing to the underlying format providers. Consumers never see format-specific paths.
/// </summary>
public interface IMinecraftProvider {
    /// <summary>
    /// Locates and resolves a single instance by its logical instance id within the bound
    /// Game Root. Returns <c>null</c> when no registered format claims the instance.
    /// </summary>
    ValueTask<MinecraftContext?> GetAsync(string instanceId, CancellationToken ct = default);

    /// <summary>Enumerates all instances under the bound Game Root.</summary>
    ValueTask<IReadOnlyList<MinecraftContext>> GetMinecraftsAsync(CancellationToken ct = default);
}
