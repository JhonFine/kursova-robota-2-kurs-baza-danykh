using CarRental.Desktop.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CarRental.Desktop.Views;

public partial class LoginWindow : Window
{
    private LoginViewModel? _attachedViewModel;

    public LoginWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Closed += LoginWindow_OnClosed;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_attachedViewModel is not null)
        {
            _attachedViewModel.RequestClose -= HandleRequestClose;
        }

        _attachedViewModel = e.NewValue as LoginViewModel;
        if (_attachedViewModel is not null)
        {
            _attachedViewModel.RequestClose += HandleRequestClose;
        }
    }

    private void HandleRequestClose(bool dialogResult)
    {
        DialogResult = dialogResult;
        Close();
    }

    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel viewModel && sender is PasswordBox passwordBox)
        {
            viewModel.Password = passwordBox.Password;
        }

        PasswordPlaceholder.Visibility = sender is PasswordBox currentPasswordBox && string.IsNullOrEmpty(currentPasswordBox.Password)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ConfirmPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel viewModel && sender is PasswordBox passwordBox)
        {
            viewModel.ConfirmPassword = passwordBox.Password;
        }

        ConfirmPasswordPlaceholder.Visibility = sender is PasswordBox confirmPasswordBox && string.IsNullOrEmpty(confirmPasswordBox.Password)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void LoginWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        FitToWorkArea();

        PasswordPlaceholder.Visibility = string.IsNullOrEmpty(PasswordInput.Password)
            ? Visibility.Visible
            : Visibility.Collapsed;

        ConfirmPasswordPlaceholder.Visibility = string.IsNullOrEmpty(ConfirmPasswordInput.Password)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void FitToWorkArea()
    {
        var workArea = SystemParameters.WorkArea;
        const double horizontalPadding = 24;
        const double verticalPadding = 24;
        const double minWidth = 560;
        const double minHeight = 520;

        var maxWidth = Math.Max(minWidth, workArea.Width - horizontalPadding);
        var maxHeight = Math.Max(minHeight, workArea.Height - verticalPadding);

        if (Width > maxWidth)
        {
            Width = maxWidth;
        }

        if (Height > maxHeight)
        {
            Height = maxHeight;
        }

        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top + (workArea.Height - Height) / 2;
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void LoginWindow_OnClosed(object? sender, EventArgs e)
    {
        if (_attachedViewModel is not null)
        {
            _attachedViewModel.RequestClose -= HandleRequestClose;
            _attachedViewModel = null;
        }

        DataContextChanged -= OnDataContextChanged;
        Closed -= LoginWindow_OnClosed;
    }

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
