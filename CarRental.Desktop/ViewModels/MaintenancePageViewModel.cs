using CarRental.Desktop.Data;
using CarRental.Desktop.Localization;
using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Maintenance;
using CarRental.Shared.ReferenceData;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Globalization;

namespace CarRental.Desktop.ViewModels;

// Екран ТО поєднує швидке внесення сервісного запису, фільтровану історію
// та прогноз майбутніх обслуговувань на основі наявних інтервалів і записів.
public sealed class MaintenancePageViewModel : PageDataViewModelBase, ITransientStateOwner
{
    private const string AllVehiclesOption = "Усі авто";
    private const string AllMaintenanceTypesOption = "Усі типи";
    private const string VehicleAvailableStatus = "Доступне";
    private const string VehicleUnavailableStatus = "Недоступне";
    private const string VehicleActiveRentalStatus = "Активна оренда";
    private const string VehicleMaintenanceStatus = "На ремонті";

    private readonly RentalDbContext _dbContext;
    private readonly IMaintenanceService _maintenanceService;
    private readonly PageRefreshCoordinator _refreshCoordinator;
    private readonly Employee _currentEmployee;
    private bool _isLoading;
    private VehicleOption? _selectedVehicle;
    private string _mileageInput = string.Empty;
    private string _nextMileageInput = string.Empty;
    private string _description = DemoSeedReferenceData.DefaultMaintenanceDescription;
    private string _costInput = string.Empty;
    private string _statusMessage = string.Empty;
    private int _guideRequestId;
    private int? _selectedHistoryVehicleId;
    private DateTime? _historyDateFrom;
    private DateTime? _historyDateTo;
    private string? _selectedHistoryMaintenanceType;
    private string _historySearchText = string.Empty;

    public MaintenancePageViewModel(
        RentalDbContext dbContext,
        IMaintenanceService maintenanceService,
        PageRefreshCoordinator refreshCoordinator,
        Employee currentEmployee)
    {
        _dbContext = dbContext;
        _maintenanceService = maintenanceService;
        _refreshCoordinator = refreshCoordinator;
        _currentEmployee = currentEmployee;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsLoading);
        AddRecordCommand = new AsyncRelayCommand(AddRecordAsync, () => !IsLoading);
        ApplyHistoryFiltersCommand = new AsyncRelayCommand(ApplyHistoryFiltersAsync, () => !IsLoading);
        ClearHistoryFiltersCommand = new AsyncRelayCommand(ClearHistoryFiltersAsync, () => !IsLoading);
        OpenVehicleFromMaintenanceCommand = new AsyncRelayCommand<int>(OpenVehicleFromMaintenanceAsync);
        RequestGuideCommand = new RelayCommand(RequestGuide);
    }

    public ObservableCollection<VehicleOption> Vehicles { get; } = [];

    public ObservableCollection<HistoryVehicleFilterOption> HistoryVehicleOptions { get; } = [];

    public ObservableCollection<HistoryMaintenanceTypeOption> HistoryMaintenanceTypeOptions { get; } = [];

    public ObservableCollection<RecordRow> Records { get; } = [];

    public ObservableCollection<DueRow> DueItems { get; } = [];

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand AddRecordCommand { get; }

    public IAsyncRelayCommand ApplyHistoryFiltersCommand { get; }

    public IAsyncRelayCommand ClearHistoryFiltersCommand { get; }

    public IAsyncRelayCommand<int> OpenVehicleFromMaintenanceCommand { get; }

    public IRelayCommand RequestGuideCommand { get; }

    public Func<int, bool, Task>? OpenVehicleRequestedAsync { get; set; }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                AddRecordCommand.NotifyCanExecuteChanged();
                ApplyHistoryFiltersCommand.NotifyCanExecuteChanged();
                ClearHistoryFiltersCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public VehicleOption? SelectedVehicle
    {
        get => _selectedVehicle;
        set => SetProperty(ref _selectedVehicle, value);
    }

    public int? SelectedHistoryVehicleId
    {
        get => _selectedHistoryVehicleId;
        set => SetProperty(ref _selectedHistoryVehicleId, value);
    }

    public DateTime? HistoryDateFrom
    {
        get => _historyDateFrom;
        set => SetProperty(ref _historyDateFrom, value);
    }

    public DateTime? HistoryDateTo
    {
        get => _historyDateTo;
        set => SetProperty(ref _historyDateTo, value);
    }

    public string? SelectedHistoryMaintenanceType
    {
        get => _selectedHistoryMaintenanceType;
        set => SetProperty(ref _selectedHistoryMaintenanceType, value);
    }

    public string HistorySearchText
    {
        get => _historySearchText;
        set => SetProperty(ref _historySearchText, value);
    }

    public string MileageInput
    {
        get => _mileageInput;
        set => SetProperty(ref _mileageInput, value);
    }

    public string NextMileageInput
    {
        get => _nextMileageInput;
        set => SetProperty(ref _nextMileageInput, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string CostInput
    {
        get => _costInput;
        set => SetProperty(ref _costInput, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public int GuideRequestId
    {
        get => _guideRequestId;
        private set => SetProperty(ref _guideRequestId, value);
    }

    public override async Task RefreshAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        try
        {
            var activeVehicleIds = await LoadActiveVehicleIdsAsync();
            var vehicles = await LoadVehicleOptionsAsync(activeVehicleIds);
            PopulateVehicleCollections(vehicles);
            await LoadHistoryMaintenanceTypeOptionsAsync();
            await LoadHistoryRecordsAsync();
            await LoadDueItemsAsync();
            MarkDataLoaded();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task AddRecordAsync()
    {
        StatusMessage = string.Empty;

        if (SelectedVehicle is null)
        {
            StatusMessage = "Оберіть авто.";
            return;
        }

        if (!int.TryParse(MileageInput, out var mileage) || mileage <= 0)
        {
            StatusMessage = "Некоректний пробіг.";
            return;
        }

        if (!int.TryParse(NextMileageInput, out var nextMileage) || nextMileage <= mileage)
        {
            StatusMessage = "Некоректний пробіг наступного ТО.";
            return;
        }

        if (!decimal.TryParse(CostInput, NumberStyles.Number, CultureInfo.InvariantCulture, out var cost) &&
            !decimal.TryParse(CostInput, NumberStyles.Number, CultureInfo.CurrentCulture, out cost))
        {
            StatusMessage = "Некоректна вартість ТО.";
            return;
        }

        if (cost <= 0)
        {
            StatusMessage = "Вартість ТО має бути більшою за 0.";
            return;
        }

        var result = await _maintenanceService.AddRecordAsync(
            new MaintenanceRequest(
                SelectedVehicle.Id,
                _currentEmployee.Id,
                DateTime.Today,
                mileage,
                Description,
                cost,
                nextMileage,
                null,
                MaintenanceTypes.Scheduled,
                null));
        StatusMessage = result.Message;

        if (result.Success)
        {
            MileageInput = string.Empty;
            NextMileageInput = string.Empty;
            CostInput = string.Empty;
            _refreshCoordinator.Invalidate(PageRefreshArea.Fleet);
            await RefreshAsync();
        }
    }

    private async Task ApplyHistoryFiltersAsync()
    {
        if (!ValidateHistoryDateRange())
        {
            return;
        }

        StatusMessage = string.Empty;
        await LoadHistoryRecordsAsync();
    }

    private async Task ClearHistoryFiltersAsync()
    {
        SelectedHistoryVehicleId = null;
        HistoryDateFrom = null;
        HistoryDateTo = null;
        SelectedHistoryMaintenanceType = null;
        HistorySearchText = string.Empty;
        StatusMessage = string.Empty;
        await LoadHistoryRecordsAsync();
    }

    private async Task OpenVehicleFromMaintenanceAsync(int vehicleId)
    {
        if (vehicleId <= 0)
        {
            return;
        }

        if (OpenVehicleRequestedAsync is null)
        {
            StatusMessage = "Перехід до автопарку недоступний.";
            return;
        }

        await OpenVehicleRequestedAsync(vehicleId, true);
    }

    private async Task<HashSet<int>> LoadActiveVehicleIdsAsync()
    {
        var today = DateTime.Today;
        var activeVehicleIds = await _dbContext.Rentals
            .AsNoTracking()
            .Where(item => item.StatusId == RentalStatus.Active && item.StartDate <= today && today <= item.EndDate)
            .Select(item => item.VehicleId)
            .Distinct()
            .ToListAsync();

        return activeVehicleIds.ToHashSet();
    }

    private async Task<List<VehicleOption>> LoadVehicleOptionsAsync(IReadOnlySet<int> activeVehicleIds)
    {
        var vehicles = await _dbContext.Vehicles
            .AsNoTracking()
            .Include(item => item.MakeLookup)
            .Include(item => item.ModelLookup)
            .OrderBy(item => item.MakeLookup!.Name)
            .ThenBy(item => item.ModelLookup!.Name)
            .ThenBy(item => item.LicensePlate)
            .ToListAsync();

        return vehicles
            .Select(vehicle =>
            {
                var statusDisplay = ResolveVehicleStatusDisplay(vehicle.VehicleStatusCode, activeVehicleIds.Contains(vehicle.Id));
                var baseDisplay = $"{vehicle.MakeName} {vehicle.ModelName} [{vehicle.LicensePlate}]";
                return new VehicleOption(
                    vehicle.Id,
                    $"{baseDisplay} - {statusDisplay}",
                    vehicle.VehicleStatusCode,
                    statusDisplay,
                    baseDisplay);
            })
            .ToList();
    }

    private void PopulateVehicleCollections(IReadOnlyList<VehicleOption> vehicles)
    {
        var selectedVehicleId = SelectedVehicle?.Id;

        Vehicles.Clear();
        foreach (var vehicle in vehicles)
        {
            Vehicles.Add(vehicle);
        }

        SelectedVehicle = selectedVehicleId.HasValue
            ? Vehicles.FirstOrDefault(item => item.Id == selectedVehicleId.Value) ?? Vehicles.FirstOrDefault()
            : Vehicles.FirstOrDefault();

        HistoryVehicleOptions.Clear();
        HistoryVehicleOptions.Add(new HistoryVehicleFilterOption(null, AllVehiclesOption));
        foreach (var vehicle in vehicles)
        {
            HistoryVehicleOptions.Add(new HistoryVehicleFilterOption(vehicle.Id, vehicle.BaseDisplay));
        }

        if (SelectedHistoryVehicleId.HasValue &&
            HistoryVehicleOptions.All(item => item.Id != SelectedHistoryVehicleId.Value))
        {
            SelectedHistoryVehicleId = null;
        }
    }

    private async Task LoadHistoryMaintenanceTypeOptionsAsync()
    {
        var types = await _dbContext.MaintenanceTypes
            .AsNoTracking()
            .OrderBy(item => item.DisplayName)
            .Select(item => item.Code)
            .ToListAsync();

        HistoryMaintenanceTypeOptions.Clear();
        HistoryMaintenanceTypeOptions.Add(new HistoryMaintenanceTypeOption(null, AllMaintenanceTypesOption));
        foreach (var typeCode in types)
        {
            HistoryMaintenanceTypeOptions.Add(new HistoryMaintenanceTypeOption(typeCode, typeCode.ToDisplayMaintenanceType()));
        }

        if (!string.IsNullOrWhiteSpace(SelectedHistoryMaintenanceType) &&
            HistoryMaintenanceTypeOptions.All(item => !string.Equals(item.Code, SelectedHistoryMaintenanceType, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedHistoryMaintenanceType = null;
        }
    }

    private async Task LoadHistoryRecordsAsync()
    {
        var query = _dbContext.MaintenanceRecords
            .AsNoTracking()
            .Select(item => new
            {
                item.Id,
                item.VehicleId,
                VehicleDisplay = item.Vehicle != null
                    ? item.Vehicle.MakeLookup!.Name + " " + item.Vehicle.ModelLookup!.Name + " [" + item.Vehicle.LicensePlate + "]"
                    : string.Empty,
                VehicleMake = item.Vehicle != null ? item.Vehicle.MakeLookup!.Name : string.Empty,
                VehicleModel = item.Vehicle != null ? item.Vehicle.ModelLookup!.Name : string.Empty,
                VehicleLicensePlate = item.Vehicle != null ? item.Vehicle.LicensePlate : string.Empty,
                item.ServiceDate,
                item.MileageAtService,
                item.NextServiceMileage,
                item.Cost,
                item.MaintenanceTypeCode,
                item.Description
            });

        if (SelectedHistoryVehicleId.HasValue)
        {
            query = query.Where(item => item.VehicleId == SelectedHistoryVehicleId.Value);
        }

        if (HistoryDateFrom.HasValue)
        {
            var dateFrom = HistoryDateFrom.Value.Date;
            query = query.Where(item => item.ServiceDate >= dateFrom);
        }

        if (HistoryDateTo.HasValue)
        {
            var dateTo = HistoryDateTo.Value.Date;
            query = query.Where(item => item.ServiceDate <= dateTo);
        }

        if (!string.IsNullOrWhiteSpace(SelectedHistoryMaintenanceType))
        {
            var normalizedType = SelectedHistoryMaintenanceType.Trim().ToUpperInvariant();
            query = query.Where(item => item.MaintenanceTypeCode == normalizedType);
        }

        var searchText = HistorySearchText.Trim();
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var pattern = $"%{searchText}%";
            query = query.Where(item =>
                EF.Functions.ILike(item.VehicleMake, pattern) ||
                EF.Functions.ILike(item.VehicleModel, pattern) ||
                EF.Functions.ILike(item.VehicleLicensePlate, pattern) ||
                EF.Functions.ILike(item.Description, pattern));
        }

        var records = await query
            .OrderByDescending(item => item.ServiceDate)
            .ThenByDescending(item => item.Id)
            .ToListAsync();

        Records.Clear();
        foreach (var record in records)
        {
            Records.Add(new RecordRow(
                record.Id,
                record.VehicleId,
                record.VehicleDisplay,
                record.ServiceDate,
                record.MileageAtService,
                record.NextServiceMileage,
                record.Cost,
                record.MaintenanceTypeCode,
                record.MaintenanceTypeCode.ToDisplayMaintenanceType(),
                record.Description));
        }
    }

    private async Task LoadDueItemsAsync()
    {
        var dueItems = await _maintenanceService.GetDueItemsAsync();
        DueItems.Clear();
        foreach (var due in dueItems)
        {
            DueItems.Add(new DueRow(
                due.VehicleId,
                due.Vehicle,
                due.CurrentMileage,
                due.NextServiceMileage,
                due.NextServiceDate,
                due.DistanceToNextServiceKm,
                due.DaysToNextService,
                due.ForecastStatus,
                due.ForecastNotes));
        }
    }

    private bool ValidateHistoryDateRange()
    {
        if (HistoryDateFrom.HasValue &&
            HistoryDateTo.HasValue &&
            HistoryDateFrom.Value.Date > HistoryDateTo.Value.Date)
        {
            StatusMessage = "Початок періоду не може бути пізніше завершення.";
            return false;
        }

        return true;
    }

    private void RequestGuide()
    {
        GuideRequestId++;
    }

    public void ReleaseTransientState()
    {
        StatusMessage = string.Empty;
    }

    private static string ResolveVehicleStatusDisplay(string? vehicleStatusCode, bool hasActiveRental)
    {
        if (hasActiveRental)
        {
            return VehicleActiveRentalStatus;
        }

        return vehicleStatusCode?.Trim().ToUpperInvariant() switch
        {
            VehicleStatuses.Ready => VehicleAvailableStatus,
            VehicleStatuses.Maintenance => VehicleMaintenanceStatus,
            _ => VehicleUnavailableStatus
        };
    }

    public sealed record VehicleOption(
        int Id,
        string Display,
        string VehicleStatusCode,
        string VehicleStatusDisplay,
        string BaseDisplay);

    public sealed record HistoryVehicleFilterOption(int? Id, string Display);

    public sealed record HistoryMaintenanceTypeOption(string? Code, string Display);

    public sealed record RecordRow(
        int Id,
        int VehicleId,
        string Vehicle,
        DateTime ServiceDate,
        int MileageAtService,
        int? NextServiceMileage,
        decimal Cost,
        string MaintenanceTypeCode,
        string MaintenanceTypeDisplay,
        string Description)
    {
        public string MileageDisplay => $"{MileageAtService:N0} км";

        public string NextServiceMileageDisplay => NextServiceMileage.HasValue
            ? $"{NextServiceMileage.Value:N0} км"
            : "-";

        public string CostDisplay => $"{Cost:N2} грн";
    }

    public sealed record DueRow(
        int VehicleId,
        string Vehicle,
        int CurrentMileage,
        int? NextServiceMileage,
        DateTime? NextServiceDate,
        int? DistanceToNextServiceKm,
        int? DaysToNextService,
        MaintenanceForecastStatus ForecastStatus,
        string ForecastNotes)
    {
        public string CurrentMileageDisplay => $"{CurrentMileage:N0} км";

        public string NextServiceMileageDisplay => NextServiceMileage.HasValue
            ? $"{NextServiceMileage.Value:N0} км"
            : "-";

        public string DistanceToNextServiceDisplay
        {
            get
            {
                if (!DistanceToNextServiceKm.HasValue)
                {
                    return "-";
                }

                return DistanceToNextServiceKm.Value >= 0
                    ? $"{DistanceToNextServiceKm.Value:N0} км"
                    : $"прострочено на {Math.Abs(DistanceToNextServiceKm.Value):N0} км";
            }
        }

        public string DaysToNextServiceDisplay
        {
            get
            {
                if (!DaysToNextService.HasValue)
                {
                    return ForecastStatus == MaintenanceForecastStatus.Overdue ? "прострочено" : "-";
                }

                return DaysToNextService.Value >= 0
                    ? $"{DaysToNextService.Value:N0} дн."
                    : $"прострочено на {Math.Abs(DaysToNextService.Value):N0} дн.";
            }
        }

        public string ForecastStatusDisplay => ForecastStatus switch
        {
            MaintenanceForecastStatus.Overdue => "Прострочено",
            MaintenanceForecastStatus.Soon => "Скоро",
            _ => "За планом"
        };
    }
}
