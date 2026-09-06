using Iridium.Enums;

namespace Iridium.Installation.Tasks;

public sealed record InstallStepProgress {
    public InstallStepKey Key { get; init; }
    public string Name { get; init; } = string.Empty;
    
    public InstallStepStatus Status { get; init; } = InstallStepStatus.Pending;

    /// <summary>Completed work units (typically one file per unit).</summary>
    public long Completed { get; init; }

    /// <summary>Total work units; 0 until the step reports its workload.</summary>
    public long Total { get; init; }

    public double Progress => Total > 0 ? Math.Clamp(Completed / (double)Total, 0d, 1d) : 0d;
}

public sealed record InstallProgress {
    public IReadOnlyList<InstallStepProgress> Steps { get; init; } = [];

    /// <summary>
    /// Number of steps whose status is <see cref="InstallStepStatus.Completed"/>.
    /// </summary>
    public int CompletedSteps { get; init; }

    public int TotalSteps { get; init; }

    /// <summary>
    /// Step.Completed — actual completed workload.
    /// </summary>
    public long CompletedUnits { get; init; }

    /// <summary>
    /// Step.Total — actual total workload.
    /// </summary>
    public long TotalUnits { get; init; }

    /// <summary>
    /// Overall progress based on real workload: CompletedUnits / TotalUnits. Prefer this
    /// over step-count ratios (a 3000-file step must not weigh as much as a 1-file step).
    /// </summary>
    public double Progress => TotalUnits > 0 ? Math.Clamp(CompletedUnits / (double)TotalUnits, 0d, 1d) : 0d;
}
