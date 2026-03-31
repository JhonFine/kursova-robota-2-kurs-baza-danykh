using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CarRental.Desktop.Converters;

public sealed class StatusBadgeBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return StatusBadgePalette.Resolve(value).Background;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public sealed class StatusBadgeForegroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return StatusBadgePalette.Resolve(value).Foreground;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

internal static class StatusBadgePalette
{
    private static readonly Brush SuccessBackground = CreateBrush(220, 252, 231);
    private static readonly Brush SuccessForeground = CreateBrush(22, 101, 52);

    private static readonly Brush WarningBackground = CreateBrush(254, 243, 199);
    private static readonly Brush WarningForeground = CreateBrush(146, 64, 14);

    private static readonly Brush DangerBackground = CreateBrush(254, 226, 226);
    private static readonly Brush DangerForeground = CreateBrush(153, 27, 27);

    private static readonly Brush NeutralBackground = CreateBrush(229, 231, 235);
    private static readonly Brush NeutralForeground = CreateBrush(55, 65, 81);

    public static StatusBadgeColors Resolve(object? statusValue)
    {
        var normalized = Normalize(statusValue);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new StatusBadgeColors(NeutralBackground, NeutralForeground);
        }

        return normalized switch
        {
            "скасовано" or "cancel" or "canceled" or "refund" or "недоступний" or "ремонт" or "помилка" or "error" or "blocked" or "прострочено" or "debt" or "борг"
                => new StatusBadgeColors(DangerBackground, DangerForeground),
            "заброньовано" or "booked" or "open" or "відкрита" or "pending" or "очікує" or "due" or "донарахування"
                => new StatusBadgeColors(WarningBackground, WarningForeground),
            "активна" or "active" or "доступний" or "available" or "вільно" or "закрита" or "closed" or "resolved" or "успішно" or "paid" or "надходження" or "сплачено"
                => new StatusBadgeColors(SuccessBackground, SuccessForeground),
            _ => new StatusBadgeColors(NeutralBackground, NeutralForeground)
        };
    }

    private static string Normalize(object? value)
    {
        return value?.ToString()?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static Brush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    internal readonly record struct StatusBadgeColors(Brush Background, Brush Foreground);
}
