using System.ComponentModel.DataAnnotations;
using CarRental.Shared.ReferenceData;
using CarRental.WebApi.Infrastructure;
using CarRental.WebApi.Models;

namespace CarRental.WebApi.Contracts;

public sealed class LoginRequest
{
    [Required, MaxLength(60)]
    public string Login { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}

public sealed class RegisterRequest
{
    [Required, MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string Login { get; set; } = string.Empty;

    [Required, MaxLength(32)]
    public string Phone { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    [Required, MaxLength(128)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class ChangeOwnRoleRequest
{
    [EnumDataType(typeof(UserRole))]
    public UserRole Role { get; set; } = UserRole.User;
}

public sealed record AccountDto(
    int Id,
    string Login,
    bool IsActive,
    DateTime? LastLoginUtc,
    DateTime? LockoutUntilUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record EmployeeDto(
    int Id,
    string FullName,
    UserRole RoleId,
    AccountDto Account,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public string Login => Account.Login;

    public bool IsActive => Account.IsActive;

    public DateTime? LastLoginUtc => Account.LastLoginUtc;

    public DateTime? LockoutUntilUtc => Account.LockoutUntilUtc;
}

public sealed record ClientDocumentDto(
    int Id,
    string DocumentTypeCode,
    string DocumentNumber,
    DateTime? ExpirationDate,
    string? StoredPath);

public sealed class ClientDocumentUpsertRequest
{
    [Required, MaxLength(30)]
    public string DocumentTypeCode { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string DocumentNumber { get; set; } = string.Empty;

    public DateTime? ExpirationDate { get; set; }

    [MaxLength(500)]
    public string? StoredPath { get; set; }
}

public sealed record ClientSummaryDto(
    int Id,
    string FullName,
    string Phone);

public sealed record AccountContextDto(
    AccountDto Account,
    UserRole Role,
    EmployeeDto? Employee,
    ClientSummaryDto? Client);

public sealed record AuthTokenResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    AccountContextDto User,
    EmployeeDto? Employee = null);

public sealed class ClientUpsertRequest
{
    [Required, MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string Phone { get; set; } = string.Empty;

    public bool IsBlacklisted { get; set; }

    [MaxLength(400)]
    public string? BlacklistReason { get; set; }

    public List<ClientDocumentUpsertRequest> Documents { get; set; } = new();

    [MaxLength(120)]
    public string PassportData { get; set; } = string.Empty;

    public DateTime? PassportExpirationDate { get; set; }

    [MaxLength(500)]
    public string? PassportPhotoPath { get; set; }

    [MaxLength(80)]
    public string DriverLicense { get; set; } = string.Empty;

    public DateTime? DriverLicenseExpirationDate { get; set; }

    [MaxLength(500)]
    public string? DriverLicensePhotoPath { get; set; }

    public IReadOnlyList<ClientDocumentUpsertRequest> ResolveDocuments()
    {
        if (Documents.Count > 0)
        {
            return Documents;
        }

        var documents = new List<ClientDocumentUpsertRequest>();
        if (!string.IsNullOrWhiteSpace(PassportData) || PassportExpirationDate.HasValue || !string.IsNullOrWhiteSpace(PassportPhotoPath))
        {
            documents.Add(new ClientDocumentUpsertRequest
            {
                DocumentTypeCode = ClientDocumentTypes.Passport,
                DocumentNumber = PassportData,
                ExpirationDate = PassportExpirationDate?.Date,
                StoredPath = PassportPhotoPath
            });
        }

        if (!string.IsNullOrWhiteSpace(DriverLicense) || DriverLicenseExpirationDate.HasValue || !string.IsNullOrWhiteSpace(DriverLicensePhotoPath))
        {
            documents.Add(new ClientDocumentUpsertRequest
            {
                DocumentTypeCode = ClientDocumentTypes.DriverLicense,
                DocumentNumber = DriverLicense,
                ExpirationDate = DriverLicenseExpirationDate?.Date,
                StoredPath = DriverLicensePhotoPath
            });
        }

        return documents;
    }
}

public sealed class SetBlacklistRequest
{
    public bool IsBlacklisted { get; set; }

    [MaxLength(400)]
    public string? BlacklistReason { get; set; }

}

public sealed record ClientDto(
    int Id,
    string FullName,
    string Phone,
    bool IsBlacklisted,
    string? BlacklistReason,
    DateTime? BlacklistedAtUtc,
    int? BlacklistedByEmployeeId,
    int? AccountId,
    IReadOnlyList<ClientDocumentDto> Documents)
{
    public string PassportData => Documents.FirstOrDefault(item => item.DocumentTypeCode == ClientDocumentTypes.Passport)?.DocumentNumber ?? string.Empty;

    public DateTime? PassportExpirationDate => Documents.FirstOrDefault(item => item.DocumentTypeCode == ClientDocumentTypes.Passport)?.ExpirationDate;

    public string? PassportPhotoPath => Documents.FirstOrDefault(item => item.DocumentTypeCode == ClientDocumentTypes.Passport)?.StoredPath;

    public string DriverLicense => Documents.FirstOrDefault(item => item.DocumentTypeCode == ClientDocumentTypes.DriverLicense)?.DocumentNumber ?? string.Empty;

    public DateTime? DriverLicenseExpirationDate => Documents.FirstOrDefault(item => item.DocumentTypeCode == ClientDocumentTypes.DriverLicense)?.ExpirationDate;

    public string? DriverLicensePhotoPath => Documents.FirstOrDefault(item => item.DocumentTypeCode == ClientDocumentTypes.DriverLicense)?.StoredPath;
}

public sealed class UpdateClientProfileRequest
{
    [Required, MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string Phone { get; set; } = string.Empty;

    public List<ClientDocumentUpsertRequest> Documents { get; set; } = new();

    [MaxLength(120)]
    public string PassportData { get; set; } = string.Empty;

    public DateTime? PassportExpirationDate { get; set; }

    [MaxLength(500)]
    public string? PassportPhotoPath { get; set; }

    [MaxLength(80)]
    public string DriverLicense { get; set; } = string.Empty;

    public DateTime? DriverLicenseExpirationDate { get; set; }

    [MaxLength(500)]
    public string? DriverLicensePhotoPath { get; set; }

    public IReadOnlyList<ClientDocumentUpsertRequest> ResolveDocuments()
        => new ClientUpsertRequest
        {
            FullName = FullName,
            Phone = Phone,
            Documents = Documents,
            PassportData = PassportData,
            PassportExpirationDate = PassportExpirationDate,
            PassportPhotoPath = PassportPhotoPath,
            DriverLicense = DriverLicense,
            DriverLicenseExpirationDate = DriverLicenseExpirationDate,
            DriverLicensePhotoPath = DriverLicensePhotoPath
        }.ResolveDocuments();
}

public sealed record ClientProfileDto(
    int Id,
    string FullName,
    string Phone,
    bool IsBlacklisted,
    string? BlacklistReason,
    DateTime? BlacklistedAtUtc,
    int? BlacklistedByEmployeeId,
    int? AccountId,
    IReadOnlyList<ClientDocumentDto> Documents,
    bool IsComplete)
{
    public string PassportData => Documents.FirstOrDefault(item => item.DocumentTypeCode == ClientDocumentTypes.Passport)?.DocumentNumber ?? string.Empty;

    public DateTime? PassportExpirationDate => Documents.FirstOrDefault(item => item.DocumentTypeCode == ClientDocumentTypes.Passport)?.ExpirationDate;

    public string? PassportPhotoPath => Documents.FirstOrDefault(item => item.DocumentTypeCode == ClientDocumentTypes.Passport)?.StoredPath;

    public string DriverLicense => Documents.FirstOrDefault(item => item.DocumentTypeCode == ClientDocumentTypes.DriverLicense)?.DocumentNumber ?? string.Empty;

    public DateTime? DriverLicenseExpirationDate => Documents.FirstOrDefault(item => item.DocumentTypeCode == ClientDocumentTypes.DriverLicense)?.ExpirationDate;

    public string? DriverLicensePhotoPath => Documents.FirstOrDefault(item => item.DocumentTypeCode == ClientDocumentTypes.DriverLicense)?.StoredPath;
}

public sealed record MediaAssetDto(
    int Id,
    string StoredPath,
    int SortOrder,
    bool IsPrimary = false,
    DateTime? CreatedAtUtc = null,
    DateTime? UpdatedAtUtc = null);

public sealed class MediaAssetUpsertRequest
{
    [Required, MaxLength(500)]
    public string StoredPath { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsPrimary { get; set; }
}

public sealed class VehicleUpsertRequest
{
    [Range(1, int.MaxValue)]
    public int MakeId { get; set; }

    [Range(1, int.MaxValue)]
    public int ModelId { get; set; }

    [DecimalRangeInvariant("0.01", "1000000")]
    public decimal PowertrainCapacityValue { get; set; }

    [Required, MaxLength(16)]
    public string PowertrainCapacityUnit { get; set; } = VehicleSpecificationUnits.Liters;

    [MaxLength(30)]
    public string? FuelTypeCode { get; set; }

    [MaxLength(30)]
    public string? FuelType { get; set; }

    [MaxLength(30)]
    public string? TransmissionTypeCode { get; set; }

    [MaxLength(30)]
    public string? TransmissionType { get; set; }

    [MaxLength(30)]
    public string? VehicleStatusCode { get; set; }

    [Range(1, 8)]
    public int DoorsCount { get; set; } = 4;

    [DecimalRangeInvariant("0.01", "1000000")]
    public decimal CargoCapacityValue { get; set; }

    [Required, MaxLength(16)]
    public string CargoCapacityUnit { get; set; } = VehicleSpecificationUnits.Liters;

    [DecimalRangeInvariant("0.01", "1000000")]
    public decimal ConsumptionValue { get; set; }

    [Required, MaxLength(24)]
    public string ConsumptionUnit { get; set; } = VehicleSpecificationUnits.LitersPer100Km;

    public bool HasAirConditioning { get; set; } = true;

    [Required, MaxLength(30)]
    public string LicensePlate { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int Mileage { get; set; }

    [DecimalRangeInvariant("0.01", "1000000")]
    public decimal DailyRate { get; set; }

    public bool? IsBookable { get; set; }

    [Range(1000, 200000)]
    public int ServiceIntervalKm { get; set; } = 10000;

    [MaxLength(500)]
    public string? PhotoPath { get; set; }

    public List<MediaAssetUpsertRequest> Photos { get; set; } = new();

    public string ResolveFuelTypeCode()
        => (FuelTypeCode ?? FuelType ?? string.Empty).Trim();

    public string ResolveTransmissionTypeCode()
        => (TransmissionTypeCode ?? TransmissionType ?? string.Empty).Trim();

    public string ResolveVehicleStatusCode()
    {
        if (!string.IsNullOrWhiteSpace(VehicleStatusCode))
        {
            return VehicleStatusCode.Trim().ToUpperInvariant();
        }

        if (IsBookable.HasValue)
        {
            return IsBookable.Value ? VehicleStatuses.Ready : VehicleStatuses.Inactive;
        }

        return VehicleStatuses.Ready;
    }

    public IReadOnlyList<MediaAssetUpsertRequest> ResolvePhotos()
    {
        if (Photos.Count > 0)
        {
            return Photos;
        }

        if (string.IsNullOrWhiteSpace(PhotoPath))
        {
            return Array.Empty<MediaAssetUpsertRequest>();
        }

        return
        [
            new MediaAssetUpsertRequest
            {
                StoredPath = PhotoPath,
                SortOrder = 0,
                IsPrimary = true
            }
        ];
    }
}

public sealed class UpdateVehicleRateRequest
{
    [DecimalRangeInvariant("0.01", "1000000")]
    public decimal DailyRate { get; set; }
}

public sealed record VehicleDto(
    int Id,
    int MakeId,
    string MakeName,
    int ModelId,
    string ModelName,
    decimal PowertrainCapacityValue,
    string PowertrainCapacityUnit,
    string FuelTypeCode,
    string TransmissionTypeCode,
    string VehicleStatusCode,
    int DoorsCount,
    decimal CargoCapacityValue,
    string CargoCapacityUnit,
    decimal ConsumptionValue,
    string ConsumptionUnit,
    bool HasAirConditioning,
    string LicensePlate,
    int Mileage,
    decimal DailyRate,
    bool IsAvailable,
    int ServiceIntervalKm,
    IReadOnlyList<MediaAssetDto> Photos)
{
    public string FuelType => FuelTypeCode;

    public string TransmissionType => TransmissionTypeCode;

    public bool IsBookable => string.Equals(VehicleStatusCode, VehicleStatuses.Ready, StringComparison.OrdinalIgnoreCase);

    public string? PhotoPath => Photos
        .OrderByDescending(item => item.IsPrimary)
        .ThenBy(item => item.SortOrder)
        .Select(item => item.StoredPath)
        .FirstOrDefault();
}

public sealed class CreateRentalRequest
{
    [Range(1, int.MaxValue)]
    public int ClientId { get; set; }

    [Range(1, int.MaxValue)]
    public int VehicleId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    [Required, MaxLength(80)]
    public string PickupLocation { get; set; } = string.Empty;

    [MaxLength(80)]
    public string ReturnLocation { get; set; } = string.Empty;

    public bool CreateInitialPayment { get; set; }

    [EnumDataType(typeof(PaymentMethod))]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [EnumDataType(typeof(PaymentDirection))]
    public PaymentDirection PaymentDirection { get; set; } = PaymentDirection.Incoming;

    [MaxLength(300)]
    public string Notes { get; set; } = string.Empty;
}

public sealed class CloseRentalRequest
{
    public DateTime ActualEndDate { get; set; }

    [Range(0, int.MaxValue)]
    public int EndMileage { get; set; }

    [Range(0, 100)]
    public int? ReturnFuelPercent { get; set; }

    [MaxLength(500)]
    public string ReturnInspectionNotes { get; set; } = string.Empty;
}

public sealed class CancelRentalRequest
{
    [Required, MaxLength(400)]
    public string Reason { get; set; } = string.Empty;
}

public sealed record RentalDto(
    int Id,
    string ContractNumber,
    int ClientId,
    string ClientName,
    int VehicleId,
    string VehicleName,
    int? CreatedByEmployeeId,
    string? CreatedByEmployeeName,
    int? ClosedByEmployeeId,
    string? ClosedByEmployeeName,
    int? CanceledByEmployeeId,
    string? CanceledByEmployeeName,
    DateTime StartDate,
    DateTime EndDate,
    string PickupLocation,
    string ReturnLocation,
    int StartMileage,
    int? EndMileage,
    RentalStatus StatusId,
    decimal TotalAmount,
    decimal OverageFee,
    decimal PaidAmount,
    decimal Balance,
    DateTime CreatedAtUtc,
    DateTime? ClosedAtUtc,
    DateTime? CanceledAtUtc,
    string? CancellationReason,
    DateTime? PickupInspectionCompletedAtUtc,
    int? PickupFuelPercent,
    string? PickupInspectionNotes,
    int? PickupInspectionPerformedByEmployeeId,
    string? PickupInspectionPerformedByEmployeeName,
    DateTime? ReturnInspectionCompletedAtUtc,
    int? ReturnFuelPercent,
    string? ReturnInspectionNotes,
    int? ReturnInspectionPerformedByEmployeeId,
    string? ReturnInspectionPerformedByEmployeeName);

public sealed class RescheduleRentalRequest
{
    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }
}

public sealed class SettleRentalBalanceRequest
{
    [MaxLength(300)]
    public string Notes { get; set; } = string.Empty;
}

public sealed class PickupInspectionRequest
{
    [Range(0, 100)]
    public int FuelPercent { get; set; }

    [MaxLength(500)]
    public string Notes { get; set; } = string.Empty;
}

public sealed class AddPaymentRequest
{
    [Range(1, int.MaxValue)]
    public int RentalId { get; set; }

    [DecimalRangeInvariant("0.01", "1000000")]
    public decimal Amount { get; set; }

    [EnumDataType(typeof(PaymentMethod))]
    public PaymentMethod MethodId { get; set; } = PaymentMethod.Cash;

    [EnumDataType(typeof(PaymentDirection))]
    public PaymentDirection DirectionId { get; set; } = PaymentDirection.Incoming;

    [EnumDataType(typeof(PaymentStatus))]
    public PaymentStatus StatusId { get; set; } = PaymentStatus.Completed;

    [MaxLength(120)]
    public string? ExternalTransactionId { get; set; }

    [MaxLength(300)]
    public string Notes { get; set; } = string.Empty;
}

public sealed record PaymentDto(
    int Id,
    int RentalId,
    int? RecordedByEmployeeId,
    string? RecordedByEmployeeName,
    decimal Amount,
    PaymentMethod MethodId,
    PaymentDirection DirectionId,
    PaymentStatus StatusId,
    string? ExternalTransactionId,
    DateTime CreatedAtUtc,
    string Notes);

public sealed record RentalBalanceDto(int RentalId, decimal Balance);

public sealed class AddDamageRequest
{
    [Range(1, int.MaxValue)]
    public int VehicleId { get; set; }

    public int? RentalId { get; set; }

    [Required, MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [DecimalRangeInvariant("0.01", "1000000")]
    public decimal RepairCost { get; set; }

    [MaxLength(500)]
    public string? PhotoPath { get; set; }

    public List<MediaAssetUpsertRequest> Photos { get; set; } = new();

    public bool AutoChargeToRental { get; set; }

    public IReadOnlyList<MediaAssetUpsertRequest> ResolvePhotos()
    {
        if (Photos.Count > 0)
        {
            return Photos;
        }

        if (string.IsNullOrWhiteSpace(PhotoPath))
        {
            return Array.Empty<MediaAssetUpsertRequest>();
        }

        return
        [
            new MediaAssetUpsertRequest
            {
                StoredPath = PhotoPath,
                SortOrder = 0,
                IsPrimary = true
            }
        ];
    }
}

public sealed record DamageDto(
    int Id,
    int VehicleId,
    string VehicleName,
    int? RentalId,
    string? ContractNumber,
    int ReportedByEmployeeId,
    string ReportedByEmployeeName,
    string Description,
    DateTime DateReported,
    decimal RepairCost,
    string DamageActNumber,
    decimal ChargedAmount,
    DamageStatus StatusId,
    IReadOnlyList<MediaAssetDto> Photos)
{
    public bool IsChargedToClient => ChargedAmount > 0m;

    public string? PhotoPath => Photos
        .OrderBy(item => item.SortOrder)
        .Select(item => item.StoredPath)
        .FirstOrDefault();
}

public sealed class AddMaintenanceRecordRequest
{
    [Range(1, int.MaxValue)]
    public int VehicleId { get; set; }

    public DateTime ServiceDate { get; set; }

    [Range(0, int.MaxValue)]
    public int MileageAtService { get; set; }

    [Required, MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [DecimalRangeInvariant("0", "1000000")]
    public decimal Cost { get; set; }

    [Range(1, int.MaxValue)]
    public int? NextServiceMileage { get; set; }

    public DateTime? NextServiceDate { get; set; }

    [Required, MaxLength(30)]
    public string MaintenanceTypeCode { get; set; } = MaintenanceTypes.Scheduled;

    [MaxLength(120)]
    public string? ServiceProviderName { get; set; }
}

public sealed record MaintenanceRecordDto(
    int Id,
    int VehicleId,
    string VehicleName,
    int? PerformedByEmployeeId,
    string? PerformedByEmployeeName,
    DateTime ServiceDate,
    int MileageAtService,
    string Description,
    decimal Cost,
    int? NextServiceMileage,
    DateTime? NextServiceDate,
    string MaintenanceTypeCode,
    string? ServiceProviderName);

public sealed record VehicleMakeDto(int Id, string Name);

public sealed record VehicleModelDto(int Id, int MakeId, string Name);

public sealed record MaintenanceDueDto(
    int VehicleId,
    string Vehicle,
    int CurrentMileage,
    int? NextServiceMileage,
    DateTime? NextServiceDate,
    int OverdueByKm,
    int OverdueByDays);

public sealed class ResetEmployeePasswordRequest
{
    [Required, MinLength(8), MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed record ReportSummaryDto(
    int TotalRentals,
    int ActiveRentals,
    decimal TotalRevenue,
    decimal TotalDamageCost);
