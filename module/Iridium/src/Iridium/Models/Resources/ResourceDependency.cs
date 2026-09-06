using Iridium.Enums;
using Iridium.Resources;
using Iridium.Interfaces;

namespace Iridium.Models.Resources;


public sealed record ResourceDependency : IResourceDependency {
    public string? ProjectId { get; init; }
    public string? VersionId { get; init; }
    public string? FileName { get; init; }
    
    public DependencyType Type { get; init; }
}
