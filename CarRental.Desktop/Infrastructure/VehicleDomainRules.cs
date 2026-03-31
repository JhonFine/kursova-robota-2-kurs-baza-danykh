using CarRental.Shared.ReferenceData;

namespace CarRental.Desktop.Infrastructure;

internal static class VehicleDomainRules
{
    public const decimal MinDailyRate = VehicleDomainReferenceData.MinDailyRate;
    public const decimal MaxDailyRate = VehicleDomainReferenceData.MaxDailyRate;
    public const decimal EconomyUpperBound = VehicleDomainReferenceData.EconomyUpperBound;
    public const decimal MidUpperBound = VehicleDomainReferenceData.MidUpperBound;
    public const decimal BusinessUpperBound = VehicleDomainReferenceData.BusinessUpperBound;

    public static decimal NormalizeSeedDailyRate(decimal legacyDailyRate)
        => VehicleDomainReferenceData.NormalizeSeedDailyRate(legacyDailyRate);

    public static string NormalizeLicensePlate(string? value)
        => VehicleDomainReferenceData.NormalizeLicensePlate(value);

    public static bool IsValidLicensePlate(string? value)
        => VehicleDomainReferenceData.IsValidLicensePlate(value);

    public static string ResolveVehicleClass(decimal dailyRate)
        => VehicleDomainReferenceData.ResolveVehicleClass(dailyRate) switch
        {
            "Economy" => "Економ",
            "Mid" => "Середній",
            "Business" => "Бізнес",
            _ => "Преміум"
        };
}
