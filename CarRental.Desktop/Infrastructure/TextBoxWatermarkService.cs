using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace CarRental.Desktop.Infrastructure;

public static class TextBoxWatermarkService
{
    public static readonly DependencyProperty WatermarkProperty = DependencyProperty.RegisterAttached(
        "Watermark",
        typeof(string),
        typeof(TextBoxWatermarkService),
        new PropertyMetadata(string.Empty, OnWatermarkChanged));

    private static readonly DependencyProperty WatermarkAdornerProperty = DependencyProperty.RegisterAttached(
        "WatermarkAdorner",
        typeof(TextBoxWatermarkAdorner),
        typeof(TextBoxWatermarkService),
        new PropertyMetadata(null));

    public static string GetWatermark(DependencyObject element)
        => (string)element.GetValue(WatermarkProperty);

    public static void SetWatermark(DependencyObject element, string value)
        => element.SetValue(WatermarkProperty, value);

    private static TextBoxWatermarkAdorner? GetWatermarkAdorner(DependencyObject element)
        => (TextBoxWatermarkAdorner?)element.GetValue(WatermarkAdornerProperty);

    private static void SetWatermarkAdorner(DependencyObject element, TextBoxWatermarkAdorner? value)
        => element.SetValue(WatermarkAdornerProperty, value);

    private static void OnWatermarkChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not TextBox textBox)
        {
            return;
        }

        textBox.Loaded -= TextBox_OnLoaded;
        textBox.Unloaded -= TextBox_OnUnloaded;
        textBox.TextChanged -= TextBox_OnStateChanged;
        textBox.GotKeyboardFocus -= TextBox_OnStateChanged;
        textBox.LostKeyboardFocus -= TextBox_OnStateChanged;

        if (string.IsNullOrWhiteSpace(e.NewValue as string))
        {
            RemoveWatermark(textBox);
            return;
        }

        textBox.Loaded += TextBox_OnLoaded;
        textBox.Unloaded += TextBox_OnUnloaded;
        textBox.TextChanged += TextBox_OnStateChanged;
        textBox.GotKeyboardFocus += TextBox_OnStateChanged;
        textBox.LostKeyboardFocus += TextBox_OnStateChanged;

        UpdateWatermark(textBox);
    }

    private static void TextBox_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            UpdateWatermark(textBox);
        }
    }

    private static void TextBox_OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            RemoveWatermark(textBox);
        }
    }

    private static void TextBox_OnStateChanged(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            UpdateWatermark(textBox);
        }
    }

    private static void UpdateWatermark(TextBox textBox)
    {
        if (!textBox.IsLoaded)
        {
            return;
        }

        var adornerLayer = AdornerLayer.GetAdornerLayer(textBox);
        if (adornerLayer is null)
        {
            return;
        }

        var adorner = GetWatermarkAdorner(textBox);
        if (adorner is null)
        {
            adorner = new TextBoxWatermarkAdorner(textBox);
            adornerLayer.Add(adorner);
            SetWatermarkAdorner(textBox, adorner);
        }

        adorner.InvalidateVisual();
    }

    private static void RemoveWatermark(TextBox textBox)
    {
        var adorner = GetWatermarkAdorner(textBox);
        if (adorner is null)
        {
            return;
        }

        var adornerLayer = AdornerLayer.GetAdornerLayer(textBox);
        adornerLayer?.Remove(adorner);
        SetWatermarkAdorner(textBox, null);
    }

    private sealed class TextBoxWatermarkAdorner(TextBox textBox) : Adorner(textBox)
    {
        private static readonly Brush WatermarkBrush = CreateWatermarkBrush();
        private readonly TextBox _textBox = textBox;

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            var watermark = GetWatermark(_textBox);
            if (string.IsNullOrWhiteSpace(watermark) ||
                !string.IsNullOrEmpty(_textBox.Text) ||
                _textBox.IsKeyboardFocusWithin ||
                _textBox.ActualWidth <= 0 ||
                _textBox.ActualHeight <= 0)
            {
                return;
            }

            var formattedText = new FormattedText(
                watermark,
                CultureInfo.CurrentUICulture,
                _textBox.FlowDirection,
                new Typeface(_textBox.FontFamily, _textBox.FontStyle, _textBox.FontWeight, _textBox.FontStretch),
                _textBox.FontSize,
                WatermarkBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            var x = _textBox.Padding.Left + 2;
            var y = Math.Max(0, (_textBox.ActualHeight - formattedText.Height) / 2);
            drawingContext.DrawText(formattedText, new Point(x, y));
        }

        private static Brush CreateWatermarkBrush()
        {
            var brush = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            brush.Freeze();
            return brush;
        }
    }
}
