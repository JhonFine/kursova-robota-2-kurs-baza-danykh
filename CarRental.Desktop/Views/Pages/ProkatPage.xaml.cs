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
        // City picker навмисно відкривається як context menu від кнопки, щоб не дублювати окремий popup-контрол.
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
        // При виході зі сторінки змиваємо тільки transient UI-стан, не чіпаючи кешовані дані каталогу.
        PageLifecycleUtilities.ReleaseTransientState(DataContext);
    }
}
