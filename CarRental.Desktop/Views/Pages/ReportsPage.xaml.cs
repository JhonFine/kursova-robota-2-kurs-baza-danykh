using CarRental.Desktop.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CarRental.Desktop.Views.Pages;

public partial class ReportsPage : UserControl
{
    private readonly List<GuideStep> _guideSteps = [];
    private ReportsPageViewModel? _viewModel;
    private int _guideStepIndex = -1;

    public ReportsPage()
    {
        InitializeComponent();
        Loaded += ReportsPage_OnLoaded;
        Unloaded += ReportsPage_OnUnloaded;
        DataContextChanged += ReportsPage_OnDataContextChanged;
    }

    private void ReportsPage_OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachViewModel();
        InitializeGuideSteps();
    }

    private void ReportsPage_OnUnloaded(object sender, RoutedEventArgs e)
    {
        PageLifecycleUtilities.ReleaseTransientState(DataContext);
        DetachViewModel();
        StopGuide();
    }

    private void ReportsPage_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
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
        _viewModel = DataContext as ReportsPageViewModel;
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
        if (e.PropertyName == nameof(ReportsPageViewModel.GuideRequestId))
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
            "Панель звітів",
            "Тут налаштовуються фільтри і дії для оновлення та експорту звітів.",
            () => ReportsFiltersCard));
        _guideSteps.Add(new GuideStep(
            "Фільтри періоду та даних",
            "Оберіть діапазон дат, авто та співробітника для вибірки даних.",
            () => ReportsFilterInputsGrid));
        _guideSteps.Add(new GuideStep(
            "Оновлення",
            "Кнопка перераховує показники на основі поточних даних у базі.",
            () => RefreshReportsButton));
        _guideSteps.Add(new GuideStep(
            "Експорт CSV",
            "Експортує звіт у CSV-файл з урахуванням вибраних фільтрів.",
            () => ExportCsvButton));
        _guideSteps.Add(new GuideStep(
            "Експорт Excel",
            "Експортує ті ж дані у форматі Excel.",
            () => ExportExcelButton));
        _guideSteps.Add(new GuideStep(
            "Ключові метрики",
            "Ці картки показують загальну статистику: оренди, дохід і витрати.",
            () => ReportsMetricsGrid));
        _guideSteps.Add(new GuideStep(
            "Панель повідомлень",
            "Тут відображається результат операцій, наприклад успішний експорт.",
            () => ReportsStatusPanel));
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
        var rootWidth = Math.Max(ReportsRoot.ActualWidth, 1);
        var rootHeight = Math.Max(ReportsRoot.ActualHeight, 1);

        var bounds = target.TransformToAncestor(ReportsRoot).TransformBounds(new Rect(0, 0, target.ActualWidth, target.ActualHeight));
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

