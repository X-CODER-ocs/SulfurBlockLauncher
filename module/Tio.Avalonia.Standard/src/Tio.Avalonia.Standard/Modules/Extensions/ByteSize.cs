namespace Tio.Avalonia.Standard.Modules.Extensions;

public static class ByteSize
{
    private static readonly string[] Units = { "bytes", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB" };

    public static string ToHumanReadableSize(this double bytes, int decimalPlaces = 2)
    {
        var (value, unit) = bytes.GetReadableRaw(decimalPlaces);
        return unit == "bytes" ? $"{Math.Round(value)} {unit}" : $"{value.ToString($"F{decimalPlaces}")} {unit}";
    }

    public static string ToHumanReadableSize(this int bytes, int decimalPlaces = 2) 
        => ((double)bytes).ToHumanReadableSize(decimalPlaces);

    public static string ToHumanReadableSize(this long bytes, int decimalPlaces = 2) 
        => ((double)bytes).ToHumanReadableSize(decimalPlaces);

    public static (double Value, string Unit) GetReadableRaw(this double bytes, int decimalPlaces = 2)
    {
        if (bytes < 0) throw new ArgumentException("字节数不能为负数");

        double value = bytes;
        int unitIndex = 0;

        while (unitIndex < Units.Length - 1 && value >= 819.2)
        {
            value /= 1024.0;
            unitIndex++;
        }

        return (Math.Round(value, decimalPlaces), Units[unitIndex]);
    }

    public static (double Value, string Unit) GetReadableRaw(this int bytes, int decimalPlaces = 2) 
        => ((double)bytes).GetReadableRaw(decimalPlaces);
}