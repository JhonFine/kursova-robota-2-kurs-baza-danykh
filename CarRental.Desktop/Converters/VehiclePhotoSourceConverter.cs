using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace CarRental.Desktop.Converters;

public sealed class VehiclePhotoSourceConverter : IValueConverter
{
    private const int DefaultDecodeWidth = 480;
    private const int DetailDecodeWidth = 960;

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.StreamSource = stream;

            var decodeWidth = ResolveDecodeWidth(parameter);
            if (decodeWidth > 0)
            {
                image.DecodePixelWidth = decodeWidth;
            }

            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    private static int ResolveDecodeWidth(object? parameter)
    {
        return parameter switch
        {
            "detail" => DetailDecodeWidth,
            int explicitWidth when explicitWidth > 0 => explicitWidth,
            string raw when int.TryParse(raw, out var explicitWidth) && explicitWidth > 0 => explicitWidth,
            _ => DefaultDecodeWidth
        };
    }
}
