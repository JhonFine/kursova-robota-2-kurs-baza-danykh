using CarRental.Desktop.Data;
using CarRental.Desktop.Localization;
using CarRental.Desktop.Services.Damages;
using CarRental.Shared.ReferenceData;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Globalization;

namespace CarRental.Desktop.ViewModels;

// Екран пошкоджень тримає зв'язок між авто, конкретною орендою і можливим автоматичним донарахуванням,
// тому вибір vehicle/rental синхронізується обережно, без зайвих повторних запитів.
public sealed class DamagesPageViewModel : PageDataViewModelBase, ITransientStateOwner
{
    private static readonly RentalOption SelectVehiclePromptRental = new(null, "Спершу оберіть авто");
    private static readonly RentalOption UnboundRental = new(null, "Без прив'язки до оренди");

    private readonly RentalDbContext _dbContext;
    private readonly IDamageService _damageService;
    private readonly PageRefreshCoordinator _refreshCoordinator;
    private bool _isLoading;
    private VehicleOption? _selectedVehicle;
    private RentalOption? _selectedRental;
    private bool _suppressVehicleSelectionRefresh;
    private string _description = DemoSeedReferenceData.DefaultDamageDescription;
    private string _repairCostInput = string.Empty;
    private string _photoPath = string.Empty;
    private bool _autoChargeToRental;
    private string _statusMessage = string.Empty;
    private int _guideRequestId;
    // Версія запиту відсікає застарілі відповіді, якщо користувач швидко перемикає автомобілі.
    private int _rentalLoadVersion;

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
        ResetRentalsToVehiclePrompt();
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
        set
        {
            if (!SetProperty(ref _selectedVehicle, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanSelectRental));

            if (_suppressVehicleSelectionRefresh)
            {
                return;
            }

            _ = RefreshRentalsForVehicleAsync(value?.Id);
        }
    }

    public RentalOption? SelectedRental
    {
        get => _selectedRental;
        set => SetProperty(ref _selectedRental, value);
    }

    public bool CanSelectRental => SelectedVehicle is not null;

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
            var selectedVehicleId = SelectedVehicle?.Id;
            var selectedRentalId = SelectedRental?.Id;

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

            var restoredVehicle = selectedVehicleId.HasValue
                ? Vehicles.FirstOrDefault(item => item.Id == selectedVehicleId.Value)
                : null;

            _suppressVehicleSelectionRefresh = true;
            try
            {
                SelectedVehicle = restoredVehicle;
            }
            finally
            {
                _suppressVehicleSelectionRefresh = false;
            }

            await RefreshRentalsForVehicleAsync(restoredVehicle?.Id, restoredVehicle is not null ? selectedRentalId : null);

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
                    item.Photos
                        .OrderBy(photo => photo.SortOrder)
                        .Select(photo => photo.StoredPath)
                        .FirstOrDefault() ?? string.Empty))
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

    private async Task RefreshRentalsForVehicleAsync(int? vehicleId, int? preferredRentalId = null)
    {
        var loadVersion = Interlocked.Increment(ref _rentalLoadVersion);
        if (!vehicleId.HasValue)
        {
            ResetRentalsToVehiclePrompt();
            return;
        }

        try
        {
            var rentals = await _dbContext.Rentals
                .AsNoTracking()
                .Where(item => item.VehicleId == vehicleId.Value)
                .OrderByDescending(item => item.StartDate)
                .Take(200)
                .Select(item => new RentalOption(item.Id, $"{item.ContractNumber} ({item.Status.ToDisplay()})"))
                .ToListAsync();

            if (loadVersion != _rentalLoadVersion || SelectedVehicle?.Id != vehicleId.Value)
            {
                return;
            }

            Rentals.Clear();
            Rentals.Add(UnboundRental);
            foreach (var rental in rentals)
            {
                Rentals.Add(rental);
            }

            SelectedRental = preferredRentalId.HasValue
                ? Rentals.FirstOrDefault(item => item.Id == preferredRentalId.Value) ?? UnboundRental
                : UnboundRental;
        }
        catch
        {
            if (loadVersion != _rentalLoadVersion)
            {
                return;
            }

            ResetRentalsToVehiclePrompt();
            StatusMessage = "Не вдалося завантажити договори для вибраного авто.";
        }
    }

    private void ResetRentalsToVehiclePrompt()
    {
        Rentals.Clear();
        Rentals.Add(SelectVehiclePromptRental);
        SelectedRental = SelectVehiclePromptRental;
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
