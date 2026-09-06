using Flurl;
using Iridium.Enums;

namespace Iridium.Download;

public class ResourceApiSource { 
    private readonly Func<ResourceApiType, Url> _urlBuilder;

    public string Name { get; }

    public static ResourceApiSource Official { get; } = new("Official", builder => builder switch {
        ResourceApiType.Modrinth => "https://api.modrinth.com/v2",
        ResourceApiType.CurseForge => "https://api.curseforge.com/v1",
        _ => throw new InvalidOperationException()
    });

    public static ResourceApiSource Mcim { get; } = new("Mcim", builder => builder switch {
        ResourceApiType.Modrinth => "https://mod.mcimirror.top/modrinth/v2",
        ResourceApiType.CurseForge => "https://mod.mcimirror.top/curseforge/v1",
        _ => throw new InvalidOperationException()
    });
    
    private ResourceApiSource(string name, Func<ResourceApiType, Url> urlBuilder) {
        Name = name;
        _urlBuilder = urlBuilder;
    }

    public Url GetApi(ResourceApiType type) => _urlBuilder.Invoke(type);
    
    public static ResourceApiSource Create(string name, Func<ResourceApiType, Url> urlBuilder) => 
        new(name, urlBuilder);
}