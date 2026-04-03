using CarRental.Desktop.ViewModels;
using Microsoft.Win32;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CarRental.Desktop.Views.Pages;

public partial class ClientsPage : UserControl
{
    private readonly List<GuideStep> _guideSteps = [];
    private ClientsPageViewModel? _viewModel;
    private int _guideStepIndex = -1;

    public ClientsPage()
    {
        InitializeComponent();
        Loaded += ClientsPage_OnLoaded;
        Unloaded += ClientsPage_OnUnloaded;
        DataContextChanged += ClientsPage_OnDataContextChanged;
    }

    private void ClientsPage_OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachViewModel();
        InitializeGuideSteps();
    }

    private void ClientsPage_OnUnloaded(object sender, RoutedEventArgs e)
    {
        PageLifecycleUtilities.ReleaseTransientState(DataContext);
        DetachViewModel();
        StopGuide();
    }

    private void ClientsPage_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        AttachViewModel();
        InitializeGuideSteps();
    }

    private void AttachViewModel()
    {
        if (_viewModel == DataContext)
        {
            return;
        }

        DetachViewModel();
        _viewModel = DataContext as ClientsPageViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        }
    }

    private void DetachViewModel()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        _viewModel = null;
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClientsPageViewModel.GuideRequestId))
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
            "CRM-панель",
            "Верхній блок відкриває швидке створення клієнта та примусове оновлення даних.",
            () => ClientsOverviewCard));
        _guideSteps.Add(new GuideStep(
            "Додавання клієнта",
            "Кнопка переводить праву панель у режим швидкої реєстрації клієнта за стійкою.",
            () => CreateClientButton));
        _guideSteps.Add(new GuideStep(
            "Пошук і список",
            "Ліворуч можна знайти клієнта за ПІБ, телефоном або документами й одразу створити профіль, якщо пошук порожній.",
            () => ClientsListGroup));
        _guideSteps.Add(new GuideStep(
            "Права панель",
            "Праворуч або картка обраного клієнта, або форма реєстрації та редагування.",
            () => ClientDetailsGroup));
        _guideSteps.Add(new GuideStep(
            "Оренда з картки",
            "Після вибору клієнта можна одразу перейти до оформлення оренди з уже підставленим профілем.",
            () => OpenRentalsButton));
        _guideSteps.Add(new GuideStep(
            "Панель повідомлень",
            "Нижній рядок показує результат останньої дії або помилку.",
            () => ClientsStatusPanel));
    }

    private void ChoosePassportFileButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var openFileDialog = new OpenFileDialog
        {
            Title = "Оберіть файл паспорта",
            Filter = "Файли документів|*.jpg;*.jpeg;*.png;*.webp|Усі файли|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (openFileDialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            _viewModel.SelectPassportSourceFile(openFileDialog.FileName);
        }
    }

    private void ChooseDriverLicenseFileButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var openFileDialog = new OpenFileDialog
        {
            Title = "Оберіть файл посвідчення водія",
            Filter = "Файли документів|*.jpg;*.jpeg;*.png;*.webp|Усі файли|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (openFileDialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            _viewModel.SelectDriverLicenseSourceFile(openFileDialog.FileName);
        }
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
        var rootWidth = Math.Max(ClientsRoot.ActualWidth, 1);
        var rootHeight = Math.Max(ClientsRoot.ActualHeight, 1);

        var bounds = target.TransformToAncestor(ClientsRoot).TransformBounds(new Rect(0, 0, target.ActualWidth, target.ActualHeight));
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
        => Math.Min(Math.Max(value, min), max);

    private void GuideOverlay_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        AdvanceGuide();
        e.Handled = true;
    }

    private sealed record GuideStep(
        string Title,
        string Description,
        Func<FrameworkElement?> ResolveTarget,
        Action? BeforeShow = null);
}
