using System.Globalization;
using System.Text.RegularExpressions;

namespace CarRental.Shared.ReferenceData;

public static class VehicleSpecificationUnits
{
    public const string Liters = "L";
    public const string KilowattHours = "KWH";
    public const string Kilograms = "KG";
    public const string Seats = "SEATS";
    public const string LitersPer100Km = "L_PER_100KM";
    public const string KilowattHoursPer100Km = "KWH_PER_100KM";

    private static readonly HashSet<string> PowertrainUnitSet = [Liters, KilowattHours];
    private static readonly HashSet<string> CargoUnitSet = [Liters, Kilograms, Seats];
    private static readonly HashSet<string> ConsumptionUnitSet = [LitersPer100Km, KilowattHoursPer100Km];

    public static IReadOnlySet<string> PowertrainUnits => PowertrainUnitSet;

    public static IReadOnlySet<string> CargoUnits => CargoUnitSet;

    public static IReadOnlySet<string> ConsumptionUnits => ConsumptionUnitSet;

    public static bool IsValidPowertrainUnit(string? value)
        => !string.IsNullOrWhiteSpace(value) && PowertrainUnitSet.Contains(value.Trim().ToUpperInvariant());

    public static bool IsValidCargoUnit(string? value)
        => !string.IsNullOrWhiteSpace(value) && CargoUnitSet.Contains(value.Trim().ToUpperInvariant());

    public static bool IsValidConsumptionUnit(string? value)
        => !string.IsNullOrWhiteSpace(value) && ConsumptionUnitSet.Contains(value.Trim().ToUpperInvariant());
}

public static class VehicleSpecifications
{
    private static readonly Regex DecimalPattern = new(@"-?\d+(?:[.,]\d+)?", RegexOptions.Compiled);

    public static bool TryParsePowertrain(string? value, out decimal parsedValue, out string unit)
        => TryParse(value, ResolvePowertrainUnit, out parsedValue, out unit);

    public static bool TryParseCargoCapacity(string? value, out decimal parsedValue, out string unit)
        => TryParse(value, ResolveCargoUnit, out parsedValue, out unit);

    public static bool TryParseConsumption(string? value, out decimal parsedValue, out string unit)
        => TryParse(value, ResolveConsumptionUnit, out parsedValue, out unit);

    public static string FormatPowertrain(decimal value, string? unit)
    {
        return NormalizeUnit(unit) switch
        {
            VehicleSpecificationUnits.Liters => $"{FormatNumber(value)} л",
            VehicleSpecificationUnits.KilowattHours => $"{FormatNumber(value)} кВт·год",
            _ => FormatNumber(value)
        };
    }

    public static string FormatCargoCapacity(decimal value, string? unit)
    {
        return NormalizeUnit(unit) switch
        {
            VehicleSpecificationUnits.Liters => $"{FormatNumber(value)} л",
            VehicleSpecificationUnits.Kilograms => $"{FormatNumber(value)} кг",
            VehicleSpecificationUnits.Seats => $"{FormatNumber(value)} місць",
            _ => FormatNumber(value)
        };
    }

    public static string FormatConsumption(decimal value, string? unit)
    {
        return NormalizeUnit(unit) switch
        {
            VehicleSpecificationUnits.LitersPer100Km => $"{FormatNumber(value)} л/100 км",
            VehicleSpecificationUnits.KilowattHoursPer100Km => $"{FormatNumber(value)} кВт·год/100 км",
            _ => FormatNumber(value)
        };
    }

    private static bool TryParse(
        string? rawValue,
        Func<string, string?> unitResolver,
        out decimal parsedValue,
        out string unit)
    {
        parsedValue = 0m;
        unit = string.Empty;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var normalized = rawValue.Trim();
        var match = DecimalPattern.Match(normalized);
        if (!match.Success ||
            !decimal.TryParse(
                match.Value.Replace(',', '.'),
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out parsedValue) ||
            parsedValue <= 0m)
        {
            return false;
        }

        unit = unitResolver(normalized) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(unit);
    }

    private static string? ResolvePowertrainUnit(string value)
    {
        var normalized = NormalizeInput(value);
        if (normalized.Contains("КВТ", StringComparison.Ordinal) ||
            normalized.Contains("KWH", StringComparison.Ordinal))
        {
            return VehicleSpecificationUnits.KilowattHours;
        }

        if (normalized.Contains('Л') || normalized.Contains('L'))
        {
            return VehicleSpecificationUnits.Liters;
        }

        return null;
    }

    private static string? ResolveCargoUnit(string value)
    {
        var normalized = NormalizeInput(value);
        if (normalized.Contains("МІС", StringComparison.Ordinal) ||
            normalized.Contains("SEAT", StringComparison.Ordinal))
        {
            return VehicleSpecificationUnits.Seats;
        }

        if (normalized.Contains("КГ", StringComparison.Ordinal) ||
            normalized.Contains("KG", StringComparison.Ordinal))
        {
            return VehicleSpecificationUnits.Kilograms;
        }

        if (normalized.Contains('Л') || normalized.Contains('L'))
        {
            return VehicleSpecificationUnits.Liters;
        }

        return null;
    }

    private static string? ResolveConsumptionUnit(string value)
    {
        var normalized = NormalizeInput(value);
        if (normalized.Contains("КВТ", StringComparison.Ordinal) ||
            normalized.Contains("KWH", StringComparison.Ordinal))
        {
            return VehicleSpecificationUnits.KilowattHoursPer100Km;
        }

        if (normalized.Contains('Л') || normalized.Contains('L'))
        {
            return VehicleSpecificationUnits.LitersPer100Km;
        }

        return null;
    }

    private static string NormalizeInput(string value)
        => value
            .Trim()
            .ToUpperInvariant()
            .Replace(" ", string.Empty, StringComparison.Ordinal);

    private static string NormalizeUnit(string? value)
        => value?.Trim().ToUpperInvariant() ?? string.Empty;

    private static string FormatNumber(decimal value)
        => value.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');
}
