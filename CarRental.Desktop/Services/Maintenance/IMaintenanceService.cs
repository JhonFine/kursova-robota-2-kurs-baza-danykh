namespace CarRental.Desktop.Services.Maintenance;

public interface IMaintenanceService
{
    Task<MaintenanceResult> AddRecordAsync(MaintenanceRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MaintenanceDueItem>> GetDueItemsAsync(CancellationToken cancellationToken = default);
}

public sealed record MaintenanceRequest(
    int VehicleId,
    DateTime ServiceDate,
    int MileageAtService,
    string Description,
    decimal Cost,
    int NextServiceMileage);

public sealed record MaintenanceResult(bool Success, string Message);

public sealed record MaintenanceDueItem(
    int VehicleId,
    string Vehicle,
    int CurrentMileage,
    int NextServiceMileage,
    int OverdueByKm);
