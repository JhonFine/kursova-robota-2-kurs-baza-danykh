using CarRental.Desktop.Data;
using CarRental.Desktop.Localization;
using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Auth;
using CarRental.Desktop.Services.Documents;
using CarRental.Desktop.Services.Payments;
using CarRental.Desktop.Services.Rentals;
using CarRental.Shared.ReferenceData;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;

namespace CarRental.Desktop.ViewModels;

// Операційний workspace для менеджера оренд:
// список договорів, платежі та точкові дії живуть на одній сторінці, а створення винесене в overlay-діалог.
public sealed class RentalsPageViewModel : PageDataViewModelBase, ITransientStateOwner
{
    private readonly RentalDbContext _dbContext;
    private readonly IRentalService _rentalService;
    private readonly IDocumentGenerator _documentGenerator;
    private readonly IPrintService _printService;
    private readonly IPaymentService _paymentService;
    private readonly IAuthorizationService _authorizationService;
    private readonly PageRefreshCoordinator _refreshCoordinator;
    private readonly Employee _currentEmployee;
    private readonly SemaphoreSlim _dbContextOperationGate = new(1, 1);
    private const int ClientSearchLimit = 40;
    private const int VehicleSearchLimit = 40;
    private const string VehicleAvailableForPeriodLabel = "доступне на обрані дати";
    private const string VehicleConflictLabel = "конфлікт на обрані дати";
    private const string VehicleUnavailableLabel = "тимчасово недоступне";
    private const string VehiclePeriodRequiredLabel = "перевірте період";
    private const string MissingDriverLicenseLabel = "без посвідчення";
    private const string CreateRentalSubmitText = "Створити оренду";
    private const string CreateRentalSubmitBusyText = "Створення...";

    private bool _isLoading;
    private bool _isCreateRentalDialogOpen;
    private bool _isCreateRentalBusy;
    private bool _isSynchronizingCreateDraft;
    private DateTime _actualReturnDate = DateTime.Today;
    private string _endMileageInput = string.Empty;
    private string _statusMessage = string.Empty;
    private RentalRow? _selectedRental;
    private string _cancelReason = DemoSeedReferenceData.StaffCancellationReason;
    private string _paymentAmountInput = string.Empty;
    private PaymentMethod _paymentMethod = PaymentMethod.Cash;
    private PaymentDirection _paymentDirection = PaymentDirection.Incoming;
    private int _guideRequestId;
    private int? _preferredSelectedRentalId;
    private Task _clientSearchTask = Task.CompletedTask;
    private Task _vehicleSearchTask = Task.CompletedTask;
    private Task _paymentsLoadTask = Task.CompletedTask;
    private CancellationTokenSource? _clientSearchCancellationTokenSource;
    private CancellationTokenSource? _vehicleSearchCancellationTokenSource;
    private CancellationTokenSource? _paymentsLoadCancellationTokenSource;
    private bool _suppressSelectedRentalPaymentsLoad;

    public RentalsPageViewModel(
        RentalDbContext dbContext,
        IRentalService rentalService,
        IDocumentGenerator documentGenerator,
        IPrintService printService,
        IPaymentService paymentService,
        IAuthorizationService authorizationService,
        PageRefreshCoordinator refreshCoordinator,
        Employee currentEmployee)
    {
        _dbContext = dbContext;
        _rentalService = rentalService;
        _documentGenerator = documentGenerator;
        _printService = printService;
        _paymentService = paymentService;
        _authorizationService = authorizationService;
        _refreshCoordinator = refreshCoordinator;
        _currentEmployee = currentEmployee;

        Locations = [.. DemoSeedReferenceData.SupportedLocations];
        CreateDraft = new RentalCreateDraft(DefaultLocation);
        CreateDraft.PropertyChanged += CreateDraft_OnPropertyChanged;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsLoading);
        OpenCreateRentalDialogCommand = new AsyncRelayCommand(OpenCreateRentalDialogAsync, CanOpenCreateRentalDialog);
        CloseCreateRentalDialogCommand = new RelayCommand(CloseCreateRentalDialog);
        SubmitCreateRentalCommand = new AsyncRelayCommand(SubmitCreateRentalAsync, CanSubmitCreateRental);
        CloseRentalCommand = new AsyncRelayCommand(CloseRentalAsync, () => !IsLoading);
        CancelRentalCommand = new AsyncRelayCommand(CancelRentalAsync, () => !IsLoading);
        AddPaymentCommand = new AsyncRelayCommand(AddPaymentAsync, () => !IsLoading);
        RequestGuideCommand = new RelayCommand(RequestGuide);
    }

    public ObservableCollection<ClientOption> Clients { get; } = [];

    public ObservableCollection<VehicleOption> Vehicles { get; } = [];

    public ObservableCollection<RentalRow> Rentals { get; } = [];

    public ObservableCollection<PaymentRow> Payments { get; } = [];

    public ObservableCollection<GanttRow> GanttRows { get; } = [];

    public ObservableCollection<string> Locations { get; }

    public ObservableCollection<string> TimeOptions { get; } = [.. DemoSeedReferenceData.TimeOptions];

    public RentalCreateDraft CreateDraft { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand OpenCreateRentalDialogCommand { get; }

    public IRelayCommand CloseCreateRentalDialogCommand { get; }

    public IAsyncRelayCommand SubmitCreateRentalCommand { get; }

    public IAsyncRelayCommand CloseRentalCommand { get; }

    public IAsyncRelayCommand CancelRentalCommand { get; }

    public IAsyncRelayCommand AddPaymentCommand { get; }

    public IRelayCommand RequestGuideCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                OpenCreateRentalDialogCommand.NotifyCanExecuteChanged();
                SubmitCreateRentalCommand.NotifyCanExecuteChanged();
                CloseRentalCommand.NotifyCanExecuteChanged();
                CancelRentalCommand.NotifyCanExecuteChanged();
                AddPaymentCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsCreateRentalDialogOpen
    {
        get => _isCreateRentalDialogOpen;
        private set => SetProperty(ref _isCreateRentalDialogOpen, value);
    }

    public bool IsCreateRentalBusy
    {
        get => _isCreateRentalBusy;
        private set
        {
            if (SetProperty(ref _isCreateRentalBusy, value))
            {
                OpenCreateRentalDialogCommand.NotifyCanExecuteChanged();
                SubmitCreateRentalCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(IsCreateRentalInteractive));
                OnPropertyChanged(nameof(CreateRentalSubmitButtonText));
            }
        }
    }

    public bool IsCreateRentalInteractive => !IsCreateRentalBusy;

    public string CreateRentalSubmitButtonText => IsCreateRentalBusy
        ? CreateRentalSubmitBusyText
        : CreateRentalSubmitText;

    public DateTime MinCreateStartDate => DateTime.Today;

    public DateTime MinCreateEndDate => CreateDraft.StartDate.Date > DateTime.Today
        ? CreateDraft.StartDate.Date
        : DateTime.Today;

    public string CreateStartDateValidationMessage => ResolveCreateStartDateValidationMessage() ?? string.Empty;

    public bool HasCreateStartDateValidationMessage => !string.IsNullOrWhiteSpace(CreateStartDateValidationMessage);

    public string CreateEndDateValidationMessage => ResolveCreateEndDateValidationMessage() ?? string.Empty;

    public bool HasCreateEndDateValidationMessage => !string.IsNullOrWhiteSpace(CreateEndDateValidationMessage);

    public string CreateClientValidationMessage => ResolveCreateClientValidationMessage() ?? string.Empty;

    public bool HasCreateClientValidationMessage => !string.IsNullOrWhiteSpace(CreateClientValidationMessage);

    public string CreateVehicleValidationMessage => ResolveCreateVehicleValidationMessage() ?? string.Empty;

    public bool HasCreateVehicleValidationMessage => !string.IsNullOrWhiteSpace(CreateVehicleValidationMessage);

    public string CreatePaymentValidationMessage => ResolveCreatePaymentValidationMessage(suppressEmptyAmount: true) ?? string.Empty;

    public bool HasCreatePaymentValidationMessage => !string.IsNullOrWhiteSpace(CreatePaymentValidationMessage);

    public string CreateRentalEstimatedAmountSummary => BuildCreateRentalEstimatedAmountSummary();

    public DateTime ActualReturnDate
    {
        get => _actualReturnDate;
        set => SetProperty(ref _actualReturnDate, value);
    }

    public string EndMileageInput
    {
        get => _endMileageInput;
        set => SetProperty(ref _endMileageInput, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public RentalRow? SelectedRental
    {
        get => _selectedRental;
        set
        {
            if (SetProperty(ref _selectedRental, value))
            {
                OnPropertyChanged(nameof(HasSelectedRental));
                OnPropertyChanged(nameof(HasNoSelectedRental));
                QueuePaymentsLoad();
            }
        }
    }

    public bool HasSelectedRental => SelectedRental is not null;

    public bool HasNoSelectedRental => SelectedRental is null;

    public string CancelReason
    {
        get => _cancelReason;
        set => SetProperty(ref _cancelReason, value);
    }

    public string PaymentAmountInput
    {
        get => _paymentAmountInput;
        set => SetProperty(ref _paymentAmountInput, value);
    }

    public PaymentMethod PaymentMethod
    {
        get => _paymentMethod;
        set => SetProperty(ref _paymentMethod, value);
    }

    public PaymentDirection PaymentDirection
    {
        get => _paymentDirection;
        set => SetProperty(ref _paymentDirection, value);
    }

    public int GuideRequestId
    {
        get => _guideRequestId;
        private set => SetProperty(ref _guideRequestId, value);
    }

    public bool CanManagePayments
        => _authorizationService.HasPermission(_currentEmployee, EmployeePermission.ManagePayments);

    public bool CanManageRentals
        => _authorizationService.HasPermission(_currentEmployee, EmployeePermission.ManageRentals);

    public override async Task RefreshAsync()
    {
        if (IsLoading)
        {
            return;
        }

        await AwaitPendingCreateDialogSearchesAsync();
        IsLoading = true;
        try
        {
            var snapshot = await WithDbContextGateAsync(async cancellationToken =>
            {
                var clients = _dbContext.Clients
                    .AsNoTracking()
                    .IgnoreQueryFilters();
                var vehicles = _dbContext.Vehicles
                    .AsNoTracking()
                    .IgnoreQueryFilters();

                var vehiclesForGantt = await _dbContext.Vehicles
                    .AsNoTracking()
                    .Include(vehicle => vehicle.MakeLookup)
                    .Include(vehicle => vehicle.ModelLookup)
                    .OrderBy(vehicle => vehicle.MakeLookup!.Name)
                    .ThenBy(vehicle => vehicle.ModelLookup!.Name)
                    .ToListAsync(cancellationToken);

                var rentals = await _dbContext.Rentals
                    .AsNoTracking()
                    .OrderByDescending(rental => rental.StartDate)
                    .Take(500)
                    .Select(rental => new RentalListSnapshot(
                        rental.Id,
                        rental.ContractNumber,
                        clients
                            .Where(client => client.Id == rental.ClientId)
                            .Select(client => client.FullName)
                            .FirstOrDefault() ?? string.Empty,
                        rental.VehicleId,
                        vehicles
                            .Where(vehicle => vehicle.Id == rental.VehicleId)
                            .Select(vehicle => vehicle.MakeLookup!.Name + " " + vehicle.ModelLookup!.Name)
                            .FirstOrDefault() ?? string.Empty,
                        rental.StartDate,
                        rental.EndDate,
                        rental.PickupLocation,
                        rental.ReturnLocation,
                        rental.TotalAmount,
                        rental.Payments.Sum(payment => (decimal?)(
                            payment.DirectionId == PaymentDirection.Incoming
                                ? payment.Amount
                                : payment.DirectionId == PaymentDirection.Refund
                                    ? -payment.Amount
                                    : 0m)) ?? 0m,
                        rental.StatusId))
                    .ToListAsync(cancellationToken);

                return (vehiclesForGantt, rentals);
            });

            await ReloadClientsAsync(CreateDraft.SelectedClient?.Id);
            await ReloadVehiclesAsync(CreateDraft.SelectedVehicle?.Id);

            var selectedRentalId = _preferredSelectedRentalId ?? SelectedRental?.Id;
            _preferredSelectedRentalId = null;
            Rentals.Clear();
            foreach (var rental in snapshot.rentals)
            {
                Rentals.Add(new RentalRow(
                    rental.Id,
                    rental.ContractNumber,
                    rental.ClientName,
                    rental.VehicleName,
                    rental.StartDate,
                    rental.EndDate,
                    rental.PickupLocation,
                    rental.ReturnLocation,
                    rental.TotalAmount,
                    rental.PaidAmount,
                    rental.TotalAmount - rental.PaidAmount,
                    rental.StatusId));
            }

            var rentalsByVehicleId = snapshot.rentals
                .GroupBy(item => item.VehicleId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<RentalListSnapshot>)group
                        .OrderBy(item => item.StartDate)
                        .ToList());

            BuildGantt(snapshot.vehiclesForGantt, rentalsByVehicleId);

            _suppressSelectedRentalPaymentsLoad = true;
            SelectedRental = selectedRentalId.HasValue
                ? Rentals.FirstOrDefault(item => item.Id == selectedRentalId.Value)
                : Rentals.FirstOrDefault();
            _suppressSelectedRentalPaymentsLoad = false;

            await LoadPaymentsAsync(SelectedRental?.Id);
            MarkDataLoaded();
        }
        finally
        {
            _suppressSelectedRentalPaymentsLoad = false;
            IsLoading = false;
        }
    }

    public async Task PrepareForClientAsync(int preferredClientId)
    {
        if (preferredClientId <= 0)
        {
            await EnsureDataAsync();
            return;
        }

        await AwaitPendingCreateDialogSearchesAsync();
        ResetCreateDraft();
        await EnsureDataAsync();
        await ReloadClientsAsync(preferredClientId);
        await ReloadVehiclesAsync(CreateDraft.SelectedVehicle?.Id);

        if (CreateDraft.SelectedClient is not null)
        {
            IsCreateRentalDialogOpen = true;
            StatusMessage = $"Клієнта {CreateDraft.SelectedClient.Display} підготовлено для оформлення оренди.";
        }
    }

    public void CloseCreateRentalDialog()
    {
        CancelPendingCreateDialogSearches();
        IsCreateRentalDialogOpen = false;
        CreateDraft.FormMessage = string.Empty;
    }

    private async Task OpenCreateRentalDialogAsync()
    {
        if (!CanManageRentals)
        {
            return;
        }

        await AwaitPendingCreateDialogSearchesAsync();
        ResetCreateDraft();
        await EnsureDataAsync();
        await ReloadClientsAsync(CreateDraft.SelectedClient?.Id);
        await ReloadVehiclesAsync(CreateDraft.SelectedVehicle?.Id);
        IsCreateRentalDialogOpen = true;
    }

    private bool CanOpenCreateRentalDialog()
    {
        return !IsLoading && !IsCreateRentalBusy && CanManageRentals;
    }

    private async Task SubmitCreateRentalAsync()
    {
        CreateDraft.FormMessage = string.Empty;

        if (!CanManageRentals)
        {
            CreateDraft.FormMessage = "Недостатньо прав для оформлення оренди.";
            return;
        }

        await AwaitPendingCreateDialogSearchesAsync();

        var validationError = ResolveCreateDialogValidationMessage();
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            CreateDraft.FormMessage = validationError;
            return;
        }

        IsCreateRentalBusy = true;
        try
        {
            var startDateTime = BuildCreateStartDateTime();
            var endDateTime = BuildCreateEndDateTime();
            var returnLocation = string.IsNullOrWhiteSpace(CreateDraft.ReturnLocation)
                ? CreateDraft.PickupLocation.Trim()
                : CreateDraft.ReturnLocation.Trim();
            var initialPaymentAmount = ParseCreateInitialPaymentAmount();

            var autoPrintContract = CreateDraft.AutoPrintContract;
            CreateRentalResult result;
            if (CreateDraft.CreateInitialPayment)
            {
                result = await WithDbContextGateAsync(
                    cancellationToken => _rentalService.CreateRentalWithPaymentAsync(
                        new CreateRentalWithPaymentRequest(
                            CreateDraft.SelectedClient!.Id,
                            CreateDraft.SelectedVehicle!.Id,
                            _currentEmployee.Id,
                            startDateTime,
                            endDateTime,
                            CreateDraft.PickupLocation.Trim(),
                            returnLocation,
                            initialPaymentAmount,
                            CreateDraft.PaymentMethod,
                            CreateDraft.PaymentDirection,
                            CreateDraft.PaymentNotes.Trim()),
                        cancellationToken));
            }
            else
            {
                result = await WithDbContextGateAsync(
                    cancellationToken => _rentalService.CreateRentalAsync(
                        new CreateRentalRequest(
                            CreateDraft.SelectedClient!.Id,
                            CreateDraft.SelectedVehicle!.Id,
                            _currentEmployee.Id,
                            startDateTime,
                            endDateTime,
                            CreateDraft.PickupLocation.Trim(),
                            returnLocation),
                        cancellationToken));
            }

            if (!result.Success)
            {
                CreateDraft.FormMessage = result.Message;
                return;
            }

            var successMessage = await BuildCreateSuccessMessageAsync(result, autoPrintContract);
            ResetCreateDraft();
            CloseCreateRentalDialog();
            EndMileageInput = string.Empty;
            _preferredSelectedRentalId = result.RentalId;
            _refreshCoordinator.Invalidate(PageRefreshArea.Fleet | PageRefreshArea.Prokat | PageRefreshArea.Reports | PageRefreshArea.UserRentals);
            await RefreshAsync();
            StatusMessage = successMessage;
        }
        finally
        {
            IsCreateRentalBusy = false;
        }
    }

    private bool CanSubmitCreateRental()
    {
        return !IsLoading &&
               !IsCreateRentalBusy &&
               CanManageRentals &&
               string.IsNullOrWhiteSpace(ResolveCreateStartDateValidationMessage()) &&
               string.IsNullOrWhiteSpace(ResolveCreateEndDateValidationMessage());
    }

    private string? ResolveCreateDialogValidationMessage()
    {
        if (CreateDraft.SelectedClient is null)
        {
            return "Оберіть клієнта.";
        }

        var clientValidationError = ResolveCreateClientValidationMessage();
        if (!string.IsNullOrWhiteSpace(clientValidationError))
        {
            return clientValidationError;
        }

        if (CreateDraft.SelectedVehicle is null)
        {
            return "Оберіть авто.";
        }

        var vehicleValidationError = ResolveCreateVehicleValidationMessage();
        if (!string.IsNullOrWhiteSpace(vehicleValidationError))
        {
            return vehicleValidationError;
        }

        if (string.IsNullOrWhiteSpace(CreateDraft.PickupLocation))
        {
            return "Вкажіть локацію отримання.";
        }

        if (string.IsNullOrWhiteSpace(CreateDraft.ReturnLocation))
        {
            return "Вкажіть локацію повернення.";
        }

        var startDateValidationError = ResolveCreateStartDateValidationMessage();
        if (!string.IsNullOrWhiteSpace(startDateValidationError))
        {
            return startDateValidationError;
        }

        var endDateValidationError = ResolveCreateEndDateValidationMessage();
        if (!string.IsNullOrWhiteSpace(endDateValidationError))
        {
            return endDateValidationError;
        }

        var paymentValidationError = ResolveCreatePaymentValidationMessage(suppressEmptyAmount: false);
        if (!string.IsNullOrWhiteSpace(paymentValidationError))
        {
            return paymentValidationError;
        }

        return null;
    }

    private string? ResolveCreateClientValidationMessage()
    {
        if (CreateDraft.SelectedClient is null)
        {
            return null;
        }

        if (CreateDraft.SelectedClient.IsBlacklisted)
        {
            return string.IsNullOrWhiteSpace(CreateDraft.SelectedClient.BlacklistReason)
                ? "Клієнт у чорному списку."
                : $"Клієнт у чорному списку: {CreateDraft.SelectedClient.BlacklistReason}.";
        }

        if (string.IsNullOrWhiteSpace(CreateDraft.SelectedClient.DriverLicense))
        {
            return "У клієнта не вказано посвідчення водія.";
        }

        if (!CreateDraft.SelectedClient.DriverLicenseExpirationDate.HasValue)
        {
            return "У клієнта не вказано строк дії посвідчення водія.";
        }

        if (CreateDraft.SelectedClient.DriverLicenseExpirationDate.Value.Date < BuildCreateStartDateTime().Date)
        {
            return $"Посвідчення водія прострочене на дату початку оренди (до {CreateDraft.SelectedClient.DriverLicenseExpirationDate.Value:dd.MM.yyyy}).";
        }

        return null;
    }

    private string? ResolveCreateVehicleValidationMessage()
    {
        if (CreateDraft.SelectedVehicle is null || CreateDraft.SelectedVehicle.IsAvailableForSelectedPeriod)
        {
            return null;
        }

        return CreateDraft.SelectedVehicle.AvailabilityMessage;
    }

    private string? ResolveCreatePaymentValidationMessage(bool suppressEmptyAmount)
    {
        if (!CreateDraft.CreateInitialPayment)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(CreateDraft.InitialPaymentAmountInput))
        {
            return suppressEmptyAmount
                ? null
                : "Вкажіть суму початкового платежу.";
        }

        if (!TryParseCreateInitialPaymentAmount(out var amount))
        {
            return "Вкажіть коректну суму початкового платежу.";
        }

        if (amount <= 0m)
        {
            return "Сума початкового платежу має бути більшою за нуль.";
        }

        var estimatedTotalAmount = CalculateCreateRentalEstimatedTotalAmount();
        if (estimatedTotalAmount > 0m && amount > estimatedTotalAmount)
        {
            return $"Початковий платіж не може перевищувати вартість оренди ({estimatedTotalAmount:N2} грн).";
        }

        return null;
    }

    private string? ResolveCreateStartDateValidationMessage()
    {
        return BuildCreateStartDateTime() < DateTime.Now
            ? "Початок оренди не може бути в минулому."
            : null;
    }

    private string? ResolveCreateEndDateValidationMessage()
    {
        if (CreateDraft.EndDate.Date < CreateDraft.StartDate.Date)
        {
            return "Дата повернення не може бути раніше дати подачі.";
        }

        return BuildCreateEndDateTime() <= BuildCreateStartDateTime()
            ? "Дата та час завершення мають бути пізнішими за початок оренди."
            : null;
    }

    private void NotifyCreateRentalValidationChanged()
    {
        OnPropertyChanged(nameof(MinCreateEndDate));
        OnPropertyChanged(nameof(CreateStartDateValidationMessage));
        OnPropertyChanged(nameof(HasCreateStartDateValidationMessage));
        OnPropertyChanged(nameof(CreateEndDateValidationMessage));
        OnPropertyChanged(nameof(HasCreateEndDateValidationMessage));
        OnPropertyChanged(nameof(CreateClientValidationMessage));
        OnPropertyChanged(nameof(HasCreateClientValidationMessage));
        OnPropertyChanged(nameof(CreateVehicleValidationMessage));
        OnPropertyChanged(nameof(HasCreateVehicleValidationMessage));
        OnPropertyChanged(nameof(CreatePaymentValidationMessage));
        OnPropertyChanged(nameof(HasCreatePaymentValidationMessage));
        OnPropertyChanged(nameof(CreateRentalEstimatedAmountSummary));
        SubmitCreateRentalCommand.NotifyCanExecuteChanged();
    }

    private DateTime BuildCreateStartDateTime()
    {
        return CombineDateAndTime(CreateDraft.StartDate, CreateDraft.StartTime, new TimeSpan(10, 0, 0));
    }

    private DateTime BuildCreateEndDateTime()
    {
        var fallback = ParseTime(CreateDraft.StartTime, new TimeSpan(10, 0, 0));
        return CombineDateAndTime(CreateDraft.EndDate, CreateDraft.EndTime, fallback);
    }

    private decimal CalculateCreateRentalEstimatedTotalAmount()
    {
        if (CreateDraft.SelectedVehicle is null)
        {
            return 0m;
        }

        var rentalHours = (decimal)(BuildCreateEndDateTime() - BuildCreateStartDateTime()).TotalHours;
        if (rentalHours <= 0m)
        {
            return 0m;
        }

        return decimal.Round(
            CreateDraft.SelectedVehicle.DailyRate * (rentalHours / 24m),
            2,
            MidpointRounding.AwayFromZero);
    }

    private string BuildCreateRentalEstimatedAmountSummary()
    {
        var estimatedTotalAmount = CalculateCreateRentalEstimatedTotalAmount();
        return estimatedTotalAmount <= 0m
            ? "Орієнтовна вартість з’явиться після вибору авто та коректного періоду."
            : $"Орієнтовна вартість оренди: {estimatedTotalAmount:N2} грн.";
    }

    private bool TryParseCreateInitialPaymentAmount(out decimal amount)
    {
        if (decimal.TryParse(CreateDraft.InitialPaymentAmountInput, NumberStyles.Number, CultureInfo.CurrentCulture, out amount))
        {
            return true;
        }

        return decimal.TryParse(CreateDraft.InitialPaymentAmountInput, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }

    private decimal ParseCreateInitialPaymentAmount()
    {
        return TryParseCreateInitialPaymentAmount(out var amount)
            ? decimal.Round(amount, 2, MidpointRounding.AwayFromZero)
            : 0m;
    }

    private async Task<string> BuildCreateSuccessMessageAsync(CreateRentalResult result, bool autoPrintContract)
    {
        var rental = await WithDbContextGateAsync(cancellationToken =>
            _dbContext.Rentals
                .AsNoTracking()
                .Include(item => item.Client)
                .Include(item => item.Vehicle)
                    .ThenInclude(item => item!.MakeLookup)
                .Include(item => item.Vehicle)
                    .ThenInclude(item => item!.ModelLookup)
                .FirstOrDefaultAsync(item => item.Id == result.RentalId, cancellationToken));

        if (rental is null || rental.Client is null || rental.Vehicle is null)
        {
            return result.Message;
        }

        try
        {
            var files = await _documentGenerator.GenerateRentalContractAsync(
                new ContractData(
                    rental.Id,
                    rental.ContractNumber,
                    rental.Client.FullName,
                    $"{rental.Vehicle.MakeName} {rental.Vehicle.ModelName} [{rental.Vehicle.LicensePlate}]",
                    rental.StartDate,
                    rental.EndDate,
                    rental.TotalAmount));

            var message = $"Оренду створено ({result.ContractNumber}). PDF: {Path.GetFileName(files.PdfPath)}.";
            if (autoPrintContract)
            {
                _printService.TryPrint(files.PdfPath, out var printMessage);
                message += $" Друк: {printMessage}";
            }

            return message;
        }
        catch (Exception exception)
        {
            return $"Оренду створено ({result.ContractNumber}), але не вдалося підготувати договір: {exception.Message}";
        }
    }

    private async Task CloseRentalAsync()
    {
        StatusMessage = string.Empty;

        if (!CanManageRentals)
        {
            StatusMessage = "Недостатньо прав для зміни статусу оренди.";
            return;
        }

        if (SelectedRental is null)
        {
            StatusMessage = "Оберіть оренду.";
            return;
        }

        if (SelectedRental.StatusId == RentalStatus.Closed)
        {
            StatusMessage = "Оренду вже закрито.";
            return;
        }

        if (SelectedRental.StatusId == RentalStatus.Canceled)
        {
            StatusMessage = "Оренду скасовано.";
            return;
        }

        if (!int.TryParse(EndMileageInput, out var endMileage) || endMileage <= 0)
        {
            StatusMessage = "Некоректний кінцевий пробіг.";
            return;
        }

        var result = await WithDbContextGateAsync(
            cancellationToken => _rentalService.CloseRentalAsync(
                new CloseRentalRequest(SelectedRental.Id, ActualReturnDate, endMileage),
                cancellationToken));
        StatusMessage = result.Success
            ? $"{result.Message} Підсумок: {result.TotalAmount:C}."
            : result.Message;

        if (result.Success)
        {
            _refreshCoordinator.Invalidate(PageRefreshArea.Fleet | PageRefreshArea.Prokat | PageRefreshArea.Reports);
        }

        await RefreshAsync();
    }

    private async Task CancelRentalAsync()
    {
        StatusMessage = string.Empty;

        if (!CanManageRentals)
        {
            StatusMessage = "Недостатньо прав для скасування оренди.";
            return;
        }

        if (SelectedRental is null)
        {
            StatusMessage = "Оберіть оренду.";
            return;
        }

        var result = await WithDbContextGateAsync(
            cancellationToken => _rentalService.CancelRentalAsync(
                new CancelRentalRequest(SelectedRental.Id, CancelReason),
                cancellationToken));
        StatusMessage = result.Message;
        if (result.Success)
        {
            _refreshCoordinator.Invalidate(PageRefreshArea.Fleet | PageRefreshArea.Prokat | PageRefreshArea.Reports);
        }

        await RefreshAsync();
    }

    private async Task AddPaymentAsync()
    {
        StatusMessage = string.Empty;

        if (!CanManagePayments)
        {
            StatusMessage = "Недостатньо прав.";
            return;
        }

        if (SelectedRental is null)
        {
            StatusMessage = "Оберіть оренду.";
            return;
        }

        if (!decimal.TryParse(PaymentAmountInput, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) &&
            !decimal.TryParse(PaymentAmountInput, NumberStyles.Number, CultureInfo.CurrentCulture, out amount))
        {
            StatusMessage = "Некоректна сума.";
            return;
        }

        var result = await WithDbContextGateAsync(
            cancellationToken => _paymentService.AddPaymentAsync(
                new PaymentRequest(
                    SelectedRental.Id,
                    _currentEmployee.Id,
                    amount,
                    PaymentMethod,
                    PaymentDirection,
                    string.Empty),
                cancellationToken));
        StatusMessage = result.Message;
        if (result.Success)
        {
            PaymentAmountInput = string.Empty;
            _refreshCoordinator.Invalidate(PageRefreshArea.Reports);
            await RefreshAsync();
        }
    }

    private void QueuePaymentsLoad()
    {
        if (_suppressSelectedRentalPaymentsLoad)
        {
            return;
        }

        if (IsLoading)
        {
            return;
        }

        _paymentsLoadCancellationTokenSource?.Cancel();
        _paymentsLoadCancellationTokenSource?.Dispose();
        _paymentsLoadCancellationTokenSource = null;

        if (SelectedRental is null)
        {
            Payments.Clear();
            return;
        }

        var cancellationTokenSource = new CancellationTokenSource();
        _paymentsLoadCancellationTokenSource = cancellationTokenSource;
        _paymentsLoadTask = LoadPaymentsAsync(SelectedRental.Id, cancellationTokenSource.Token);
    }

    private async Task LoadPaymentsAsync(int? rentalId, CancellationToken cancellationToken = default)
    {
        Payments.Clear();
        if (!rentalId.HasValue)
        {
            return;
        }

        try
        {
            var payments = await WithDbContextGateAsync(
                token => _paymentService.GetRentalPaymentsAsync(rentalId.Value, token),
                cancellationToken);
            if (cancellationToken.IsCancellationRequested || SelectedRental?.Id != rentalId.Value)
            {
                return;
            }

            foreach (var payment in payments)
            {
                Payments.Add(new PaymentRow(
                    payment.Id,
                    payment.CreatedAtUtc,
                    payment.MethodId,
                    payment.DirectionId,
                    payment.Amount,
                    payment.Notes));
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore outdated selection changes.
        }
        catch (Exception exception)
        {
            StatusMessage = $"Не вдалося завантажити платежі: {exception.Message}";
        }
    }

    private async Task QueueClientSearchAsync()
    {
        _clientSearchCancellationTokenSource?.Cancel();
        _clientSearchCancellationTokenSource?.Dispose();

        var cancellationTokenSource = new CancellationTokenSource();
        _clientSearchCancellationTokenSource = cancellationTokenSource;

        try
        {
            await Task.Delay(220, cancellationTokenSource.Token);
            if (!IsCreateRentalDialogOpen || IsCreateRentalBusy)
            {
                return;
            }

            await ReloadClientsAsync(CreateDraft.SelectedClient?.Id, cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            // Ignore outdated search requests.
        }
        catch (Exception exception)
        {
            if (!cancellationTokenSource.IsCancellationRequested)
            {
                CreateDraft.FormMessage = $"Не вдалося оновити список клієнтів: {exception.Message}";
            }
        }
    }

    private async Task QueueVehicleSearchAsync()
    {
        _vehicleSearchCancellationTokenSource?.Cancel();
        _vehicleSearchCancellationTokenSource?.Dispose();

        var cancellationTokenSource = new CancellationTokenSource();
        _vehicleSearchCancellationTokenSource = cancellationTokenSource;

        try
        {
            await Task.Delay(220, cancellationTokenSource.Token);
            if (!IsCreateRentalDialogOpen || IsCreateRentalBusy)
            {
                return;
            }

            await ReloadVehiclesAsync(CreateDraft.SelectedVehicle?.Id, cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            // Ignore outdated search requests.
        }
        catch (Exception exception)
        {
            if (!cancellationTokenSource.IsCancellationRequested)
            {
                CreateDraft.FormMessage = $"Не вдалося оновити список авто: {exception.Message}";
            }
        }
    }

    private async Task ReloadClientsAsync(int? preferredClientId, CancellationToken cancellationToken = default)
    {
        var searchText = CreateDraft.ClientSearchText.Trim();
        var clientData = await WithDbContextGateAsync(async token =>
        {
            var passportDocuments = _dbContext.ClientDocuments
                .AsNoTracking()
                .Where(item => item.DocumentTypeCode == ClientDocumentTypes.Passport);
            var driverLicenseDocuments = _dbContext.ClientDocuments
                .AsNoTracking()
                .Where(item => item.DocumentTypeCode == ClientDocumentTypes.DriverLicense);

            var query = _dbContext.Clients
                .AsNoTracking()
                .Select(client => new
                {
                    client.Id,
                    client.FullName,
                    client.Phone,
                    client.IsBlacklisted,
                    BlacklistReason = client.BlacklistReason ?? string.Empty,
                    PassportData = passportDocuments
                        .Where(document => document.ClientId == client.Id)
                        .Select(document => document.DocumentNumber)
                        .FirstOrDefault() ?? string.Empty,
                    DriverLicense = driverLicenseDocuments
                        .Where(document => document.ClientId == client.Id)
                        .Select(document => document.DocumentNumber)
                        .FirstOrDefault() ?? string.Empty,
                    DriverLicenseExpirationDate = driverLicenseDocuments
                        .Where(document => document.ClientId == client.Id)
                        .Select(document => document.ExpirationDate)
                        .FirstOrDefault()
                });

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                query = query.Where(client =>
                    client.FullName.Contains(searchText) ||
                    client.DriverLicense.Contains(searchText) ||
                    client.Phone.Contains(searchText) ||
                    client.PassportData.Contains(searchText));
            }

            var items = await query
                .OrderBy(client => client.FullName)
                .ThenBy(client => client.DriverLicense)
                .Take(ClientSearchLimit)
                .ToListAsync(token);

            if (preferredClientId.HasValue && items.All(item => item.Id != preferredClientId.Value))
            {
                var selectedClient = await _dbContext.Clients
                    .AsNoTracking()
                    .Select(client => new
                    {
                        client.Id,
                        client.FullName,
                        client.Phone,
                        client.IsBlacklisted,
                        BlacklistReason = client.BlacklistReason ?? string.Empty,
                        PassportData = passportDocuments
                            .Where(document => document.ClientId == client.Id)
                            .Select(document => document.DocumentNumber)
                            .FirstOrDefault() ?? string.Empty,
                        DriverLicense = driverLicenseDocuments
                            .Where(document => document.ClientId == client.Id)
                            .Select(document => document.DocumentNumber)
                            .FirstOrDefault() ?? string.Empty,
                        DriverLicenseExpirationDate = driverLicenseDocuments
                            .Where(document => document.ClientId == client.Id)
                            .Select(document => document.ExpirationDate)
                            .FirstOrDefault()
                    })
                    .Where(client => client.Id == preferredClientId.Value)
                    .FirstOrDefaultAsync(token);

                if (selectedClient is not null)
                {
                    items.Insert(0, selectedClient);
                }
            }

            return items;
        }, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        var clientOptions = clientData
            .Select(client => new ClientOption(
                client.Id,
                BuildClientOptionDisplay(client.FullName, client.DriverLicense),
                client.FullName,
                client.Phone,
                client.DriverLicense,
                client.DriverLicenseExpirationDate,
                client.IsBlacklisted,
                client.BlacklistReason))
            .ToList();

        Clients.Clear();
        foreach (var clientOption in clientOptions)
        {
            Clients.Add(clientOption);
        }

        if (preferredClientId.HasValue)
        {
            CreateDraft.SelectedClient = Clients.FirstOrDefault(item => item.Id == preferredClientId.Value);
        }
    }

    private async Task ReloadVehiclesAsync(int? preferredVehicleId, CancellationToken cancellationToken = default)
    {
        var searchText = CreateDraft.VehicleSearchText.Trim();
        var selectedPeriodStart = NormalizeBusinessTimestamp(BuildCreateStartDateTime());
        var selectedPeriodEnd = NormalizeBusinessTimestamp(BuildCreateEndDateTime());
        var hasValidSelectedPeriod = selectedPeriodEnd > selectedPeriodStart;

        var vehicleData = await WithDbContextGateAsync(async token =>
        {
            var conflictingVehicleIds = _dbContext.Rentals
                .AsNoTracking()
                .Where(rental =>
                    rental.StatusId != RentalStatus.Closed &&
                    rental.StatusId != RentalStatus.Canceled &&
                    (!hasValidSelectedPeriod ||
                     (rental.StartDate < selectedPeriodEnd && selectedPeriodStart < rental.EndDate)))
                .Select(rental => rental.VehicleId);
            var query = _dbContext.Vehicles
                .AsNoTracking()
                .Where(vehicle => vehicle.VehicleStatusCode == VehicleStatuses.Ready);

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                query = query.Where(vehicle =>
                    vehicle.MakeLookup!.Name.Contains(searchText) ||
                    vehicle.ModelLookup!.Name.Contains(searchText) ||
                    vehicle.LicensePlate.Contains(searchText));
            }

            if (hasValidSelectedPeriod)
            {
                query = query.Where(vehicle => !conflictingVehicleIds.Contains(vehicle.Id));
            }

            var items = await query
                .OrderBy(vehicle => vehicle.MakeLookup!.Name)
                .ThenBy(vehicle => vehicle.ModelLookup!.Name)
                .ThenBy(vehicle => vehicle.LicensePlate)
                .Select(vehicle => new
                {
                    vehicle.Id,
                    BaseDisplay = vehicle.MakeLookup!.Name + " " + vehicle.ModelLookup!.Name + " [" + vehicle.LicensePlate + "]",
                    vehicle.DailyRate,
                    IsReady = vehicle.VehicleStatusCode == VehicleStatuses.Ready,
                    HasConflict = hasValidSelectedPeriod && conflictingVehicleIds.Contains(vehicle.Id)
                })
                .Take(VehicleSearchLimit)
                .ToListAsync(token);

            if (preferredVehicleId.HasValue && items.All(item => item.Id != preferredVehicleId.Value))
            {
                var selectedVehicle = await _dbContext.Vehicles
                    .AsNoTracking()
                    .Where(vehicle => vehicle.Id == preferredVehicleId.Value)
                    .Select(vehicle => new
                    {
                        vehicle.Id,
                        BaseDisplay = vehicle.MakeLookup!.Name + " " + vehicle.ModelLookup!.Name + " [" + vehicle.LicensePlate + "]",
                        vehicle.DailyRate,
                        IsReady = vehicle.VehicleStatusCode == VehicleStatuses.Ready,
                        HasConflict = hasValidSelectedPeriod &&
                                      _dbContext.Rentals
                                          .AsNoTracking()
                                          .Any(rental =>
                                              rental.VehicleId == vehicle.Id &&
                                              rental.StatusId != RentalStatus.Closed &&
                                              rental.StatusId != RentalStatus.Canceled &&
                                              rental.StartDate < selectedPeriodEnd &&
                                              selectedPeriodStart < rental.EndDate)
                    })
                    .FirstOrDefaultAsync(token);

                if (selectedVehicle is not null)
                {
                    items.Insert(0, selectedVehicle);
                }
            }

            return items;
        }, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        var vehicleOptions = vehicleData
            .Select(vehicle => BuildVehicleOption(
                vehicle.Id,
                vehicle.BaseDisplay,
                vehicle.DailyRate,
                vehicle.IsReady,
                vehicle.HasConflict,
                hasValidSelectedPeriod))
            .ToList();

        Vehicles.Clear();
        foreach (var vehicleOption in vehicleOptions)
        {
            Vehicles.Add(vehicleOption);
        }

        if (preferredVehicleId.HasValue)
        {
            CreateDraft.SelectedVehicle = Vehicles.FirstOrDefault(item => item.Id == preferredVehicleId.Value);
        }
    }

    private void CreateDraft_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isSynchronizingCreateDraft)
        {
            return;
        }

        if (e.PropertyName != nameof(RentalCreateDraft.FormMessage) &&
            e.PropertyName != nameof(RentalCreateDraft.HasFormMessage) &&
            CreateDraft.HasFormMessage)
        {
            CreateDraft.FormMessage = string.Empty;
        }

        switch (e.PropertyName)
        {
            case nameof(RentalCreateDraft.ClientSearchText):
                if (IsCreateRentalDialogOpen && !IsCreateRentalBusy)
                {
                    _clientSearchTask = QueueClientSearchAsync();
                }
                break;
            case nameof(RentalCreateDraft.VehicleSearchText):
                if (IsCreateRentalDialogOpen && !IsCreateRentalBusy)
                {
                    _vehicleSearchTask = QueueVehicleSearchAsync();
                }
                break;
            case nameof(RentalCreateDraft.SelectedClient):
            case nameof(RentalCreateDraft.SelectedVehicle):
            case nameof(RentalCreateDraft.CreateInitialPayment):
            case nameof(RentalCreateDraft.InitialPaymentAmountInput):
                NotifyCreateRentalValidationChanged();
                break;
            case nameof(RentalCreateDraft.StartDate):
            case nameof(RentalCreateDraft.StartTime):
            case nameof(RentalCreateDraft.EndDate):
            case nameof(RentalCreateDraft.EndTime):
            case nameof(RentalCreateDraft.PickupLocation):
            case nameof(RentalCreateDraft.ReturnLocation):
                NotifyCreateRentalValidationChanged();
                if (IsCreateRentalDialogOpen && !IsCreateRentalBusy)
                {
                    _vehicleSearchTask = QueueVehicleSearchAsync();
                }
                break;
        }
    }

    private void BuildGantt(
        IReadOnlyList<Vehicle> vehicles,
        IReadOnlyDictionary<int, IReadOnlyList<RentalListSnapshot>> rentalsByVehicleId)
    {
        GanttRows.Clear();
        var horizon = Enumerable.Range(0, 14).Select(offset => DateTime.Today.AddDays(offset)).ToArray();

        foreach (var vehicle in vehicles)
        {
            rentalsByVehicleId.TryGetValue(vehicle.Id, out var rowRentals);
            rowRentals ??= Array.Empty<RentalListSnapshot>();
            var cells = new List<GanttCell>(horizon.Length);

            for (var i = 0; i < horizon.Length; i++)
            {
                var date = horizon[i];
                var active = rowRentals.FirstOrDefault(item =>
                    item.StartDate <= date &&
                    date <= item.EndDate &&
                    item.StatusId != RentalStatus.Canceled);

                var statusText = active?.StatusId switch
                {
                    RentalStatus.Active => "Активна",
                    RentalStatus.Booked => "Бронь",
                    RentalStatus.Closed => "Закрита",
                    _ => "Вільно"
                };

                var fallbackSymbol = active?.StatusId switch
                {
                    RentalStatus.Active => 'A',
                    RentalStatus.Booked => 'B',
                    RentalStatus.Closed => 'C',
                    _ => '.'
                };

                cells.Add(new GanttCell(date, statusText, fallbackSymbol));
            }

            GanttRows.Add(new GanttRow(
                $"{vehicle.MakeName} {vehicle.ModelName} [{vehicle.LicensePlate}]",
                cells));
        }
    }

    private void RequestGuide()
    {
        GuideRequestId++;
    }

    private string DefaultLocation => Locations.FirstOrDefault() ?? string.Empty;

    private void ResetCreateDraft()
    {
        SynchronizeCreateDraft(() => CreateDraft.Reset(DefaultLocation));
        NotifyCreateRentalValidationChanged();
    }

    private void SynchronizeCreateDraft(Action action)
    {
        _isSynchronizingCreateDraft = true;
        try
        {
            action();
        }
        finally
        {
            _isSynchronizingCreateDraft = false;
        }
    }

    public void ReleaseTransientState()
    {
        CancelAndDisposeToken(ref _clientSearchCancellationTokenSource);
        CancelAndDisposeToken(ref _vehicleSearchCancellationTokenSource);
        CancelAndDisposeToken(ref _paymentsLoadCancellationTokenSource);
        _clientSearchTask = Task.CompletedTask;
        _vehicleSearchTask = Task.CompletedTask;
        _paymentsLoadTask = Task.CompletedTask;

        _suppressSelectedRentalPaymentsLoad = true;
        SelectedRental = null;
        _suppressSelectedRentalPaymentsLoad = false;

        Payments.Clear();
        PaymentAmountInput = string.Empty;
        StatusMessage = string.Empty;
        IsCreateRentalDialogOpen = false;
        ResetCreateDraft();
    }

    private static string BuildClientOptionDisplay(string fullName, string driverLicense)
    {
        var driverLicenseDisplay = string.IsNullOrWhiteSpace(driverLicense)
            ? MissingDriverLicenseLabel
            : driverLicense;

        return $"{fullName} ({driverLicenseDisplay})";
    }

    private static VehicleOption BuildVehicleOption(
        int id,
        string baseDisplay,
        decimal dailyRate,
        bool isReady,
        bool hasConflict,
        bool hasValidSelectedPeriod)
    {
        if (!isReady)
        {
            return new VehicleOption(
                id,
                $"{baseDisplay} - {VehicleUnavailableLabel}",
                dailyRate,
                false,
                "Обране авто тимчасово недоступне для бронювання.");
        }

        if (hasValidSelectedPeriod && hasConflict)
        {
            return new VehicleOption(
                id,
                $"{baseDisplay} - {VehicleConflictLabel}",
                dailyRate,
                false,
                "Обране авто недоступне на обрані дати.");
        }

        var availabilityLabel = hasValidSelectedPeriod
            ? VehicleAvailableForPeriodLabel
            : VehiclePeriodRequiredLabel;

        return new VehicleOption(
            id,
            $"{baseDisplay} - {availabilityLabel}",
            dailyRate,
            hasValidSelectedPeriod,
            string.Empty);
    }

    public sealed record ClientOption(
        int Id,
        string Display,
        string FullName,
        string Phone,
        string DriverLicense,
        DateTime? DriverLicenseExpirationDate,
        bool IsBlacklisted,
        string BlacklistReason);

    public sealed record VehicleOption(
        int Id,
        string Display,
        decimal DailyRate,
        bool IsAvailableForSelectedPeriod,
        string AvailabilityMessage);

    public sealed record RentalRow(
        int Id,
        string ContractNumber,
        string ClientName,
        string VehicleName,
        DateTime StartDate,
        DateTime EndDate,
        string PickupLocation,
        string ReturnLocation,
        decimal TotalAmount,
        decimal PaidAmount,
        decimal Balance,
        RentalStatus StatusId)
    {
        public string StatusDisplay => StatusId.ToDisplay();

        public string RouteDisplay => $"{PickupLocation} -> {ReturnLocation}";
    }

    public sealed record PaymentRow(
        int Id,
        DateTime CreatedAtUtc,
        PaymentMethod Method,
        PaymentDirection Direction,
        decimal Amount,
        string Notes)
    {
        public string MethodDisplay => Method.ToDisplay();

        public string DirectionDisplay => Direction.ToDisplay();
    }

    public sealed record GanttRow(string Vehicle, IReadOnlyList<GanttCell> Cells)
    {
        public string Timeline => string.Concat(Cells.Select(cell => cell.FallbackSymbol));
    }

    public sealed record GanttCell(DateTime Date, string StatusText, char FallbackSymbol)
    {
        public string DayLabel => Date.ToString("dd", CultureInfo.InvariantCulture);

        public string Tooltip => $"{Date:dd.MM}: {StatusText}";
    }

    private sealed record RentalListSnapshot(
        int Id,
        string ContractNumber,
        string ClientName,
        int VehicleId,
        string VehicleName,
        DateTime StartDate,
        DateTime EndDate,
        string PickupLocation,
        string ReturnLocation,
        decimal TotalAmount,
        decimal PaidAmount,
        RentalStatus StatusId);

    private async Task<T> WithDbContextGateAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        await _dbContextOperationGate.WaitAsync(cancellationToken);
        try
        {
            return await operation(cancellationToken);
        }
        finally
        {
            _dbContextOperationGate.Release();
        }
    }

    private async Task AwaitPendingCreateDialogSearchesAsync()
    {
        CancelPendingCreateDialogSearches();

        try
        {
            await Task.WhenAll(_clientSearchTask, _vehicleSearchTask);
        }
        catch (OperationCanceledException)
        {
            // Ignore outdated lookups that were explicitly canceled.
        }
    }

    private void CancelPendingCreateDialogSearches()
    {
        CancelAndDisposeToken(ref _clientSearchCancellationTokenSource);
        CancelAndDisposeToken(ref _vehicleSearchCancellationTokenSource);
    }

    private static void CancelAndDisposeToken(ref CancellationTokenSource? cancellationTokenSource)
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        cancellationTokenSource = null;
    }

    private static DateTime NormalizeBusinessTimestamp(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Unspecified => value,
            DateTimeKind.Utc => DateTime.SpecifyKind(value, DateTimeKind.Unspecified),
            DateTimeKind.Local => DateTime.SpecifyKind(value.ToUniversalTime(), DateTimeKind.Unspecified),
            _ => value
        };
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
}



