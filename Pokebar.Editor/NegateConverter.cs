using System;
using System.Globalization;
using System.Windows.Data;

namespace Pokebar.Editor;

public class NegateConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d) return -d;
        if (value is int i) return -i;
        return value;
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Convert(value, targetType, parameter, culture);
}
