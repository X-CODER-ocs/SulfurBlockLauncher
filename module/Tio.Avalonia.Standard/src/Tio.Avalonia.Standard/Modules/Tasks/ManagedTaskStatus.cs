namespace Tio.Avalonia.Standard.Modules.Tasks;

/// <summary>
/// 表示受管理任务的生命周期状态。
/// </summary>
public enum ManagedTaskStatus
{
    Pending,
    Waiting,
    Running,
    Cancelling,
    Cancelled,
    Completed,
    Faulted
}
