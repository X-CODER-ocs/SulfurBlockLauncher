using Avalonia.Media.Imaging;
using LiteSkinViewer2D;
using LiteSkinViewer2D.Extensions;
using SulfurLauncher.Core.Minecraft.Classes;
using SkiaSharp;

namespace SulfurLauncher.Core.Module.SkinLibrary;

/// <summary>
/// 皮肤库对外门面：把共享皮肤库与 Minecraft 账户（<see cref="MinecraftAccount.Skin"/>，
/// 以 base64 PNG 存储）桥接起来，提供导入、应用、删除、同步与头像生成。
/// </summary>
public sealed class SkinLibraryService
{
    private readonly SkinCacheService _cache = new();

    private SkinLibraryService()
    {
    }

    public static SkinLibraryService Instance { get; } = new();

    /// <summary>返回共享皮肤库中的所有皮肤。</summary>
    public IReadOnlyList<SkinLibraryItem> GetAll() => _cache.List();

    /// <summary>从本地 PNG 文件导入皮肤库。</summary>
    public SkinLibraryItem? ImportFile(string filePath, SkinModel skinModel) =>
        _cache.ImportFile(filePath, skinModel);

    /// <summary>把当前账户的皮肤（base64）保存进皮肤库。</summary>
    public SkinLibraryItem? ImportFromAccount(MinecraftAccount account)
    {
        if (string.IsNullOrWhiteSpace(account.Skin))
            return null;

        var bytes = TryDecodeBase64(account.Skin);
        if (bytes == null)
            return null;
        return _cache.Import(bytes, SkinModel.Classic);
    }

    /// <summary>把皮肤库中的某个皮肤应用（写入）到指定账户。</summary>
    public void ApplyToAccount(MinecraftAccount account, SkinLibraryItem item)
    {
        if (account is null || item is null || !File.Exists(item.FilePath))
            return;
        account.Skin = Convert.ToBase64String(File.ReadAllBytes(item.FilePath));
    }

    /// <summary>同步：把各账户当前展示的皮肤（非默认 Steve）补齐到共享皮肤库。</summary>
    public int SyncFromAccounts(IEnumerable<MinecraftAccount> accounts)
    {
        var added = 0;
        foreach (var account in accounts)
        {
            if (account is null)
                continue;

            var item = ImportFromAccount(account);
            if (item != null)
                added++;
        }

        return added;
    }

    public void Delete(SkinLibraryItem item) => _cache.Delete(item);

    /// <summary>生成皮肤头部头像（Avalonia Bitmap），用于列表缩略图展示。</summary>
    public Bitmap? GetHeadImage(SkinLibraryItem item, int size = 48)
    {
        if (item is null || !File.Exists(item.FilePath))
            return null;

        try
        {
            using var skin = SKBitmap.Decode(File.ReadAllBytes(item.FilePath));
            using var head = HeadCapturer.Default.Capture(skin);
            return head.ToBitmap(size);
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? TryDecodeBase64(string value)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch
        {
            return null;
        }
    }
}