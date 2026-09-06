using Iridium.Installation.Tasks;
using Iridium.Models.Installation;

namespace Iridium.Installation.Installer;

public abstract class InstallerBase<TInput> {
    public event EventHandler<InstallerCompletedEventArgs>? Completed;
    public event EventHandler<InstallProgressChangedEventArgs>? ProgressChanged;
    
    public virtual async Task<IInstallResult> InstallAsync(TInput input, int maxConcurrency = 32, CancellationToken ct = default) {
        var result = await RunTaskAsync(CreateTask(input), maxConcurrency, ct).ConfigureAwait(false);
        return result;
    }

    protected abstract InstallTask CreateTask(TInput input);

    protected async Task<InstallResult> RunTaskAsync(InstallTask task, int maxConcurrency, CancellationToken ct) {
        var state = new InstallState();
        state.Set(InstallState.DownloadConcurrencyKey, maxConcurrency);
        var result = await task.InstallAsync(ReportProgress, state, ct).ConfigureAwait(false);
        ReportCompleted(result.IsSuccess, result.Failures.FirstOrDefault());
        return result;
    }

    protected void ReportProgress(InstallProgress progress) =>
        ProgressChanged?.Invoke(this, new InstallProgressChangedEventArgs(progress));

    protected void ReportCompleted(bool isSuccess, Exception? exception) =>
        Completed?.Invoke(this, new InstallerCompletedEventArgs(isSuccess, exception));
}