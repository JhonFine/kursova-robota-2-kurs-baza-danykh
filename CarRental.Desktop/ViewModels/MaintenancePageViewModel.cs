using CarRental.Desktop.Data;
using CarRental.Desktop.Services.Maintenance;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Globalization;

namespace CarRental.Desktop.ViewModels;

public sealed class MaintenancePageViewModel : PageDataViewModelBase, ITransientStateOwner
{
    private readonly RentalDbContext _dbContext;
    private readonly IMaintenanceService _maintenanceService;
    private readonly PageRefreshCoordinator _refreshCoordinator;
    private bool _isLoading;
    private VehicleOption? _selectedVehicle;
    private string _mileageInput = string.Empty;
    private string _nextMileageInput = string.Empty;
    private string _description = "Планове техобслуговування";
    private string _costInput = string.Empty;
    private string _statusMessage = string.Empty;
    private int _guideRequestId;

    public MaintenancePageViewModel(
        RentalDbContext dbContext,
        IMaintenanceService maintenanceService,
        PageRefreshCoordinator refreshCoordinator)
    {
        _dbContext = dbContext;
        _maintenanceService = maintenanceService;
        _refreshCoordinator = refreshCoordinator;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsLoading);
        AddRecordCommand = new AsyncRelayCommand(AddRecordAsync, () => !IsLoading);
        RequestGuideCommand = new RelayCommand(RequestGuide);
    }

    public ObservableCollection<VehicleOption> Vehicles { get; } = [];

    public ObservableCollection<RecordRow> Records { get; } = [];

    public ObservableCollection<DueRow> DueItems { get; } = [];

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand AddRecordCommand { get; }

    public IRelayCommand RequestGuideCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                AddRecordCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public VehicleOption? SelectedVehicle
    {
        get => _selectedVehicle;
        set => SetProperty(ref _selectedVehicle, value);
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
            var vehicles = await _dbContext.Vehicles
                .AsNoTracking()
                .OrderBy(item => item.Make)
                .ThenBy(item => item.Model)
                .ToListAsync();

            Vehicles.Clear();
            foreach (var vehicle in vehicles)
            {
                Vehicles.Add(new VehicleOption(vehicle.Id, $"{vehicle.Make} {vehicle.Model} [{vehicle.LicensePlate}]"));
            }
            SelectedVehicle ??= Vehicles.FirstOrDefault();

            var records = await _dbContext.MaintenanceRecords
                .AsNoTracking()
                .OrderByDescending(item => item.ServiceDate)
                .Take(200)
                .Select(item => new RecordRow(
                    item.Id,
                    item.Vehicle != null ? $"{item.Vehicle.Make} {item.Vehicle.Model}" : string.Empty,
                    item.ServiceDate,
                    item.MileageAtService,
                    item.NextServiceMileage,
                    item.Cost,
                    item.Description))
                .ToListAsync();
            Records.Clear();
            foreach (var record in records)
            {
                Records.Add(record);
            }

            var dueItems = await _maintenanceService.GetDueItemsAsync();
            DueItems.Clear();
            foreach (var due in dueItems)
            {
                DueItems.Add(new DueRow(due.Vehicle, due.CurrentMileage, due.NextServiceMileage, due.OverdueByKm));
            }

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

        var result = await _maintenanceService.AddRecordAsync(
            new MaintenanceRequest(
                SelectedVehicle.Id,
                DateTime.Today,
                mileage,
                Description,
                cost,
                nextMileage));
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

    private void RequestGuide()
    {
        GuideRequestId++;
    }

    public void ReleaseTransientState()
    {
        StatusMessage = string.Empty;
    }

    public sealed record VehicleOption(int Id, string Display);

    public sealed record RecordRow(
        int Id,
        string Vehicle,
        DateTime ServiceDate,
        int MileageAtService,
        int NextServiceMileage,
        decimal Cost,
        string Description);

    public sealed record DueRow(
        string Vehicle,
        int CurrentMileage,
        int NextServiceMileage,
        int OverdueByKm);
}
