using SulfurLauncher.Core.Services;

namespace SulfurLauncher.Core.Module.LittleSkin;

/// <summary>
/// LittleSkin OAuth2 集成配置。
/// <para>
/// client_id 需站长在 <c>https://littleskin.cn/user/oauth/manage</c> 注册 OAuth 应用后取得，
/// 并在该应用中注册本地回调地址 <c>http://127.0.0.1:{RedirectPort}/callback</c>
/// （LittleSkin 要求固定端口）。授权采用标准授权码 + PKCE 流程，无需白名单。
/// </para>
/// </summary>
public static class LittleSkinSettings
{
    public const string OAuthBase = "https://open.littleskin.cn";
    public const string ApiBase = "https://littleskin.cn/api";
    public const string TextureBase = "https://littleskin.cn/textures";
    public const string YggdrasilUrl = "https://littleskin.cn/api/yggdrasil";

    /// <summary>衣柜 API 所需申请的 OAuth 作用域。</summary>
    public static readonly string[] Scopes = ["Closet.Read", "User.Read"];

    /// <summary>
    /// 本地回调固定端口。LittleSkin 要求回调地址为固定端口，此端口必须与 OAuth 应用中注册的一致。
    /// 默认 <c>50123</c>，可用环境变量 <c>LITTLESKIN_REDIRECT_PORT</c> 覆盖。
    /// </summary>
    public static int RedirectPort
    {
        get
        {
            var raw = Environment.GetEnvironmentVariable("LITTLESKIN_REDIRECT_PORT");
            return int.TryParse(raw, out var port) && port is > 0 and < 65536 ? port : 50123;
        }
    }

    /// <summary>
    /// LittleSkin OAuth 客户端 ID。留空则 LittleSkin 登录时跳过衣柜皮肤同步。
    /// 优先读取构建时嵌入的 AssemblyMetadata（来自 GitHub Secret <c>LITTLESKIN_CLIENT_ID</c>），
    /// 其次回退到运行环境变量 <c>LITTLESKIN_CLIENT_ID</c>，均未设置时使用 <see cref="ConfiguredClientId"/>。
    /// </summary>
    public static string ClientId =>
        CredentialsService.LittleSkinClientId ?? ConfiguredClientId;

    /// <summary>编译期默认客户端 ID（占位）。发布前请在 CI 中配置 GitHub Secret <c>LITTLESKIN_CLIENT_ID</c>。</summary>
    public static string ConfiguredClientId { get; set; } = "";

    /// <summary>判断一个 Yggdrasil 服务器地址是否属于 LittleSkin。</summary>
    public static bool IsLittleSkinUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
            return false;

        return uri.Host.Equals("littleskin.cn", StringComparison.OrdinalIgnoreCase)
               || uri.Host.EndsWith(".littleskin.cn", StringComparison.OrdinalIgnoreCase)
               || uri.Host.Equals("mcskin.littleservice.cn", StringComparison.OrdinalIgnoreCase);
    }
}