using System.Windows;
using System.Windows.Controls;

namespace CarRental.Desktop.Views.Pages;

public partial class UserRentalsPage : UserControl
{
    public UserRentalsPage()
    {
        InitializeComponent();
        Unloaded += UserRentalsPage_OnUnloaded;
    }

    private void UserRentalsPage_OnUnloaded(object sender, RoutedEventArgs e)
    {
        PageLifecycleUtilities.ReleaseTransientState(DataContext);
    }
}
