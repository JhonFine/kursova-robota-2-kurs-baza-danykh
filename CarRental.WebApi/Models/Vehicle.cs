namespace CarRental.WebApi.Models;

public sealed class Vehicle
{
    public int Id { get; set; }

    public string Make { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string EngineDisplay { get; set; } = string.Empty;

    public string FuelType { get; set; } = string.Empty;

    public string TransmissionType { get; set; } = string.Empty;

    public int DoorsCount { get; set; } = 4;

    public string CargoCapacityDisplay { get; set; } = string.Empty;

    public string ConsumptionDisplay { get; set; } = string.Empty;

    public bool HasAirConditioning { get; set; } = true;

    public string LicensePlate { get; set; } = string.Empty;

    public int Mileage { get; set; }

    public decimal DailyRate { get; set; }

    public bool IsAvailable { get; set; } = true;

    public int ServiceIntervalKm { get; set; } = 10000;

    public string? PhotoPath { get; set; }

    public ICollection<Rental> Rentals { get; set; } = new List<Rental>();

    public ICollection<Damage> Damages { get; set; } = new List<Damage>();

    public ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = new List<MaintenanceRecord>();
}

