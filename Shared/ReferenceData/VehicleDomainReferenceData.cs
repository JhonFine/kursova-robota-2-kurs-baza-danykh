using System.Text.RegularExpressions;

namespace CarRental.Shared.ReferenceData;

internal static partial class VehicleDomainReferenceData
{
    public const decimal MinDailyRate = 1000m;
    public const decimal MaxDailyRate = 3500m;
    public const decimal EconomyUpperBound = 1500m;
    public const decimal MidUpperBound = 2000m;
    public const decimal BusinessUpperBound = 2500m;
    public const string UaLicensePlatePattern = "^[ABCEHIKMOPTX]{2}\\d{4}[ABCEHIKMOPTX]{2}$";

    private const decimal LegacyMinDailyRate = 30m;
    private const decimal LegacyMaxDailyRate = 145m;
    private const decimal RateStep = 50m;

    public static decimal NormalizeSeedDailyRate(decimal legacyDailyRate)
    {
        var clampedLegacyRate = decimal.Clamp(legacyDailyRate, LegacyMinDailyRate, LegacyMaxDailyRate);
        var scaledRate = MinDailyRate +
            ((clampedLegacyRate - LegacyMinDailyRate) / (LegacyMaxDailyRate - LegacyMinDailyRate)) *
            (MaxDailyRate - MinDailyRate);
        var roundedRate = decimal.Round(scaledRate / RateStep, 0, MidpointRounding.AwayFromZero) * RateStep;
        return decimal.Clamp(roundedRate, MinDailyRate, MaxDailyRate);
    }

    public static string NormalizeLicensePlate(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? string.Empty : normalized;
    }

    public static bool IsValidLicensePlate(string? value)
    {
        var normalized = NormalizeLicensePlate(value);
        return !string.IsNullOrWhiteSpace(normalized) && UaLicensePlateRegex().IsMatch(normalized);
    }

    public static string ResolveVehicleClass(decimal dailyRate)
    {
        return dailyRate switch
        {
            < EconomyUpperBound => "Economy",
            < MidUpperBound => "Mid",
            < BusinessUpperBound => "Business",
            _ => "Premium"
        };
    }

    [GeneratedRegex(UaLicensePlatePattern, RegexOptions.CultureInvariant)]
    private static partial Regex UaLicensePlateRegex();
}
