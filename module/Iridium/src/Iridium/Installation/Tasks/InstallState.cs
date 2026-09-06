namespace Iridium.Installation.Tasks;

/// <summary>
/// Generic, per-execution shared state: a simple bag that install steps exchange runtime
/// results through. It is deliberately free of any business type (no Minecraft, downloads,
/// targets, ...) so the <see cref="InstallTask"/> core stays a generic small task framework.
/// Steps that need no shared state simply ignore it.
/// </summary>
public sealed class InstallState {
    public const string DownloadConcurrencyKey = "install.download-concurrency";

    private readonly Dictionary<string, object?> _store = new(StringComparer.Ordinal);

    public void Set(string key, object? value) => _store[key] = value;

    public T? Get<T>(string key) =>
        _store.TryGetValue(key, out var value) && value is T typed ? typed : default;
}