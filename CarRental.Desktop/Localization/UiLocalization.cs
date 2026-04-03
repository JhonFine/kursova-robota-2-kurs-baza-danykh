using CarRental.Desktop.Models;
using CarRental.Shared.ReferenceData;

namespace CarRental.Desktop.Localization;

public static class UiLocalization
{
    public static string ToDisplay(this UserRole role) => role switch
    {
        UserRole.Admin => "Адміністратор",
        UserRole.Manager => "Менеджер",
        UserRole.User => "Користувач",
        _ => role.ToString()
    };

    public static string ToDisplay(this RentalStatus status) => status switch
    {
        RentalStatus.Booked => "Заброньовано",
        RentalStatus.Active => "Активна",
        RentalStatus.Closed => "Закрита",
        RentalStatus.Canceled => "Скасована",
        _ => status.ToString()
    };

    public static string ToDisplay(this PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Готівка",
        PaymentMethod.Card => "Карта",
        _ => method.ToString()
    };

    public static string ToDisplay(this PaymentDirection direction) => direction switch
    {
        PaymentDirection.Incoming => "Надходження",
        PaymentDirection.Refund => "Повернення",
        _ => direction.ToString()
    };

    public static string ToDisplay(this DamageStatus status) => status switch
    {
        DamageStatus.Open => "Відкрите",
        DamageStatus.Charged => "Донараховано",
        DamageStatus.Resolved => "Закрите",
        _ => status.ToString()
    };

    public static string ToDisplayMaintenanceType(this string? maintenanceTypeCode)
    {
        var normalizedCode = maintenanceTypeCode?.Trim().ToUpperInvariant();
        return normalizedCode switch
        {
            MaintenanceTypes.Scheduled => "Планове ТО",
            MaintenanceTypes.Repair => "Ремонт",
            MaintenanceTypes.Tires => "Шини",
            MaintenanceTypes.Inspection => "Огляд",
            _ => string.IsNullOrWhiteSpace(maintenanceTypeCode) ? "Невідомо" : maintenanceTypeCode.Trim()
        };
    }
}
