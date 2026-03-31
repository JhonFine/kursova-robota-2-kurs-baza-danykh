namespace CarRental.Shared.ReferenceData;

internal static class DemoSeedReferenceData
{
    public const int SeedRandomValue = 20260320;
    public const int TotalClients = 180;
    public const int TotalClosedRentals = 288;
    public const int TotalActiveRentals = 8;
    public const int TotalCanceledRentals = 4;
    public const int TotalDamages = 21;
    public const string PlateAlphabet = "ABCEHIKMOPTX";
    public const string AdminLogin = "admin";
    public const string ManagerLogin = "manager";
    public const string AdminFallbackPassword = "admin123";
    public const string ManagerFallbackPassword = "manager123";
    public const string SelfServiceCancelReasonWeb = "Скасовано клієнтом через сайт";
    public const string SelfServiceCancelReasonDesktop = "Скасовано клієнтом через застосунок";
    public const string StaffCancellationReason = "За запитом клієнта";
    public const string DefaultMaintenanceDescription = "Планове техобслуговування";
    public const string DefaultDamageDescription = "Пошкодження кузова";

    public static IReadOnlyList<RentalDurationDistributionItem> RentalDurationDistribution { get; } =
    [
        new RentalDurationDistributionItem(1, 45),
        new RentalDurationDistributionItem(2, 84),
        new RentalDurationDistributionItem(3, 75),
        new RentalDurationDistributionItem(4, 42),
        new RentalDurationDistributionItem(5, 27),
        new RentalDurationDistributionItem(6, 12),
        new RentalDurationDistributionItem(7, 9),
        new RentalDurationDistributionItem(8, 3),
        new RentalDurationDistributionItem(10, 2),
        new RentalDurationDistributionItem(14, 1)
    ];

    public static IReadOnlyList<LocationSeedDefinition> LocationSeeds { get; } =
    [
        new LocationSeedDefinition("Київ", 40, "AA", "KA"),
        new LocationSeedDefinition("Львів", 20, "BC", "HC"),
        new LocationSeedDefinition("Одеса", 16, "BH", "HH"),
        new LocationSeedDefinition("Дніпро", 14, "AE", "KE"),
        new LocationSeedDefinition("Харків", 10, "AX", "KX")
    ];

    public static IReadOnlyList<string> SupportedLocations { get; } = LocationSeeds.Select(item => item.City).ToArray();

    public static IReadOnlyList<string> TimeOptions { get; } =
    [
        "08:00",
        "09:00",
        "10:00",
        "11:00",
        "12:00",
        "13:00",
        "14:00",
        "15:00",
        "16:00",
        "17:00",
        "18:00",
        "19:00",
        "20:00"
    ];

    public static IReadOnlyList<string> MaleFirstNames { get; } =
    [
        "Олександр", "Андрій", "Максим", "Дмитро", "Іван", "Богдан", "Тарас", "Владислав", "Назар",
        "Сергій", "Роман", "Денис", "Віталій", "Павло", "Микола", "Ярослав", "Євген", "Артем"
    ];

    public static IReadOnlyList<string> FemaleFirstNames { get; } =
    [
        "Олена", "Ірина", "Наталія", "Марія", "Софія", "Анастасія", "Катерина", "Юлія", "Тетяна",
        "Дарина", "Христина", "Вікторія", "Аліна", "Оксана", "Анна", "Валерія", "Соломія", "Надія"
    ];

    public static IReadOnlyList<string> FamilyNames { get; } =
    [
        "Шевченко", "Бондаренко", "Коваленко", "Ткачук", "Мельник", "Бойко",
        "Кравченко", "Олійник", "Савчук", "Лисенко", "Романюк", "Клименко"
    ];

    public static IReadOnlyList<string> PhonePrefixes { get; } = ["50", "63", "66", "67", "68", "73", "91", "93", "95", "96", "97", "98", "99"];
    public static IReadOnlyList<string> PassportSeries { get; } = ["КВ", "ЛВ", "ОД", "ХА", "ДП", "ІФ", "ЧЕ", "ЗП", "ЖТ", "ВН"];
    public static IReadOnlyList<string> DriverLicenseSeries { get; } = ["ВА", "ВС", "ВН", "КА", "КС", "КЕ", "КХ", "СА", "СВ", "СН"];

    public static IReadOnlySet<int> BlacklistedClientIndexes { get; } = new HashSet<int> { 27, 91, 154 };

    public static IReadOnlyList<string> CancellationReasons { get; } =
    [
        "Клієнт змінив плани поїздки.",
        "Не підтверджено оплату бронювання.",
        "Клієнт переніс поїздку на іншу дату.",
        "Бронювання скасовано до видачі авто."
    ];

    public static IReadOnlyList<string> DamageDescriptions { get; } =
    [
        "Подряпина переднього бампера.",
        "Вм'ятина заднього крила після паркування.",
        "Скол лобового скла.",
        "Подряпина на правих дверцятах.",
        "Пошкодження легкосплавного диска.",
        "Тріщина нижньої частини бампера.",
        "Деформація пластикової накладки порога.",
        "Потертість заднього бампера.",
        "Скол фарби на кришці багажника."
    ];

    public static IReadOnlyList<string> MaintenanceDescriptions { get; } =
    [
        "Планове ТО: заміна масла, фільтрів і комп'ютерна діагностика.",
        "Регламентне ТО: заміна масла, салонного фільтра та перевірка гальм.",
        "Планове обслуговування з перевіркою підвіски та розвал-сходженням.",
        "ТО після інтенсивної експлуатації: рідини, фільтри, ходова частина."
    ];

    public static IReadOnlyList<decimal> DamageCostTemplate { get; } =
    [
        2800m, 3000m, 3200m, 3400m, 3500m, 3600m, 3800m, 3900m, 4000m, 4200m, 4400m,
        4700m, 5000m, 5400m, 5800m, 6200m, 6600m, 6900m, 8200m, 9600m, 11100m
    ];

    public static IReadOnlySet<int> ChargedDamageIndices { get; } = new HashSet<int> { 1, 4, 7, 11, 13, 15, 18, 20 };
}

internal sealed record RentalDurationDistributionItem(int Days, int Count);
internal sealed record LocationSeedDefinition(string City, int Weight, string PrimaryPrefix, string SecondaryPrefix);
