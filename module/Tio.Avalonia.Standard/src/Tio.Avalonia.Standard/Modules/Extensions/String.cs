using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tio.Avalonia.Standard.Modules.Extensions;

public static class Extensions
{
    public static string AsJson(this object obj, bool indented = true)
    {
        var options = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        if (indented)
        {
            options.WriteIndented = true;
            options.IndentSize = 2;
        }

        return JsonSerializer.Serialize(obj, obj.GetType(), options);
    }

    public static bool IsNullOrWhiteSpace(this string? str)
    {
        return string.IsNullOrWhiteSpace(str);
    }

    public static string Truncate(this string? str, int maxLength, string suffix = "...")
    {
        if (string.IsNullOrEmpty(str) || str.Length <= maxLength) return str ?? string.Empty;
        return str.Substring(0, maxLength) + suffix;
    }

    public static int ToInt(this string? str, int defaultValue = 0)
    {
        return int.TryParse(str, out int result) ? result : defaultValue;
    }

    public static bool IsNullOrEmpty<T>(this IEnumerable<T>? source)
    {
        return source == null || !source.Any();
    }

    public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (action == null) throw new ArgumentNullException(nameof(action));

        foreach (T item in source)
        {
            action(item);
        }
    }

    public static T ThrowIfNull<T>(this T? obj, string paramName, string? message = null) where T : class
    {
        if (obj == null)
            throw new ArgumentNullException(paramName, message ?? $"{paramName} cannot be null.");
        return obj;
    }

    public static bool In<T>(this T val, params T[] values)
    {
        if (values == null) return false;
        return values.Contains(val);
    }

    public static long ToUnixTimestamp(this DateTime dateTime)
    {
        return new DateTimeOffset(dateTime).ToUnixTimeSeconds();
    }

    public static string ToRelativeTime(this DateTime dateTime)
    {
        var timeSpan = DateTime.Now - dateTime;

        if (timeSpan <= TimeSpan.FromSeconds(60))
            return "刚刚";
        if (timeSpan <= TimeSpan.FromMinutes(60))
            return timeSpan.Minutes > 1 ? $"{timeSpan.Minutes} 分钟前" : "1 分钟前";
        if (timeSpan <= TimeSpan.FromHours(24))
            return timeSpan.Hours > 1 ? $"{timeSpan.Hours} 小时前" : "1 小时前";
        if (timeSpan <= TimeSpan.FromDays(30))
            return timeSpan.Days > 1 ? $"{timeSpan.Days} 天前" : "昨天";
        
        return dateTime.ToString("yyyy-MM-dd");
    }
}