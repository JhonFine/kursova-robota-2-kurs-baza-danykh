namespace CarRental.WebApi.Models;

public sealed record AddVehicleDraft(
    string Make,
    string Model,
    string LicensePlate,
    string Mileage,
    string DailyRate,
    string ServiceIntervalKm,
    string? PhotoPath);

