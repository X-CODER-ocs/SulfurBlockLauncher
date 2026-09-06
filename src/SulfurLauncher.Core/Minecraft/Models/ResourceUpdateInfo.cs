using SulfurLauncher.Core.Minecraft.Classes;

namespace SulfurLauncher.Core.Minecraft.Models;

public sealed record ResourceUpdateCandidate(
    string FilePath,
    ResourceKind Kind,
    string? Sha1 = null,
    uint? Fingerprint = null,
    string? Source = null,
    string? ProjectId = null,
    string? VersionId = null);

public sealed record ResourceUpdateResult(
    string FilePath,
    ModDetailsSource? Source,
    string? ProjectId,
    string? CurrentVersionId,
    string? TargetVersionId,
    ResourceVersionFileItem? TargetFile)
{
    public bool HasUpdate => TargetFile != null &&
                             !string.IsNullOrEmpty(TargetVersionId) &&
                             !string.Equals(CurrentVersionId, TargetVersionId, StringComparison.Ordinal);

    public bool HasIdentity => Source != null && !string.IsNullOrEmpty(ProjectId);
}
