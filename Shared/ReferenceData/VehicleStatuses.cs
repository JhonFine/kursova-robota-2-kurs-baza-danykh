namespace CarRental.Shared.ReferenceData;

public static class VehicleStatuses
{
    public const string Ready = "READY";
    public const string Rented = "RENTED";
    public const string Maintenance = "MAINTENANCE";
    public const string Damaged = "DAMAGED";
    public const string Inactive = "INACTIVE";

    private static readonly HashSet<string> AllowedCodes =
    [
        Ready,
        Rented,
        Maintenance,
        Damaged,
        Inactive
    ];

    public static IReadOnlySet<string> All => AllowedCodes;

    public static bool IsValid(string? code)
        => !string.IsNullOrWhiteSpace(code) && AllowedCodes.Contains(code.Trim().ToUpperInvariant());
}
