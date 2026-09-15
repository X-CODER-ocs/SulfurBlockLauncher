namespace SulfurLauncher.Core.Module.LittleSkin;

/// <summary>
/// LittleSkin OAuth2 集成配置。
/// <para>
/// client_id 需站长在 <c>https://littleskin.cn/user/oauth/manage</c> 注册 OAuth 应用后取得，
/// 并把回调地址设置为 <c>https://open.littleskin.cn/oauth/callback</c>；同时设备代码流还需要
/// 发邮件向 LittleSkin 申请白名单后才能真正使用。
/// </para>
/// </summary>
public static class LittleSkinSettings
{
    public const string OAuthBase = "https://open.littleskin.cn";
    public const string ApiBase = "https://littleskin.cn/api";
    public const string TextureBase = "https://littleskin.cn/textures";
    public const string YggdrasilUrl = "https://littleskin.cn/api/yggdrasil";

    /// <summary>设备代码流与衣柜 API 所需申请的 OAuth 作用域。</summary>
    public static readonly string[] Scopes = ["Closet.Read", "User.Read"];

    /// <summary>
    /// LittleSkin OAuth 客户端 ID。留空则 LittleSkin 登录时跳过衣柜皮肤同步。
    /// 优先读取环境变量 <c>LITTLESKIN_CLIENT_ID</c>，未设置时回退到编译期常量 <see cref="ConfiguredClientId"/>。
    /// </summary>
    public static string ClientId =>
        Environment.GetEnvironmentVariable("LITTLESKIN_CLIENT_ID")
        ?? ConfiguredClientId;

    /// <summary>编译期默认客户端 ID（占位）。发布前请回填真实值。</summary>
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