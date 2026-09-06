using Flurl;
using Flurl.Http;
using Flurl.Http.Configuration;
using System.Net;
using System.Diagnostics;

namespace MinecraftLaunch.Utilities;

public static class HttpUtil {
    private static HttpClient _downloaderClient = CreateClient(false, null, "MinecraftLaunch/4.0");
    private static IFlurlClient _flurlClient = CreateFlurlClient(false, null, "MinecraftLaunch/4.0");

    internal static HttpClient DownloaderClient => _downloaderClient;
    // Installers are public APIs and must work even when the host has not supplied custom settings.
    public static IFlurlClient FlurlClient => _flurlClient;
    public static HttpClient Client => _downloaderClient;

    public static void Configure(bool disableSystemProxy, string proxyServer, string userAgent) {
        var newDownloaderClient = CreateClient(disableSystemProxy, proxyServer, userAgent);
        var newFlurlClient = CreateFlurlClient(disableSystemProxy, proxyServer, userAgent);
        Interlocked.Exchange(ref _downloaderClient, newDownloaderClient);
        Interlocked.Exchange(ref _flurlClient, newFlurlClient);
    }

    public static IFlurlRequest Request(Url url) {
        return FlurlClient.Request(url);
    }

    public static IFlurlRequest Request(string url) {
        return FlurlClient.Request(url);
    }

    public static IFlurlRequest Request(Url baseUrl, params string[] paths) {
        return FlurlClient.Request(baseUrl.AppendPathSegments(paths));
    }

    private static IFlurlClient CreateFlurlClient(bool disableSystemProxy, string proxyServer, string userAgent) {
        return new FlurlClient(CreateClient(disableSystemProxy, proxyServer, userAgent)) {
            Settings = {
                Timeout = TimeSpan.FromSeconds(15),
                JsonSerializer = new DefaultJsonSerializer(JsonSerializerUtil.GetDefaultOptions()),
                Redirects = { Enabled = true }
            }
        };
    }

    private static HttpClient CreateClient(bool disableSystemProxy, string proxyServer, string userAgent) {
        var hasProxyServer = TryGetProxyUri(proxyServer, out var proxyUri);
        var socketsHandler = new SocketsHttpHandler {
            UseProxy = !disableSystemProxy || hasProxyServer
        };
        if (hasProxyServer) socketsHandler.Proxy = new WebProxy(proxyUri);
        var client = new HttpClient(new DownloadSourceHandler(socketsHandler));
        if (!string.IsNullOrWhiteSpace(userAgent)) client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        return client;
    }

    private sealed class DownloadSourceHandler(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler) {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            var canFallback = request.Method == HttpMethod.Get || request.Method == HttpMethod.Head;
            var urls = canFallback ? DownloadManager.ResolveUrls(request.RequestUri!.AbsoluteUri) : [request.RequestUri!.AbsoluteUri];
            var body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            Exception lastError = null;
            HttpResponseMessage lastResponse = null;
            var attempts = urls.Count;

            for (var attempt = 0; attempt < attempts; attempt++) {
                var url = urls[attempt % urls.Count];
                using var copy = CloneRequest(request, url, body);
                var started = Stopwatch.GetTimestamp();
                try {
                    var response = await base.SendAsync(copy, cancellationToken).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode) {
                        lastResponse?.Dispose();
                        if (!DownloadManager.IsFileTransfer(url))
                            DownloadManager.RecordSuccess(url, Stopwatch.GetElapsedTime(started));
                        return response;
                    }
                    lastError = new HttpRequestException($"HTTP {(int)response.StatusCode} from {new Uri(url).Host}");
                    DownloadManager.RecordFailure(url);
                    lastResponse?.Dispose();
                    lastResponse = response;
                } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                    throw;
                } catch (Exception exception) {
                    lastError = exception;
                    DownloadManager.RecordFailure(url);
                }

                if (attempt + 1 < attempts)
                    await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), cancellationToken).ConfigureAwait(false);
            }
            if (lastResponse is not null)
                return lastResponse;
            throw lastError ?? new HttpRequestException("All download sources failed.");
        }

        private static HttpRequestMessage CloneRequest(HttpRequestMessage source, string url, byte[] body) {
            var clone = new HttpRequestMessage(source.Method, url) { Version = source.Version, VersionPolicy = source.VersionPolicy };
            foreach (var header in source.Headers)
                if (!IsSensitiveMirrorHeader(header.Key, source.RequestUri!, new Uri(url)))
                    clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            if (body is not null) {
                clone.Content = new ByteArrayContent(body);
                foreach (var header in source.Content!.Headers)
                    clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            foreach (var option in source.Options)
                clone.Options.TryAdd(option.Key, option.Value);
            return clone;
        }

        private static bool IsSensitiveMirrorHeader(string name, Uri source, Uri target) =>
            !string.Equals(source.Host, target.Host, StringComparison.OrdinalIgnoreCase) &&
            (name.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
             name.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase) ||
             name.Equals("Cookie", StringComparison.OrdinalIgnoreCase) ||
             name.Equals("x-api-key", StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryGetProxyUri(string proxyServer, out Uri proxyUri) {
        if (!string.IsNullOrWhiteSpace(proxyServer) && !proxyServer.Contains("://", StringComparison.Ordinal))
            proxyServer = $"http://{proxyServer}";
        return Uri.TryCreate(proxyServer, UriKind.Absolute, out proxyUri);
    }
}
