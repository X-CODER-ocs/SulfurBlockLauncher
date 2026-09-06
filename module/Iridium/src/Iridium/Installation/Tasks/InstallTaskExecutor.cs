using System.Collections.Concurrent;
using Iridium.Enums;

namespace Iridium.Installation.Tasks;

/// <summary>
/// Internal DAG runner: schedules ready steps, runs independent ones in parallel (bounded by
/// a fixed step concurrency), waits on dependencies, cancels the remaining graph on the first
/// failure, honours a CancellationToken and aggregates the complete
/// <see cref="InstallProgress"/> snapshot. This is an implementation detail — task execution
/// is exposed through <see cref="InstallTask.InstallAsync"/>.
/// </summary>
internal static class InstallTaskExecutor {
    /// <summary>Internal fixed maximum number of install steps run in parallel.</summary>
    private const int MaxConcurrency = 4;

    public static async System.Threading.Tasks.Task<InstallResult> ExecuteAsync(
        InstallTask task,
        InstallState state,
        Action<InstallProgress>? reportProgress,
        CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(state);

        var nodes = task.Nodes;
        if (nodes.Count == 0)
            return new InstallResult { State = state };

        task.Validate();

        var steps = nodes.ToDictionary(n => n.Key, n => new StepState { Step = n.Step });

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var semaphore = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
        var completions = nodes.ToDictionary(
            n => n.Key,
            _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        var failures = new ConcurrentQueue<Exception>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        async System.Threading.Tasks.Task RunNodeAsync(InstallStepNode node) {
            var step = steps[node.Key];
            Exception? failure = null;
            try {
                var dependencies = node.DependsOn.Select(d => completions[d].Task).ToArray();
                await System.Threading.Tasks.Task.WhenAll(dependencies).ConfigureAwait(false);

                await semaphore.WaitAsync(linkedCts.Token).ConfigureAwait(false);
                try {
                    linkedCts.Token.ThrowIfCancellationRequested();
                    step.Status = InstallStepStatus.Running;
                    Emit();

                    await node.Step.ExecuteAsync(
                        state,
                        (completed, total) => {
                            step.Completed = completed;
                            step.Total = total;
                            Emit();
                        },
                        linkedCts.Token).ConfigureAwait(false);
                } finally {
                    semaphore.Release();
                }
            } catch (Exception ex) when (linkedCts.IsCancellationRequested) {
                failure = new OperationCanceledException("Installation canceled.", ex);
                step.Status = InstallStepStatus.Cancelled;
            } catch (Exception ex) {
                failure = ex;
                step.Status = InstallStepStatus.Failed;
                await linkedCts.CancelAsync().ConfigureAwait(false);
            }

            if (failure is null) {
                if (step.Total == 0) {
                    step.Completed = 1;
                    step.Total = 1;
                } else {
                    step.Completed = step.Total;
                }
                step.Status = InstallStepStatus.Completed;
                Emit();
                completions[node.Key].SetResult();
            } else {
                if (failure is not OperationCanceledException)
                    failures.Enqueue(failure);
                Emit();
                completions[node.Key].SetException(failure);
            }
        }

        try {
            await System.Threading.Tasks.Task.WhenAll(nodes.Select(RunNodeAsync)).ConfigureAwait(false);
        } finally {
            stopwatch.Stop();
        }

        return new InstallResult {
            State = state,
            Failures = [.. failures],
            Elapsed = stopwatch.Elapsed
        };

        void Emit() {
            if (reportProgress is null)
                return;

            var snapshot = new List<InstallStepProgress>(nodes.Count);
            long completedUnits = 0;
            long totalUnits = 0;
            var completedSteps = 0;

            foreach (var node in nodes) {
                var step = steps[node.Key];
                snapshot.Add(new InstallStepProgress {
                    Key = node.Key,
                    Name = step.Step.Name,
                    Status = step.Status,
                    Completed = step.Completed,
                    Total = step.Total
                });

                if (step.Status == InstallStepStatus.Completed)
                    completedSteps++;
                completedUnits += step.Completed;
                totalUnits += step.Total;
            }

            reportProgress(new InstallProgress {
                Steps = snapshot,
                CompletedSteps = completedSteps,
                TotalSteps = nodes.Count,
                CompletedUnits = completedUnits,
                TotalUnits = totalUnits
            });
        }
    }

    private sealed class StepState {
        public required IInstallStep Step { get; init; }
        public InstallStepStatus Status { get; set; } = InstallStepStatus.Pending;
        public long Completed { get; set; }
        public long Total { get; set; }
    }
}