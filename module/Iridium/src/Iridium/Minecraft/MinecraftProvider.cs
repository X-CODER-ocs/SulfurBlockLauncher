using Iridium.Interfaces;
using Iridium.Minecraft.Formats;

using IFormatProvider = Iridium.Interfaces.IFormatProvider;

namespace Iridium.Minecraft;

public sealed class MinecraftProvider : IMinecraftProvider {
    public static readonly IReadOnlyList<IFormatProvider> Default = [
        new StandardMinecraftProvider(),
        new PrismMinecraftProvider()
    ];

    private readonly IReadOnlyList<IFormatProvider> _providers;

    public MinecraftProvider(DirectoryInfo root, IEnumerable<IFormatProvider>? providers = null) {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        _providers = [.. (providers ?? Default).OrderByDescending(p => p.Priority)];
    }

    /// <summary>Game Root：本 Provider 绑定的游戏/Launcher 根目录。</summary>
    public DirectoryInfo Root { get; }

    public async ValueTask<MinecraftContext?> GetAsync(string instanceId, CancellationToken ct = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        foreach (var provider in _providers)
            if (await provider.GetAsync(Root, instanceId, ct) is { } context)
                return context;
        return null;
    }

    public async ValueTask<IReadOnlyList<MinecraftContext>> GetMinecraftsAsync(CancellationToken ct = default) {
        foreach (var provider in _providers) {
            if (!provider.CanResolve(Root))
                continue;
            return await provider.GetMinecraftsAsync(Root, ct);
        }
        return [];
    }
}
