using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Flurl;
using Flurl.Http;
using Iridium.Download;
using Iridium.Enums;
using Iridium.Resources;
using Iridium.Interfaces;

namespace Iridium.Resources.CurseForge;

public sealed partial class CurseForgeClient : IResourceClient {
    private const int MinecraftGameId = 432;
    private const int MaxBatchSize = 50;
    private const int PageSize = 20;

    private readonly string _apiKey;

    public ResourceApiSource ResourceApiSource { get; }

    public CurseForgeClient(string apiKey, ResourceApiSource? source = null) {
        ArgumentException.ThrowIfNullOrEmpty(apiKey);
        
        ResourceApiSource = source ?? ResourceApiSource.Official;
        _apiKey = apiKey;
    }

    private Url BaseUrl => ResourceApiSource.GetApi(ResourceApiType.CurseForge);

    private async Task<T?> GetJsonAsync<T>(Url url, JsonTypeInfo<T> jsonType, CancellationToken cancellationToken) where T : class {
        using var response = await url.WithHeader("x-api-key", _apiKey)
            .GetAsync(HttpCompletionOption.ResponseContentRead, cancellationToken);
        
        return await ReadJsonAsync(response, jsonType, cancellationToken);
    }

    private async Task<T?> GetJsonOrNullAsync<T>(Url url, JsonTypeInfo<T> jsonType, CancellationToken cancellationToken) where T : class {
        try {
            return await GetJsonAsync(url, jsonType, cancellationToken);
        } catch (FlurlHttpException exception) when (exception.StatusCode == (int)HttpStatusCode.NotFound) {
            return null;
        }
    }

    private static async Task<T?> ReadJsonAsync<T>(IFlurlResponse response, JsonTypeInfo<T> jsonType, CancellationToken cancellationToken) where T : class {
        await using var stream = await response.GetStreamAsync(); 
        return await JsonSerializer.DeserializeAsync(stream, jsonType, cancellationToken);
    }
    
    private async Task<TResult?> PostJsonAsync<TBody, TResult>(
        Url url,
        TBody body,
        JsonTypeInfo<TBody> bodyType,
        JsonTypeInfo<TResult> resultType,
        CancellationToken cancellationToken) where TResult : class {
        using var response = await url.WithHeader("x-api-key", _apiKey)
            .PostAsync(JsonContent.Create(body, bodyType), cancellationToken: cancellationToken);
        
        return await ReadJsonAsync(response, resultType, cancellationToken);
    }

    private static long ParseId(string id) =>
        long.TryParse(id, out var value)
            ? value
            : throw new ArgumentException($"无效的 CurseForge ID: {id}", nameof(id));
}
