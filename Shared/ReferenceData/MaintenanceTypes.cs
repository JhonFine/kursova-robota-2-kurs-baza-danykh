namespace CarRental.Shared.ReferenceData;

public static class MaintenanceTypes
{
    public const string Scheduled = "SCHEDULED";
    public const string Repair = "REPAIR";
    public const string Tires = "TIRES";
    public const string Inspection = "INSPECTION";

    private static readonly HashSet<string> AllowedCodes =
    [
        Scheduled,
        Repair,
        Tires,
        Inspection
    ];

    public static IReadOnlySet<string> All => AllowedCodes;

    public static bool IsValid(string? code)
        => !string.IsNullOrWhiteSpace(code) && AllowedCodes.Contains(code.Trim().ToUpperInvariant());
}
