namespace Tio.Avalonia.Standard.Modules.Tasks;

public enum TaskLogLevel
{
    Debug,
    Information,
    Warning,
    Error
}

/// <summary>
/// 一条属于特定任务的结构化日志。
/// </summary>
public sealed record TaskLogEntry(
    DateTimeOffset Timestamp,
    TaskLogLevel Level,
    string Message,
    Exception? Exception = null);
