using CarRental.Desktop.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CarRental.Desktop.Views.Pages;

public partial class AdminPage : UserControl
{
    private readonly List<GuideStep> _guideSteps = [];
    private AdminPageViewModel? _attachedViewModel;
    private int _guideStepIndex = -1;

    public AdminPage()
    {
        InitializeComponent();
        Loaded += AdminPage_OnLoaded;
        Unloaded += AdminPage_OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void AdminPage_OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachViewModel();
        InitializeGuideSteps();
    }

    private void AdminPage_OnUnloaded(object sender, RoutedEventArgs e)
    {
        PageLifecycleUtilities.ReleaseTransientState(DataContext);
        DetachViewModel();
        StopGuide();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        AttachViewModel();
        InitializeGuideSteps();
    }

    private void AttachViewModel()
    {
        if (_attachedViewModel == DataContext)
        {
            return;
        }

        DetachViewModel();
        _attachedViewModel = DataContext as AdminPageViewModel;
        if (_attachedViewModel is not null)
        {
            _attachedViewModel.RequestPasswordFieldsClear += HandleRequestPasswordFieldsClear;
            _attachedViewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        }
    }

    private void DetachViewModel()
    {
        if (_attachedViewModel is null)
        {
            return;
        }

        _attachedViewModel.RequestPasswordFieldsClear -= HandleRequestPasswordFieldsClear;
        _attachedViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        _attachedViewModel = null;
    }

    private void CurrentPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is AdminPageViewModel viewModel && sender is PasswordBox passwordBox)
        {
            viewModel.CurrentPassword = passwordBox.Password;
        }
    }

    private void NewPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is AdminPageViewModel viewModel && sender is PasswordBox passwordBox)
        {
            viewModel.NewPassword = passwordBox.Password;
        }
    }

    private void HandleRequestPasswordFieldsClear()
    {
        CurrentPasswordBox.Password = string.Empty;
        NewPasswordBox.Password = string.Empty;
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AdminPageViewModel.GuideRequestId))
        {
            Dispatcher.Invoke(StartGuide);
        }
    }

    private void InitializeGuideSteps()
    {
        if (_guideSteps.Count > 0)
        {
            return;
        }

        _guideSteps.Add(new GuideStep(
            "Панель адміністратора",
            "Верхній блок містить призначення сторінки та швидкий доступ до фільтрації.",
            () => AdminHeaderCard));
        _guideSteps.Add(new GuideStep(
            "Пошук і оновлення",
            "Вводьте частину імені/логіну та оновлюйте список працівників.",
            () => SearchAndRefreshPanel));
        _guideSteps.Add(new GuideStep(
            "Ключові показники",
            "Картки показують загальну статистику по працівниках і блокуваннях.",
            () => AdminMetricsGrid));
        _guideSteps.Add(new GuideStep(
            "Список працівників",
            "Тут обирається працівник для виконання адміністративних дій.",
            () => EmployeesListCard));
        _guideSteps.Add(new GuideStep(
            "Керування працівником",
            "Праворуч доступні дії: активувати/деактивувати, змінити роль, розблокувати.",
            () => EmployeeActionsCard));
        _guideSteps.Add(new GuideStep(
            "Зміна пароля",
            "Нижня картка праворуч призначена для зміни вашого пароля.",
            () => PasswordChangeCard));
        _guideSteps.Add(new GuideStep(
            "Панель статусу",
            "Нижній блок показує поточний стан операцій та помилки.",
            () => AdminStatusPanel));
    }

    private void StartGuide()
    {
        InitializeGuideSteps();
        if (_guideSteps.Count == 0)
        {
            return;
        }

        GuideOverlay.Visibility = Visibility.Visible;
        GuideOverlay.CaptureMouse();
        _guideStepIndex = -1;
        AdvanceGuide();
    }

    private void StopGuide()
    {
        GuideOverlay.ReleaseMouseCapture();
        GuideOverlay.Visibility = Visibility.Collapsed;
    }

    private void AdvanceGuide()
    {
        if (GuideOverlay.Visibility != Visibility.Visible)
        {
            return;
        }

        while (true)
        {
            _guideStepIndex++;
            if (_guideStepIndex >= _guideSteps.Count)
            {
                StopGuide();
                return;
            }

            var step = _guideSteps[_guideStepIndex];
            step.BeforeShow?.Invoke();
            UpdateLayout();

            var target = step.ResolveTarget();
            if (target is null || !target.IsVisible || target.ActualWidth <= 0 || target.ActualHeight <= 0)
            {
                continue;
            }

            target.BringIntoView();
            UpdateLayout();
            ShowGuideStep(step, target);
            break;
        }
    }

    private void ShowGuideStep(GuideStep step, FrameworkElement target)
    {
        var rootWidth = Math.Max(AdminRoot.ActualWidth, 1);
        var rootHeight = Math.Max(AdminRoot.ActualHeight, 1);

        var bounds = target.TransformToAncestor(AdminRoot).TransformBounds(new Rect(0, 0, target.ActualWidth, target.ActualHeight));
        bounds.Inflate(8, 8);

        var left = Clamp(bounds.Left, 0, rootWidth);
        var top = Clamp(bounds.Top, 0, rootHeight);
        var right = Clamp(bounds.Right, 0, rootWidth);
        var bottom = Clamp(bounds.Bottom, 0, rootHeight);
        var highlightWidth = Math.Max(0, right - left);
        var highlightHeight = Math.Max(0, bottom - top);

        SetRect(GuideDimTop, 0, 0, rootWidth, top);
        SetRect(GuideDimLeft, 0, top, left, highlightHeight);
        SetRect(GuideDimRight, right, top, Math.Max(0, rootWidth - right), highlightHeight);
        SetRect(GuideDimBottom, 0, bottom, rootWidth, Math.Max(0, rootHeight - bottom));

        Canvas.SetLeft(GuideHighlightBorder, left);
        Canvas.SetTop(GuideHighlightBorder, top);
        GuideHighlightBorder.Width = highlightWidth;
        GuideHighlightBorder.Height = highlightHeight;

        GuideStepTitle.Text = step.Title;
        GuideStepDescription.Text = step.Description;
        GuideStepProgress.Text = $"{_guideStepIndex + 1}/{_guideSteps.Count} - Натисніть будь-де для наступного кроку";

        GuideCard.Measure(new Size(GuideCard.Width, double.PositiveInfinity));
        var cardWidth = GuideCard.Width;
        var cardHeight = GuideCard.DesiredSize.Height;
        var cardLeft = left;
        if (cardLeft + cardWidth > rootWidth - 12)
        {
            cardLeft = rootWidth - cardWidth - 12;
        }

        if (cardLeft < 12)
        {
            cardLeft = 12;
        }

        var cardTop = bottom + 12;
        if (cardTop + cardHeight > rootHeight - 12)
        {
            cardTop = top - cardHeight - 12;
        }

        if (cardTop < 12)
        {
            cardTop = 12;
        }

        Canvas.SetLeft(GuideCard, cardLeft);
        Canvas.SetTop(GuideCard, cardTop);
    }

    private static void SetRect(FrameworkElement rectangle, double left, double top, double width, double height)
    {
        Canvas.SetLeft(rectangle, left);
        Canvas.SetTop(rectangle, top);
        rectangle.Width = Math.Max(0, width);
        rectangle.Height = Math.Max(0, height);
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private void GuideOverlay_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        AdvanceGuide();
    }

    private sealed record GuideStep(
        string Title,
        string Description,
        Func<FrameworkElement?> ResolveTarget,
        Action? BeforeShow = null);
}
