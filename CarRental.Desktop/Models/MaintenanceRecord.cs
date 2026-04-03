using CarRental.Shared.ReferenceData;

namespace CarRental.Desktop.Models;

public sealed class MaintenanceRecord
{
    public int Id { get; set; }

    public int VehicleId { get; set; }

    public int? PerformedByEmployeeId { get; set; }

    public DateTime ServiceDate { get; set; } = DateTime.UtcNow;

    public int MileageAtService { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Cost { get; set; }

    public int? NextServiceMileage { get; set; }

    public DateTime? NextServiceDate { get; set; }

    public string MaintenanceTypeCode { get; set; } = MaintenanceTypes.Scheduled;

    public string? ServiceProviderName { get; set; }

    public Vehicle? Vehicle { get; set; }

    public Employee? PerformedByEmployee { get; set; }

    public MaintenanceTypeLookup? MaintenanceType { get; set; }
}

