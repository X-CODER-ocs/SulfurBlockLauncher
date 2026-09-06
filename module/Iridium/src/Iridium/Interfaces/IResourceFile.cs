using Iridium.Enums;

namespace Iridium.Interfaces;


public interface IResourceFile {
    string Id { get; }
    string ProjectId { get; }
    string? Name { get; }
    ReleaseType ReleaseType { get; }
    DateTime? Published { get; }
    long Downloads { get; }
    IReadOnlyList<string> GameVersions { get; }
    IReadOnlyList<ResourceLoaderType> Loaders { get; }
    IResourceFileEntry? PrimaryFile { get; }
    IReadOnlyList<IResourceDependency> Dependencies { get; }
}
