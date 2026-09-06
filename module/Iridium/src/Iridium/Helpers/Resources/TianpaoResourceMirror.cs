using Iridium.Resources;

namespace Iridium.Helpers.Resources;

public sealed class TianpaoResourceMirror : IResourceMirror {
    public string Name => "Tianpao";

    private static readonly (string From, string To)[] Rewrites =
    [
        ("https://edge.forgecdn.net/files/", "https://mod.telepao.com/files/"),
        ("http://edge.forgecdn.net/files/", "https://mod.telepao.com/files/"),
        ("https://mediafilez.forgecdn.net/files/", "https://mod.telepao.com/files/"),
        ("http://mediafilez.forgecdn.net/files/", "https://mod.telepao.com/files/"),
        ("https://cdn.modrinth.com/data/", "https://mod.telepao.com/data/"),
        ("http://cdn.modrinth.com/data/", "https://mod.telepao.com/data/"),
        ("https://media.forgecdn.net/", "https://mod.telepao.com/media/"),
        ("http://media.forgecdn.net/", "https://mod.telepao.com/media/")
    ];

    public string? TryRewrite(string url) {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        foreach (var (from, to) in Rewrites)
            if (url.StartsWith(from, StringComparison.OrdinalIgnoreCase))
                return to + url[from.Length..];

        return null;
    }
}
