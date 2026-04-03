using CarRental.Desktop.Data;
using CarRental.Desktop.Infrastructure;
using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Analytics;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.IO;

namespace CarRental.Desktop.ViewModels;

// Р—РІС–С‚РЅР° СЃС‚РѕСЂС–РЅРєР° РЅР°РІРјРёСЃРЅРѕ Р±СѓРґСѓС” Р»РµРіРєРёР№ Р°РіСЂРµРіРѕРІР°РЅРёР№ snapshot,
// Р° РґРµС‚Р°Р»С–Р·РѕРІР°РЅРёР№ РµРєСЃРїРѕСЂС‚ РІС–РґРґР°С” РѕРєСЂРµРјРѕРјСѓ СЃРµСЂРІС–СЃСѓ, С‰РѕР± UI РЅРµ РґСѓР±Р»СЋРІР°РІ CSV/XLSX Р»РѕРіС–РєСѓ.
public sealed class ReportsPageViewModel : PageDataViewModelBase, ITransientStateOwner
{
    private readonly RentalDbContext _dbContext;
    private readonly IAnalyticsExportService _analyticsExportService;
    private bool _isLoading;
    private int _totalRentals;
    private int _activeRentals;
    private decimal _totalRevenue;
    private decimal _totalDamageCost;
    private DateTime _fromDate = DateTime.Today.AddMonths(-1);
    private DateTime _toDate = DateTime.Today;
    private VehicleFilterOption? _selectedVehicle;
    private EmployeeFilterOption? _selectedEmployee;
    private string _statusMessage = string.Empty;
    private int _guideRequestId;

    public ReportsPageViewModel(RentalDbContext dbContext, IAnalyticsExportService analyticsExportService)
    {
        _dbContext = dbContext;
        _analyticsExportService = analyticsExportService;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsLoading);
        ExportCsvCommand = new AsyncRelayCommand(ExportCsvAsync, () => !IsLoading);
        ExportExcelCommand = new AsyncRelayCommand(ExportExcelAsync, () => !IsLoading);
        RequestGuideCommand = new RelayCommand(RequestGuide);
    }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand ExportCsvCommand { get; }

    public IAsyncRelayCommand ExportExcelCommand { get; }

    public IRelayCommand RequestGuideCommand { get; }

    public ObservableCollection<VehicleFilterOption> VehicleFilters { get; } = [];

    public ObservableCollection<EmployeeFilterOption> EmployeeFilters { get; } = [];

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                ExportCsvCommand.NotifyCanExecuteChanged();
                ExportExcelCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public int TotalRentals
    {
        get => _totalRentals;
        private set => SetProperty(ref _totalRentals, value);
    }

    public int ActiveRentals
    {
        get => _activeRentals;
        private set => SetProperty(ref _activeRentals, value);
    }

    public decimal TotalRevenue
    {
        get => _totalRevenue;
        private set => SetProperty(ref _totalRevenue, value);
    }

    public decimal TotalDamageCost
    {
        get => _totalDamageCost;
        private set => SetProperty(ref _totalDamageCost, value);
    }

    public DateTime FromDate
    {
        get => _fromDate;
        set => SetProperty(ref _fromDate, value);
    }

    public DateTime ToDate
    {
        get => _toDate;
        set => SetProperty(ref _toDate, value);
    }

    public VehicleFilterOption? SelectedVehicle
    {
        get => _selectedVehicle;
        set => SetProperty(ref _selectedVehicle, value);
    }

    public EmployeeFilterOption? SelectedEmployee
    {
        get => _selectedEmployee;
        set => SetProperty(ref _selectedEmployee, value);
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

        // РћРґРёРЅ refresh С„РѕСЂРјСѓС” С– KPI-РєР°СЂС‚РєРё, С– РґРѕРІС–РґРЅРёРєРё С„С–Р»СЊС‚СЂС–РІ, С‰РѕР± РµРєСЃРїРѕСЂС‚ РїСЂР°С†СЋРІР°РІ Р· С‚РёРј СЃР°РјРёРј Р·СЂС–Р·РѕРј РґР°РЅРёС….
        IsLoading = true;
        try
        {
            var today = DateTime.Today;

            TotalRentals = await _dbContext.Rentals.AsNoTracking().CountAsync();
            ActiveRentals = await _dbContext.Rentals
                .AsNoTracking()
                .CountAsync(rental => rental.StatusId == RentalStatus.Active && rental.StartDate <= today && today <= rental.EndDate);

            TotalRevenue = await _dbContext.Rentals
                .AsNoTracking()
                .Where(rental => rental.StatusId == RentalStatus.Closed)
                .SumAsync(rental => (decimal?)rental.TotalAmount) ?? 0m;

            TotalDamageCost = await _dbContext.Damages
                .AsNoTracking()
                .SumAsync(damage => (decimal?)damage.RepairCost) ?? 0m;

            var vehicles = await _dbContext.Vehicles
                .AsNoTracking()
                .Include(item => item.MakeLookup)
                .Include(item => item.ModelLookup)
                .OrderBy(item => item.MakeLookup!.Name)
                .ThenBy(item => item.ModelLookup!.Name)
                .ToListAsync();
            var employees = await StaffVisibilityQuery.VisibleStaff(_dbContext)
                .OrderBy(item => item.FullName)
                .Select(item => new EmployeeFilterOption(item.Id, item.FullName))
                .ToListAsync();

            VehicleFilters.Clear();
            VehicleFilters.Add(new VehicleFilterOption(null, "Усі авто"));
            foreach (var vehicle in vehicles)
            {
                VehicleFilters.Add(new VehicleFilterOption(vehicle.Id, $"{vehicle.MakeName} {vehicle.ModelName} [{vehicle.LicensePlate}]"));
            }

            EmployeeFilters.Clear();
            EmployeeFilters.Add(new EmployeeFilterOption(null, "Усі співробітники"));
            foreach (var employee in employees)
            {
                EmployeeFilters.Add(employee);
            }

            SelectedVehicle ??= VehicleFilters.FirstOrDefault();
            SelectedEmployee ??= EmployeeFilters.FirstOrDefault();
            MarkDataLoaded();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ExportCsvAsync()
    {
        var path = await _analyticsExportService.ExportRentalsCsvAsync(BuildRequest());
        StatusMessage = $"CSV експортовано: {Path.GetFileName(path)}";
    }

    private async Task ExportExcelAsync()
    {
        var path = await _analyticsExportService.ExportRentalsExcelAsync(BuildRequest());
        StatusMessage = $"Excel експортовано: {Path.GetFileName(path)}";
    }

    private ExportRequest BuildRequest()
    {
        return new ExportRequest(
            FromDate,
            ToDate,
            SelectedVehicle?.Id,
            SelectedEmployee?.Id);
    }

    private void RequestGuide()
    {
        GuideRequestId++;
    }

    public void ReleaseTransientState()
    {
        StatusMessage = string.Empty;
    }

    public sealed record VehicleFilterOption(int? Id, string Label);

    public sealed record EmployeeFilterOption(int? Id, string Label);
}

