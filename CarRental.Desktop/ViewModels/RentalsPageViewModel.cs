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
using System.Globalization;
using System.IO;

namespace CarRental.Desktop.ViewModels;

// Операційний workspace для менеджера оренд:
// тут в одному місці живуть створення договору, оплати, закриття та таймлайн завантаженості.
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
    private const int ClientSearchLimit = 40;
    private const int VehicleSearchLimit = 40;
    private const string VehicleAvailableLabel = "\u0434\u043E\u0441\u0442\u0443\u043F\u043D\u0435";
    private const string VehicleUnavailableLabel = "\u0437\u0430\u0439\u043D\u044F\u0442\u0435";

    private bool _isLoading;
    private ClientOption? _selectedClient;
    private VehicleOption? _selectedVehicle;
    private string _clientSearchText = string.Empty;
    private string _vehicleSearchText = string.Empty;
    private DateTime _startDate = DateTime.Today;
    private string _startTime = "10:00";
    private DateTime _endDate = DateTime.Today.AddDays(1);
    private string _endTime = "10:00";
    private DateTime _actualReturnDate = DateTime.Today;
    private string _endMileageInput = string.Empty;
    private string _statusMessage = string.Empty;
    private RentalRow? _selectedRental;
    private string _cancelReason = DemoSeedReferenceData.StaffCancellationReason;
    private string _paymentAmountInput = string.Empty;
    private PaymentMethod _paymentMethod = PaymentMethod.Cash;
    private PaymentDirection _paymentDirection = PaymentDirection.Incoming;
    private bool _autoPrintContract;
    private int _guideRequestId;
    // Живий пошук у комбобоксах перезапускає запит при кожному вводі, тому попередні пошуки треба скасовувати.
    private CancellationTokenSource? _clientSearchCancellationTokenSource;
    private CancellationTokenSource? _vehicleSearchCancellationTokenSource;
    private CancellationTokenSource? _paymentsLoadCancellationTokenSource;
    // Під час програмної зміни вибору оренди не треба повторно вантажити платежі ще до завершення refresh.
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

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsLoading);
        CreateRentalCommand = new AsyncRelayCommand(CreateRentalAsync, CanCreateRental);
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

    public ObservableCollection<string> TimeOptions { get; } = [.. DemoSeedReferenceData.TimeOptions];

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand CreateRentalCommand { get; }

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
                CreateRentalCommand.NotifyCanExecuteChanged();
                CloseRentalCommand.NotifyCanExecuteChanged();
                CancelRentalCommand.NotifyCanExecuteChanged();
                AddPaymentCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public ClientOption? SelectedClient
    {
        get => _selectedClient;
        set => SetProperty(ref _selectedClient, value);
    }

    public VehicleOption? SelectedVehicle
    {
        get => _selectedVehicle;
        set => SetProperty(ref _selectedVehicle, value);
    }

    public string ClientSearchText
    {
        get => _clientSearchText;
        set
        {
            if (SetProperty(ref _clientSearchText, value))
            {
                _ = QueueClientSearchAsync();
            }
        }
    }

    public string VehicleSearchText
    {
        get => _vehicleSearchText;
        set
        {
            if (SetProperty(ref _vehicleSearchText, value))
            {
                _ = QueueVehicleSearchAsync();
            }
        }
    }

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

                NotifyCreateRentalValidationChanged();
            }
        }
    }

    public string StartTime
    {
        get => _startTime;
        set
        {
            if (SetProperty(ref _startTime, value))
            {
                NotifyCreateRentalValidationChanged();
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
                NotifyCreateRentalValidationChanged();
            }
        }
    }

    public string EndTime
    {
        get => _endTime;
        set
        {
            if (SetProperty(ref _endTime, value))
            {
                NotifyCreateRentalValidationChanged();
            }
        }
    }

    public DateTime MinCreateStartDate => DateTime.Today;

    public DateTime MinCreateEndDate => StartDate.Date > DateTime.Today ? StartDate.Date : DateTime.Today;

    public string CreateStartDateValidationMessage => ResolveCreateStartDateValidationMessage() ?? string.Empty;

    public bool HasCreateStartDateValidationMessage => !string.IsNullOrWhiteSpace(CreateStartDateValidationMessage);

    public string CreateEndDateValidationMessage => ResolveCreateEndDateValidationMessage() ?? string.Empty;

    public bool HasCreateEndDateValidationMessage => !string.IsNullOrWhiteSpace(CreateEndDateValidationMessage);

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
        set => SetProperty(ref _statusMessage, value);
    }

    public RentalRow? SelectedRental
    {
        get => _selectedRental;
        set
        {
            if (SetProperty(ref _selectedRental, value))
            {
                QueuePaymentsLoad();
            }
        }
    }

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

    public bool AutoPrintContract
    {
        get => _autoPrintContract;
        set => SetProperty(ref _autoPrintContract, value);
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

        IsLoading = true;
        try
        {
            var clients = _dbContext.Clients
                .AsNoTracking()
                .IgnoreQueryFilters();
            var vehicles = _dbContext.Vehicles
                .AsNoTracking()
                .IgnoreQueryFilters();

            var vehiclesForGantt = await _dbContext.Vehicles
                .AsNoTracking()
                .OrderBy(vehicle => vehicle.Make)
                .ThenBy(vehicle => vehicle.Model)
                .ToListAsync();

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
                        .Select(vehicle => vehicle.Make + " " + vehicle.Model)
                        .FirstOrDefault() ?? string.Empty,
                    rental.StartDate,
                    rental.EndDate,
                    rental.TotalAmount,
                    rental.Payments.Sum(payment => (decimal?)(
                        payment.Direction == PaymentDirection.Incoming
                            ? payment.Amount
                            : payment.Direction == PaymentDirection.Refund
                                ? -payment.Amount
                                : 0m)) ?? 0m,
                    rental.Status))
                .ToListAsync();

            await ReloadClientsAsync(SelectedClient?.Id);

            await ReloadVehiclesAsync(SelectedVehicle?.Id);

            var selectedRentalId = SelectedRental?.Id;
            Rentals.Clear();
            foreach (var rental in rentals)
            {
                Rentals.Add(new RentalRow(
                    rental.Id,
                    rental.ContractNumber,
                    rental.ClientName,
                    rental.VehicleName,
                    rental.StartDate,
                    rental.EndDate,
                    rental.TotalAmount,
                    rental.PaidAmount,
                    rental.TotalAmount - rental.PaidAmount,
                    rental.Status));
            }

            var rentalsByVehicleId = rentals
                .GroupBy(item => item.VehicleId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<RentalListSnapshot>)group
                        .OrderBy(item => item.StartDate)
                        .ToList());

            BuildGantt(vehiclesForGantt, rentalsByVehicleId);

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

    private async Task CreateRentalAsync()
    {
        StatusMessage = string.Empty;

        if (!CanManageRentals)
        {
            StatusMessage = "Недостатньо прав для оформлення оренди.";
            return;
        }

        if (SelectedClient is null || SelectedVehicle is null)
        {
            StatusMessage = "Оберіть клієнта та авто.";
            return;
        }

        var startDateValidationError = ResolveCreateStartDateValidationMessage();
        if (!string.IsNullOrWhiteSpace(startDateValidationError))
        {
            StatusMessage = startDateValidationError;
            return;
        }

        var endDateValidationError = ResolveCreateEndDateValidationMessage();
        if (!string.IsNullOrWhiteSpace(endDateValidationError))
        {
            StatusMessage = endDateValidationError;
            return;
        }

        var startDateTime = BuildCreateStartDateTime();
        var endDateTime = BuildCreateEndDateTime();

        var result = await _rentalService.CreateRentalAsync(
            new CreateRentalRequest(
                SelectedClient.Id,
                SelectedVehicle.Id,
                _currentEmployee.Id,
                startDateTime,
                endDateTime));

        if (!result.Success)
        {
            StatusMessage = result.Message;
            return;
        }

        var rental = await _dbContext.Rentals
            .AsNoTracking()
            .Include(item => item.Client)
            .Include(item => item.Vehicle)
            .FirstOrDefaultAsync(item => item.Id == result.RentalId);

        if (rental is not null && rental.Client is not null && rental.Vehicle is not null)
        {
            var files = await _documentGenerator.GenerateRentalContractAsync(
                new ContractData(
                    rental.Id,
                    rental.ContractNumber,
                    rental.Client.FullName,
                    $"{rental.Vehicle.Make} {rental.Vehicle.Model} [{rental.Vehicle.LicensePlate}]",
                    rental.StartDate,
                    rental.EndDate,
                    rental.TotalAmount));

            StatusMessage =
                $"Оренду створено ({result.ContractNumber}). Файли: " +
                $"{Path.GetFileName(files.TextPath)}, {Path.GetFileName(files.DocxPath)}, {Path.GetFileName(files.PdfPath)}";

            if (AutoPrintContract)
            {
                _printService.TryPrint(files.PdfPath, out var printMessage);
                StatusMessage += $" Друк: {printMessage}";
            }
        }
        else
        {
            StatusMessage = result.Message;
        }

        EndMileageInput = string.Empty;
        ActualReturnDate = EndDate;
        _refreshCoordinator.Invalidate(PageRefreshArea.Fleet | PageRefreshArea.Prokat | PageRefreshArea.Reports);
        await RefreshAsync();
    }

    private bool CanCreateRental()
    {
        return !IsLoading &&
               string.IsNullOrWhiteSpace(ResolveCreateStartDateValidationMessage()) &&
               string.IsNullOrWhiteSpace(ResolveCreateEndDateValidationMessage());
    }

    private string? ResolveCreateStartDateValidationMessage()
    {
        return BuildCreateStartDateTime() < DateTime.Now
            ? "Початок оренди не може бути в минулому."
            : null;
    }

    private string? ResolveCreateEndDateValidationMessage()
    {
        if (EndDate.Date < StartDate.Date)
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
        CreateRentalCommand.NotifyCanExecuteChanged();
    }

    private DateTime BuildCreateStartDateTime()
    {
        return CombineDateAndTime(StartDate, StartTime, new TimeSpan(10, 0, 0));
    }

    private DateTime BuildCreateEndDateTime()
    {
        var fallback = ParseTime(StartTime, new TimeSpan(10, 0, 0));
        return CombineDateAndTime(EndDate, EndTime, fallback);
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

        if (SelectedRental.Status == RentalStatus.Closed)
        {
            StatusMessage = "Оренду вже закрито.";
            return;
        }

        if (SelectedRental.Status == RentalStatus.Canceled)
        {
            StatusMessage = "Оренду скасовано.";
            return;
        }

        if (!int.TryParse(EndMileageInput, out var endMileage) || endMileage <= 0)
        {
            StatusMessage = "Некоректний кінцевий пробіг.";
            return;
        }

        var result = await _rentalService.CloseRentalAsync(
            new CloseRentalRequest(SelectedRental.Id, ActualReturnDate, endMileage));
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

        var result = await _rentalService.CancelRentalAsync(
            new CancelRentalRequest(SelectedRental.Id, CancelReason));
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

        var result = await _paymentService.AddPaymentAsync(
            new PaymentRequest(
                SelectedRental.Id,
                _currentEmployee.Id,
                amount,
                PaymentMethod,
                PaymentDirection,
                string.Empty));
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
        _ = LoadPaymentsAsync(SelectedRental.Id, cancellationTokenSource.Token);
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
            var payments = await _paymentService.GetRentalPaymentsAsync(rentalId.Value, cancellationToken);
            if (cancellationToken.IsCancellationRequested || SelectedRental?.Id != rentalId.Value)
            {
                return;
            }

            foreach (var payment in payments)
            {
                Payments.Add(new PaymentRow(
                    payment.Id,
                    payment.CreatedAtUtc,
                    payment.Method,
                    payment.Direction,
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
            await ReloadClientsAsync(SelectedClient?.Id, cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            // Ignore outdated search requests.
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
            await ReloadVehiclesAsync(SelectedVehicle?.Id, cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            // Ignore outdated search requests.
        }
    }

    private async Task ReloadClientsAsync(int? preferredClientId, CancellationToken cancellationToken = default)
    {
        var searchText = ClientSearchText.Trim();
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
                PassportData = passportDocuments
                    .Where(document => document.ClientId == client.Id)
                    .Select(document => document.DocumentNumber)
                    .FirstOrDefault() ?? string.Empty,
                DriverLicense = driverLicenseDocuments
                    .Where(document => document.ClientId == client.Id)
                    .Select(document => document.DocumentNumber)
                    .FirstOrDefault() ?? string.Empty
            });

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = query.Where(client =>
                client.FullName.Contains(searchText) ||
                client.DriverLicense.Contains(searchText) ||
                client.Phone.Contains(searchText) ||
                client.PassportData.Contains(searchText));
        }

        var clientOptions = await query
            .OrderBy(client => client.FullName)
            .ThenBy(client => client.DriverLicense)
            .Select(client => new ClientOption(client.Id, $"{client.FullName} ({client.DriverLicense})"))
            .Take(ClientSearchLimit)
            .ToListAsync(cancellationToken);

        if (preferredClientId.HasValue && clientOptions.All(item => item.Id != preferredClientId.Value))
        {
            var selectedClient = await _dbContext.Clients
                .AsNoTracking()
                .Where(client => client.Id == preferredClientId.Value)
                .Select(client => new ClientOption(client.Id, $"{client.FullName} ({client.DriverLicense})"))
                .FirstOrDefaultAsync(cancellationToken);

            if (selectedClient is not null)
            {
                clientOptions.Insert(0, selectedClient);
            }
        }

        Clients.Clear();
        foreach (var clientOption in clientOptions)
        {
            Clients.Add(clientOption);
        }

        if (preferredClientId.HasValue)
        {
            SelectedClient = Clients.FirstOrDefault(item => item.Id == preferredClientId.Value);
        }
    }

    private async Task ReloadVehiclesAsync(int? preferredVehicleId, CancellationToken cancellationToken = default)
    {
        var searchText = VehicleSearchText.Trim();
        var activeRentalVehicleIds = _dbContext.Rentals
            .AsNoTracking()
            .Where(rental => rental.Status == RentalStatus.Active)
            .Select(rental => rental.VehicleId);
        var query = _dbContext.Vehicles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = query.Where(vehicle =>
                vehicle.Make.Contains(searchText) ||
                vehicle.Model.Contains(searchText) ||
                vehicle.LicensePlate.Contains(searchText));
        }

        var vehicleOptions = await query
            .OrderBy(vehicle => vehicle.Make)
            .ThenBy(vehicle => vehicle.Model)
            .ThenBy(vehicle => vehicle.LicensePlate)
            .Select(vehicle => new VehicleOption(
                vehicle.Id,
                $"{vehicle.Make} {vehicle.Model} [{vehicle.LicensePlate}] - {(vehicle.VehicleStatusCode == VehicleStatuses.Ready && !activeRentalVehicleIds.Contains(vehicle.Id) ? VehicleAvailableLabel : VehicleUnavailableLabel)}"))
            .Take(VehicleSearchLimit)
            .ToListAsync(cancellationToken);

        if (preferredVehicleId.HasValue && vehicleOptions.All(item => item.Id != preferredVehicleId.Value))
        {
            var selectedVehicle = await _dbContext.Vehicles
                .AsNoTracking()
                .Where(vehicle => vehicle.Id == preferredVehicleId.Value)
                .Select(vehicle => new VehicleOption(
                    vehicle.Id,
                    $"{vehicle.Make} {vehicle.Model} [{vehicle.LicensePlate}] - {(vehicle.VehicleStatusCode == VehicleStatuses.Ready && !activeRentalVehicleIds.Contains(vehicle.Id) ? VehicleAvailableLabel : VehicleUnavailableLabel)}"))
                .FirstOrDefaultAsync(cancellationToken);

            if (selectedVehicle is not null)
            {
                vehicleOptions.Insert(0, selectedVehicle);
            }
        }

        Vehicles.Clear();
        foreach (var vehicleOption in vehicleOptions)
        {
            Vehicles.Add(vehicleOption);
        }

        if (preferredVehicleId.HasValue)
        {
            SelectedVehicle = Vehicles.FirstOrDefault(item => item.Id == preferredVehicleId.Value);
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
                    item.Status != RentalStatus.Canceled);

                var statusText = active?.Status switch
                {
                    RentalStatus.Active => "Активна",
                    RentalStatus.Booked => "Бронь",
                    RentalStatus.Closed => "Закрита",
                    _ => "Вільно"
                };

                var fallbackSymbol = active?.Status switch
                {
                    RentalStatus.Active => 'A',
                    RentalStatus.Booked => 'B',
                    RentalStatus.Closed => 'C',
                    _ => '.'
                };

                cells.Add(new GanttCell(date, statusText, fallbackSymbol));
            }

            GanttRows.Add(new GanttRow(
                $"{vehicle.Make} {vehicle.Model} [{vehicle.LicensePlate}]",
                cells));
        }
    }

    private void RequestGuide()
    {
        GuideRequestId++;
    }

    public void ReleaseTransientState()
    {
        CancelAndDisposeToken(ref _clientSearchCancellationTokenSource);
        CancelAndDisposeToken(ref _vehicleSearchCancellationTokenSource);
        CancelAndDisposeToken(ref _paymentsLoadCancellationTokenSource);

        _suppressSelectedRentalPaymentsLoad = true;
        SelectedRental = null;
        _suppressSelectedRentalPaymentsLoad = false;

        Payments.Clear();
        PaymentAmountInput = string.Empty;
        StatusMessage = string.Empty;
    }

    public sealed record ClientOption(int Id, string Display);

    public sealed record VehicleOption(int Id, string Display);

    public sealed record RentalRow(
        int Id,
        string ContractNumber,
        string ClientName,
        string VehicleName,
        DateTime StartDate,
        DateTime EndDate,
        decimal TotalAmount,
        decimal PaidAmount,
        decimal Balance,
        RentalStatus Status)
    {
        public string StatusDisplay => Status.ToDisplay();
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
        decimal TotalAmount,
        decimal PaidAmount,
        RentalStatus Status);

    private static void CancelAndDisposeToken(ref CancellationTokenSource? cancellationTokenSource)
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        cancellationTokenSource = null;
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


