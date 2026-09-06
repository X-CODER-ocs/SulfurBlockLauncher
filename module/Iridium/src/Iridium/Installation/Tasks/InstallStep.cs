namespace Iridium.Installation.Tasks;

/// <summary>
/// Execution contract of a delegate-based step. A step receives the shared
/// <see cref="InstallState"/> (a generic bag; ignored when not needed) and reports progress
/// with a plain synchronous <c>Action&lt;long, long&gt;</c> (completed / total units) — the
/// executor aggregates it synchronously in the current execution thread.
/// </summary>
public delegate ValueTask InstallStepHandler(InstallState state, Action<long, long> report, CancellationToken ct);

/// <summary>
/// A logical stage in an install task (download a version, resolve a manifest, install a
/// loader, apply overrides, ...). A step carries a stable typed <see cref="Key"/> (used for
/// task dependencies, composition de-duplication and progress) separate from its display
/// <see cref="Name"/>. The <see cref="InstallTaskExecutor"/> runs the DAG and aggregates the
/// full progress snapshot.
/// </summary>
public interface IInstallStep {
    /// <summary>Stable step identity: the same key means the same logical operation.</summary>
    InstallStepKey Key { get; }

    /// <summary>Human-readable step name shown in progress snapshots.</summary>
    string Name { get; }

    ValueTask ExecuteAsync(InstallState state, Action<long, long> report, CancellationToken ct = default);
}

/// <summary>
/// A delegate-based step: adapts an <see cref="InstallStepHandler"/> so the DSL can treat
/// delegate steps and class-based steps uniformly.
/// </summary>
public sealed class InstallStep(InstallStepKey key, string name, InstallStepHandler handler) : IInstallStep {
    public InstallStepKey Key => key;
    public string Name => name;

    public ValueTask ExecuteAsync(InstallState state, Action<long, long> report, CancellationToken ct = default) =>
        handler(state, report, ct);
}

/// <summary>A node in the install DAG: a step plus its explicit dependencies.</summary>
internal sealed record InstallStepNode {
    public required InstallStepKey Key { get; init; }
    public required IInstallStep Step { get; init; }
    public IReadOnlyList<InstallStepKey> DependsOn { get; init; } = [];
}