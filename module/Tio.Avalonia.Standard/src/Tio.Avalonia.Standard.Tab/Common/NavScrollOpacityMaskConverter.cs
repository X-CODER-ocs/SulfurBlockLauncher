using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Tio.Avalonia.Standard.Tab.Common;

public class NavScrollOpacityMaskConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var showLeftFade = value switch
        {
            bool flag => flag,
            Vector offset => offset.X > 0.5,
            _ => false
        };

        if (showLeftFade)
            return new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                GradientStops =
                [
                    new GradientStop(Colors.Transparent, 0.0),
                    new GradientStop(Colors.White, 0.03),
                    new GradientStop(Colors.White, 0.97),
                    new GradientStop(Colors.Transparent, 1.0)
                ]
            };

        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Colors.White, 0.0),
                new GradientStop(Colors.White, 0.97),
                new GradientStop(Colors.Transparent, 1.0)
            ]
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }
}
