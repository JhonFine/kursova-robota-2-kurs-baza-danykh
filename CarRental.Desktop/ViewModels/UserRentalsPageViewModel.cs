using CarRental.Desktop.Data;
using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Rentals;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Globalization;

namespace CarRental.Desktop.ViewModels;

public sealed class UserRentalsPageViewModel : PageDataViewModelBase, ITransientStateOwner
{
    private const string SelfServiceCancelReason = "Скасовано клієнтом через застосунок";

    private readonly RentalDbContext _dbContext;
    private readonly IRentalService _rentalService;
    private readonly PageRefreshCoordinator _refreshCoordinator;
    private readonly Employee _currentEmployee;

    private bool _isLoading;
    private string _statusMessage = string.Empty;
    private string _actionMessage = string.Empty;
    private string _clientSummaryText = "Тут зібрані ваші майбутні, активні та завершені договори.";
    private UserRentalRow? _cancelTargetRental;
    private bool _isCancelDialogOpen;

    public UserRentalsPageViewModel(
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
        OpenCatalogCommand = new AsyncRelayCommand(OpenCatalogAsync, () => !IsLoading);
        RequestCancelCommand = new RelayCommand<UserRentalRow?>(RequestCancel);
        ConfirmCancelCommand = new AsyncRelayCommand(ConfirmCancelAsync, () => !IsLoading && CancelTargetRental is not null);
        CloseCancelDialogCommand = new RelayCommand(CloseCancelDialog);
        BookAgainCommand = new AsyncRelayCommand<UserRentalRow?>(BookAgainAsync, rental => !IsLoading && rental is not null);
    }

    public ObservableCollection<UserRentalRow> UpcomingRentals { get; } = [];

    public ObservableCollection<UserRentalRow> ActiveRentals { get; } = [];

    public ObservableCollection<UserRentalRow> HistoryRentals { get; } = [];

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand OpenCatalogCommand { get; }

    public IRelayCommand<UserRentalRow?> RequestCancelCommand { get; }

    public IAsyncRelayCommand ConfirmCancelCommand { get; }

    public IRelayCommand CloseCancelDialogCommand { get; }

    public IAsyncRelayCommand<UserRentalRow?> BookAgainCommand { get; }

    public Func<int, Task>? RebookRequestedAsync { get; set; }

    public Func<Task>? OpenCatalogRequestedAsync { get; set; }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                OpenCatalogCommand.NotifyCanExecuteChanged();
                ConfirmCancelCommand.NotifyCanExecuteChanged();
                BookAgainCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string ActionMessage
    {
        get => _actionMessage;
        set => SetProperty(ref _actionMessage, value);
    }

    public string ClientSummaryText
    {
        get => _clientSummaryText;
        private set => SetProperty(ref _clientSummaryText, value);
    }

    public UserRentalRow? CancelTargetRental
    {
        get => _cancelTargetRental;
        private set
        {
            if (SetProperty(ref _cancelTargetRental, value))
            {
                ConfirmCancelCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(CancelDialogContractText));
                OnPropertyChanged(nameof(CancelDialogVehicleText));
                OnPropertyChanged(nameof(CancelDialogPeriodText));
                OnPropertyChanged(nameof(CancelDialogReasonText));
            }
        }
    }

    public bool IsCancelDialogOpen
    {
        get => _isCancelDialogOpen;
        private set => SetProperty(ref _isCancelDialogOpen, value);
    }

    public int UpcomingCount => UpcomingRentals.Count;

    public int ActiveCount => ActiveRentals.Count;

    public int HistoryCount => HistoryRentals.Count;

    public bool HasRentals => UpcomingCount > 0 || ActiveCount > 0 || HistoryCount > 0;

    public bool HasUpcomingRentals => UpcomingCount > 0;

    public bool HasActiveRentals => ActiveCount > 0;

    public bool HasHistoryRentals => HistoryCount > 0;

    public string CancelDialogContractText => CancelTargetRental?.ContractNumber ?? "—";

    public string CancelDialogVehicleText => CancelTargetRental?.VehicleName ?? "—";

    public string CancelDialogPeriodText => CancelTargetRental?.PeriodDisplay ?? "—";

    public string CancelDialogReasonText => SelfServiceCancelReason;

    public override async Task RefreshAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        try
        {
            StatusMessage = string.Empty;

            var clientId = await EnsureClientProfileAsync();
            if (!clientId.HasValue)
            {
                ClearCollections();
                ClientSummaryText = "Не вдалося знайти або створити клієнтський профіль для вашого акаунта.";
                StatusMessage = "Не вдалося підготувати профіль клієнта.";
                MarkDataLoaded();
                return;
            }

            var rentals = await _dbContext.Rentals
                .AsNoTracking()
                .Where(item => item.ClientId == clientId.Value)
                .OrderByDescending(item => item.StartDate)
                .Select(item => new
                {
                    item.Id,
                    item.ContractNumber,
                    item.VehicleId,
                    VehicleName = item.Vehicle != null
                        ? $"{item.Vehicle.Make} {item.Vehicle.Model} [{item.Vehicle.LicensePlate}]"
                        : "Авто не знайдено",
                    item.StartDate,
                    item.EndDate,
                    item.Status,
                    item.TotalAmount,
                    PaidAmount = item.Payments.Sum(payment => (decimal?)(
                        payment.Direction == PaymentDirection.Incoming
                            ? payment.Amount
                            : payment.Direction == PaymentDirection.Refund
                                ? -payment.Amount
                                : 0m)) ?? 0m,
                    item.CreatedAtUtc,
                    item.ClosedAtUtc,
                    item.CanceledAtUtc,
                    item.CancellationReason
                })
                .ToListAsync();

            var rows = rentals
                .Select(item => new UserRentalRow(
                    item.Id,
                    item.ContractNumber,
                    item.VehicleId,
                    item.VehicleName,
                    item.StartDate,
                    item.EndDate,
                    item.Status,
                    item.TotalAmount,
                    item.PaidAmount,
                    item.TotalAmount - item.PaidAmount,
                    item.CreatedAtUtc,
                    item.ClosedAtUtc,
                    item.CanceledAtUtc,
                    item.CancellationReason))
                .ToList();

            ReplaceCollection(
                UpcomingRentals,
                rows.Where(item => item.Status == RentalStatus.Booked)
                    .OrderBy(item => item.StartDate));
            ReplaceCollection(
                ActiveRentals,
                rows.Where(item => item.Status == RentalStatus.Active)
                    .OrderBy(item => item.EndDate));
            ReplaceCollection(
                HistoryRentals,
                rows.Where(item => item.Status == RentalStatus.Closed || item.Status == RentalStatus.Canceled)
                    .OrderByDescending(item => item.HistoryMoment));

            ClientSummaryText = HasRentals
                ? $"{_currentEmployee.FullName}, тут видно майбутні бронювання, активні оренди та історію ваших договорів."
                : $"{_currentEmployee.FullName}, у вас ще немає оформлених договорів. Почніть із підбору авто в каталозі прокату.";

            NotifyCollectionSummaryChanged();
            MarkDataLoaded();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void ReleaseTransientState()
    {
        IsCancelDialogOpen = false;
        CancelTargetRental = null;
        StatusMessage = string.Empty;
        ActionMessage = string.Empty;
    }

    private async Task OpenCatalogAsync()
    {
        if (OpenCatalogRequestedAsync is null)
        {
            return;
        }

        StatusMessage = string.Empty;
        ActionMessage = string.Empty;
        await OpenCatalogRequestedAsync();
    }

    private void RequestCancel(UserRentalRow? rental)
    {
        if (rental is null)
        {
            return;
        }

        ActionMessage = string.Empty;
        StatusMessage = string.Empty;
        CancelTargetRental = rental;
        IsCancelDialogOpen = true;
    }

    private async Task ConfirmCancelAsync()
    {
        if (CancelTargetRental is null)
        {
            StatusMessage = "Оберіть бронювання для скасування.";
            return;
        }

        if (!CancelTargetRental.CanCancel)
        {
            StatusMessage = "Самоскасування доступне лише для майбутніх бронювань.";
            return;
        }

        var rental = CancelTargetRental;
        IsCancelDialogOpen = false;
        CancelTargetRental = null;
        StatusMessage = string.Empty;

        var result = await _rentalService.CancelRentalAsync(
            new CancelRentalRequest(rental.Id, SelfServiceCancelReason));
        if (!result.Success)
        {
            StatusMessage = result.Message;
            return;
        }

        _refreshCoordinator.Invalidate(
            PageRefreshArea.Fleet |
            PageRefreshArea.Rentals |
            PageRefreshArea.Prokat |
            PageRefreshArea.Reports |
            PageRefreshArea.UserRentals);

        ActionMessage = $"Бронювання {rental.ContractNumber} скасовано.";
        await RefreshAsync();
    }

    private void CloseCancelDialog()
    {
        IsCancelDialogOpen = false;
        CancelTargetRental = null;
    }

    private async Task BookAgainAsync(UserRentalRow? rental)
    {
        if (rental is null)
        {
            return;
        }

        if (RebookRequestedAsync is null)
        {
            return;
        }

        StatusMessage = string.Empty;
        ActionMessage = string.Empty;
        await RebookRequestedAsync(rental.VehicleId);
    }

    private async Task<int?> EnsureClientProfileAsync()
    {
        var employee = await _dbContext.Employees
            .FirstOrDefaultAsync(item => item.Id == _currentEmployee.Id);
        if (employee is null)
        {
            return null;
        }

        var passportData = $"EMP-{employee.Id:D6}";
        var driverLicense = $"USR-{employee.Id:D6}";

        Client? client = null;
        if (employee.ClientId.HasValue)
        {
            client = await _dbContext.Clients
                .FirstOrDefaultAsync(item => item.Id == employee.ClientId.Value);
        }

        client ??= await _dbContext.Clients
            .FirstOrDefaultAsync(existing =>
                existing.PassportData == passportData ||
                existing.DriverLicense == driverLicense);

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

        if (employee.ClientId != client.Id)
        {
            employee.ClientId = client.Id;
            _currentEmployee.ClientId = client.Id;
            hasChanges = true;
        }

        if (hasChanges)
        {
            await _dbContext.SaveChangesAsync();
        }

        return client.Id;
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

        var digits = string.Concat(value.Where(char.IsDigit));
        return digits.Length is >= 10 and <= 15 ? $"+{digits}" : null;
    }

    private void ClearCollections()
    {
        UpcomingRentals.Clear();
        ActiveRentals.Clear();
        HistoryRentals.Clear();
        NotifyCollectionSummaryChanged();
    }

    private void ReplaceCollection(ObservableCollection<UserRentalRow> target, IEnumerable<UserRentalRow> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private void NotifyCollectionSummaryChanged()
    {
        OnPropertyChanged(nameof(UpcomingCount));
        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(HistoryCount));
        OnPropertyChanged(nameof(HasRentals));
        OnPropertyChanged(nameof(HasUpcomingRentals));
        OnPropertyChanged(nameof(HasActiveRentals));
        OnPropertyChanged(nameof(HasHistoryRentals));
    }

    public sealed record UserRentalRow(
        int Id,
        string ContractNumber,
        int VehicleId,
        string VehicleName,
        DateTime StartDate,
        DateTime EndDate,
        RentalStatus Status,
        decimal TotalAmount,
        decimal PaidAmount,
        decimal Balance,
        DateTime CreatedAtUtc,
        DateTime? ClosedAtUtc,
        DateTime? CanceledAtUtc,
        string? CancellationReason)
    {
        public bool CanCancel => Status == RentalStatus.Booked;

        public bool CanBookAgain => Status is RentalStatus.Closed or RentalStatus.Canceled;

        public string StatusText => Status switch
        {
            RentalStatus.Booked => "Заброньовано",
            RentalStatus.Active => "Активна",
            RentalStatus.Closed => "Завершена",
            RentalStatus.Canceled => "Скасована",
            _ => Status.ToString()
        };

        public string StatusBadgeBackground => Status switch
        {
            RentalStatus.Booked => "#FEF3C7",
            RentalStatus.Active => "#DCFCE7",
            RentalStatus.Closed => "#DBEAFE",
            RentalStatus.Canceled => "#FEE2E2",
            _ => "#E5E7EB"
        };

        public string StatusBadgeForeground => Status switch
        {
            RentalStatus.Booked => "#92400E",
            RentalStatus.Active => "#166534",
            RentalStatus.Closed => "#1D4ED8",
            RentalStatus.Canceled => "#991B1B",
            _ => "#374151"
        };

        public string PeriodDisplay => $"{StartDate:dd.MM.yyyy HH:mm} - {EndDate:dd.MM.yyyy HH:mm}";

        public string BalanceDisplay => $"{Balance:C}";

        public string TotalAmountDisplay => $"{TotalAmount:C}";

        public string PaidAmountDisplay => $"{PaidAmount:C}";

        public bool HasCancellationReason => !string.IsNullOrWhiteSpace(CancellationReason);

        public DateTime HistoryMoment => ClosedAtUtc ?? CanceledAtUtc ?? CreatedAtUtc;

        public string HistoryMomentDisplay => HistoryMoment.ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture);
    }
}
