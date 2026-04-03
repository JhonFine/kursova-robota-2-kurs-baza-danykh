using CarRental.Desktop.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace CarRental.Desktop.Views.Pages;

public partial class ProkatPage : UserControl
{
    public ProkatPage()
    {
        InitializeComponent();
        Unloaded += ProkatPage_OnUnloaded;
    }

    private void PriceSlider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (DataContext is ProkatPageViewModel viewModel &&
            viewModel.ApplyPriceCommand.CanExecute(null))
        {
            viewModel.ApplyPriceCommand.Execute(null);
        }
    }

    private void CityPickerButton_OnClick(object sender, RoutedEventArgs e)
    {
        // City picker РЅР°РІРјРёСЃРЅРѕ РІС–РґРєСЂРёРІР°С”С‚СЊСЃСЏ СЏРє context menu РІС–Рґ РєРЅРѕРїРєРё, С‰РѕР± РЅРµ РґСѓР±Р»СЋРІР°С‚Рё РѕРєСЂРµРјРёР№ popup-РєРѕРЅС‚СЂРѕР».
        if (sender is Button { ContextMenu: { } contextMenu } button)
        {
            contextMenu.PlacementTarget = button;
            contextMenu.IsOpen = true;
            e.Handled = true;
        }
    }

    private void CityMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProkatPageViewModel viewModel ||
            sender is not MenuItem menuItem ||
            menuItem.DataContext is not string selectedCity ||
            string.IsNullOrWhiteSpace(selectedCity))
        {
            return;
        }

        viewModel.PickupLocation = selectedCity;
    }

    private void ProkatPage_OnUnloaded(object sender, RoutedEventArgs e)
    {
        // РџСЂРё РІРёС…РѕРґС– Р·С– СЃС‚РѕСЂС–РЅРєРё Р·РјРёРІР°С”РјРѕ С‚С–Р»СЊРєРё transient UI-СЃС‚Р°РЅ, РЅРµ С‡С–РїР°СЋС‡Рё РєРµС€РѕРІР°РЅС– РґР°РЅС– РєР°С‚Р°Р»РѕРіСѓ.
        PageLifecycleUtilities.ReleaseTransientState(DataContext);
    }
}

