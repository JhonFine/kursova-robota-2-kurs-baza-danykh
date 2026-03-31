using CarRental.WebApi.Contracts;
using CarRental.WebApi.Models;

namespace CarRental.WebApi.Infrastructure;

public static class ApiDtoMapping
{
    public static AccountDto ToDto(this Account account)
        => new(
            account.Id,
            account.Login,
            account.IsActive,
            account.LastLoginUtc,
            account.LockoutUntilUtc,
            account.CreatedAtUtc,
            account.UpdatedAtUtc);

    public static EmployeeDto ToDto(this Employee employee)
        => new(
            employee.Id,
            employee.FullName,
            employee.Role,
            employee.Account?.ToDto()
                ?? new AccountDto(
                    employee.AccountId,
                    employee.Login,
                    employee.IsActive,
                    employee.LastLoginUtc,
                    employee.LockoutUntilUtc,
                    employee.CreatedAtUtc,
                    employee.UpdatedAtUtc),
            employee.CreatedAtUtc,
            employee.UpdatedAtUtc);

    public static ClientSummaryDto ToSummaryDto(this Client client)
        => new(client.Id, client.FullName, client.Phone);

    public static ClientDocumentDto ToDto(this ClientDocument document)
        => new(
            document.Id,
            document.DocumentTypeCode,
            document.DocumentNumber,
            document.ExpirationDate,
            document.StoredPath);

    public static ClientDto ToDto(this Client client)
        => new(
            client.Id,
            client.FullName,
            client.Phone,
            client.IsBlacklisted,
            client.BlacklistReason,
            client.BlacklistedAtUtc,
            client.AccountId,
            client.Documents
                .Where(item => !item.IsDeleted)
                .OrderBy(item => item.DocumentTypeCode)
                .Select(item => item.ToDto())
                .ToList());

    public static ClientProfileDto ToProfileDto(this Client client, bool isComplete)
        => new(
            client.Id,
            client.FullName,
            client.Phone,
            client.IsBlacklisted,
            client.BlacklistReason,
            client.BlacklistedAtUtc,
            client.AccountId,
            client.Documents
                .Where(item => !item.IsDeleted)
                .OrderBy(item => item.DocumentTypeCode)
                .Select(item => item.ToDto())
                .ToList(),
            isComplete);

    public static MediaAssetDto ToDto(this VehiclePhoto photo)
        => new(photo.Id, photo.StoredPath, photo.SortOrder, photo.IsPrimary, photo.CreatedAtUtc);

    public static MediaAssetDto ToDto(this DamagePhoto photo)
        => new(photo.Id, photo.StoredPath, photo.SortOrder, false, photo.CreatedAtUtc);

    public static VehicleDto ToDto(this Vehicle vehicle, bool? isAvailable = null)
        => new(
            vehicle.Id,
            vehicle.Make,
            vehicle.Model,
            vehicle.PowertrainCapacityValue,
            vehicle.PowertrainCapacityUnit,
            vehicle.FuelTypeCode,
            vehicle.TransmissionTypeCode,
            vehicle.VehicleStatusCode,
            vehicle.DoorsCount,
            vehicle.CargoCapacityValue,
            vehicle.CargoCapacityUnit,
            vehicle.ConsumptionValue,
            vehicle.ConsumptionUnit,
            vehicle.HasAirConditioning,
            vehicle.LicensePlate,
            vehicle.Mileage,
            vehicle.DailyRate,
            isAvailable ?? vehicle.IsAvailable,
            vehicle.ServiceIntervalKm,
            vehicle.Photos
                .OrderByDescending(item => item.IsPrimary)
                .ThenBy(item => item.SortOrder)
                .Select(item => item.ToDto())
                .ToList());

    public static PaymentDto ToDto(this Payment payment)
        => new(
            payment.Id,
            payment.RentalId,
            payment.EmployeeId,
            payment.Employee?.FullName ?? string.Empty,
            payment.Amount,
            payment.Method,
            payment.Direction,
            payment.Status,
            payment.ExternalTransactionId,
            payment.CreatedAtUtc,
            payment.Notes);

    public static DamageDto ToDto(this Damage damage)
        => new(
            damage.Id,
            damage.VehicleId,
            damage.Vehicle is null ? string.Empty : $"{damage.Vehicle.Make} {damage.Vehicle.Model} [{damage.Vehicle.LicensePlate}]",
            damage.RentalId,
            damage.Rental?.ContractNumber,
            damage.ReportedByEmployeeId,
            damage.ReportedByEmployee?.FullName ?? string.Empty,
            damage.Description,
            damage.DateReported,
            damage.RepairCost,
            damage.ActNumber,
            damage.ChargedAmount,
            damage.Status,
            damage.Photos
                .OrderBy(item => item.SortOrder)
                .Select(item => item.ToDto())
                .ToList());

    public static MaintenanceRecordDto ToDto(this MaintenanceRecord record)
        => new(
            record.Id,
            record.VehicleId,
            record.Vehicle is null ? string.Empty : $"{record.Vehicle.Make} {record.Vehicle.Model} [{record.Vehicle.LicensePlate}]",
            record.PerformedByEmployeeId,
            record.PerformedByEmployee?.FullName,
            record.ServiceDate,
            record.MileageAtService,
            record.Description,
            record.Cost,
            record.NextServiceMileage,
            record.MaintenanceTypeCode,
            record.ServiceProviderName);

    public static AccountContextDto ToAccountContextDto(
        this Account account,
        Employee? employee,
        Client? client,
        UserRole role)
        => new(
            account.ToDto(),
            role,
            employee?.ToDto(),
            client?.ToSummaryDto());
}
