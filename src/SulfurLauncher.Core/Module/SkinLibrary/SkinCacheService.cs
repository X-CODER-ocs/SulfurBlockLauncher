using SulfurLauncher.Core.Const;

namespace SulfurLauncher.Core.Module.SkinLibrary;

/// <summary>
/// 皮肤库磁盘缓存。所有皮肤按内容哈希持久化到共享皮肤库目录
/// （{UserDataRootPath}/Skins/_shared-library），文件名遵循
/// <c>v1-{hash24}-{model}.png</c>，同内容皮肤自动去重。
/// </summary>
public sealed class SkinCacheService
{
    public const string SkinCacheVersion = "v1";
    private const string SharedLibraryDirectoryName = "_shared-library";

    public string SharedLibraryDirectory =>
        Path.Combine(ConfigPath.UserDataRootPath, "Skins", SharedLibraryDirectoryName);

    /// <summary>导入 PNG 皮肤字节到共享皮肤库，若已存在同内容皮肤则直接复用。</summary>
    public SkinLibraryItem? Import(byte[] pngBytes, SkinModel skinModel)
    {
        if (pngBytes.Length == 0)
            return null;

        var contentHash = SkinContentHasher.Compute(pngBytes);
        var skinPath = CreateLibrarySkinPath(contentHash, skinModel);
        if (!File.Exists(skinPath))
            File.WriteAllBytes(skinPath, pngBytes);
        return CreateItem(contentHash, skinModel, skinPath);
    }

    /// <summary>导入本地 PNG 皮肤文件到共享皮肤库。</summary>
    public SkinLibraryItem? ImportFile(string filePath, SkinModel skinModel)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;
        return Import(File.ReadAllBytes(filePath), skinModel);
    }

    /// <summary>确保给定字节已存在于共享皮肤库，常用于迁移/同步账户当前皮肤。</summary>
    public SkinLibraryItem? EnsurePresent(byte[] pngBytes, SkinModel skinModel) => Import(pngBytes, skinModel);

    /// <summary>扫描共享皮肤库，返回按添加时间升序排列的皮肤记录。</summary>
    public IReadOnlyList<SkinLibraryItem> List()
    {
        var directory = SharedLibraryDirectory;
        if (!Directory.Exists(directory))
            return [];

        return Directory.EnumerateFiles(directory, "*.png")
            .Select(path => TryCreateItemForFile(path))
            .Where(item => item is not null)
            .Select(item => item!)
            .OrderBy(item => item.AddedAtUtc)
            .ToList();
    }

    /// <summary>删除记录对应的皮肤文件。仅允许删除位于共享皮肤库目录内的文件，外部路径绝不删除。</summary>
    public void Delete(SkinLibraryItem item)
    {
        var directory = Path.GetFullPath(SharedLibraryDirectory);
        if (!Directory.Exists(directory) || item is null || string.IsNullOrWhiteSpace(item.FilePath))
            return;

        var fullPath = Path.GetFullPath(item.FilePath);
        if (IsPathInDirectory(fullPath, directory) && File.Exists(fullPath))
            File.Delete(fullPath);
    }

    private SkinLibraryItem? TryCreateItemForFile(string filePath)
    {
        try
        {
            var contentHash = SkinContentHasher.Compute(File.ReadAllBytes(filePath));
            var skinModel = TryParseSkinModel(filePath) ?? SkinModel.Classic;
            return CreateItem(contentHash, skinModel, filePath);
        }
        catch
        {
            return null;
        }
    }

    private SkinLibraryItem CreateItem(string contentHash, SkinModel skinModel, string filePath)
    {
        return new SkinLibraryItem(
            $"skin-{contentHash[..Math.Min(16, contentHash.Length)]}-{skinModel.ToString().ToLowerInvariant()}",
            filePath,
            skinModel,
            contentHash,
            new DateTimeOffset(File.GetCreationTimeUtc(filePath), TimeSpan.Zero));
    }

    private string CreateLibrarySkinPath(string contentHash, SkinModel skinModel)
    {
        var directory = SharedLibraryDirectory;
        Directory.CreateDirectory(directory);
        var safeHash = contentHash.Length > 24 ? contentHash[..24] : contentHash;
        return Path.Combine(
            directory,
            $"{SkinCacheVersion}-{safeHash}-{skinModel.ToString().ToLowerInvariant()}.png");
    }

    private static SkinModel? TryParseSkinModel(string skinPath)
    {
        var name = Path.GetFileNameWithoutExtension(skinPath);
        if (name.EndsWith("-slim", StringComparison.OrdinalIgnoreCase))
            return SkinModel.Slim;
        if (name.EndsWith("-classic", StringComparison.OrdinalIgnoreCase))
            return SkinModel.Classic;
        return null;
    }

    private static bool IsPathInDirectory(string path, string directory)
    {
        var normalizedDirectory = directory.EndsWith(Path.DirectorySeparatorChar)
            ? directory
            : directory + Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
    }
}