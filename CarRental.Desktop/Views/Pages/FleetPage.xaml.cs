using CarRental.Desktop.ViewModels;
using CarRental.Desktop.Views.Windows;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CarRental.Desktop.Views.Pages;

public partial class FleetPage : UserControl
{
    private readonly List<GuideStep> _guideSteps = [];
    private FleetPageViewModel? _viewModel;
    private int _guideStepIndex = -1;
    private bool _searchPanelWasOpenBeforeGuide;

    public FleetPage()
    {
        InitializeComponent();
        Loaded += FleetPage_OnLoaded;
        Unloaded += FleetPage_OnUnloaded;
        DataContextChanged += FleetPage_OnDataContextChanged;
    }

    private void FleetPage_OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachViewModel();
        InitializeGuideSteps();
    }

    private void FleetPage_OnUnloaded(object sender, RoutedEventArgs e)
    {
        PageLifecycleUtilities.ReleaseTransientState(DataContext);
        DetachViewModel();
        StopGuide();
    }

    private void FleetPage_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
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
        _viewModel = DataContext as FleetPageViewModel;
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
        if (e.PropertyName == nameof(FleetPageViewModel.GuideRequestId))
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
            "Фільтри",
            "Тут можна відфільтрувати автопарк за маркою, класом і статусом.",
            () => FleetFiltersCard));
        _guideSteps.Add(new GuideStep(
            "Кнопка пошуку",
            "Кнопка праворуч відкриває швидкий запит по конкретному полю без прихованих жестів.",
            () => SearchToggleButton));
        _guideSteps.Add(new GuideStep(
            "Панель пошуку",
            "Оберіть поле, введіть значення і натисніть \"Знайти\" або \"Очистити\".",
            () => SearchPanelCard,
            EnsureSearchPanelOpen));
        _guideSteps.Add(new GuideStep(
            "Список авто",
            "Тут відображаються всі авто. Деталі відкриваються кнопкою під таблицею, а подвійний клік лишається як швидка дія.",
            () => FleetListGroup));
        _guideSteps.Add(new GuideStep(
            "Додавання авто",
            "Кнопка праворуч відкриває форму створення нового автомобіля.",
            () => FleetAddGroup));
    }

    private void EnsureSearchPanelOpen()
    {
        if (_viewModel is not null)
        {
            _viewModel.IsSearchPanelOpen = true;
        }
    }

    private void StartGuide()
    {
        if (_viewModel is null)
        {
            return;
        }

        InitializeGuideSteps();
        if (_guideSteps.Count == 0)
        {
            return;
        }

        _searchPanelWasOpenBeforeGuide = _viewModel.IsSearchPanelOpen;
        _viewModel.IsSearchPanelOpen = false;
        _viewModel.IsVehicleDetailsDialogOpen = false;

        GuideOverlay.Visibility = Visibility.Visible;
        GuideOverlay.CaptureMouse();
        _guideStepIndex = -1;
        AdvanceGuide();
    }

    private void StopGuide()
    {
        GuideOverlay.ReleaseMouseCapture();
        GuideOverlay.Visibility = Visibility.Collapsed;

        if (_viewModel is not null)
        {
            _viewModel.IsSearchPanelOpen = _searchPanelWasOpenBeforeGuide;
        }
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
        var rootWidth = Math.Max(FleetRoot.ActualWidth, 1);
        var rootHeight = Math.Max(FleetRoot.ActualHeight, 1);

        var bounds = target.TransformToAncestor(FleetRoot).TransformBounds(new Rect(0, 0, target.ActualWidth, target.ActualHeight));
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

    private async void AddVehicleButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not FleetPageViewModel viewModel)
        {
            return;
        }

        var dialog = new AddVehicleWindow
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true || dialog.Draft is null)
        {
            return;
        }

        await viewModel.AddVehicleAsync(dialog.Draft);
    }

    private void VehiclesGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not FleetPageViewModel viewModel)
        {
            return;
        }

        if (sender is DataGrid grid && grid.SelectedItem is FleetPageViewModel.FleetRow row)
        {
            viewModel.SelectedVehicle = row;
        }

        viewModel.OpenSelectedVehicleDetails();
    }

    private sealed record GuideStep(
        string Title,
        string Description,
        Func<FrameworkElement?> ResolveTarget,
        Action? BeforeShow = null);
}
