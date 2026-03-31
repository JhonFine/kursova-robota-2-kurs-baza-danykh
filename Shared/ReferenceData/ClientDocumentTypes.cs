namespace CarRental.Shared.ReferenceData;

public static class ClientDocumentTypes
{
    public const string Passport = "PASSPORT";
    public const string DriverLicense = "DRIVER_LICENSE";

    private static readonly HashSet<string> AllowedCodes =
    [
        Passport,
        DriverLicense
    ];

    public static IReadOnlySet<string> All => AllowedCodes;

    public static bool IsValid(string? code)
        => !string.IsNullOrWhiteSpace(code) && AllowedCodes.Contains(code.Trim().ToUpperInvariant());
}
