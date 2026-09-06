using System.Diagnostics;
using Iridium.Installation.Tasks;

namespace Iridium.Sample;

/// <summary>
/// A pure generic-task test: no Minecraft types anywhere. Verifies Do/Parallel/Then DAG
/// semantics through <see cref="InstallTask.InstallAsync"/>.
/// </summary>
public static class TestInstaller {
    private static readonly InstallStepKey Step1 = nameof(Step1);
    private static readonly InstallStepKey StepPA = nameof(StepPA);
    private static readonly InstallStepKey StepPB = nameof(StepPB);
    private static readonly InstallStepKey Step4 = nameof(Step4);

    public static InstallTask CreateTask() =>
        InstallTask.Define(t => t
            .Do(Step1, "Run A", (state, report, ct) => CreateA(report))
            .Parallel(
                (StepPA, "Run PA", (state, report, ct) => CreatePA(report)),
                (StepPB, "Run PB", (state, report, ct) => CreatePB(report)))
            .Then(Step4, "Run C", (state, report, ct) => CreateC(report)));

    public static async Task RunAsync() {
        var stopwatch = Stopwatch.StartNew();
        var result = await CreateTask().InstallAsync();
        stopwatch.Stop();

        Console.WriteLine(stopwatch.Elapsed);
    }

    private static async ValueTask Run(string name, Action<long, long> report) {
        report(0, 1);
        Console.WriteLine($"  [{DateTime.Now:HH:mm:ss.fff}] {name}");
        await Task.Delay(TimeSpan.FromSeconds(5));
        report(1, 1);
    }

    public static ValueTask CreateA(Action<long, long> report) => Run("Run A", report);
    public static ValueTask CreatePA(Action<long, long> report) => Run("Run PA", report);
    public static ValueTask CreatePB(Action<long, long> report) => Run("Run PB", report);
    public static ValueTask CreateC(Action<long, long> report) => Run("Run C", report);
}