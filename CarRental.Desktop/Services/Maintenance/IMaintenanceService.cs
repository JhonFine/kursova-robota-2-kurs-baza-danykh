namespace CarRental.Desktop.Services.Maintenance;

public interface IMaintenanceService
{
    Task<MaintenanceResult> AddRecordAsync(MaintenanceRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MaintenanceDueItem>> GetDueItemsAsync(CancellationToken cancellationToken = default);
}

public enum MaintenanceForecastStatus
{
    Overdue = 0,
    Soon = 1,
    OnTrack = 2
}

public sealed record MaintenanceRequest(
    int VehicleId,
    int? PerformedByEmployeeId,
    DateTime ServiceDate,
    int MileageAtService,
    string Description,
    decimal Cost,
    int? NextServiceMileage,
    DateTime? NextServiceDate,
    string MaintenanceTypeCode,
    string? ServiceProviderName);

public sealed record MaintenanceResult(bool Success, string Message);

public sealed record MaintenanceDueItem(
    int VehicleId,
    string Vehicle,
    int CurrentMileage,
    int? NextServiceMileage,
    DateTime? NextServiceDate,
    int? DistanceToNextServiceKm,
    int? DaysToNextService,
    MaintenanceForecastStatus ForecastStatus,
    string ForecastNotes);


