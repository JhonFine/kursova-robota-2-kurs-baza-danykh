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
                // Р†РЅС–С†С–Р°Р»С–Р·Р°С†С–СЏ shell РІС–РґР±СѓРІР°С”С‚СЊСЃСЏ РїС–СЃР»СЏ РїРѕР±СѓРґРѕРІРё РІС–РєРЅР°, С‰РѕР± navigation VM РјС–Рі Р±РµР·РїРµС‡РЅРѕ РїСЂРѕРіСЂС–С‚Рё РїРµСЂС€Сѓ СЃС‚РѕСЂС–РЅРєСѓ.
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
        // РќР°Р»Р°С€С‚СѓРІР°РЅРЅСЏ РІС–РґРєСЂРёРІР°СЋС‚СЊСЃСЏ СЏРє context menu РІС–Рґ РєРЅРѕРїРєРё, С‰РѕР± РЅРµ Р·Р°РІРѕРґРёС‚Рё РѕРєСЂРµРјРµ РјРѕРґР°Р»СЊРЅРµ РІС–РєРЅРѕ РґР»СЏ РїСЂРѕСЃС‚РёС… РґС–Р№.
        if (sender is Button { ContextMenu: { } contextMenu } button)
        {
            contextMenu.PlacementTarget = button;
            contextMenu.IsOpen = true;
            e.Handled = true;
        }
    }
}

