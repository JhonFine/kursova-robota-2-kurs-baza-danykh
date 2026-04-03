using CarRental.Desktop.Data;
using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Rentals;
using CarRental.Shared.ReferenceData;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Globalization;

namespace CarRental.Desktop.ViewModels;

// Self-service С–СЃС‚РѕСЂС–СЏ РґРѕРіРѕРІРѕСЂС–РІ РґР»СЏ РєР»С–С”РЅС‚Р°:
// РІС–РґРѕР±СЂР°Р¶Р°С” Р±СЂРѕРЅСЋРІР°РЅРЅСЏ Р·Р° СЃС‚Р°С‚СѓСЃР°РјРё С‚Р° РґР°С” Р±РµР·РїРµС‡РЅС– callbacks Сѓ РєР°С‚Р°Р»РѕРі С– СЃС†РµРЅР°СЂС–Р№ РїРѕРІС‚РѕСЂРЅРѕРіРѕ Р±СЂРѕРЅСЋРІР°РЅРЅСЏ.
public sealed class UserRentalsPageViewModel : PageDataViewModelBase, ITransientStateOwner
{
    private const string SelfServiceCancelReason = DemoSeedReferenceData.SelfServiceCancelReasonDesktop;

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

    // ViewModel РЅРµ Р·РЅР°С” РїСЂРѕ РєРѕРЅРєСЂРµС‚РЅСѓ РЅР°РІС–РіР°С†С–СЋ shell, С‚РѕРјСѓ РїСЂРѕСЃРёС‚СЊ Р·РѕРІРЅС–С€РЅС–Р№ РѕР±СЂРѕР±РЅРёРє РІС–РґРєСЂРёС‚Рё РєР°С‚Р°Р»РѕРі.
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

        // РџСЂРѕС„С–Р»СЊ РєР»С–С”РЅС‚Р° РјРѕР¶Рµ Р±СѓС‚Рё СЃС‚РІРѕСЂРµРЅРёР№ Р»С–РЅРёРІРѕ РїС–Рґ staff/account РјРѕРґРµР»СЊ, С‚РѕРјСѓ СЃС‚РѕСЂС–РЅРєР° СЃР°РјР° self-heal-РёС‚СЊ С†РµР№ Р·РІ'СЏР·РѕРє.
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

            var vehicles = _dbContext.Vehicles
                .AsNoTracking()
                .IgnoreQueryFilters();

            var rentals = await _dbContext.Rentals
                .AsNoTracking()
                .Where(item => item.ClientId == clientId.Value)
                .OrderByDescending(item => item.StartDate)
                .Select(item => new
                {
                    item.Id,
                    item.ContractNumber,
                    item.VehicleId,
                    VehicleName = vehicles
                        .Where(vehicle => vehicle.Id == item.VehicleId)
                        .Select(vehicle => vehicle.MakeLookup!.Name + " " + vehicle.ModelLookup!.Name + " [" + vehicle.LicensePlate + "]")
                        .FirstOrDefault() ?? "Авто не знайдено",
                    item.StartDate,
                    item.EndDate,
                    item.StatusId,
                    item.TotalAmount,
                    PaidAmount = item.Payments.Sum(payment => (decimal?)(
                        payment.DirectionId == PaymentDirection.Incoming
                            ? payment.Amount
                            : payment.DirectionId == PaymentDirection.Refund
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
                    item.StatusId,
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
                rows.Where(item => item.StatusId == RentalStatus.Booked)
                    .OrderBy(item => item.StartDate));
            ReplaceCollection(
                ActiveRentals,
                rows.Where(item => item.StatusId == RentalStatus.Active)
                    .OrderBy(item => item.EndDate));
            ReplaceCollection(
                HistoryRentals,
                rows.Where(item => item.StatusId == RentalStatus.Closed || item.StatusId == RentalStatus.Canceled)
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
        if (_currentEmployee.AccountId <= 0)
        {
            return null;
        }

        var account = await _dbContext.Accounts
            .Include(item => item.Client)
                .ThenInclude(item => item!.Documents)
            .FirstOrDefaultAsync(item => item.Id == _currentEmployee.AccountId);
        if (account is null)
        {
            return null;
        }

        var passportData = $"ACC-{account.Id:D6}";
        var driverLicense = $"USR-{account.Id:D6}";

        Client? client = account.Client;
        if (client is null)
        {
            client = await _dbContext.Clients
                .Include(item => item.Documents)
                .FirstOrDefaultAsync(item => item.AccountId == account.Id);
        }

        if (client is null)
        {
            client = new Client
            {
                AccountId = account.Id,
                FullName = _currentEmployee.FullName,
                PassportData = passportData,
                DriverLicense = driverLicense,
                Phone = ResolveClientPhone(account.Login)
            };

            _dbContext.Clients.Add(client);
            await _dbContext.SaveChangesAsync();
        }

        var hasChanges = false;
        var normalizedPhone = ResolveClientPhone(account.Login, client.Phone);
        if (!string.Equals(client.Phone, normalizedPhone, StringComparison.Ordinal))
        {
            client.Phone = normalizedPhone;
            hasChanges = true;
        }

        if (!string.Equals(client.FullName, _currentEmployee.FullName, StringComparison.Ordinal))
        {
            client.FullName = _currentEmployee.FullName;
            hasChanges = true;
        }

        if (client.AccountId != account.Id)
        {
            client.AccountId = account.Id;
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
        var normalizedCurrent = ClientProfileConventions.TryNormalizePhone(currentPhone);
        if (normalizedCurrent is not null)
        {
            return normalizedCurrent;
        }

        var normalizedLogin = ClientProfileConventions.TryNormalizePhone(login);
        if (normalizedLogin is not null)
        {
            return normalizedLogin;
        }

        return "Не вказано";
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
        RentalStatus StatusId,
        decimal TotalAmount,
        decimal PaidAmount,
        decimal Balance,
        DateTime CreatedAtUtc,
        DateTime? ClosedAtUtc,
        DateTime? CanceledAtUtc,
        string? CancellationReason)
    {
        public bool CanCancel => StatusId == RentalStatus.Booked;

        public bool CanBookAgain => StatusId is RentalStatus.Closed or RentalStatus.Canceled;

        public string StatusText => StatusId switch
        {
            RentalStatus.Booked => "Заброньовано",
            RentalStatus.Active => "Активна",
            RentalStatus.Closed => "Завершена",
            RentalStatus.Canceled => "Скасована",
            _ => StatusId.ToString()
        };

        public string StatusBadgeBackground => StatusId switch
        {
            RentalStatus.Booked => "#FEF3C7",
            RentalStatus.Active => "#DCFCE7",
            RentalStatus.Closed => "#DBEAFE",
            RentalStatus.Canceled => "#FEE2E2",
            _ => "#E5E7EB"
        };

        public string StatusBadgeForeground => StatusId switch
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
