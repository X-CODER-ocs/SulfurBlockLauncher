using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Tio.Avalonia.Standard.Modules.DiskIO;

namespace SulfurLauncher.Core.Module.LittleSkin;

/// <summary>
/// 设备代码对（RFC 8628）。用户代码需要展示给用户，设备代码用于应用轮询授权结果。
/// </summary>
public sealed record LittleSkinDeviceCode(
    string UserCode,
    string DeviceCode,
    string VerificationUri,
    string VerificationUriComplete,
    int ExpiresIn,
    int Interval);

/// <summary>可通过调用获得，用于轮询授权结果期间主动取消。</summary>
public enum LittleSkinPollError
{
    /// <summary>用户尚未完成授权，继续轮询。</summary>
    Pending,

    /// <summary>轮询过快，需按间隔稍后再试。</summary>
    SlowDown,

    /// <summary>设备代码/授权已过期。</summary>
    Expired,

    /// <summary>用户拒绝了授权。</summary>
    Denied,

    /// <summary>客户端 ID 无效（通常为未加入设备代码流白名单）。</summary>
    InvalidClient,

    /// <summary>其它错误。</summary>
    Unknown
}

/// <summary>
/// LittleSkin OAuth2 设备代码流服务端。负责请求设备代码对，并在用户授权后轮询换取访问令牌。
/// 相关协议见 <c>https://manual.littleskin.cn/advanced/oauth2/device-authorization-grant</c>。
/// </summary>
public sealed class LittleSkinOAuthService
{
    private const string DeviceCodeGrantType = "urn:ietf:params:oauth:grant-type:device_code";
    private const string RefreshTokenGrantType = "refresh_token";

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };

    private LittleSkinOAuthService()
    {
    }

    public static LittleSkinOAuthService Instance { get; } = new();

    /// <summary>
    /// 请求一个设备代码对。需要 <see cref="LittleSkinSettings.ClientId"/> 已配置，
    /// 且该应用已在 LittleSkin 申请设备代码流白名单。
    /// </summary>
    public async Task<LittleSkinDeviceCode> RequestDeviceCodeAsync(string[] scopes,
        CancellationToken cancellationToken)
    {
        var clientId = LittleSkinSettings.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("LittleSkin client_id 未配置，请先设置 LITTLESKIN_CLIENT_ID。");

        var scope = scopes.Length > 0 ? string.Join(" ", scopes) : "User.Read";
        var body = $"client_id={Uri.EscapeDataString(clientId)}&scope={Uri.EscapeDataString(scope)}";

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"{LittleSkinSettings.OAuthBase}/oauth/device_code")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await Client.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = TryReadOAuthError(json);
            Logger.Warning(
                $"[LittleSkin] 请求设备代码对失败 (HTTP {(int)response.StatusCode}), error={error?.Error}");
            throw new InvalidOperationException(
                error is not null && error.Error == "invalid_client"
                    ? "LittleSkin 应用未加入设备代码流白名单，或客户端 ID 无效。"
                    : $"LittleSkin 请求设备代码对失败：HTTP {(int)response.StatusCode}。");
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return new LittleSkinDeviceCode(
            GetString(root, "user_code") ?? string.Empty,
            GetString(root, "device_code") ?? string.Empty,
            GetString(root, "verification_uri") ?? $"{LittleSkinSettings.OAuthBase}/oauth/link",
            GetString(root, "verification_uri_complete") ?? string.Empty,
            GetInt(root, "expires_in", 300),
            GetInt(root, "interval", 5));
    }

    /// <summary>
    /// 以 interval 为间隔轮询授权结果，直到用户在授权页面完成授权或失败/过期。
    /// </summary>
    /// <returns>轮询得到的状态。若 <see cref="LittleSkinPollResult.Succeeded"/> 为 true，
    /// 则 <see cref="LittleSkinPollResult.AccessToken"/> 可用。</returns>
    public async Task<LittleSkinPollResult> PollForTokenAsync(LittleSkinDeviceCode code,
        CancellationToken cancellationToken)
    {
        var clientId = LittleSkinSettings.ClientId;
        var interval = Math.Max(5, code.Interval);
        while (!cancellationToken.IsCancellationRequested)
        {
            var body =
                $"grant_type={Uri.EscapeDataString(DeviceCodeGrantType)}&client_id={Uri.EscapeDataString(clientId)}&device_code={Uri.EscapeDataString(code.DeviceCode)}";

            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"{LittleSkinSettings.OAuthBase}/oauth/token")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded")
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await Client.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var accessToken = GetString(root, "access_token");
                if (!string.IsNullOrWhiteSpace(accessToken))
                {
                    return new LittleSkinPollResult(
                        true, accessToken,
                        GetString(root, "refresh_token"),
                        GetString(root, "id_token"));
                }
            }

            var error = TryReadOAuthError(json);
            if (error is not null)
            {
                switch (error.Error)
                {
                    case "authorization_pending":
                        break;
                    case "slow_down":
                        interval += 5;
                        break;
                    case "access_denied":
                        return new LittleSkinPollResult(false, null, null, null, LittleSkinPollError.Denied,
                            "用户拒绝了授权。");
                    case "expired_token":
                        return new LittleSkinPollResult(false, null, null, null, LittleSkinPollError.Expired,
                            "设备代码已过期，请重新开始授权。");
                    case "invalid_client":
                        return new LittleSkinPollResult(false, null, null, null, LittleSkinPollError.InvalidClient,
                            "客户端 ID 无效或应用未加入白名单。");
                    default:
                        return new LittleSkinPollResult(false, null, null, null, LittleSkinPollError.Unknown,
                            $"授权失败：{error.Error}");
                }
            }
            else
            {
                return new LittleSkinPollResult(false, null, null, null, LittleSkinPollError.Unknown,
                    $"授权失败：HTTP {(int)response.StatusCode}");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return new LittleSkinPollResult(false, null, null, null, LittleSkinPollError.Unknown, "授权已取消。");
    }

    /// <summary>使用刷新令牌换取新的访问令牌。</summary>
    public async Task<string?> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var clientId = LittleSkinSettings.ClientId;
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"{LittleSkinSettings.OAuthBase}/oauth/token")
        {
            Content = new StringContent(
                $"grant_type={Uri.EscapeDataString(RefreshTokenGrantType)}&refresh_token={Uri.EscapeDataString(refreshToken)}&client_id={Uri.EscapeDataString(clientId)}",
                Encoding.UTF8, "application/x-www-form-urlencoded")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await Client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        return GetString(doc.RootElement, "access_token");
    }

    private sealed record OAuthError(string Error, string? Description);

    private static OAuthError? TryReadOAuthError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var error = GetString(root, "error");
            if (error is null) return null;
            return new OAuthError(error, GetString(root, "error_description") ?? GetString(root, "message"));
        }
        catch
        {
            return null;
        }
    }

    private static string? GetString(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int GetInt(JsonElement element, string property, int defaultValue)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value))
            return defaultValue;
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var i) => i,
            JsonValueKind.String when int.TryParse(value.GetString(), out var i) => i,
            _ => defaultValue
        };
    }
}

/// <summary>轮询授权结果后的返回数据。</summary>
public sealed record LittleSkinPollResult(
    bool Succeeded,
    string? AccessToken,
    string? RefreshToken,
    string? IdToken,
    LittleSkinPollError Error = LittleSkinPollError.Unknown,
    string? ErrorMessage = null);