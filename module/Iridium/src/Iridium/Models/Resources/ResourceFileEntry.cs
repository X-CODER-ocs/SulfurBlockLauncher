using Iridium.Resources;
using Iridium.Interfaces;

namespace Iridium.Models.Resources;


public sealed record ResourceFileEntry : IResourceFileEntry {
    public long Size { get; init; }

    public bool IsPrimary { get; init; }
    
    public string? FileName { get; init; }
    public string? Url { get; init; }
    public string? Sha1 { get; init; }
    public string? Sha512 { get; init; }
    public string? Md5 { get; init; }
}
