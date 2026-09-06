namespace Tio.Avalonia.Standard.Modules.Tasks;

/// <summary>
/// 创建任务操作时使用的定义。操作的执行、可见性和可用性都会基于当前任务实时计算。
/// </summary>
public sealed class TaskActionDefinition
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? IconKey { get; init; }

    public required Func<ManagedTask, CancellationToken, Task> ExecuteAsync { get; init; }

    public Func<ManagedTask, bool>? CanExecute { get; init; }

    public Func<ManagedTask, bool>? IsVisible { get; init; }
}
