namespace Tio.Avalonia.Standard.Modules.Tasks;

/// <summary>
/// 创建受管理任务的配置。
/// </summary>
public sealed class TaskOptions
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>
    /// 任务的初始进度，范围为 0 到 1。<see langword="null"/> 表示进度暂不可确定。
    /// </summary>
    public double? Progress { get; init; }

    public IReadOnlyCollection<TaskActionDefinition> Actions { get; init; } = [];
}
