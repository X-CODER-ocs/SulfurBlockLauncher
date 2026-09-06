using MinecraftLaunch.Base.Models.Game;

namespace MinecraftLaunch.Base.Models.Network;

/// <summary>
/// 描述一个可从本地资源目录复制到目标目录的依赖项。
/// </summary>
public readonly record struct ResourceCopyItem(MinecraftDependency Dependency, string SourcePath) {
    public string TargetPath => Dependency.FullPath;
}
