using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tio.Avalonia.Standard.Modules.Tasks;

/// <summary>
/// 表示应用生命周期内的一个可观察、可取消任务节点。
/// </summary>
public sealed class ManagedTask : ObservableObject
{
    private readonly ObservableCollection<ManagedTask> _children = [];
    private readonly ObservableCollection<TaskAction> _actions = [];
    private readonly ObservableCollection<TaskLogEntry> _logs = [];
    private readonly CancellationTokenSource _cancellationTokenSource;
    private Func<TaskExecutionContext, Task>? _operation;
    private readonly TaskCompletionSource _completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string? _description;
    private ManagedTaskStatus _status = ManagedTaskStatus.Pending;
    private DateTimeOffset? _startedAt;
    private DateTimeOffset? _completedAt;
    private Exception? _exception;
    private double? _progress;

    internal ManagedTask(ManagedTask? parent, TaskOptions options, Func<TaskExecutionContext, Task>? operation)
    {
        ArgumentNullException.ThrowIfNull(options);

        Id = Guid.NewGuid();
        Parent = parent;
        Name = options.Name ?? throw new ArgumentException("任务名称不能为空。", nameof(options));
        Description = options.Description;
        Progress = options.Progress;
        _operation = operation;
        _cancellationTokenSource = parent is null
            ? new CancellationTokenSource()
            : CancellationTokenSource.CreateLinkedTokenSource(parent.CancellationToken);

        Children = new ReadOnlyObservableCollection<ManagedTask>(_children);
        Actions = new ReadOnlyObservableCollection<TaskAction>(_actions);
        Logs = new ReadOnlyObservableCollection<TaskLogEntry>(_logs);
        _children.CollectionChanged += OnChildrenChanged;

        foreach (var action in options.Actions)
        {
            _actions.Add(new TaskAction(this, action));
        }
    }

    public Guid Id { get; }

    public string Name { get; }

    public string? Description
    {
        get => _description;
        private set
        {
            if (!SetProperty(ref _description, value)) return;
            OnPropertyChanged(nameof(DisplayDescription));
            Parent?.NotifyDisplayDescriptionChanged();
        }
    }

    public ManagedTask? Parent { get; }

    public ReadOnlyObservableCollection<ManagedTask> Children { get; }

    public ReadOnlyObservableCollection<TaskAction> Actions { get; }

    public ReadOnlyObservableCollection<TaskLogEntry> Logs { get; }

    public ManagedTaskStatus Status
    {
        get => _status;
        private set
        {
            if (!SetProperty(ref _status, value)) return;

            OnPropertyChanged(nameof(IsTerminal));
            OnPropertyChanged(nameof(CanBeCancelled));
            OnPropertyChanged(nameof(DisplayDescription));
            Parent?.NotifyDisplayDescriptionChanged();
            foreach (var action in _actions) action.Refresh();
        }
    }

    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.Now;

    public DateTimeOffset? StartedAt
    {
        get => _startedAt;
        private set => SetProperty(ref _startedAt, value);
    }

    public DateTimeOffset? CompletedAt
    {
        get => _completedAt;
        private set => SetProperty(ref _completedAt, value);
    }

    public Exception? Exception
    {
        get => _exception;
        private set => SetProperty(ref _exception, value);
    }

    public string? ErrorMessage => Exception?.Message;

    /// <summary>
    /// 当前节点或其后代中最需要关注的任务说明。
    /// </summary>
    public string? DisplayDescription => GetDisplayTask()?.Description ?? Description;

    /// <summary>
    /// 当前节点的进度，范围为 0 到 1。<see langword="null"/> 表示无法确定进度。
    /// </summary>
    public double? Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, value);
    }

    /// <summary>
    /// 当前任务树的连续进度。它会将当前节点和所有子任务的已知进度平均，
    /// 适合在父任务上显示，避免分阶段任务完成时进度条回跳。
    /// </summary>
    public double? AggregateProgress
    {
        get
        {
            var progressValues = new List<double>();
            CollectProgress(this, progressValues);
            return progressValues.Count == 0 ? null : progressValues.Average();
        }
    }

    public bool IsTerminal => Status is ManagedTaskStatus.Cancelled or ManagedTaskStatus.Completed or ManagedTaskStatus.Faulted;

    public bool CanBeCancelled => Status is ManagedTaskStatus.Pending or ManagedTaskStatus.Waiting or ManagedTaskStatus.Running;

    public bool IsCancellationRequested => _cancellationTokenSource.IsCancellationRequested;

    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    public Task Completion => _completionSource.Task;

    /// <summary>
    /// 启动任务。没有执行器的任务进入 <see cref="ManagedTaskStatus.Running"/>，
    /// 并由调用方通过 <see cref="Complete"/> 或 <see cref="Fault"/> 终结。
    /// </summary>
    public void Start()
    {
        if (Status != ManagedTaskStatus.Pending)
            throw new InvalidOperationException("只有等待执行的任务可以启动。");

        StartedAt = DateTimeOffset.Now;
        Status = ManagedTaskStatus.Running;

        if (_operation is not null) _ = RunAsync();
    }

    /// <summary>
    /// 为无执行器的任务指定执行器并启动。任务只能启动一次。
    /// </summary>
    public void Start(Func<TaskExecutionContext, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (_operation is not null)
            throw new InvalidOperationException("此任务已配置执行器。");

        _operation = operation;
        Start();
    }

    public void RequestCancellation()
    {
        if (!CanBeCancelled) return;

        Status = ManagedTaskStatus.Cancelling;
        LogInformation("已请求取消任务，正在等待清理完成。");
        // Cancel() synchronously invokes every registered callback. Download and timer callbacks can be
        // expensive, so invoking it from an Avalonia command freezes the UI until all callbacks return.
        // Child tokens are linked to this token and receive cancellation from this single request.
        _ = Task.Run(CancelCore);

        foreach (var child in _children)
        {
            child.MarkCancelling();
        }
    }

    private void CancelCore()
    {
        try
        {
            _cancellationTokenSource.Cancel();
        }
        catch
        {
            // Cancellation callbacks must never bring down the worker that propagates cancellation.
        }
    }

    private void MarkCancelling()
    {
        if (!CanBeCancelled) return;

        Status = ManagedTaskStatus.Cancelling;
        foreach (var child in _children) child.MarkCancelling();
    }

    public string GetFormattedLog()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"任务: {Name}");
        builder.AppendLine($"状态: {Status}");
        builder.AppendLine($"创建时间: {CreatedAt:O}");

        foreach (var entry in _logs)
        {
            builder.Append($"[{entry.Timestamp:O}] [{entry.Level}] {entry.Message}");
            if (entry.Exception is not null) builder.Append($"{Environment.NewLine}{entry.Exception}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    public ManagedTask CreateChild(TaskOptions options) => CreateChildCore(options, null);

    public ManagedTask CreateChild(TaskOptions options, Func<TaskExecutionContext, Task> operation) =>
        CreateChildCore(options, operation);

    private ManagedTask CreateChildCore(TaskOptions options, Func<TaskExecutionContext, Task>? operation)
    {
        if (IsCancellationRequested || IsTerminal)
            throw new InvalidOperationException("已取消或已完成的任务不能创建子任务。");

        var child = new ManagedTask(this, options, operation);
        _children.Add(child);
        return child;
    }

    /// <summary>
    /// 在任务存活期间动态添加一个可绑定的 UI 操作。
    /// </summary>
    public TaskAction AddAction(TaskActionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (IsTerminal) throw new InvalidOperationException("已结束的任务不能添加操作。");

        var action = new TaskAction(this, definition);
        _actions.Add(action);
        return action;
    }

    /// <summary>
    /// 刷新动态任务操作的可见性和可用性。
    /// </summary>
    public void RefreshActions()
    {
        foreach (var action in _actions) action.Refresh();
    }

    /// <summary>
    /// 将一个无执行器的运行中或等待中任务标记为成功完成。
    /// </summary>
    public void Complete()
    {
        EnsureCanComplete();
        CompleteCore(IsCancellationRequested ? ManagedTaskStatus.Cancelled : ManagedTaskStatus.Completed);
    }

    /// <summary>
    /// 将一个无执行器的运行中或等待中任务标记为失败。
    /// </summary>
    public void Fault(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        EnsureCanComplete();
        Exception = exception;
        OnPropertyChanged(nameof(ErrorMessage));
        LogError("任务执行失败。", exception);
        CompleteCore(ManagedTaskStatus.Faulted);
    }

    internal void SetWaiting(string? description)
    {
        EnsureActive();
        if (description is not null) Description = description;
        Status = ManagedTaskStatus.Waiting;
    }

    internal void SetRunning(string? description)
    {
        EnsureActive();
        if (description is not null) Description = description;
        Status = ManagedTaskStatus.Running;
    }

    internal void SetDescription(string? description) => Description = description;

    internal void ReportProgress(double? progress)
    {
        if (progress is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(progress), "任务进度必须在 0 到 1 之间。");

        EnsureActive();
        Progress = progress;
        NotifyAggregateProgressChanged();
    }

    internal void LogDebug(string message) => AddLog(TaskLogLevel.Debug, message);

    internal void LogInformation(string message) => AddLog(TaskLogLevel.Information, message);

    internal void LogWarning(string message) => AddLog(TaskLogLevel.Warning, message);

    internal void LogError(string message, Exception? exception = null) => AddLog(TaskLogLevel.Error, message, exception);

    private async Task RunAsync()
    {
        StartedAt = DateTimeOffset.Now;
        Status = ManagedTaskStatus.Running;

        try
        {
            await _operation!(new TaskExecutionContext(this));
            if (IsCancellationRequested)
            {
                CompleteCore(ManagedTaskStatus.Cancelled);
            }
            else
            {
                CompleteCore(ManagedTaskStatus.Completed);
            }
        }
        catch (OperationCanceledException) when (IsCancellationRequested)
        {
            CompleteCore(ManagedTaskStatus.Cancelled);
        }
        catch (Exception exception)
        {
            Exception = exception;
            OnPropertyChanged(nameof(ErrorMessage));
            LogError("任务执行失败。", exception);
            CompleteCore(ManagedTaskStatus.Faulted);
        }
    }

    private void CompleteCore(ManagedTaskStatus status)
    {
        if (status == ManagedTaskStatus.Completed) Progress = 1;
        NotifyAggregateProgressChanged();
        Status = status;
        CompletedAt = DateTimeOffset.Now;
        _completionSource.TrySetResult();
    }

    private static void CollectProgress(ManagedTask task, ICollection<double> progressValues)
    {
        if (task.Progress is { } progress) progressValues.Add(progress);
        foreach (var child in task._children) CollectProgress(child, progressValues);
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (ManagedTask child in e.NewItems)
            {
                child.PropertyChanged += OnChildPropertyChanged;
            }
        }

        if (e.OldItems is not null)
        {
            foreach (ManagedTask child in e.OldItems)
            {
                child.PropertyChanged -= OnChildPropertyChanged;
            }
        }

        NotifyAggregateProgressChanged();
    }

    private void OnChildPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Progress) or nameof(AggregateProgress)) NotifyAggregateProgressChanged();
        if (e.PropertyName is nameof(Description) or nameof(DisplayDescription) or nameof(Status))
            NotifyDisplayDescriptionChanged();
    }

    private void NotifyAggregateProgressChanged()
    {
        OnPropertyChanged(nameof(AggregateProgress));
        Parent?.NotifyAggregateProgressChanged();
    }

    private void NotifyDisplayDescriptionChanged()
    {
        OnPropertyChanged(nameof(DisplayDescription));
        Parent?.NotifyDisplayDescriptionChanged();
    }

    private ManagedTask? GetDisplayTask() => GetDescendants()
        .Where(task => !string.IsNullOrWhiteSpace(task.Description))
        .OrderByDescending(task => task.Status switch
        {
            ManagedTaskStatus.Faulted => 4,
            ManagedTaskStatus.Running or ManagedTaskStatus.Cancelling => 3,
            ManagedTaskStatus.Pending or ManagedTaskStatus.Waiting => 2,
            _ => 1
        })
        .ThenByDescending(task => task.CreatedAt)
        .FirstOrDefault();

    private IEnumerable<ManagedTask> GetDescendants()
    {
        foreach (var child in _children)
        {
            yield return child;
            foreach (var descendant in child.GetDescendants()) yield return descendant;
        }
    }

    private void AddLog(TaskLogLevel level, string message, Exception? exception = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        _logs.Add(new TaskLogEntry(DateTimeOffset.Now, level, message, exception));
        foreach (var action in _actions) action.Refresh();
    }

    private void EnsureActive()
    {
        CancellationToken.ThrowIfCancellationRequested();

        if (IsTerminal)
            throw new InvalidOperationException("已取消或已完成的任务不能更新执行状态。");
    }

    private void EnsureCanComplete()
    {
        if (Status is not (ManagedTaskStatus.Running or ManagedTaskStatus.Waiting or ManagedTaskStatus.Cancelling))
            throw new InvalidOperationException("只有运行中、等待中或取消中的任务可以结束。");
    }
}
