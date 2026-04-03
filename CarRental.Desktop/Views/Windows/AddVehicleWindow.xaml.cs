using CarRental.Desktop.Models;
using CarRental.Desktop.ViewModels;
using Microsoft.Win32;
using System.Windows;

namespace CarRental.Desktop.Views.Windows;

public partial class AddVehicleWindow : Window
{
    public AddVehicleWindow()
    {
        InitializeComponent();
        ViewModel = new AddVehicleDialogViewModel();
        DataContext = ViewModel;
    }

    public AddVehicleDialogViewModel ViewModel { get; }

    public AddVehicleDraft? Draft { get; private set; }

    private void ChoosePhotoButton_OnClick(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Оберіть фото автомобіля",
            Filter = "Файли зображень|*.jpg;*.jpeg;*.png;*.bmp;*.webp|Усі файли|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (openFileDialog.ShowDialog(this) == true)
        {
            ViewModel.PhotoPath = openFileDialog.FileName;
            ViewModel.ValidationMessage = string.Empty;
        }
    }

    private void ClearPhotoButton_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.PhotoPath = string.Empty;
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.TryBuildDraft(out var draft, out var validationError))
        {
            ViewModel.ValidationMessage = validationError;
            return;
        }

        Draft = draft;
        DialogResult = true;
    }
}

