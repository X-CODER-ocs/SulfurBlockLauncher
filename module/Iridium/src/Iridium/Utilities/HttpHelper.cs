using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Flurl;
using Flurl.Http;

namespace Iridium.Utilities;

public static class HttpHelper {
    public static async Task<T?> GetJsonAsync<T>(
        Url url,
        JsonTypeInfo<T> jsonType,
        CancellationToken cancellationToken = default) where T : class {
        using var response = await url.GetAsync(
            HttpCompletionOption.ResponseContentRead, cancellationToken);
        
        return await ReadJsonAsync(response, jsonType, cancellationToken);
    }

    public static async Task<T?> GetJsonOrNullAsync<T>(
        Url url,
        JsonTypeInfo<T> jsonType, 
        CancellationToken cancellationToken = default) where T : class {
        try {
            return await GetJsonAsync(url, jsonType, cancellationToken);
        } catch (FlurlHttpException exception) when (exception.StatusCode == (int)HttpStatusCode.NotFound) {
            return null;
        }
    }

    public static async Task<TResult?> PostJsonAsync<TBody, TResult>(
        Url url,
        TBody body,
        JsonTypeInfo<TBody> bodyType,
        JsonTypeInfo<TResult> resultType,
        CancellationToken cancellationToken = default) where TResult : class {
        using var response = await url.PostAsync(
            JsonContent.Create(body, bodyType), cancellationToken: cancellationToken);
        
        return await ReadJsonAsync(response, resultType, cancellationToken);
    }

    public static async Task<TResult?> PostJsonOrNullAsync<TBody, TResult>(
        Url url,
        TBody body,
        JsonTypeInfo<TBody> bodyType,
        JsonTypeInfo<TResult> resultType,
        CancellationToken cancellationToken = default) where TResult : class {
        try {
            return await PostJsonAsync(url, body, bodyType, resultType, cancellationToken);
        } catch (FlurlHttpException exception) when (exception.StatusCode == (int)HttpStatusCode.NotFound) {
            return null;
        }
    }

    public static async Task<T?> ReadJsonAsync<T>(
        IFlurlResponse response, 
        JsonTypeInfo<T> jsonType,
        CancellationToken cancellationToken) where T : class {
        await using var stream = await response.GetStreamAsync();
        return await JsonSerializer.DeserializeAsync(stream, jsonType, cancellationToken);
    }
}