using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Tio.Avalonia.Standard.Modules.Converters;

public class CharacterWrapConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string text && !string.IsNullOrEmpty(text))
        {
            return string.Join("\u200B", text.ToCharArray());
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}