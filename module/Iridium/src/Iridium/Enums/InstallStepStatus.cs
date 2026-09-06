namespace Iridium.Enums;

/// <summary>Lifecycle status of a single install step.</summary>
public enum InstallStepStatus {
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}
