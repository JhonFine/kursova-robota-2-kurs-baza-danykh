using CarRental.Desktop.Data;
using CarRental.Desktop.Localization;
using CarRental.Desktop.Services.Damages;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Globalization;

namespace CarRental.Desktop.ViewModels;

public sealed class DamagesPageViewModel : PageDataViewModelBase, ITransientStateOwner
{
    private readonly RentalDbContext _dbContext;
    private readonly IDamageService _damageService;
    private readonly PageRefreshCoordinator _refreshCoordinator;
    private bool _isLoading;
    private VehicleOption? _selectedVehicle;
    private RentalOption? _selectedRental;
    private string _description = "Пошкодження кузова";
    private string _repairCostInput = string.Empty;
    private string _photoPath = string.Empty;
    private bool _autoChargeToRental;
    private string _statusMessage = string.Empty;
    private int _guideRequestId;

    public DamagesPageViewModel(
        RentalDbContext dbContext,
        IDamageService damageService,
        PageRefreshCoordinator refreshCoordinator)
    {
        _dbContext = dbContext;
        _damageService = damageService;
        _refreshCoordinator = refreshCoordinator;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsLoading);
        AddDamageCommand = new AsyncRelayCommand(AddDamageAsync, () => !IsLoading);
        RequestGuideCommand = new RelayCommand(RequestGuide);
    }

    public ObservableCollection<VehicleOption> Vehicles { get; } = [];

    public ObservableCollection<RentalOption> Rentals { get; } = [];

    public ObservableCollection<DamageRow> Damages { get; } = [];

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand AddDamageCommand { get; }

    public IRelayCommand RequestGuideCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                AddDamageCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public VehicleOption? SelectedVehicle
    {
        get => _selectedVehicle;
        set => SetProperty(ref _selectedVehicle, value);
    }

    public RentalOption? SelectedRental
    {
        get => _selectedRental;
        set => SetProperty(ref _selectedRental, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string RepairCostInput
    {
        get => _repairCostInput;
        set => SetProperty(ref _repairCostInput, value);
    }

    public string PhotoPath
    {
        get => _photoPath;
        set => SetProperty(ref _photoPath, value);
    }

    public bool AutoChargeToRental
    {
        get => _autoChargeToRental;
        set => SetProperty(ref _autoChargeToRental, value);
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

            var rentals = await _dbContext.Rentals
                .AsNoTracking()
                .OrderByDescending(item => item.StartDate)
                .Take(200)
                .ToListAsync();
            Rentals.Clear();
            Rentals.Add(new RentalOption(null, "Без прив'язки до оренди"));
            foreach (var rental in rentals)
            {
                Rentals.Add(new RentalOption(rental.Id, $"{rental.ContractNumber} ({rental.Status.ToDisplay()})"));
            }
            SelectedRental ??= Rentals.FirstOrDefault();

            var damages = await _dbContext.Damages
                .AsNoTracking()
                .OrderByDescending(item => item.DateReported)
                .Take(300)
                .Select(item => new DamageRow(
                    item.Id,
                    item.ActNumber,
                    item.Vehicle != null ? $"{item.Vehicle.Make} {item.Vehicle.Model}" : string.Empty,
                    item.Rental != null ? item.Rental.ContractNumber : "-",
                    item.DateReported,
                    item.RepairCost,
                    item.ChargedAmount,
                    item.Status.ToDisplay(),
                    item.PhotoPath ?? string.Empty))
                .ToListAsync();
            Damages.Clear();
            foreach (var damage in damages)
            {
                Damages.Add(damage);
            }

            MarkDataLoaded();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task AddDamageAsync()
    {
        StatusMessage = string.Empty;
        if (SelectedVehicle is null)
        {
            StatusMessage = "Оберіть авто.";
            return;
        }

        if (!decimal.TryParse(RepairCostInput, NumberStyles.Number, CultureInfo.InvariantCulture, out var cost) &&
            !decimal.TryParse(RepairCostInput, NumberStyles.Number, CultureInfo.CurrentCulture, out cost))
        {
            StatusMessage = "Некоректна вартість ремонту.";
            return;
        }

        var result = await _damageService.AddDamageAsync(
            new DamageRequest(
                SelectedVehicle.Id,
                SelectedRental?.Id,
                Description,
                cost,
                string.IsNullOrWhiteSpace(PhotoPath) ? null : PhotoPath,
                AutoChargeToRental));
        StatusMessage = result.Message;

        if (result.Success)
        {
            RepairCostInput = string.Empty;
            PhotoPath = string.Empty;
            _refreshCoordinator.Invalidate(PageRefreshArea.Rentals | PageRefreshArea.Prokat | PageRefreshArea.Reports);
            await RefreshAsync();
        }
    }

    private void RequestGuide()
    {
        GuideRequestId++;
    }

    public void ReleaseTransientState()
    {
        PhotoPath = string.Empty;
        StatusMessage = string.Empty;
    }

    public sealed record VehicleOption(int Id, string Display);

    public sealed record RentalOption(int? Id, string Display);

    public sealed record DamageRow(
        int Id,
        string ActNumber,
        string Vehicle,
        string ContractNumber,
        DateTime ReportedAt,
        decimal RepairCost,
        decimal ChargedAmount,
        string Status,
        string PhotoPath);
}
