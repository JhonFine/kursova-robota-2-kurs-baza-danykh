using CarRental.Shared.ReferenceData;

namespace CarRental.Desktop.Infrastructure;

// Desktop РІРёРєРѕСЂРёСЃС‚РѕРІСѓС” shared reference data СЏРє canonical source of truth,
// Р° С†РµР№ Р°РґР°РїС‚РµСЂ Р»РёС€Рµ РґРѕРґР°С” Р»РѕРєР°Р»С–Р·РѕРІР°РЅС– РїС–РґРїРёСЃРё РґР»СЏ UI-СЂС–РІРЅСЏ.
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

