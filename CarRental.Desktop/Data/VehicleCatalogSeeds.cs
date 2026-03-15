namespace CarRental.Desktop.Data;

internal static class VehicleCatalogSeeds
{
    public static IReadOnlyList<CatalogVehicleSeed> All { get; } =
    [
        new CatalogVehicleSeed("Toyota Corolla 2018", 34m, "1,6 л", "Бензин", "Автомат", 4, "452 л", "6.3 л/100км", true, 1, "Лідер прокату!", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Ford Fiesta VI", 31m, "1,0 л", "Бензин", "Автомат", 5, "276 л", "4.9 л/100км", true, 2, "Лідер прокату!", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Volkswagen Polo Sedan", 33m, "1,6 л", "Бензин", "Автомат", 4, "460 л", "7.0 л/100км", true, 3, "Лідер прокату!", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Renault Trafic", 44m, "1,6 л", "Дизель", "Механіка", 4, "9 місць", "7.7 л/100км", true, 4, "Лідер прокату!", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Hyundai Elantra", 34m, "1,6 л", "Бензин", "Автомат", 4, "458 л", "6.9 л/100км", true, 5, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Toyota LC Prado 150", 83m, "2,7 л", "Бензин", "Автомат", 5, "621 л", "11.1 л/100км", true, 6, "Лідер прокату!", "#7E22CE", "Суперціна!", "#FB923C"),
        new CatalogVehicleSeed("Infiniti Q50", 62m, "3,0 л", "Бензин", "Автомат", 4, "500 л", "9.2 л/100км", true, 7, "Знижка!", "#39B54A", "", "#FB923C"),
        new CatalogVehicleSeed("Mercedes G55 AMG", 117m, "5,5 л", "Бензин", "Автомат", 5, "480 л", "15.3 л/100км", true, 8, "Лідер прокату!", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Peugeot 301", 36m, "1,6 л", "Дизель", "Механіка", 4, "506 л", "5.2 л/100км", true, 9, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Skoda Octavia A8", 43m, "1,5 л", "Бензин", "Автомат", 5, "600 л", "6.1 л/100км", true, 10, "Лідер прокату!", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Honda Civic X", 44m, "1,5 л", "Бензин", "Автомат", 4, "519 л", "6.8 л/100км", true, 11, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Mazda 3 BM", 46m, "2,0 л", "Бензин", "Автомат", 4, "419 л", "6.4 л/100км", true, 12, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Kia Ceed SW", 44m, "1,5 л", "Бензин", "Автомат", 5, "625 л", "6.7 л/100км", true, 13, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Nissan Leaf e+", 56m, "62 кВт", "Електро", "Автомат", 5, "435 л", "18.1 кВт/100км", true, 14, "Знижка!", "#39B54A", "", "#FB923C"),
        new CatalogVehicleSeed("Tesla Model 3 LR", 80m, "75 кВт", "Електро", "Автомат", 4, "425 л", "16.0 кВт/100км", true, 15, "Лідер прокату!", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("BMW 320d G20", 67m, "2,0 л", "Дизель", "Автомат", 4, "480 л", "5.4 л/100км", true, 16, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Audi A4 B9", 68m, "2,0 л", "Бензин", "Автомат", 4, "460 л", "6.8 л/100км", true, 17, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Mercedes C200 W205", 70m, "2,0 л", "Бензин", "Автомат", 4, "455 л", "6.7 л/100км", true, 18, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Volvo S60", 68m, "2,0 л", "Бензин", "Автомат", 4, "442 л", "7.1 л/100км", true, 19, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Lexus ES 250", 93m, "2,5 л", "Бензин", "Автомат", 4, "454 л", "7.5 л/100км", true, 20, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Toyota Camry XV70", 65m, "2,5 л", "Бензин", "Автомат", 4, "524 л", "7.3 л/100км", true, 21, "Лідер прокату!", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Hyundai Sonata DN8", 62m, "2,5 л", "Бензин", "Автомат", 4, "510 л", "7.2 л/100км", true, 22, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Kia Sportage QL", 58m, "2,0 л", "Бензин", "Автомат", 5, "491 л", "8.2 л/100км", true, 23, "Знижка!", "#39B54A", "", "#FB923C"),
        new CatalogVehicleSeed("Nissan X-Trail T32", 70m, "2,0 л", "Дизель", "Автомат", 5, "565 л", "7.6 л/100км", true, 24, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Mitsubishi Outlander", 67m, "2,4 л", "Бензин", "Автомат", 5, "591 л", "8.4 л/100км", true, 25, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Subaru Forester", 68m, "2,0 л", "Бензин", "Автомат", 5, "520 л", "8.0 л/100км", true, 26, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Volkswagen Tiguan", 71m, "2,0 л", "Бензин", "Автомат", 5, "615 л", "7.9 л/100км", true, 27, "Лідер прокату!", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("BMW X5 G05", 124m, "3,0 л", "Дизель", "Автомат", 5, "650 л", "8.7 л/100км", true, 28, "Лідер прокату!", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Audi Q7 45TDI", 130m, "3,0 л", "Дизель", "Автомат", 5, "865 л", "7.8 л/100км", true, 29, "Лідер прокату!", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Mercedes GLE 350d", 127m, "3,0 л", "Дизель", "Автомат", 5, "630 л", "8.9 л/100км", true, 30, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Toyota RAV4 Hybrid", 77m, "2,5 л", "Гібрид", "Автомат", 5, "580 л", "5.8 л/100км", true, 31, "Знижка!", "#39B54A", "", "#FB923C"),
        new CatalogVehicleSeed("Renault Logan II", 30m, "1,6 л", "Бензин", "Механіка", 4, "510 л", "6.5 л/100км", true, 32, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Dacia Duster", 53m, "1,6 л", "Бензин", "Механіка", 5, "478 л", "7.3 л/100км", true, 33, "Знижка!", "#39B54A", "", "#FB923C"),
        new CatalogVehicleSeed("Opel Astra K", 40m, "1,6 л", "Дизель", "Механіка", 5, "370 л", "5.3 л/100км", true, 34, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Seat Leon", 43m, "1,5 л", "Бензин", "Автомат", 5, "380 л", "6.2 л/100км", true, 35, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Citroen C4", 41m, "1,2 л", "Бензин", "Автомат", 5, "380 л", "6.1 л/100км", true, 36, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Peugeot 308", 44m, "1,5 л", "Дизель", "Автомат", 5, "412 л", "5.4 л/100км", true, 37, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Ford Focus III", 38m, "1,6 л", "Бензин", "Механіка", 5, "316 л", "6.5 л/100км", true, 38, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Volkswagen Golf VII", 46m, "2,0 л", "Дизель", "Автомат", 5, "380 л", "5.0 л/100км", true, 39, "Лідер прокату!", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Fiat 500C", 73m, "1,2 л", "Бензин", "Автомат", 3, "185 л", "5.9 л/100км", true, 40, "Знижка!", "#39B54A", "", "#FB923C"),
        new CatalogVehicleSeed("Mazda MX-5", 92m, "2,0 л", "Бензин", "Механіка", 2, "130 л", "6.8 л/100км", true, 41, "Лідер прокату!", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("BMW Z4 Roadster", 110m, "2,0 л", "Бензин", "Автомат", 2, "281 л", "7.4 л/100км", true, 42, "Лідер прокату!", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Mercedes E220d W213", 78m, "2,0 л", "Дизель", "Автомат", 4, "540 л", "5.7 л/100км", true, 43, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Audi A6 C8", 80m, "2,0 л", "Дизель", "Автомат", 4, "530 л", "5.9 л/100км", true, 44, "Лідер прокату!", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Skoda Superb", 73m, "2,0 л", "Бензин", "Автомат", 5, "625 л", "6.9 л/100км", true, 45, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Renault Kangoo", 49m, "1,5 л", "Дизель", "Механіка", 5, "750 л", "5.8 л/100км", true, 46, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Opel Vivaro", 61m, "2,0 л", "Дизель", "Механіка", 4, "6000 л", "6.4 л/100км", true, 47, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Ford Transit Custom", 64m, "2,0 л", "Дизель", "Механіка", 4, "6200 л", "7.1 л/100км", true, 48, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Mercedes Sprinter", 77m, "2,7 л", "Дизель", "Механіка", 4, "10500 л", "8.4 л/100км", true, 49, "Лідер прокату!", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Volkswagen T6 Multivan", 71m, "2,0 л", "Дизель", "Автомат", 5, "7 місць", "7.5 л/100км", true, 50, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Toyota Hilux", 81m, "2,8 л", "Дизель", "Автомат", 4, "1000 кг", "8.9 л/100км", true, 51, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Ford Ranger", 80m, "2,0 л", "Дизель", "Автомат", 4, "946 кг", "7.5 л/100км", true, 52, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Nissan Navara", 75m, "2,3 л", "Дизель", "Автомат", 4, "1050 кг", "8.1 л/100км", true, 53, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Isuzu D-Max", 73m, "1,9 л", "Дизель", "Механіка", 4, "1100 кг", "7.8 л/100км", true, 54, "", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Jeep Wrangler", 101m, "2,0 л", "Бензин", "Автомат", 2, "365 л", "11.2 л/100км", true, 55, "Лідер прокату!", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Land Rover Discovery", 138m, "3,0 л", "Дизель", "Автомат", 5, "986 л", "8.5 л/100км", true, 56, "Лідер прокату!", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Porsche Cayenne", 145m, "3,0 л", "Бензин", "Автомат", 5, "770 л", "11.0 л/100км", true, 57, "Лідер прокату!", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Tesla Model Y", 90m, "75 кВт", "Електро", "Автомат", 5, "854 л", "16.8 кВт/100км", true, 58, "Лідер прокату!", "#7E22CE", "", "#FB923C"),
        new CatalogVehicleSeed("Hyundai Ioniq 5", 83m, "72 кВт", "Електро", "Автомат", 5, "527 л", "17.4 кВт/100км", true, 59, "Знижка!", "#39B54A", "", "#FB923C"),
        new CatalogVehicleSeed("Kia EV6", 84m, "77 кВт", "Електро", "Автомат", 5, "520 л", "17.2 кВт/100км", true, 60, "Лідер прокату!", "#7E22CE", "", "#FB923C")
    ];

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
        decimal DailyRate,
        string EngineDisplay,
        string FuelType,
        string TransmissionType,
        int DoorsCount,
        string CargoCapacityDisplay,
        string ConsumptionDisplay,
        bool HasAirConditioning,
        int PopularityRank,
        string PrimaryBadgeText,
        string PrimaryBadgeColor,
        string SecondaryBadgeText,
        string SecondaryBadgeColor)
    {
        public string Make => SplitMakeAndModel(FullName).Make;

        public string Model => SplitMakeAndModel(FullName).Model;

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
}
