using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tio.Avalonia.Standard.Modules.Tasks;

/// <summary>
/// 应用生命周期内唯一的任务创建入口和根任务记录器。
/// </summary>
public sealed class TaskManager : ObservableObject
{
    private readonly ObservableCollection<ManagedTask> _rootTasks = [];

    private TaskManager()
    {
        RootTasks = new ReadOnlyObservableCollection<ManagedTask>(_rootTasks);
        _rootTasks.CollectionChanged += OnRootTasksChanged;
    }

    public static TaskManager Instance { get; } = new();

    public ReadOnlyObservableCollection<ManagedTask> RootTasks { get; }

    /// <summary>
    /// 按状态优先级选出的根任务；同一状态取列表中的最后一个任务。
    /// </summary>
    public ManagedTask? CurrentTask => GetDisplayRootTasks().LastOrDefault();

    public ManagedTaskStatus? CurrentStatus => CurrentTask?.Status;

    /// <summary>
    /// 按状态优先级选出的根任务标题。
    /// </summary>
    public string? CurrentTaskTitle
    {
        get
        {
            var task = CurrentTask;
            if (task is null) return null;

            return task.Name;
        }
    }

    public int AdditionalTaskCount => Math.Max(0, _rootTasks.Count - 1);

    public bool HasAdditionalTasks => AdditionalTaskCount > 0;

    /// <summary>
    /// 创建处于 <see cref="ManagedTaskStatus.Pending"/> 状态的根任务。
    /// 调用 <see cref="ManagedTask.Start"/> 前不会执行任何任务代码。
    /// </summary>
    public ManagedTask CreateTask(TaskOptions options)
    {
        var task = new ManagedTask(null, options, null);
        _rootTasks.Add(task);
        return task;
    }

    /// <summary>
    /// 创建带执行器的根任务。仍需由调用方在准备完成后显式调用 <see cref="ManagedTask.Start"/>。
    /// </summary>
    public ManagedTask CreateTask(TaskOptions options, Func<TaskExecutionContext, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var task = new ManagedTask(null, options, operation);
        _rootTasks.Add(task);
        return task;
    }

    public void RequestCancellationForAll()
    {
        foreach (var task in _rootTasks)
        {
            task.RequestCancellation();
        }
    }

    /// <summary>
    /// 从任务列表中移除已结束的根任务。
    /// </summary>
    public bool RemoveTerminalTask(ManagedTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (task.Parent is not null || !task.IsTerminal) return false;

        return _rootTasks.Remove(task);
    }

    private void OnRootTasksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (ManagedTask task in e.NewItems) ObserveTaskTree(task);
        }

        if (e.OldItems is not null)
        {
            foreach (ManagedTask task in e.OldItems) StopObservingTaskTree(task);
        }

        NotifyCurrentTaskChanged();
    }

    private void ObserveTaskTree(ManagedTask task)
    {
        task.PropertyChanged += OnTaskPropertyChanged;
        ((INotifyCollectionChanged)task.Children).CollectionChanged += OnTaskChildrenChanged;
        foreach (var child in task.Children) ObserveTaskTree(child);
    }

    private void StopObservingTaskTree(ManagedTask task)
    {
        task.PropertyChanged -= OnTaskPropertyChanged;
        ((INotifyCollectionChanged)task.Children).CollectionChanged -= OnTaskChildrenChanged;
        foreach (var child in task.Children) StopObservingTaskTree(child);
    }

    private void OnTaskChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (ManagedTask task in e.NewItems) ObserveTaskTree(task);
        if (e.OldItems is not null)
            foreach (ManagedTask task in e.OldItems) StopObservingTaskTree(task);
        NotifyCurrentTaskChanged();
    }

    private void OnTaskPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ManagedTask.Status) or nameof(ManagedTask.Description))
            NotifyCurrentTaskChanged();
    }

    private void NotifyCurrentTaskChanged()
    {
        OnPropertyChanged(nameof(CurrentTask));
        OnPropertyChanged(nameof(CurrentStatus));
        OnPropertyChanged(nameof(CurrentTaskTitle));
        OnPropertyChanged(nameof(AdditionalTaskCount));
        OnPropertyChanged(nameof(HasAdditionalTasks));
    }

    private IReadOnlyList<ManagedTask> GetDisplayRootTasks()
    {
        if (_rootTasks.Count == 0) return [];

        var priority = _rootTasks.Max(GetDisplayPriority);
        return _rootTasks.Where(task => GetDisplayPriority(task) == priority).ToList();
    }

    private static int GetDisplayPriority(ManagedTask task) => task.Status switch
    {
        ManagedTaskStatus.Faulted => 4,
        ManagedTaskStatus.Running or ManagedTaskStatus.Cancelling => 3,
        ManagedTaskStatus.Pending or ManagedTaskStatus.Waiting => 2,
        _ => 1
    };

}
