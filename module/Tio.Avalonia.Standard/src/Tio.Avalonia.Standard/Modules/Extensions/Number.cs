namespace Tio.Avalonia.Standard.Modules.Extensions;

public static class NumberExtensions
{
    public static bool Between(this int value, int min, int max, bool inclusive = true)
    {
        return inclusive 
            ? value >= min && value <= max 
            : value > min && value < max;
    }

    public static bool Between(this double value, double min, double max, bool inclusive = true)
    {
        return inclusive 
            ? value >= min && value <= max 
            : value > min && value < max;
    }

    public static double Round(this double value, int decimals = 2)
    {
        return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
    }

    public static decimal Round(this decimal value, int decimals = 2)
    {
        return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
    }

    public static double PercentOf(this int part, int total, int decimals = 2)
    {
        if (total == 0) return 0;
        return ((double)part / total * 100).Round(decimals);
    }

    public static double PercentOf(this double part, double total, int decimals = 2)
    {
        if (total == 0) return 0;
        return (part / total * 100).Round(decimals);
    }

    public static int Clamp(this int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    public static double Clamp(this double value, double min, double max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    public static string ToFileSize(this long bytes)
    {
        string[] suffixes = { "B", "KiB", "MiB", "GiB", "TiB", "PiB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:F2} {suffixes[counter]}";
    }

    public static string ToFileSize(this int bytes)
    {
        return ((long)bytes).ToFileSize();
    }

    public static bool IsEven(this int value)
    {
        return value % 2 == 0;
    }

    public static bool IsOdd(this int value)
    {
        return value % 2 != 0;
    }
}