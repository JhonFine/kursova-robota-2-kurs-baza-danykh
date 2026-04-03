namespace CarRental.Desktop.Services.Analytics;

public interface IAnalyticsExportService
{
    Task<string> ExportRentalsCsvAsync(ExportRequest request, CancellationToken cancellationToken = default);

    Task<string> ExportRentalsExcelAsync(ExportRequest request, CancellationToken cancellationToken = default);
}

public sealed record ExportRequest(
    DateTime FromDate,
    DateTime ToDate,
    int? VehicleId = null,
    int? EmployeeId = null);

