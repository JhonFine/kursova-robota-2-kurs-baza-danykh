namespace CarRental.Desktop.Models;

public sealed class MaintenanceRecord
{
    public int Id { get; set; }

    public int VehicleId { get; set; }

    public DateTime ServiceDate { get; set; } = DateTime.UtcNow;

    public int MileageAtService { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Cost { get; set; }

    public int NextServiceMileage { get; set; }

    public Vehicle? Vehicle { get; set; }
}
