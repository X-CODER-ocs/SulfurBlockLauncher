using System;

namespace Portal.Core.Helpers;

public static class UrlHelper
{
    public static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        string input = url.Trim();
        string tempUriStr = input;
        if (!input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            tempUriStr = $"https://{input}";
        }

        try
        {
            Uri uri = new Uri(tempUriStr);
            string hostPart = uri.Authority;
            string path = uri.AbsolutePath.TrimEnd('/');
            string query = uri.Query;
            string fragment = uri.Fragment;
            string result = $"{hostPart}{path}{query}{fragment}";
            return result;
        }
        catch (UriFormatException)
        {
            return input.TrimEnd('/');
        }
    }

    public static bool AreUrlsEqual(string url1, string url2)
    {
        if (string.IsNullOrWhiteSpace(url1) && string.IsNullOrWhiteSpace(url2))
            return true;

        if (string.IsNullOrWhiteSpace(url1) || string.IsNullOrWhiteSpace(url2))
            return false;

        var normalizedUrl1 = NormalizeUrl(url1);
        var normalizedUrl2 = NormalizeUrl(url2);

        return string.Equals(normalizedUrl1, normalizedUrl2, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsValidUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        url = "http://" + NormalizeUrl(url);

        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
               && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }

    public static string GetHost(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        string input = url.Trim();

        if (Uri.TryCreate(input, UriKind.Absolute, out var uriResult) &&
            (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
        {
            return uriResult.Host;
        }

        string prefixed = input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                          input.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? input
            : $"https://{input}";

        if (Uri.TryCreate(prefixed, UriKind.Absolute, out var uriResult2))
        {
            return uriResult2.Host;
        }

        return string.Empty;
    }
}