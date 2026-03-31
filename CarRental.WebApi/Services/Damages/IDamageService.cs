namespace CarRental.WebApi.Services.Damages;

public interface IDamageService
{
    Task<DamageResult> AddDamageAsync(DamageRequest request, CancellationToken cancellationToken = default);
}

public sealed record DamageRequest(
    int VehicleId,
    int? RentalId,
    string Description,
    decimal RepairCost,
    string? PhotoPath = null,
    bool AutoChargeToRental = false,
    int ReportedByEmployeeId = 1,
    IReadOnlyList<string>? PhotoPaths = null)
{
    public IReadOnlyList<string> ResolvedPhotoPaths => PhotoPaths is { Count: > 0 }
        ? PhotoPaths
        : string.IsNullOrWhiteSpace(PhotoPath)
            ? Array.Empty<string>()
            : new[] { PhotoPath };
}

public sealed record DamageResult(bool Success, string Message, int DamageId = 0);

