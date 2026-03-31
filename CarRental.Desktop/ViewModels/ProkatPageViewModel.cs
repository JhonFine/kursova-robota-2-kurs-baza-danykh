using CarRental.Desktop.Data;
using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Rentals;
using CarRental.Desktop.Infrastructure;
using CarRental.Shared.ReferenceData;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;

namespace CarRental.Desktop.ViewModels;

// Self-service каталог прокату тримає одночасно маркетингове представлення картки авто
// і реальний вибір конкретного екземпляра під період бронювання.
public sealed class ProkatPageViewModel : PageDataViewModelBase, ITransientStateOwner
{
    private const string AvailableStatusKey = "Вільні";
    private const string UnavailableStatusKey = "Зайняті / Недоступні";
    private const int CardholderNameMaxLength = 120;
    private const int CardNumberMaxDigits = 16;
    private const int CardNumberInputMaxLength = 19;
    private const int CardExpiryMaxDigits = 4;
    private const int CardExpiryInputMaxLength = 5;
    private const int CardCvvMaxDigits = 4;
    private const int DefaultMinPrice = (int)VehicleDomainRules.MinDailyRate;
    private const int DefaultMaxPrice = (int)VehicleDomainRules.MaxDailyRate;

    private readonly RentalDbContext _dbContext;
    private readonly IRentalService _rentalService;
    private readonly PageRefreshCoordinator _refreshCoordinator;
    private readonly Employee _currentEmployee;

    private bool _isLoading;
    private ProkatCarCard? _selectedVehicle;
    private DateTime _startDate = DateTime.Today;
    private DateTime _endDate = DateTime.Today.AddDays(1);
    private string _statusMessage = string.Empty;
    private string _pickupLocation = DemoSeedReferenceData.LocationSeeds[0].City;
    private string _returnLocation = DemoSeedReferenceData.LocationSeeds[0].City;
    private string _pickupTime = "10:00";
    private string _returnTime = "10:00";
    private string _selectedSortOption = "Спочатку популярні";
    private string _minPriceInput = "1000";
    private string _maxPriceInput = "3500";
    private double _minPriceValue = DefaultMinPrice;
    private double _maxPriceValue = DefaultMaxPrice;
    // Картка агрегує модель, а checkout уже працює з конкретними машинами, доступними на вибраний період.
    private readonly List<ProkatCarCard> _allCards = [];
    private readonly Dictionary<int, IReadOnlyList<VehicleVariantRow>> _vehicleVariantsByCardId = [];
    private bool _isVehicleDetailsDialogOpen;
    private bool _isCheckoutDialogOpen;
    private ProkatCarCard? _detailsVehicle;
    private VehicleVariantRow? _selectedVehicleVariant;
    private string _cardholderNameInput = string.Empty;
    private string _cardNumberInput = string.Empty;
    private string _cardExpiryInput = string.Empty;
    private string _cardCvvInput = string.Empty;
    private string _checkoutAttemptHint = string.Empty;
    // Зміна дат чи локацій може прилетіти серією setter-ів, тому availability-refresh згортається в один прохід.
    private bool _periodAvailabilityRefreshPending;
    private bool _suppressFilterApply;

    public ProkatPageViewModel(
        RentalDbContext dbContext,
        IRentalService rentalService,
        PageRefreshCoordinator refreshCoordinator,
        Employee currentEmployee)
    {
        _dbContext = dbContext;
        _rentalService = rentalService;
        _refreshCoordinator = refreshCoordinator;
        _currentEmployee = currentEmployee;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsLoading);
        OpenSelectedVehicleDetailsCommand = new AsyncRelayCommand(OpenSelectedVehicleDetailsAsync, () => !IsLoading);
        OpenVehicleDetailsCommand = new AsyncRelayCommand<ProkatCarCard?>(OpenVehicleDetailsAsync, _ => !IsLoading);
        CloseVehicleDetailsCommand = new RelayCommand(CloseVehicleDetailsDialog);
        ChooseVehicleVariantCommand = new AsyncRelayCommand<VehicleVariantRow?>(ChooseVehicleVariantAsync, _ => !IsLoading);
        CloseCheckoutCommand = new RelayCommand(CloseCheckoutDialog);
        CompleteCheckoutCommand = new AsyncRelayCommand(CompleteCheckoutAsync, CanCompleteCheckout);
        ApplyPriceCommand = new RelayCommand(ApplyPriceFilter);
        RentVehicleCommand = new AsyncRelayCommand(RentVehicleAsync, () => !IsLoading);
        SelectVehicleCommand = new RelayCommand<ProkatCarCard?>(SelectVehicle);
        ResetFiltersCommand = new RelayCommand(ResetFilters);

        InitializeFilterCollections();
    }

    public ObservableCollection<ProkatCarCard> Vehicles { get; } = [];

    public ObservableCollection<string> Locations { get; } = [.. DemoSeedReferenceData.SupportedLocations];

    public ObservableCollection<string> TimeOptions { get; } = [.. DemoSeedReferenceData.TimeOptions];

    public ObservableCollection<string> SortOptions { get; } =
    [
        "Спочатку популярні",
        "Спочатку дешевші",
        "Спочатку дорожчі"
    ];

    public ObservableCollection<FilterOption> CarClassFilters { get; } = [];

    public ObservableCollection<FilterOption> FuelFilters { get; } = [];

    public ObservableCollection<FilterOption> TransmissionFilters { get; } = [];

    public ObservableCollection<FilterOption> VehicleStatusFilters { get; } = [];

    public ObservableCollection<VehicleVariantRow> VehicleVariants { get; } = [];

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand OpenSelectedVehicleDetailsCommand { get; }

    public IAsyncRelayCommand<ProkatCarCard?> OpenVehicleDetailsCommand { get; }

    public IRelayCommand CloseVehicleDetailsCommand { get; }

    public IAsyncRelayCommand<VehicleVariantRow?> ChooseVehicleVariantCommand { get; }

    public IRelayCommand CloseCheckoutCommand { get; }

    public IAsyncRelayCommand CompleteCheckoutCommand { get; }

    public IRelayCommand ApplyPriceCommand { get; }

    public IAsyncRelayCommand RentVehicleCommand { get; }

    public IRelayCommand<ProkatCarCard?> SelectVehicleCommand { get; }

    public IRelayCommand ResetFiltersCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                OpenSelectedVehicleDetailsCommand.NotifyCanExecuteChanged();
                OpenVehicleDetailsCommand.NotifyCanExecuteChanged();
                ChooseVehicleVariantCommand.NotifyCanExecuteChanged();
                CompleteCheckoutCommand.NotifyCanExecuteChanged();
                RentVehicleCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public ProkatCarCard? SelectedVehicle
    {
        get => _selectedVehicle;
        set => SetProperty(ref _selectedVehicle, value);
    }

    public DateTime MinStartDate => DateTime.Today;

    public DateTime MinEndDate => StartDate.Date > DateTime.Today ? StartDate.Date : DateTime.Today;

    public DateTime StartDate
    {
        get => _startDate;
        set
        {
            if (SetProperty(ref _startDate, value))
            {
                if (EndDate.Date < value.Date)
                {
                    EndDate = value.Date;
                }

                OnPropertyChanged(nameof(CheckoutPeriodSummary));
                OnPropertyChanged(nameof(CheckoutAmountSummary));
                NotifyCheckoutValidationChanged();
                RequestPeriodAvailabilityRefresh();
            }
        }
    }

    public DateTime EndDate
    {
        get => _endDate;
        set
        {
            if (SetProperty(ref _endDate, value))
            {
                OnPropertyChanged(nameof(CheckoutPeriodSummary));
                OnPropertyChanged(nameof(CheckoutAmountSummary));
                NotifyCheckoutValidationChanged();
                RequestPeriodAvailabilityRefresh();
            }
        }
    }

    public string CheckoutStartDateValidationMessage => ResolveCheckoutStartDateValidationMessage() ?? string.Empty;

    public bool HasCheckoutStartDateValidationMessage => !string.IsNullOrWhiteSpace(CheckoutStartDateValidationMessage);

    public string CheckoutEndDateValidationMessage => ResolveCheckoutEndDateValidationMessage() ?? string.Empty;

    public bool HasCheckoutEndDateValidationMessage => !string.IsNullOrWhiteSpace(CheckoutEndDateValidationMessage);

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string CheckoutActionHint
    {
        get
        {
            var validationError = ValidateCheckoutInputs();
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return validationError;
            }

            if (!string.IsNullOrWhiteSpace(_checkoutAttemptHint))
            {
                return _checkoutAttemptHint;
            }

            return ShouldWarnAboutCardNumber(CardNumberInput)
                ? "Ймовірно тестова картка."
                : string.Empty;
        }
    }

    public bool HasCheckoutActionHint => !string.IsNullOrWhiteSpace(CheckoutActionHint);

    public bool IsVehicleDetailsDialogOpen
    {
        get => _isVehicleDetailsDialogOpen;
        set => SetProperty(ref _isVehicleDetailsDialogOpen, value);
    }

    public bool IsCheckoutDialogOpen
    {
        get => _isCheckoutDialogOpen;
        set
        {
            if (SetProperty(ref _isCheckoutDialogOpen, value))
            {
                NotifyCheckoutValidationChanged();
            }
        }
    }

    public ProkatCarCard? DetailsVehicle
    {
        get => _detailsVehicle;
        set
        {
            if (SetProperty(ref _detailsVehicle, value))
            {
                OnPropertyChanged(nameof(DetailsVehicleSummary));
                OnPropertyChanged(nameof(DetailsVehiclePricingSummary));
                OnPropertyChanged(nameof(CheckoutAmountSummary));
            }
        }
    }

    public string DetailsVehicleSummary => DetailsVehicle is null
        ? string.Empty
        : $"{DetailsVehicle.EngineFuelTransmission} • Клас: {DetailsVehicle.CarClass} • В автопарку: {DetailsVehicle.FleetCount} шт. (доступно на період: {DetailsVehicle.FleetAvailableCount})";

    public string DetailsVehiclePricingSummary => DetailsVehicle is null
        ? string.Empty
        : $"Тарифи: {DetailsVehicle.PriceFor26PlusDaysDisplay} / {DetailsVehicle.PriceFor10To25DaysDisplay} / {DetailsVehicle.PriceFor4To9DaysDisplay} / {DetailsVehicle.PriceFor1To3DaysDisplay}";

    public string PickupLocation
    {
        get => _pickupLocation;
        set
        {
            if (SetProperty(ref _pickupLocation, value))
            {
                OnPropertyChanged(nameof(PickupLocationHeader));
                OnPropertyChanged(nameof(CheckoutPeriodSummary));
                NotifyCheckoutValidationChanged();
            }
        }
    }

    public string PickupLocationHeader => ResolvePickupLocationHeader(PickupLocation);

    public string ReturnLocation
    {
        get => _returnLocation;
        set
        {
            if (SetProperty(ref _returnLocation, value))
            {
                OnPropertyChanged(nameof(CheckoutPeriodSummary));
                NotifyCheckoutValidationChanged();
            }
        }
    }

    public string PickupTime
    {
        get => _pickupTime;
        set
        {
            if (SetProperty(ref _pickupTime, value))
            {
                OnPropertyChanged(nameof(CheckoutPeriodSummary));
                NotifyCheckoutValidationChanged();
                RequestPeriodAvailabilityRefresh();
            }
        }
    }

    public string ReturnTime
    {
        get => _returnTime;
        set
        {
            if (SetProperty(ref _returnTime, value))
            {
                OnPropertyChanged(nameof(CheckoutPeriodSummary));
                NotifyCheckoutValidationChanged();
                RequestPeriodAvailabilityRefresh();
            }
        }
    }

    public VehicleVariantRow? SelectedVehicleVariant
    {
        get => _selectedVehicleVariant;
        set
        {
            if (SetProperty(ref _selectedVehicleVariant, value))
            {
                OnPropertyChanged(nameof(SelectedVehicleVariantInfo));
                OnPropertyChanged(nameof(CheckoutAmountSummary));
                NotifyCheckoutValidationChanged();
            }
        }
    }

    public string CardholderNameInput
    {
        get => _cardholderNameInput;
        set
        {
            var normalized = value.Length > CardholderNameMaxLength
                ? value[..CardholderNameMaxLength]
                : value;

            if (SetProperty(ref _cardholderNameInput, normalized))
            {
                NotifyCheckoutValidationChanged();
            }
        }
    }

    public string CardNumberInput
    {
        get => _cardNumberInput;
        set
        {
            var formatted = FormatCardNumberInput(value);
            if (SetProperty(ref _cardNumberInput, formatted))
            {
                NotifyCheckoutValidationChanged();
            }
        }
    }

    public string CardExpiryInput
    {
        get => _cardExpiryInput;
        set
        {
            var formatted = FormatCardExpiryInput(value);
            if (SetProperty(ref _cardExpiryInput, formatted))
            {
                NotifyCheckoutValidationChanged();
            }
        }
    }

    public string CardCvvInput
    {
        get => _cardCvvInput;
        set
        {
            var formatted = FormatCardCvvInput(value);
            if (SetProperty(ref _cardCvvInput, formatted))
            {
                NotifyCheckoutValidationChanged();
            }
        }
    }

    public string SelectedVehicleVariantInfo => SelectedVehicleVariant is null
        ? "Варіант авто ще не обрано."
        : $"Варіант: {SelectedVehicleVariant.VariantName} • Номер: {SelectedVehicleVariant.LicensePlate} • Пробіг: {SelectedVehicleVariant.MileageDisplay}";

    public string CheckoutPeriodSummary
    {
        get
        {
            var pickupDateTime = BuildPickupDateTime();
            var returnDateTime = BuildReturnDateTime();
            return $"Подача: {PickupLocation}, {pickupDateTime:dd.MM.yyyy HH:mm}   Повернення: {ReturnLocation}, {returnDateTime:dd.MM.yyyy HH:mm}";
        }
    }

    private static string ResolvePickupLocationHeader(string? city)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            return "Оренда авто";
        }

        return city switch
        {
            "Київ" => "Оренда авто у Києві",
            "Львів" => "Оренда авто у Львові",
            "Одеса" => "Оренда авто в Одесі",
            "Дніпро" => "Оренда авто у Дніпрі",
            "Харків" => "Оренда авто у Харкові",
            _ => $"Оренда авто у {city}"
        };
    }

    public string CheckoutAmountSummary
    {
        get
        {
            if (SelectedVehicleVariant is null)
            {
                return "До сплати: -";
            }

            var pickupDateTime = BuildPickupDateTime();
            var returnDateTime = BuildReturnDateTime();
            var rentalHours = CalculateRentalHours(pickupDateTime, returnDateTime);
            if (rentalHours <= 0m)
            {
                return "До сплати: -";
            }

            var total = CalculateProratedAmount(SelectedVehicleVariant.DailyRate, rentalHours);
            return $"До сплати: {total:N0} грн за {FormatRentalDuration(rentalHours)}";
        }
    }

    public string ActiveFiltersSummary
    {
        get
        {
            var summary = new List<string>
            {
                $"Ціна: {MinPriceInput} - {MaxPriceInput} грн",
                $"Сортування: {SelectedSortOption}"
            };

            AppendSelectedFilterSummary(summary, "Клас", CarClassFilters);
            AppendSelectedFilterSummary(summary, "Пальне", FuelFilters);
            AppendSelectedFilterSummary(summary, "Трансмісія", TransmissionFilters);
            AppendSelectedFilterSummary(summary, "Статус", VehicleStatusFilters);

            return string.Join(" • ", summary);
        }
    }

    public string SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (SetProperty(ref _selectedSortOption, value))
            {
                ApplyFilters();
            }
        }
    }

    public string MinPriceInput
    {
        get => _minPriceInput;
        set => SetProperty(ref _minPriceInput, value);
    }

    public string MaxPriceInput
    {
        get => _maxPriceInput;
        set => SetProperty(ref _maxPriceInput, value);
    }

    public double MinPriceValue
    {
        get => _minPriceValue;
        set
        {
            var normalized = Math.Min(value, MaxPriceValue);
            if (SetProperty(ref _minPriceValue, normalized))
            {
                MinPriceInput = ((int)Math.Round(normalized)).ToString(CultureInfo.InvariantCulture);
            }
        }
    }

    public double MaxPriceValue
    {
        get => _maxPriceValue;
        set
        {
            var normalized = Math.Max(value, MinPriceValue);
            if (SetProperty(ref _maxPriceValue, normalized))
            {
                MaxPriceInput = ((int)Math.Round(normalized)).ToString(CultureInfo.InvariantCulture);
            }
        }
    }

    public override async Task RefreshAsync()
    {
        if (IsLoading)
        {
            return;
        }

        await ReloadCatalogAsync(resetTransientState: true);
    }

    private async Task ReloadCatalogAsync(bool resetTransientState)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        try
        {
            var selectedCardId = SelectedVehicle?.Id;
            var detailsCardId = DetailsVehicle?.Id;
            var selectedVariantVehicleId = SelectedVehicleVariant?.VehicleId;
            var wasVehicleDetailsOpen = IsVehicleDetailsDialogOpen;
            var wasCheckoutOpen = IsCheckoutDialogOpen;
            var requestStart = BuildPickupDateTime();
            var requestEnd = BuildReturnDateTime();

            var vehicles = await _dbContext.Vehicles
                .AsNoTracking()
                .OrderBy(vehicle => vehicle.Make)
                .ThenBy(vehicle => vehicle.Model)
                .ToListAsync();

            var overlappingRentals = requestEnd > requestStart
                ? await _dbContext.Rentals
                    .AsNoTracking()
                    .Where(rental =>
                        (rental.Status == RentalStatus.Active || rental.Status == RentalStatus.Booked) &&
                        rental.StartDate <= requestEnd &&
                        requestStart <= rental.EndDate)
                    .OrderBy(rental => rental.StartDate)
                    .ToListAsync()
                : [];
            var rentalsByVehicleId = overlappingRentals
                .GroupBy(rental => rental.VehicleId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<Rental>)group.ToList());

            var vehicleAvailability = new List<VehicleAvailability>(vehicles.Count);
            foreach (var vehicle in vehicles)
            {
                rentalsByVehicleId.TryGetValue(vehicle.Id, out var vehicleRentals);
                vehicleRentals ??= Array.Empty<Rental>();

                vehicleAvailability.Add(ResolveVehicleAvailability(
                    vehicle,
                    requestStart,
                    requestEnd,
                    vehicleRentals));
            }

            var vehicleIds = vehicles.Select(vehicle => vehicle.Id).ToList();
            var damages = await _dbContext.Damages
                .AsNoTracking()
                .Where(damage => vehicleIds.Contains(damage.VehicleId))
                .OrderByDescending(damage => damage.DateReported)
                .ToListAsync();
            var damagesByVehicleId = damages
                .GroupBy(damage => damage.VehicleId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<Damage>)group.ToList());
            var availabilityByVehicleId = vehicleAvailability.ToDictionary(item => item.VehicleId);

            _allCards.Clear();
            _allCards.AddRange(BuildShowcaseCards(vehicles, availabilityByVehicleId, damagesByVehicleId));
            ApplyFilters();

            SelectedVehicle = selectedCardId.HasValue
                ? Vehicles.FirstOrDefault(item => item.Id == selectedCardId.Value) ??
                  Vehicles.FirstOrDefault(item => item.IsAvailableNow) ??
                  Vehicles.FirstOrDefault()
                : Vehicles.FirstOrDefault(item => item.IsAvailableNow) ?? Vehicles.FirstOrDefault();

            if (resetTransientState)
            {
                IsVehicleDetailsDialogOpen = false;
                IsCheckoutDialogOpen = false;
                VehicleVariants.Clear();
                DetailsVehicle = null;
                SelectedVehicleVariant = null;
                ResetCardPaymentInputs();
            }
            else
            {
                RestoreAvailabilityContext(detailsCardId, selectedVariantVehicleId, wasVehicleDetailsOpen, wasCheckoutOpen);
            }

            var totalFleetVehicles = CountFleetVehicles();
            var availableOnSelectedPeriod = Vehicles.Count(item => item.IsAvailableNow);
            if (resetTransientState)
            {
                StatusMessage = SelectedVehicle is not null
                    ? $"Знайдено {totalFleetVehicles} авто у автопарку ({Vehicles.Count} моделей). Доступно на обраний період: {availableOnSelectedPeriod}. Обране: {SelectedVehicle.Car}."
                    : $"Знайдено {totalFleetVehicles} авто у автопарку ({Vehicles.Count} моделей). Доступно на обраний період: {availableOnSelectedPeriod}.";
            }
            else if (IsCheckoutDialogOpen && SelectedVehicleVariant is not null && !SelectedVehicleVariant.CanSelect)
            {
                StatusMessage = $"Авто {SelectedVehicleVariant.LicensePlate} зайняте на обраний період.";
            }
            else if (IsVehicleDetailsDialogOpen && DetailsVehicle is not null && DetailsVehicle.FleetAvailableCount <= 0)
            {
                StatusMessage = $"Для {DetailsVehicle.Car} немає вільних варіантів на обраний період.";
            }
            else
            {
                StatusMessage = $"Знайдено {totalFleetVehicles} авто у автопарку ({Vehicles.Count} моделей). Доступно на обраний період: {availableOnSelectedPeriod}.";
            }

            MarkDataLoaded();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RentVehicleAsync()
    {
        await OpenSelectedVehicleDetailsAsync();
    }

    private async Task OpenSelectedVehicleDetailsAsync()
    {
        await OpenVehicleDetailsAsync(SelectedVehicle);
    }

    private async Task OpenVehicleDetailsAsync(ProkatCarCard? card)
    {
        if (IsInitialized)
        {
            await ReloadCatalogAsync(resetTransientState: false);
            if (card is not null)
            {
                card = _allCards.FirstOrDefault(item => item.Id == card.Id) ?? card;
            }
        }

        if (card is null)
        {
            StatusMessage = "Оберіть авто зі списку.";
            return;
        }

        SelectedVehicle = card;
        if (!_vehicleVariantsByCardId.TryGetValue(card.Id, out var variants) || variants.Count == 0)
        {
            StatusMessage = $"Для {card.Car} немає доступних варіантів в автопарку.";
            return;
        }

        DetailsVehicle = card;
        SelectedVehicleVariant = null;
        IsCheckoutDialogOpen = false;
        ResetCardPaymentInputs();
        VehicleVariants.Clear();
        foreach (var variant in variants)
        {
            VehicleVariants.Add(variant);
        }

        IsVehicleDetailsDialogOpen = true;
        StatusMessage = card.FleetAvailableCount switch
        {
            <= 0 => $"Для {card.Car} немає вільних варіантів на обраний період.",
            1 => $"Для {card.Car} доступний 1 варіант на обраний період.",
            _ => $"Для {card.Car} доступно {card.FleetAvailableCount} варіанти на обраний період. Оберіть екземпляр."
        };
        await Task.CompletedTask;
    }

    private void CloseVehicleDetailsDialog()
    {
        IsVehicleDetailsDialogOpen = false;
    }

    private async Task ChooseVehicleVariantAsync(VehicleVariantRow? variant)
    {
        if (variant is null)
        {
            return;
        }

        if (!variant.CanSelect)
        {
            StatusMessage = $"Авто {variant.LicensePlate} зараз недоступне.";
            return;
        }

        IsVehicleDetailsDialogOpen = false;
        SelectedVehicleVariant = variant;
        ReturnLocation = string.IsNullOrWhiteSpace(ReturnLocation) ? PickupLocation : ReturnLocation;
        if (EndDate.Date < StartDate.Date)
        {
            EndDate = StartDate.Date;
        }

        if (string.IsNullOrWhiteSpace(ReturnTime))
        {
            ReturnTime = PickupTime;
        }

        var pickupDateTime = BuildPickupDateTime();
        var returnDateTime = BuildReturnDateTime();
        if (returnDateTime <= pickupDateTime)
        {
            var normalizedReturn = pickupDateTime.AddHours(1);
            EndDate = normalizedReturn.Date;
            ReturnTime = normalizedReturn.ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        IsCheckoutDialogOpen = true;
        StatusMessage = $"Обрано варіант {variant.LicensePlate}. Заповніть оплату для завершення оформлення.";
        await Task.CompletedTask;
    }

    private void CloseCheckoutDialog()
    {
        IsCheckoutDialogOpen = false;
        ResetCardPaymentInputs();
    }

    private async Task CompleteCheckoutAsync()
    {
        if (SelectedVehicleVariant is null)
        {
            StatusMessage = "Оберіть варіант авто перед оформленням.";
            return;
        }

        var validationError = ValidateCheckoutInputs();
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            StatusMessage = validationError;
            return;
        }

        if (ShouldWarnAboutCardNumber(CardNumberInput))
        {
            var warningResult = MessageBox.Show(
                "Ймовірно, це не справжня картка. Продовжити?",
                "Підозріла картка",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (warningResult != MessageBoxResult.Yes)
            {
                return;
            }
        }

        var pickupDateTime = BuildPickupDateTime();
        var returnDateTime = BuildReturnDateTime();
        var carName = DetailsVehicle?.Car ?? SelectedVehicle?.Car ?? "обране авто";
        var paymentNote = BuildPaymentNote();

        var success = await CreateRentalForVehicleAsync(
            SelectedVehicleVariant.VehicleId,
            carName,
            pickupDateTime,
            returnDateTime,
            createCardPayment: true,
            paymentNote);

        if (success)
        {
            IsCheckoutDialogOpen = false;
            IsVehicleDetailsDialogOpen = false;
            ResetCardPaymentInputs();
            SelectedVehicleVariant = null;
        }
    }

    private async Task<bool> CreateRentalForVehicleAsync(
        int vehicleId,
        string carName,
        DateTime pickupDateTime,
        DateTime returnDateTime,
        bool createCardPayment,
        string paymentNote)
    {
        if (pickupDateTime >= returnDateTime)
        {
            const string message = "Час повернення має бути пізніше часу подачі.";
            StatusMessage = message;
            SetCheckoutAttemptHint(message);
            return false;
        }

        if (pickupDateTime < DateTime.Now)
        {
            const string message = "Початок оренди не може бути в минулому.";
            StatusMessage = message;
            SetCheckoutAttemptHint(message);
            return false;
        }

        var clientId = await EnsureClientProfileAsync();
        if (clientId is null)
        {
            const string message = "Не вдалося створити профіль клієнта для оренди.";
            StatusMessage = message;
            SetCheckoutAttemptHint(message);
            return false;
        }

        CreateRentalResult result;
        if (createCardPayment)
        {
            result = await _rentalService.CreateRentalWithPaymentAsync(
                new CreateRentalWithPaymentRequest(
                    clientId.Value,
                    vehicleId,
                    _currentEmployee.Id,
                    pickupDateTime,
                    returnDateTime,
                    PickupLocation,
                    string.IsNullOrWhiteSpace(ReturnLocation) ? PickupLocation : ReturnLocation,
                    PaymentMethod.Card,
                    PaymentDirection.Incoming,
                    paymentNote));
        }
        else
        {
            result = await _rentalService.CreateRentalAsync(
                new CreateRentalRequest(
                    clientId.Value,
                    vehicleId,
                    _currentEmployee.Id,
                    pickupDateTime,
                    returnDateTime,
                    PickupLocation,
                    string.IsNullOrWhiteSpace(ReturnLocation) ? PickupLocation : ReturnLocation));
        }

        if (!result.Success)
        {
            StatusMessage = result.Message;
            SetCheckoutAttemptHint(result.Message);
            return false;
        }

        SetCheckoutAttemptHint(string.Empty);
        _refreshCoordinator.Invalidate(PageRefreshArea.Fleet | PageRefreshArea.Rentals | PageRefreshArea.Prokat | PageRefreshArea.Reports | PageRefreshArea.UserRentals);
        await RefreshAsync();
        StatusMessage = $"Оренду оформлено. Договір: {result.ContractNumber}. Авто: {carName}.";
        return true;
    }

    public async Task PrepareRebookingAsync(int vehicleId)
    {
        if (vehicleId <= 0)
        {
            return;
        }

        await EnsureDataAsync();

        if (_allCards.Count == 0)
        {
            StatusMessage = "Каталог прокату порожній.";
            return;
        }

        _suppressFilterApply = true;
        try
        {
            SetFilterSelection(CarClassFilters);
            SetFilterSelection(FuelFilters);
            SetFilterSelection(TransmissionFilters);
            foreach (var statusFilter in VehicleStatusFilters)
            {
                statusFilter.IsSelected = true;
            }

            SelectedSortOption = "Спочатку популярні";

            var minPrice = _allCards.Min(card => (double)card.PriceFor26PlusDays);
            var maxPrice = _allCards.Max(card => (double)Math.Max(card.PriceFor1To3Days, card.PriceFor1To3DaysMax));
            MinPriceValue = minPrice;
            MaxPriceValue = maxPrice;
            MinPriceInput = ((int)Math.Floor(minPrice)).ToString(CultureInfo.InvariantCulture);
            MaxPriceInput = ((int)Math.Ceiling(maxPrice)).ToString(CultureInfo.InvariantCulture);
        }
        finally
        {
            _suppressFilterApply = false;
        }

        ApplyFilters();

        var matchingCard = _allCards.FirstOrDefault(card =>
            card.BackingVehicleId == vehicleId ||
            (_vehicleVariantsByCardId.TryGetValue(card.Id, out var variants) &&
             variants.Any(variant => variant.VehicleId == vehicleId)));
        if (matchingCard is null)
        {
            StatusMessage = "Не вдалося знайти авто для повторного оформлення.";
            return;
        }

        SelectedVehicle = Vehicles.FirstOrDefault(item => item.Id == matchingCard.Id) ?? matchingCard;
        IsVehicleDetailsDialogOpen = false;
        IsCheckoutDialogOpen = false;
        DetailsVehicle = null;
        SelectedVehicleVariant = null;
        VehicleVariants.Clear();
        ResetCardPaymentInputs();

        var matchingVariant = _vehicleVariantsByCardId.TryGetValue(matchingCard.Id, out var rows)
            ? rows.FirstOrDefault(item => item.VehicleId == vehicleId)
            : null;
        StatusMessage = matchingVariant is null
            ? $"Підібрано {matchingCard.Car} для повторного оформлення."
            : $"Підібрано {matchingCard.Car} ({matchingVariant.LicensePlate}) для повторного оформлення.";
    }

    private string? ValidateCheckoutInputs()
    {
        if (SelectedVehicleVariant is null)
        {
            return "Оберіть варіант авто.";
        }

        if (!SelectedVehicleVariant.CanSelect)
        {
            return "Обраний варіант уже зайнятий на обраний період.";
        }

        if (string.IsNullOrWhiteSpace(ReturnLocation))
        {
            return "Оберіть пункт повернення.";
        }

        var startDateValidationError = ResolveCheckoutStartDateValidationMessage();
        if (!string.IsNullOrWhiteSpace(startDateValidationError))
        {
            return startDateValidationError;
        }

        var endDateValidationError = ResolveCheckoutEndDateValidationMessage();
        if (!string.IsNullOrWhiteSpace(endDateValidationError))
        {
            return endDateValidationError;
        }

        if (string.IsNullOrWhiteSpace(CardholderNameInput))
        {
            return "Вкажіть ім'я власника картки.";
        }

        var cardDigits = DigitsOnly(CardNumberInput);
        if (cardDigits.Length != CardNumberMaxDigits)
        {
            return "Вкажіть 16-значний номер картки.";
        }

        if (!TryParseCardExpiry(CardExpiryInput, out var expiryDate))
        {
            return "Вкажіть термін дії картки у форматі MM/YY.";
        }

        if (expiryDate < DateTime.Today)
        {
            return "Термін дії картки минув.";
        }

        var cvvDigits = DigitsOnly(CardCvvInput);
        if (cvvDigits.Length is < 3 or > 4)
        {
            return "CVV має містити 3 або 4 цифри.";
        }

        return null;
    }

    private bool CanCompleteCheckout()
    {
        return !IsLoading &&
               string.IsNullOrWhiteSpace(ResolveCheckoutStartDateValidationMessage()) &&
               string.IsNullOrWhiteSpace(ResolveCheckoutEndDateValidationMessage());
    }

    private string? ResolveCheckoutStartDateValidationMessage()
    {
        return BuildPickupDateTime() < DateTime.Now
            ? "Початок оренди не може бути в минулому."
            : null;
    }

    private string? ResolveCheckoutEndDateValidationMessage()
    {
        if (EndDate.Date < StartDate.Date)
        {
            return "Дата повернення не може бути раніше дати подачі.";
        }

        return BuildReturnDateTime() <= BuildPickupDateTime()
            ? "Дата/час повернення мають бути пізніше подачі."
            : null;
    }

    private DateTime BuildPickupDateTime()
    {
        return CombineDateAndTime(StartDate, PickupTime, new TimeSpan(10, 0, 0));
    }

    private void NotifyCheckoutValidationChanged(bool preserveAttemptHint = false)
    {
        if (!preserveAttemptHint && !string.IsNullOrWhiteSpace(_checkoutAttemptHint))
        {
            _checkoutAttemptHint = string.Empty;
        }

        OnPropertyChanged(nameof(MinEndDate));
        OnPropertyChanged(nameof(CheckoutStartDateValidationMessage));
        OnPropertyChanged(nameof(HasCheckoutStartDateValidationMessage));
        OnPropertyChanged(nameof(CheckoutEndDateValidationMessage));
        OnPropertyChanged(nameof(HasCheckoutEndDateValidationMessage));
        OnPropertyChanged(nameof(CheckoutActionHint));
        OnPropertyChanged(nameof(HasCheckoutActionHint));
        CompleteCheckoutCommand.NotifyCanExecuteChanged();
    }

    private void SetCheckoutAttemptHint(string? value)
    {
        var normalizedValue = value?.Trim() ?? string.Empty;
        if (string.Equals(_checkoutAttemptHint, normalizedValue, StringComparison.Ordinal))
        {
            return;
        }

        _checkoutAttemptHint = normalizedValue;
        NotifyCheckoutValidationChanged(preserveAttemptHint: true);
    }

    private DateTime BuildReturnDateTime()
    {
        var fallback = ParseTime(PickupTime, new TimeSpan(10, 0, 0));
        return CombineDateAndTime(EndDate, ReturnTime, fallback);
    }

    private static decimal CalculateRentalHours(DateTime startDateTime, DateTime endDateTime)
    {
        var hours = (decimal)(endDateTime - startDateTime).TotalHours;
        if (hours <= 0m)
        {
            return 0m;
        }

        return decimal.Round(hours, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal CalculateProratedAmount(decimal dailyRate, decimal rentalHours)
    {
        if (rentalHours <= 0m)
        {
            return 0m;
        }

        return decimal.Round(dailyRate * (rentalHours / 24m), 2, MidpointRounding.AwayFromZero);
    }

    private static string FormatRentalDuration(decimal rentalHours)
    {
        var totalMinutes = (int)Math.Round((double)(rentalHours * 60m), MidpointRounding.AwayFromZero);
        if (totalMinutes <= 0)
        {
            return "0 год";
        }

        var days = totalMinutes / (24 * 60);
        var remainingMinutes = totalMinutes % (24 * 60);
        var hours = remainingMinutes / 60;
        var minutes = remainingMinutes % 60;

        if (days > 0 && hours > 0)
        {
            return $"{days} д {hours} год";
        }

        if (days > 0)
        {
            return minutes > 0
                ? $"{days} д {minutes} хв"
                : $"{days} д";
        }

        if (hours > 0)
        {
            return minutes > 0
                ? $"{hours} год {minutes} хв"
                : $"{hours} год";
        }

        return $"{minutes} хв";
    }

    private static DateTime CombineDateAndTime(DateTime date, string timeInput, TimeSpan fallbackTime)
    {
        var time = ParseTime(timeInput, fallbackTime);
        return date.Date.Add(time);
    }

    private static TimeSpan ParseTime(string timeInput, TimeSpan fallbackTime)
    {
        if (TimeSpan.TryParseExact(timeInput, "hh\\:mm", CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        if (TimeSpan.TryParse(timeInput, CultureInfo.CurrentCulture, out parsed))
        {
            return parsed;
        }

        return fallbackTime;
    }

    private string BuildPaymentNote()
    {
        var cardDigits = DigitsOnly(CardNumberInput);
        var tail = cardDigits.Length >= 4 ? cardDigits[^4..] : cardDigits;
        var owner = CardholderNameInput.Trim();
        var builder = new StringBuilder();
        builder.Append("Оплата карткою");
        if (!string.IsNullOrWhiteSpace(tail))
        {
            builder.Append(" ****");
            builder.Append(tail);
        }

        if (!string.IsNullOrWhiteSpace(owner))
        {
            builder.Append(". Власник: ");
            builder.Append(owner);
        }

        return builder.ToString();
    }

    private static string DigitsOnly(string value)
    {
        return string.Concat(value.Where(char.IsDigit));
    }

    private static string FormatCardNumberInput(string value)
    {
        var digits = DigitsOnly(value);
        if (digits.Length > CardNumberMaxDigits)
        {
            digits = digits[..CardNumberMaxDigits];
        }

        var builder = new StringBuilder(CardNumberInputMaxLength);
        for (var index = 0; index < digits.Length; index++)
        {
            if (index > 0 && index % 4 == 0)
            {
                builder.Append(' ');
            }

            builder.Append(digits[index]);
        }

        return builder.ToString();
    }

    private static string FormatCardExpiryInput(string value)
    {
        var digits = DigitsOnly(value);
        if (digits.Length > CardExpiryMaxDigits)
        {
            digits = digits[..CardExpiryMaxDigits];
        }

        if (digits.Length <= 2)
        {
            return digits;
        }

        return $"{digits[..2]}/{digits[2..]}";
    }

    private static string FormatCardCvvInput(string value)
    {
        var digits = DigitsOnly(value);
        return digits.Length > CardCvvMaxDigits
            ? digits[..CardCvvMaxDigits]
            : digits;
    }

    private static bool ShouldWarnAboutCardNumber(string value)
    {
        var digits = DigitsOnly(value);
        return digits.Length == CardNumberMaxDigits && !PassesLuhnCheck(digits);
    }

    private static bool PassesLuhnCheck(string digits)
    {
        var sum = 0;
        var alternate = false;
        for (var index = digits.Length - 1; index >= 0; index--)
        {
            var digit = digits[index] - '0';
            if (digit is < 0 or > 9)
            {
                return false;
            }

            if (alternate)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }

    private static bool TryParseCardExpiry(string input, out DateTime expiryDate)
    {
        expiryDate = DateTime.MinValue;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var parts = input.Trim().Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var month) ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var year))
        {
            return false;
        }

        if (year < 100)
        {
            year += 2000;
        }

        if (month is < 1 or > 12 || year is < 2000 or > 2099)
        {
            return false;
        }

        var lastDay = DateTime.DaysInMonth(year, month);
        expiryDate = new DateTime(year, month, lastDay);
        return true;
    }

    private void ResetCardPaymentInputs()
    {
        CardholderNameInput = string.Empty;
        CardNumberInput = string.Empty;
        CardExpiryInput = string.Empty;
        CardCvvInput = string.Empty;
    }

    private void SelectVehicle(ProkatCarCard? vehicle)
    {
        if (vehicle is null)
        {
            return;
        }

        SelectedVehicle = vehicle;
        StatusMessage = $"Обрано авто: {vehicle.Car}. В автопарку: {vehicle.FleetCount} шт.";
    }

    private async Task<int?> EnsureClientProfileAsync()
    {
        var employee = await _dbContext.Employees
            .Include(item => item.Account)
                .ThenInclude(item => item!.Client)
                .ThenInclude(item => item!.Documents)
            .FirstOrDefaultAsync(item => item.Id == _currentEmployee.Id);
        if (employee is null)
        {
            return null;
        }

        var passportData = $"EMP-{employee.Id:D6}";
        var driverLicense = $"USR-{employee.Id:D6}";

        Client? client = employee.Account?.Client;
        if (client is null && employee.PortalClientId.HasValue)
        {
            client = await _dbContext.Clients
                .FirstOrDefaultAsync(item => item.Id == employee.PortalClientId.Value);
        }

        if (client is null && employee.AccountId > 0)
        {
            client = await _dbContext.Clients
                .FirstOrDefaultAsync(item => item.AccountId == employee.AccountId);
        }

        client ??= await _dbContext.Clients
            .FirstOrDefaultAsync(existing =>
                _dbContext.ClientDocuments.Any(document =>
                    document.ClientId == existing.Id &&
                    ((document.DocumentTypeCode == ClientDocumentTypes.Passport && document.DocumentNumber == passportData) ||
                     (document.DocumentTypeCode == ClientDocumentTypes.DriverLicense && document.DocumentNumber == driverLicense))));

        if (client is null)
        {
            client = new Client
            {
                FullName = employee.FullName,
                PassportData = passportData,
                DriverLicense = driverLicense,
                Phone = ResolveClientPhone(employee.Login)
            };

            _dbContext.Clients.Add(client);
            await _dbContext.SaveChangesAsync();
        }

        var hasChanges = false;
        var normalizedPhone = ResolveClientPhone(employee.Login, client.Phone);
        if (!string.Equals(client.Phone, normalizedPhone, StringComparison.Ordinal))
        {
            client.Phone = normalizedPhone;
            hasChanges = true;
        }

        if (!string.Equals(client.FullName, employee.FullName, StringComparison.Ordinal))
        {
            client.FullName = employee.FullName;
            hasChanges = true;
        }

        if (employee.PortalClientId != client.Id)
        {
            employee.PortalClientId = client.Id;
            _currentEmployee.PortalClientId = client.Id;
            hasChanges = true;
        }

        if (hasChanges)
        {
            await _dbContext.SaveChangesAsync();
        }

        return client.Id;
    }

    private void RequestPeriodAvailabilityRefresh()
    {
        if (!IsInitialized)
        {
            return;
        }

        _periodAvailabilityRefreshPending = true;
        if (IsLoading)
        {
            return;
        }

        RefreshPeriodAvailabilityAsync();
    }

    private async void RefreshPeriodAvailabilityAsync()
    {
        try
        {
            while (_periodAvailabilityRefreshPending)
            {
                _periodAvailabilityRefreshPending = false;
                await ReloadCatalogAsync(resetTransientState: false);
            }
        }
        catch (Exception exception)
        {
            StatusMessage = $"Не вдалося оновити доступність авто: {exception.Message}";
        }
    }

    private void RestoreAvailabilityContext(
        int? detailsCardId,
        int? selectedVariantVehicleId,
        bool wasVehicleDetailsOpen,
        bool wasCheckoutOpen)
    {
        var detailsCard = detailsCardId.HasValue
            ? _allCards.FirstOrDefault(item => item.Id == detailsCardId.Value)
            : null;

        DetailsVehicle = detailsCard;
        VehicleVariants.Clear();
        if (detailsCard is null || !_vehicleVariantsByCardId.TryGetValue(detailsCard.Id, out var variants) || variants.Count == 0)
        {
            SelectedVehicleVariant = null;
            IsVehicleDetailsDialogOpen = false;
            IsCheckoutDialogOpen = false;
            return;
        }

        foreach (var variant in variants)
        {
            VehicleVariants.Add(variant);
        }

        SelectedVehicleVariant = selectedVariantVehicleId.HasValue
            ? VehicleVariants.FirstOrDefault(item => item.VehicleId == selectedVariantVehicleId.Value)
            : null;

        IsVehicleDetailsDialogOpen = wasVehicleDetailsOpen && !wasCheckoutOpen;
        IsCheckoutDialogOpen = wasCheckoutOpen && SelectedVehicleVariant is not null;
    }

    private static VehicleAvailability ResolveVehicleAvailability(
        Vehicle vehicle,
        DateTime requestStart,
        DateTime requestEnd,
        IReadOnlyList<Rental> conflictingRentals)
    {
        if (requestEnd <= requestStart)
        {
            return new VehicleAvailability(
                vehicle.Id,
                false,
                requestStart,
                "Вкажіть коректний період оренди.");
        }

        if (!vehicle.IsBookable)
        {
            return new VehicleAvailability(
                vehicle.Id,
                false,
                requestStart,
                "Авто тимчасово недоступне в системі.");
        }

        if (conflictingRentals.Count == 0)
        {
            return new VehicleAvailability(
                vehicle.Id,
                true,
                requestStart,
                "Доступне на обраний період");
        }

        var availableFrom = conflictingRentals.Max(item => item.EndDate);
        return new VehicleAvailability(
            vehicle.Id,
            false,
            availableFrom,
            $"Зайнято до {FormatAvailabilityMoment(availableFrom)}");
    }

    private void InitializeFilterCollections()
    {
        CarClassFilters.Add(new FilterOption("Економ", "Економ", "🚗", ApplyFilters));
        CarClassFilters.Add(new FilterOption("Середній", "Середній", "🚘", ApplyFilters));
        CarClassFilters.Add(new FilterOption("Бізнес", "Бізнес", "🚖", ApplyFilters));
        CarClassFilters.Add(new FilterOption("Преміум", "Преміум", "🏎", ApplyFilters));
        CarClassFilters.Add(new FilterOption("Позашляховик", "Позашляховик", "🚙", ApplyFilters));
        CarClassFilters.Add(new FilterOption("Мінівен", "Мінівен", "🚐", ApplyFilters));
        CarClassFilters.Add(new FilterOption("Комерційний", "Комерційний", "🚚", ApplyFilters));
        CarClassFilters.Add(new FilterOption("Електромобілі", "Електромобілі", "⚡", ApplyFilters));
        CarClassFilters.Add(new FilterOption("Пікап", "Пікап", "🛻", ApplyFilters));
        CarClassFilters.Add(new FilterOption("Кабріолет", "Кабріолет", "🏁", ApplyFilters));

        FuelFilters.Add(new FilterOption("Бензин", "Бензин", "⛽", ApplyFilters));
        FuelFilters.Add(new FilterOption("Дизель", "Дизель", "⛽", ApplyFilters));
        FuelFilters.Add(new FilterOption("Електро", "Електро", "🔌", ApplyFilters));
        FuelFilters.Add(new FilterOption("Гібрид", "Гібрид", "⚙", ApplyFilters));

        TransmissionFilters.Add(new FilterOption("Механіка", "Механіка", "🕹", ApplyFilters));
        TransmissionFilters.Add(new FilterOption("Автомат", "Автомат", "⚙", ApplyFilters));

        VehicleStatusFilters.Add(new FilterOption(AvailableStatusKey, AvailableStatusKey, "✅", ApplyFilters, isSelected: true));
        VehicleStatusFilters.Add(new FilterOption(UnavailableStatusKey, UnavailableStatusKey, "⛔", ApplyFilters));
    }

    private void ApplyPriceFilter()
    {
        var min = ParsePriceInput(MinPriceInput, DefaultMinPrice);
        var max = ParsePriceInput(MaxPriceInput, DefaultMaxPrice);

        if (min > max)
        {
            (min, max) = (max, min);
        }

        MinPriceValue = min;
        MaxPriceValue = max;
        MinPriceInput = ((int)Math.Round(min)).ToString(CultureInfo.InvariantCulture);
        MaxPriceInput = ((int)Math.Round(max)).ToString(CultureInfo.InvariantCulture);
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        if (_suppressFilterApply)
        {
            return;
        }

        var selectedClasses = CarClassFilters
            .Where(item => item.IsSelected)
            .Select(item => item.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedFuels = FuelFilters
            .Where(item => item.IsSelected)
            .Select(item => item.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedTransmissions = TransmissionFilters
            .Where(item => item.IsSelected)
            .Select(item => item.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedStatuses = VehicleStatusFilters
            .Where(item => item.IsSelected)
            .Select(item => item.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var minPrice = ParsePriceInput(MinPriceInput, MinPriceValue);
        var maxPrice = ParsePriceInput(MaxPriceInput, MaxPriceValue);
        if (minPrice > maxPrice)
        {
            (minPrice, maxPrice) = (maxPrice, minPrice);
        }

        IEnumerable<ProkatCarCard> query = _allCards.Where(card =>
            card.PriceFor26PlusDays >= (decimal)minPrice &&
            card.PriceFor26PlusDays <= (decimal)maxPrice);

        if (selectedClasses.Count > 0)
        {
            query = query.Where(card => selectedClasses.Contains(card.CarClass));
        }

        if (selectedFuels.Count > 0)
        {
            query = query.Where(card => selectedFuels.Contains(card.FuelType));
        }

        if (selectedTransmissions.Count > 0)
        {
            query = query.Where(card => selectedTransmissions.Contains(card.TransmissionType));
        }

        if (selectedStatuses.Count > 0)
        {
            query = query.Where(card =>
                (card.IsAvailableNow && selectedStatuses.Contains(AvailableStatusKey)) ||
                (!card.IsAvailableNow && selectedStatuses.Contains(UnavailableStatusKey)));
        }

        query = SelectedSortOption switch
        {
            "Спочатку дешевші" => query.OrderBy(card => card.PriceFor26PlusDays),
            "Спочатку дорожчі" => query.OrderByDescending(card => card.PriceFor26PlusDays),
            _ => query.OrderBy(card => card.PopularityRank)
        };

        var selectedId = SelectedVehicle?.Id;

        Vehicles.Clear();
        foreach (var card in query)
        {
            Vehicles.Add(card);
        }

        SelectedVehicle = selectedId.HasValue
            ? Vehicles.FirstOrDefault(item => item.Id == selectedId.Value)
            : Vehicles.FirstOrDefault(item => item.IsAvailableNow) ?? Vehicles.FirstOrDefault();

        var availableNow = Vehicles.Count(item => item.IsAvailableNow);
        var totalFleetVehicles = CountFleetVehicles();
        StatusMessage = $"Підібрано {totalFleetVehicles} авто у автопарку ({Vehicles.Count} моделей). Доступно на обраний період: {availableNow}.";
        OnPropertyChanged(nameof(ActiveFiltersSummary));
    }

    private void ResetFilters()
    {
        _suppressFilterApply = true;
        try
        {
            SetFilterSelection(CarClassFilters);
            SetFilterSelection(FuelFilters);
            SetFilterSelection(TransmissionFilters);
            SetFilterSelection(VehicleStatusFilters, AvailableStatusKey);
            SelectedSortOption = "Спочатку популярні";
            MinPriceValue = DefaultMinPrice;
            MaxPriceValue = DefaultMaxPrice;
            MinPriceInput = DefaultMinPrice.ToString(CultureInfo.InvariantCulture);
            MaxPriceInput = DefaultMaxPrice.ToString(CultureInfo.InvariantCulture);
        }
        finally
        {
            _suppressFilterApply = false;
        }

        ApplyFilters();
    }

    private static void SetFilterSelection(IEnumerable<FilterOption> filters, string? selectedKey = null)
    {
        foreach (var filter in filters)
        {
            filter.IsSelected = selectedKey is not null &&
                string.Equals(filter.Key, selectedKey, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AppendSelectedFilterSummary(
        ICollection<string> summary,
        string title,
        IEnumerable<FilterOption> filters)
    {
        var selectedValues = filters
            .Where(item => item.IsSelected)
            .Select(item => item.Label)
            .ToList();

        if (selectedValues.Count == 0)
        {
            return;
        }

        summary.Add($"{title}: {string.Join(", ", selectedValues)}");
    }

    private int CountFleetVehicles()
    {
        return _vehicleVariantsByCardId.Values
            .SelectMany(items => items)
            .Select(item => item.VehicleId)
            .Distinct()
            .Count();
    }

    private static string ResolveVehicleClass(string car, decimal dailyRate)
    {
        var normalized = car.Trim().ToLowerInvariant();

        if (ContainsAny(normalized, "mx-5", "z4", "500c", "roadster", "cabrio"))
        {
            return "Кабріолет";
        }

        if (ContainsAny(normalized, "hilux", "ranger", "navara", "d-max", "dmax"))
        {
            return "Пікап";
        }

        if (ContainsAny(normalized, "leaf", "model 3", "model y", "ioniq 5", "ev6"))
        {
            return "Електромобілі";
        }

        if (ContainsAny(normalized, "transit", "sprinter", "vivaro"))
        {
            return "Комерційний";
        }

        if (ContainsAny(normalized, "trafi", "kangoo", "multivan"))
        {
            return "Мінівен";
        }

        if (ContainsAny(normalized, "prado", "x5", "q7", "gle", "rav4", "sportage", "x-trail", "outlander", "forester", "tiguan", "duster", "wrangler", "discovery", "cayenne"))
        {
            return "Позашляховик";
        }

        if (dailyRate >= VehicleDomainRules.BusinessUpperBound)
        {
            return "Преміум";
        }

        if (dailyRate >= VehicleDomainRules.MidUpperBound)
        {
            return "Бізнес";
        }

        return dailyRate >= VehicleDomainRules.EconomyUpperBound ? "Середній" : "Економ";
    }

    private static bool ContainsAny(string source, params string[] tokens)
    {
        return tokens.Any(token => source.Contains(token, StringComparison.Ordinal));
    }

    private static double ParsePriceInput(string value, double fallback)
    {
        if (double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedInvariant))
        {
            return parsedInvariant;
        }

        if (double.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsedCurrent))
        {
            return parsedCurrent;
        }

        return fallback;
    }

    private static string FormatAvailabilityMoment(DateTime value)
    {
        return value.TimeOfDay == TimeSpan.Zero
            ? value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
            : value.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
    }

    private List<ProkatCarCard> BuildShowcaseCards(
        IReadOnlyList<Vehicle> fleetVehicles,
        IReadOnlyDictionary<int, VehicleAvailability> availabilityByVehicleId,
        IReadOnlyDictionary<int, IReadOnlyList<Damage>> damagesByVehicleId)
    {
        _vehicleVariantsByCardId.Clear();
        var groupedVehicles = fleetVehicles
            .GroupBy(BuildCatalogGroupKey)
            .Select(group => group.ToList())
            .OrderBy(group => ResolveCatalogSeed(group[0])?.PopularityRank ?? int.MaxValue)
            .ThenBy(group => ResolveCardDisplayName(group[0], ResolveCatalogSeed(group[0])), StringComparer.Ordinal)
            .ToList();

        var cards = new List<ProkatCarCard>(groupedVehicles.Count);
        var nextCardId = 1;
        foreach (var groupVehicles in groupedVehicles)
        {
            if (groupVehicles.Count == 0)
            {
                continue;
            }

            var representativeVehicle = ResolveRepresentativeVehicle(groupVehicles, availabilityByVehicleId);
            var seed = ResolveCatalogSeed(representativeVehicle);
            var variantRows = groupVehicles
                .Select(vehicle =>
                {
                    availabilityByVehicleId.TryGetValue(vehicle.Id, out var availability);
                    damagesByVehicleId.TryGetValue(vehicle.Id, out var vehicleDamages);
                    var condition = ResolveCondition(vehicleDamages);

                    return new VehicleVariantRow(
                        string.Empty,
                        vehicle.Id,
                        vehicle.LicensePlate,
                        vehicle.Mileage,
                        condition,
                        availability?.IsAvailableNow ?? ResolveVehicleAvailabilityFallback(vehicle),
                        availability?.AvailableFrom ?? DateTime.Today,
                        availability?.AvailabilityLabel ?? "Доступне на обраний період",
                        vehicle.DailyRate);
                })
                .OrderByDescending(item => item.IsAvailableNow)
                .ThenBy(item => item.Mileage)
                .ToList();
            variantRows = variantRows
                .Select((item, variantIndex) => item with { VariantName = $"Варіант {variantIndex + 1}" })
                .ToList();

            var imageSource = ResolveVehicleImageSource(groupVehicles);
            if (string.IsNullOrWhiteSpace(imageSource))
            {
                continue;
            }

            _vehicleVariantsByCardId[nextCardId] = variantRows;

            var fleetCount = variantRows.Count;
            var fleetAvailableCount = variantRows.Count(item => item.IsAvailableNow);
            var nearestAvailableFrom = variantRows.Count == 0
                ? DateTime.Today
                : variantRows.Min(item => item.AvailableFrom);
            var unavailableSummaryText = fleetAvailableCount <= 0
                ? variantRows
                    .Select(item => item.AvailabilityDisplay)
                    .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text)) ?? $"Зайнято до {FormatAvailabilityMoment(nearestAvailableFrom)}"
                : string.Empty;
            var tariffBand = BuildTariffBand(variantRows);

            var displayName = ResolveCardDisplayName(representativeVehicle, seed);
            var engineDisplay = ResolveGroupTextValue(groupVehicles.Select(vehicle => vehicle.EngineDisplay), seed?.EngineDisplay);
            var fuelType = ResolveGroupTextValue(groupVehicles.Select(vehicle => vehicle.FuelType), seed?.FuelType);
            var transmissionType = ResolveGroupTextValue(groupVehicles.Select(vehicle => vehicle.TransmissionType), seed?.TransmissionType);
            var cargoCapacityDisplay = ResolveGroupTextValue(groupVehicles.Select(vehicle => vehicle.CargoCapacityDisplay), seed?.CargoCapacityDisplay);
            var consumptionDisplay = ResolveGroupTextValue(groupVehicles.Select(vehicle => vehicle.ConsumptionDisplay), seed?.ConsumptionDisplay);
            var doorsCount = ResolveGroupNumericValue(groupVehicles.Select(vehicle => vehicle.DoorsCount), seed?.DoorsCount ?? 4);
            var carClass = ResolveVehicleClass(displayName, variantRows.Min(item => item.DailyRate));

            cards.Add(new ProkatCarCard(
                nextCardId,
                representativeVehicle.Id,
                displayName,
                BuildSpecificationDisplay(engineDisplay, fuelType, transmissionType),
                imageSource,
                carClass,
                fuelType,
                transmissionType,
                FormatDoorsDisplay(doorsCount),
                cargoCapacityDisplay,
                consumptionDisplay,
                tariffBand.PriceFor26PlusDays,
                tariffBand.PriceFor10To25Days,
                tariffBand.PriceFor4To9Days,
                tariffBand.PriceFor1To3Days,
                tariffBand.PriceFor26PlusDaysMax,
                tariffBand.PriceFor10To25DaysMax,
                tariffBand.PriceFor4To9DaysMax,
                tariffBand.PriceFor1To3DaysMax,
                fleetAvailableCount > 0,
                nearestAvailableFrom,
                seed?.PopularityRank ?? nextCardId,
                seed?.PrimaryBadgeText ?? string.Empty,
                seed?.PrimaryBadgeColor ?? "#7E22CE",
                seed?.SecondaryBadgeText ?? string.Empty,
                seed?.SecondaryBadgeColor ?? "#FB923C",
                unavailableSummaryText,
                fleetCount,
                fleetAvailableCount));
            nextCardId++;
        }

        return cards;
    }

    private static string BuildCatalogGroupKey(Vehicle vehicle)
    {
        var seed = ResolveCatalogSeed(vehicle);
        if (seed is not null)
        {
            return seed.FullName;
        }

        return $"{NormalizeAlphaNumeric(vehicle.Make)}|{NormalizeAlphaNumeric(vehicle.Model)}";
    }

    private static VehicleCatalogSeeds.CatalogVehicleSeed? ResolveCatalogSeed(Vehicle vehicle)
        => VehicleCatalogSeeds.TryFindByVehicle(vehicle.Make, vehicle.Model);

    private static Vehicle ResolveRepresentativeVehicle(
        IReadOnlyList<Vehicle> vehicles,
        IReadOnlyDictionary<int, VehicleAvailability> availabilityByVehicleId)
    {
        return vehicles
            .OrderByDescending(vehicle => IsVehicleAvailableNow(vehicle, availabilityByVehicleId))
            .ThenBy(vehicle => vehicle.Mileage)
            .First();
    }

    private static bool IsVehicleAvailableNow(
        Vehicle vehicle,
        IReadOnlyDictionary<int, VehicleAvailability> availabilityByVehicleId)
    {
        return availabilityByVehicleId.TryGetValue(vehicle.Id, out var availability)
            ? availability.IsAvailableNow
            : ResolveVehicleAvailabilityFallback(vehicle);
    }

    private static bool ResolveVehicleAvailabilityFallback(Vehicle vehicle)
        => !vehicle.IsDeleted && vehicle.IsBookable;

    private static string ResolveCardDisplayName(Vehicle vehicle, VehicleCatalogSeeds.CatalogVehicleSeed? seed)
    {
        if (seed is not null)
        {
            return seed.FullName;
        }

        return $"{vehicle.Make} {vehicle.Model}".Trim();
    }

    private static string ResolveGroupTextValue(IEnumerable<string> values, string? fallback = null)
    {
        var resolved = values
            .Select(value => value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value!, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(resolved))
        {
            return resolved;
        }

        return fallback?.Trim() ?? string.Empty;
    }

    private static int ResolveGroupNumericValue(IEnumerable<int> values, int fallback)
    {
        var resolved = values
            .Where(value => value > 0)
            .GroupBy(value => value)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => group.Key)
            .FirstOrDefault();

        return resolved > 0 ? resolved : fallback;
    }

    private static string BuildSpecificationDisplay(string engineDisplay, string fuelType, string transmissionType)
    {
        var parts = new[] { engineDisplay, fuelType, transmissionType }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return parts.Length == 0
            ? "Характеристики уточнюються"
            : string.Join(" | ", parts);
    }

    private static string FormatDoorsDisplay(int doorsCount)
    {
        if (doorsCount <= 0)
        {
            return "—";
        }

        var mod10 = doorsCount % 10;
        var mod100 = doorsCount % 100;
        var useFewForm = mod10 is >= 2 and <= 4 && (mod100 < 12 || mod100 > 14);
        return useFewForm
            ? $"{doorsCount} двері"
            : $"{doorsCount} дверей";
    }

    private static string ResolveVehicleImageSource(IEnumerable<Vehicle> vehicles)
    {
        foreach (var vehicle in vehicles)
        {
            if (VehiclePhotoCatalog.TryResolveStoredPhotoPath(vehicle.PhotoPath, out var fullPath) &&
                !string.IsNullOrWhiteSpace(fullPath))
            {
                return fullPath;
            }
        }

        return string.Empty;
    }

    private static string NormalizeAlphaNumeric(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Concat(value
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit));
    }

    private static string ResolveClientPhone(string login, string? currentPhone = null)
    {
        var normalizedCurrent = TryNormalizePhone(currentPhone);
        if (normalizedCurrent is not null)
        {
            return normalizedCurrent;
        }

        var normalizedLogin = TryNormalizePhone(login);
        if (normalizedLogin is not null)
        {
            return normalizedLogin;
        }

        return "Не вказано";
    }

    private static string? TryNormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length is < 10 or > 15)
        {
            return null;
        }

        return "+" + digits;
    }

    private static TariffBand BuildTariffBand(IReadOnlyList<VehicleVariantRow> variantRows)
    {
        if (variantRows.Count == 0)
        {
            return new TariffBand(0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m);
        }

        var minDailyRate = variantRows.Min(item => item.DailyRate);
        var maxDailyRate = variantRows.Max(item => item.DailyRate);
        if (minDailyRate <= 0m || maxDailyRate <= 0m)
        {
            return new TariffBand(0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m);
        }

        return new TariffBand(
            CalculateTierRate(minDailyRate, 0.86m),
            CalculateTierRate(minDailyRate, 0.92m),
            CalculateTierRate(minDailyRate, 1.00m),
            CalculateTierRate(minDailyRate, 1.10m),
            CalculateTierRate(maxDailyRate, 0.86m),
            CalculateTierRate(maxDailyRate, 0.92m),
            CalculateTierRate(maxDailyRate, 1.00m),
            CalculateTierRate(maxDailyRate, 1.10m));
    }

    private static decimal CalculateTierRate(decimal dailyRate, decimal factor)
    {
        return decimal.Round(dailyRate * factor, 0, MidpointRounding.AwayFromZero);
    }

    private static string ResolveCondition(IReadOnlyList<Damage>? damages)
    {
        if (damages is null || damages.Count == 0)
        {
            return "Ідеальний";
        }

        var latestOpen = damages
            .OrderByDescending(item => item.DateReported)
            .FirstOrDefault(item => item.Status != DamageStatus.Resolved);
        if (latestOpen is not null)
        {
            var text = latestOpen.Description.Trim();
            if (text.Length <= 40)
            {
                return text;
            }

            return text[..37] + "...";
        }

        return "Після ремонту";
    }

    private sealed record VehicleAvailability(
        int VehicleId,
        bool IsAvailableNow,
        DateTime AvailableFrom,
        string AvailabilityLabel);

    public void ReleaseTransientState()
    {
        IsVehicleDetailsDialogOpen = false;
        IsCheckoutDialogOpen = false;
        DetailsVehicle = null;
        SelectedVehicleVariant = null;
        VehicleVariants.Clear();
        StatusMessage = string.Empty;
        ResetCardPaymentInputs();
    }

    private sealed record TariffBand(
        decimal PriceFor26PlusDays,
        decimal PriceFor10To25Days,
        decimal PriceFor4To9Days,
        decimal PriceFor1To3Days,
        decimal PriceFor26PlusDaysMax,
        decimal PriceFor10To25DaysMax,
        decimal PriceFor4To9DaysMax,
        decimal PriceFor1To3DaysMax);

    public sealed record ProkatCarCard(
        int Id,
        int? BackingVehicleId,
        string Car,
        string EngineFuelTransmission,
        string ImageSource,
        string CarClass,
        string FuelType,
        string TransmissionType,
        string DoorsDisplay,
        string TrunkDisplay,
        string ConsumptionDisplay,
        decimal PriceFor26PlusDays,
        decimal PriceFor10To25Days,
        decimal PriceFor4To9Days,
        decimal PriceFor1To3Days,
        decimal PriceFor26PlusDaysMax,
        decimal PriceFor10To25DaysMax,
        decimal PriceFor4To9DaysMax,
        decimal PriceFor1To3DaysMax,
        bool IsAvailableNow,
        DateTime AvailableFrom,
        int PopularityRank,
        string PrimaryBadgeText,
        string PrimaryBadgeColor,
        string SecondaryBadgeText,
        string SecondaryBadgeColor,
        string UnavailableSummaryText,
        int FleetCount,
        int FleetAvailableCount)
    {
        public bool HasPrimaryBadge => !string.IsNullOrWhiteSpace(PrimaryBadgeText);

        public bool HasSecondaryBadge => !string.IsNullOrWhiteSpace(SecondaryBadgeText);

        public string PriceFor26PlusDaysDisplay => FormatPriceRange(PriceFor26PlusDays, PriceFor26PlusDaysMax);

        public string PriceFor10To25DaysDisplay => FormatPriceRange(PriceFor10To25Days, PriceFor10To25DaysMax);

        public string PriceFor4To9DaysDisplay => FormatPriceRange(PriceFor4To9Days, PriceFor4To9DaysMax);

        public string PriceFor1To3DaysDisplay => FormatPriceRange(PriceFor1To3Days, PriceFor1To3DaysMax);

        public bool HasMultipleFleetVariants => FleetCount > 1;

        public bool CanOpenDetails => FleetCount > 0;

        public string FleetCountBadgeText => $"В автопарку: {FleetCount} шт.";

        public string ActionButtonText => FleetCount switch
        {
            <= 0 => "Немає в автопарку",
            _ when FleetAvailableCount <= 0 => string.IsNullOrWhiteSpace(UnavailableSummaryText)
                ? $"Зайнято до {FormatAvailabilityMoment(AvailableFrom)}"
                : UnavailableSummaryText,
            1 => "Перейти до оформлення",
            _ => "Вибрати варіант"
        };

        private static string FormatPriceRange(decimal minPrice, decimal maxPrice)
        {
            var low = decimal.Round(Math.Min(minPrice, maxPrice), 0, MidpointRounding.AwayFromZero);
            var high = decimal.Round(Math.Max(minPrice, maxPrice), 0, MidpointRounding.AwayFromZero);
            return high > low
                ? $"{low:N0}-{high:N0} грн"
                : $"{low:N0} грн";
        }
    }

    public sealed record VehicleVariantRow(
        string VariantName,
        int VehicleId,
        string LicensePlate,
        int Mileage,
        string Condition,
        bool IsAvailableNow,
        DateTime AvailableFrom,
        string AvailabilityDisplay,
        decimal DailyRate)
    {
        public string MileageDisplay => $"{Mileage:N0} км";

        public string SelectActionText => IsAvailableNow ? "Обрати" : "Недоступно";

        public bool CanSelect => IsAvailableNow;
    }

    public sealed class FilterOption(
        string key,
        string label,
        string icon,
        Action onSelectionChanged,
        bool isSelected = false) : ObservableObject
    {
        private bool _isSelected = isSelected;

        public string Key { get; } = key;

        public string Label { get; } = label;

        public string Icon { get; } = icon;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    onSelectionChanged();
                }
            }
        }
    }
}



