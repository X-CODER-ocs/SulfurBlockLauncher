namespace Tio.Avalonia.Standard.Modules.Tasks;

/// <summary>
/// 任务执行器使用的上下文，提供取消信号、状态切换、日志和子任务创建功能。
/// </summary>
public sealed class TaskExecutionContext
{
    private readonly ManagedTask _task;

    internal TaskExecutionContext(ManagedTask task)
    {
        _task = task;
    }

    public ManagedTask Task => _task;

    public CancellationToken CancellationToken => _task.CancellationToken;

    public void SetWaiting(string? description = null) => _task.SetWaiting(description);

    public void SetRunning(string? description = null) => _task.SetRunning(description);

    /// <summary>
    /// 更新任务描述，例如当前正在下载的文件名。
    /// </summary>
    public void SetDescription(string? description) => _task.SetDescription(description);

    /// <summary>
    /// 上报范围为 0 到 1 的任务进度。<see langword="null"/> 表示进度暂不可确定。
    /// </summary>
    public void ReportProgress(double? progress) => _task.ReportProgress(progress);

    public void LogDebug(string message) => _task.LogDebug(message);

    public void LogInformation(string message) => _task.LogInformation(message);

    public void LogWarning(string message) => _task.LogWarning(message);

    public void LogError(string message, Exception? exception = null) => _task.LogError(message, exception);

    public ManagedTask CreateChild(TaskOptions options) => _task.CreateChild(options);

    public ManagedTask CreateChild(TaskOptions options, Func<TaskExecutionContext, Task> operation) =>
        _task.CreateChild(options, operation);

    public TaskAction AddAction(TaskActionDefinition definition) => _task.AddAction(definition);

    public void Complete() => _task.Complete();

    public void Fault(Exception exception) => _task.Fault(exception);
}
