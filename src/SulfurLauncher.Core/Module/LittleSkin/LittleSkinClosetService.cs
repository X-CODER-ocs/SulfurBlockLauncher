using System.Net.Http.Headers;
using System.Text.Json;
using SulfurLauncher.Core.Module.SkinLibrary;
using Tio.Avalonia.Standard.Modules.DiskIO;

namespace SulfurLauncher.Core.Module.LittleSkin;

/// <summary>LittleSkin 衣柜中的一条皮肤。Hash 用于拼接贴图下载地址，Model 区分经典/纤瘦。</summary>
public sealed record LittleSkinSkinItem(
    string Id,
    string Name,
    string TextureHash,
    SkinModel Model);

/// <summary>
/// LittleSkin 衣柜皮肤列表服务。通过 OAuth 访问令牌调用 Blessing Skin API 列出用户衣柜
/// 并从 LittleSkin 下载贴图 PNG 字节。
/// <para>
/// 注意：LittleSkin/Blessing Skin API 目前处于试验阶段，字段结构可能随版本变化，
/// 这里的解析采用克制而容错的方式，关键字段缺失时该项会被跳过。
/// </para>
/// </summary>
public sealed class LittleSkinClosetService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };

    private LittleSkinClosetService()
    {
    }

    public static LittleSkinClosetService Instance { get; } = new();

    /// <summary>拉取用户衣柜中的全部皮肤（自动翻页）。需要 <c>Closet.Read</c> 权限。</summary>
    public async Task<List<LittleSkinSkinItem>> ListClosetAsync(string accessToken,
        CancellationToken cancellationToken)
    {
        var items = new List<LittleSkinSkinItem>();
        var page = 0;
        var lastPage = 1;

        while (page < lastPage)
        {
            page++;
            var endpoint = $"{LittleSkinSettings.ApiBase}/closet?page={page}";
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await Client.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Warning($"[LittleSkin] 拉取衣柜失败 (HTTP {(int)response.StatusCode})：{Truncate(json)}");
                throw new HttpRequestException($"拉取衣柜失败：HTTP {(int)response.StatusCode}");
            }

            var (parsed, nextLastPage) = ParsePage(json);
            items.AddRange(parsed);
            lastPage = nextLastPage;

            if (parsed.Count == 0)
                break;
        }

        return items;
    }

    /// <summary>根据贴图 Hash 下载皮肤 PNG 字节。</summary>
    public async Task<byte[]?> DownloadTextureAsync(string textureHash, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(textureHash))
            return null;

        var url = $"{LittleSkinSettings.TextureBase}/{textureHash}";
        try
        {
            using var response = await Client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.Warning($"[LittleSkin] 下载贴图 {textureHash} 失败：{ex.Message}");
            return null;
        }
    }

    private static (List<LittleSkinSkinItem> Items, int LastPage) ParsePage(string json)
    {
        var items = new List<LittleSkinSkinItem>();
        var lastPage = 1;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var data = FindDataElement(root);
            if (data is null)
                return (items, lastPage);

            // 分页信息（部分版本为 data.pagination.last_page，部分为顶层）
            lastPage = ReadLastPage(root) ?? ReadLastPage(data.Value) ?? 1;

            var array = data.Value.ValueKind == JsonValueKind.Array ? data.Value : FindArrayElement(data.Value);
            if (array is null)
                return (items, lastPage);

            foreach (var entry in array.EnumerateArray())
            {
                var item = TryParseClosetItem(entry);
                if (item != null)
                    items.Add(item);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.Warning($"[LittleSkin] 解析衣柜响应失败：{ex.Message}");
        }

        return (items, lastPage);
    }

    private static LittleSkinSkinItem? TryParseClosetItem(JsonElement entry)
    {
        var hash = FindString(entry, new[] { "texture", "hash", "t_hash", "texture_hash", "hashValue" });
        if (string.IsNullOrWhiteSpace(hash))
            return null;

        var id = FindString(entry, new[] { "id" }) ?? hash;
        var name = FirstNonEmpty(entry, new[] { "item_name", "name", "texture_name" }) ?? "LittleSkin 皮肤";
        var model = DetectModel(entry);

        return new LittleSkinSkinItem(id, name, hash, model);
    }

    private static SkinModel DetectModel(JsonElement entry)
    {
        // 皮肤模型中常见取值：slim / Alexander / slim_alex；经典为 classic / Steve / steve
        var raw = FirstNonEmpty(entry, new[] { "model", "skin_model" });
        if (raw is not null && raw.Contains("slim", StringComparison.OrdinalIgnoreCase))
            return SkinModel.Slim;
        return SkinModel.Classic;
    }

    private static JsonElement? FindDataElement(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data))
            return data;
        return null;
    }

    private static JsonElement? FindArrayElement(JsonElement data)
    {
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("data", out var nested) &&
            nested.ValueKind == JsonValueKind.Array)
            return nested;
        return null;
    }

    private static int? ReadLastPage(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;
        if (element.TryGetProperty("pagination", out var pagination) &&
            pagination.ValueKind == JsonValueKind.Object &&
            pagination.TryGetProperty("last_page", out var lp) && lp.TryGetInt32(out var lpV))
            return lpV;
        if (element.TryGetProperty("last_page", out var last) && last.TryGetInt32(out var lv))
            return lv;
        return null;
    }

    private static string? FindString(JsonElement entry, string[] candidates)
    {
        if (entry.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var candidate in candidates)
        {
            if (!entry.TryGetProperty(candidate, out var value))
                continue;
            if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                return value.GetString();

            // 嵌套对象场景：texture 字段本身是一个贴图对象，内含 hash
            if (value.ValueKind == JsonValueKind.Object)
            {
                var inner = FirstNonEmpty(value, new[] { "hash", "data", "texture" });
                if (inner != null)
                    return inner;
            }
        }

        return null;
    }

    private static string? FirstNonEmpty(JsonElement entry, string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var value = FindString(entry, new[] { candidate });
            if (value is not null)
                return value;
        }
        return null;
    }

    private static string Truncate(string value, int max = 200) =>
        value.Length > max ? value[..max] : value;
}