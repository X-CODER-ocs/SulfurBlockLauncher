namespace SulfurLauncher.Core.Module.SkinLibrary;

/// <summary>
/// 皮肤库中的一条皮肤记录，<see cref="FilePath"/> 指向共享皮肤库中的本地 PNG 文件。
/// 内容身份基于皮肤解码后的像素哈希，便于跨账号去重与复用。
/// </summary>
public sealed record SkinLibraryItem(
    string Id,
    string FilePath,
    SkinModel SkinModel,
    string ContentHash,
    DateTimeOffset AddedAtUtc);