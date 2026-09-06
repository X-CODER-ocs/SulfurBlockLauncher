namespace Iridium.Installation.Tasks;

/// <summary>
/// Polymorphic install result. <see cref="InstallTask.InstallAsync"/> returns the generic
/// <see cref="InstallResult"/>; concrete installers may return their own richer implementation
/// (e.g. <c>MinecraftInstallResult</c>) through this interface.
/// </summary>
public interface IInstallResult {
    bool IsSuccess { get; }

    IReadOnlyList<Exception> Failures { get; }

    TimeSpan Elapsed { get; }
}

/// <summary>
/// Result of executing an <see cref="InstallTask"/>. Success/failure, elapsed time and the
/// shared <see cref="InstallState"/> (which the executed steps populated) — no business type.
/// </summary>
public sealed record InstallResult : IInstallResult {
    public required InstallState State { get; init; }

    public TimeSpan Elapsed { get; init; }

    public IReadOnlyList<Exception> Failures { get; init; } = [];

    public bool IsSuccess => Failures.Count == 0;
}