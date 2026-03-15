namespace CarRental.Desktop.Models;

public sealed record AddVehicleDraft(
    string Make,
    string Model,
    string EngineDisplay,
    string FuelType,
    string TransmissionType,
    string DoorsCount,
    string CargoCapacityDisplay,
    string ConsumptionDisplay,
    bool HasAirConditioning,
    string LicensePlate,
    string Mileage,
    string DailyRate,
    string ServiceIntervalKm,
    string? PhotoPath);
