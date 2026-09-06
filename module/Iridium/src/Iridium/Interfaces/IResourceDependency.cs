using Iridium.Enums;

namespace Iridium.Interfaces;


public interface IResourceDependency {
    string? ProjectId { get; }
    string? VersionId { get; }
    string? FileName { get; }
    DependencyType Type { get; }
}
