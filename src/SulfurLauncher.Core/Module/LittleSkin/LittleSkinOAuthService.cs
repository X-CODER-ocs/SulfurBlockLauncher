using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Tio.Avalonia.Standard.Modules.DiskIO;

namespace SulfurLauncher.Core.Module.LittleSkin;

/// <summary>
/// 本地授权会话：应用在环回地址临时监听一个端口，引导用户在浏览器中完成授权后，
/// LittleSkin 会把 <c>code</c> 回调到该端口，应用再用 PKCE 校验码换取访问令牌。
/// 整个过程遵循标准 OAuth2 授权码 + PKCE，无需设备代码流白名单。
/// </summary>
public sealed class LittleSkinLocalAuthSession : IDisposable
{
    private readonly string _codeVerifier;
    private readonly string _state;
    private readonly TcpListener _listener;

    internal LittleSkinLocalAuthSession(
        string authorizationUri, string redirectUri,
        string codeVerifier, string state, TcpListener listener)
    {
        AuthorizationUri = authorizationUri;
        RedirectUri = redirectUri;
        _codeVerifier = codeVerifier;
        _state = state;
        _listener = listener;
    }

    /// <summary>需要引导用户浏览器打开的授权地址。</summary>
    public string AuthorizationUri { get; }

    /// <summary>回调地址（形如 http://127.0.0.1:端口/callback）。</summary>
    public string RedirectUri { get; }

    /// <summary>
    /// 等待用户在浏览器中完成授权，通过 PKCE 换取访问令牌。
    /// 浏览器关闭或本地监听被取消时返回 null。
    /// </summary>
    public async Task<string?> WaitForTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
            var downstream = client.GetStream();
            var (path, query) = await ReadRequestAsync(downstream, cancellationToken);

            var code = QueryValue(query, "code");
            var state = QueryValue(query, "state");

            // 无论成败都给浏览器一个收尾页面，避免用户停在空白页。
            var ok = state == _state && !string.IsNullOrWhiteSpace(code);
            await WriteResponseAsync(downstream, ok
                ? "授权成功，现在可以回到启动器了。"
                : "授权失败或已取消，请回到启动器重试。", cancellationToken);

            if (state != _state)
                return null;
            if (string.IsNullOrWhiteSpace(code))
                return null;

            return await ExchangeCodeForTokenAsync(code, _codeVerifier, RedirectUri, cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
        catch (SocketException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        try
        {
            _listener.Stop();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 关闭监听失败可以忽略
        }
    }

    private static async Task<(string Path, string Query)> ReadRequestAsync(
        NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[8 * 1024];
        var sb = new StringBuilder();
        while (sb.Length < buffer.Length && !CheckHeaderEnd(sb))
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read <= 0)
                break;
            sb.Append(Encoding.UTF8.GetString(buffer, 0, read));
        }

        var header = sb.ToString();
        var requestLine = header.Split('\n')[0];
        var parts = requestLine.Split(' ');
        var path = parts.Length > 1 ? parts[1] : "/";
        var query = string.Empty;
        var qIndex = path.IndexOf('?');
        if (qIndex >= 0)
        {
            query = path[(qIndex + 1)..];
            path = path[..qIndex];
        }

        _ = path; // 仅需 query 中的参数
        return (path, query);
    }

    private static bool CheckHeaderEnd(StringBuilder sb) =>
        sb.ToString().Contains("\r\n\r\n", StringComparison.Ordinal) ||
        sb.ToString().Contains("\n\n", StringComparison.Ordinal);

    private static string? QueryValue(string query, string key)
    {
        foreach (var pair in query.Split('&'))
        {
            if (string.IsNullOrEmpty(pair)) continue;
            var eq = pair.IndexOf('=');
            var name = eq < 0 ? pair : pair[..eq];
            var value = eq < 0 ? "" : System.Net.WebUtility.UrlDecode(pair[(eq + 1)..]);
            if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                return value;
        }

        return null;
    }

    private static async Task WriteResponseAsync(
        NetworkStream stream, string message, CancellationToken cancellationToken)
    {
        var body = Encoding.UTF8.GetBytes(message);
        var header = $"HTTP/1.1 200 OK\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(header), cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<string?> ExchangeCodeForTokenAsync(
        string code, string codeVerifier, string redirectUri, CancellationToken cancellationToken)
    {
        var clientId = LittleSkinSettings.ClientId;
        var body =
            $"grant_type=authorization_code" +
            $"&client_id={Uri.EscapeDataString(clientId)}" +
            $"&code={Uri.EscapeDataString(code)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&code_verifier={Uri.EscapeDataString(codeVerifier)}";

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"{LittleSkinSettings.OAuthBase}/oauth/token")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await LittleSkinOAuthService.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            Logger.Warning($"[LittleSkin] 换取访问令牌失败 (HTTP {(int)response.StatusCode})");
            return null;
        }

        using var doc = JsonDocument.Parse(json);
        return GetString(doc.RootElement, "access_token");
    }

    private static string? GetString(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}

/// <summary>
/// 授权码 + PKCE 的 OAuth2 服务端。替代设备代码流，可通过本地环回端口完成授权，无需白名单。
/// </summary>
public sealed class LittleSkinOAuthService
{
    private LittleSkinOAuthService()
    {
    }

    public static LittleSkinOAuthService Instance { get; } = new();

    /// <summary>
    /// 启动一次本地授权：分配临时端口、生成 PKCE 校验码，返回授权会话。
    /// <see cref="LittleSkinLocalAuthSession.AuthorizationUri"/> 用于浏览器跳转。
    /// </summary>
    public LittleSkinLocalAuthSession StartAuthorization(string[] scopes, out string? error)
    {
        var clientId = LittleSkinSettings.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            error = "尚未配置 LittleSkin client_id。";
            return null!;
        }

        error = null;

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var redirectUri = $"http://127.0.0.1:{port}/callback";

        var codeVerifier = CreateCodeVerifier();
        var codeChallenge = CreateCodeChallenge(codeVerifier);
        var state = CreateState();

        var scope = scopes.Length > 0 ? string.Join(" ", scopes) : "User.Read";
        var authorizationUri =
            $"{LittleSkinSettings.OAuthBase}/oauth/authorize" +
            $"?client_id={Uri.EscapeDataString(clientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&response_type=code" +
            $"&scope={Uri.EscapeDataString(scope)}" +
            $"&state={Uri.EscapeDataString(state)}" +
            $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
            $"&code_challenge_method=S256";

        return new LittleSkinLocalAuthSession(authorizationUri, redirectUri,
            codeVerifier, state, listener);
    }

    internal static Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // LittleSkinLocalAuthSession 复用同一个 HttpClient，避免重复创建连接。
        return LittleSkinOAuthServiceShared.SendAsync(request, cancellationToken);
    }

    private static string CreateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string CreateCodeChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string CreateState() =>
        Guid.NewGuid().ToString("N");
}

/// <summary>共享的 HTTP 客户端主机，供 OAuth 服务与本地授权会话复用拆线化管理。</summary>
internal static class LittleSkinOAuthServiceShared
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };

    internal static Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        Client.SendAsync(request, cancellationToken);
}