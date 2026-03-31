using CarRental.Shared.ReferenceData;
using CarRental.WebApi.Infrastructure;

namespace CarRental.WebApi.Data;

internal static class VehicleCatalogSeeds
{
    public static IReadOnlyList<CatalogVehicleSeed> All { get; } = VehicleCatalogReferenceData.All
        .Select(MapRecord)
        .ToArray();

    public static CatalogVehicleSeed? TryFindByVehicle(string? make, string? model)
    {
        var normalizedMake = NormalizeAlphaNumeric(make);
        var normalizedModel = NormalizeAlphaNumeric(model);
        if (string.IsNullOrWhiteSpace(normalizedMake) || string.IsNullOrWhiteSpace(normalizedModel))
        {
            return null;
        }

        var directMatch = All.FirstOrDefault(seed => seed.Matches(normalizedMake, normalizedModel));
        if (directMatch is not null)
        {
            return directMatch;
        }

        var swappedMatch = All.FirstOrDefault(seed => seed.Matches(normalizedModel, normalizedMake));
        if (swappedMatch is not null)
        {
            return swappedMatch;
        }

        var candidateTokens = Tokenize(make)
            .Concat(Tokenize(model))
            .Where(IsLooseMatchToken)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (candidateTokens.Length < 2)
        {
            return null;
        }

        return All
            .Select(seed => new
            {
                Seed = seed,
                Score = seed.GetLooseMatchScore(candidateTokens)
            })
            .Where(item => item.Score == candidateTokens.Length)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Seed.PopularityRank)
            .Select(item => item.Seed)
            .FirstOrDefault();
    }

    public sealed record CatalogVehicleSeed(
        string FullName,
        decimal LegacyDailyRate,
        string EngineDisplay,
        decimal PowertrainCapacityValue,
        string PowertrainCapacityUnit,
        string FuelType,
        string TransmissionType,
        int DoorsCount,
        string CargoCapacityDisplay,
        decimal CargoCapacityValue,
        string CargoCapacityUnit,
        string ConsumptionDisplay,
        decimal ConsumptionValue,
        string ConsumptionUnit,
        bool HasAirConditioning,
        int PopularityRank,
        string PrimaryBadgeText,
        string PrimaryBadgeColor,
        string SecondaryBadgeText,
        string SecondaryBadgeColor)
    {
        public string Make => SplitMakeAndModel(FullName).Make;

        public string Model => SplitMakeAndModel(FullName).Model;

        public decimal DailyRate => VehicleDomainRules.NormalizeSeedDailyRate(LegacyDailyRate);

        public string SpecificationDisplay => $"{EngineDisplay} | {FuelType} | {TransmissionType}";

        internal bool Matches(string normalizedMake, string normalizedModel)
        {
            var seedMake = NormalizeAlphaNumeric(Make);
            var seedModel = NormalizeAlphaNumeric(Model);
            if (!AreEquivalentMakes(normalizedMake, seedMake))
            {
                return false;
            }

            return normalizedModel.Equals(seedModel, StringComparison.Ordinal) ||
                   normalizedModel.Contains(seedModel, StringComparison.Ordinal) ||
                   seedModel.Contains(normalizedModel, StringComparison.Ordinal);
        }

        internal int GetLooseMatchScore(IReadOnlyCollection<string> candidateTokens)
        {
            var seedMake = NormalizeAlphaNumeric(Make);
            if (!candidateTokens.Any(token => AreEquivalentMakes(token, seedMake)))
            {
                return 0;
            }

            var seedTokens = Tokenize(FullName)
                .Where(IsLooseMatchToken)
                .ToArray();

            return candidateTokens.Count(candidateToken =>
                seedTokens.Any(seedToken => TokensOverlap(candidateToken, seedToken)));
        }
    }

    private static (string Make, string Model) SplitMakeAndModel(string fullName)
    {
        var normalized = fullName.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return ("Unknown", "Model");
        }

        if (normalized.StartsWith("Land Rover ", StringComparison.OrdinalIgnoreCase))
        {
            return ("Land Rover", normalized["Land Rover ".Length..].Trim());
        }

        var parts = normalized.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            return (parts[0], "Model");
        }

        return (parts[0], parts[1]);
    }

    private static bool AreEquivalentMakes(string left, string right)
    {
        return string.Equals(left, right, StringComparison.Ordinal) ||
               (left, right) is ("renault", "dacia") or ("dacia", "renault");
    }

    private static bool TokensOverlap(string left, string right)
    {
        return string.Equals(left, right, StringComparison.Ordinal) ||
               left.Contains(right, StringComparison.Ordinal) ||
               right.Contains(left, StringComparison.Ordinal);
    }

    private static bool IsLooseMatchToken(string token)
        => token.Length > 1 && token.Any(char.IsLetter);

    private static IEnumerable<string> Tokenize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var normalized = new string(value
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
            .ToArray());

        return normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => !string.IsNullOrWhiteSpace(token));
    }

    private static string NormalizeAlphaNumeric(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Concat(value
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit));
    }

    private static CatalogVehicleSeed MapRecord(VehicleCatalogReferenceData.VehicleCatalogRecord record)
    {
        var (powertrainCapacityValue, powertrainCapacityUnit) = ParsePowertrain(record.EngineDisplay);
        var (cargoCapacityValue, cargoCapacityUnit) = ParseCargoCapacity(record.CargoCapacityDisplay);
        var (consumptionValue, consumptionUnit) = ParseConsumption(record.ConsumptionDisplay);

        return new(
            record.FullName,
            record.LegacyDailyRate,
            record.EngineDisplay,
            powertrainCapacityValue,
            powertrainCapacityUnit,
            record.FuelType,
            record.TransmissionType,
            record.DoorsCount,
            record.CargoCapacityDisplay,
            cargoCapacityValue,
            cargoCapacityUnit,
            record.ConsumptionDisplay,
            consumptionValue,
            consumptionUnit,
            record.HasAirConditioning,
            record.PopularityRank,
            record.PrimaryBadgeText,
            record.PrimaryBadgeColor,
            record.SecondaryBadgeText,
            record.SecondaryBadgeColor);
    }

    private static (decimal Value, string Unit) ParsePowertrain(string value)
    {
        if (!VehicleSpecifications.TryParsePowertrain(value, out var parsedValue, out var unit))
        {
            throw new InvalidOperationException($"Unable to parse engine specification '{value}'.");
        }

        return (parsedValue, unit);
    }

    private static (decimal Value, string Unit) ParseCargoCapacity(string value)
    {
        if (!VehicleSpecifications.TryParseCargoCapacity(value, out var parsedValue, out var unit))
        {
            throw new InvalidOperationException($"Unable to parse cargo capacity '{value}'.");
        }

        return (parsedValue, unit);
    }

    private static (decimal Value, string Unit) ParseConsumption(string value)
    {
        if (!VehicleSpecifications.TryParseConsumption(value, out var parsedValue, out var unit))
        {
            throw new InvalidOperationException($"Unable to parse consumption '{value}'.");
        }

        return (parsedValue, unit);
    }
}
