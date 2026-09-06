namespace Iridium.Resources;

public interface IResourceMirror {

    string Name { get; }

    string? TryRewrite(string url);
}
