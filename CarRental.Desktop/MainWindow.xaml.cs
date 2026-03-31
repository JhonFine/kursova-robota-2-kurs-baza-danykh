using CarRental.Desktop.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace CarRental.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            try
            {
                // Ініціалізація shell відбувається після побудови вікна, щоб navigation VM міг безпечно прогріти першу сторінку.
                await viewModel.InitializeAsync();
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    $"Помилка ініціалізації: {exception.Message}",
                    "Помилка запуску",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Close();
            }
        }
    }

    private void SettingsButton_OnClick(object sender, RoutedEventArgs e)
    {
        // Налаштування відкриваються як context menu від кнопки, щоб не заводити окреме модальне вікно для простих дій.
        if (sender is Button { ContextMenu: { } contextMenu } button)
        {
            contextMenu.PlacementTarget = button;
            contextMenu.IsOpen = true;
            e.Handled = true;
        }
    }
}
