using System.Globalization;
using Avalonia.Data.Converters;

namespace SulfurLauncher.Module.Converter;

public sealed class NullableDoubleToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not double;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}