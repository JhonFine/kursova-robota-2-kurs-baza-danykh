using System.Globalization;
using System.Windows.Data;

namespace CarRental.Desktop.Converters;

public sealed class UppercaseConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return value.ToString()?.ToUpper(culture) ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
