namespace CarRental.Desktop.Services.Damages;

public interface IDamageService
{
    Task<DamageResult> AddDamageAsync(DamageRequest request, CancellationToken cancellationToken = default);
}

public sealed record DamageRequest(
    int VehicleId,
    int? RentalId,
    string Description,
    decimal RepairCost,
    string? PhotoPath,
    bool AutoChargeToRental);

public sealed record DamageResult(bool Success, string Message);

