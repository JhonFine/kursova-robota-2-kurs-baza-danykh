using System.ComponentModel.DataAnnotations;
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

public sealed record EmployeeDto(
    int Id,
    string FullName,
    string Login,
    UserRole Role,
    bool IsActive,
    DateTime? LastLoginUtc,
    DateTime? LockoutUntilUtc);

public sealed record AuthTokenResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    EmployeeDto Employee);

public sealed class ClientUpsertRequest
{
    [Required, MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string PassportData { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string DriverLicense { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string Phone { get; set; } = string.Empty;

    public bool Blacklisted { get; set; }
}

public sealed class SetBlacklistRequest
{
    public bool Blacklisted { get; set; }
}

public sealed record ClientDto(
    int Id,
    string FullName,
    string PassportData,
    string DriverLicense,
    string Phone,
    bool Blacklisted);

public sealed class UpdateClientProfileRequest
{
    [Required, MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string PassportData { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string DriverLicense { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string Phone { get; set; } = string.Empty;
}

public sealed record ClientProfileDto(
    int Id,
    string FullName,
    string PassportData,
    string DriverLicense,
    string Phone,
    bool Blacklisted,
    bool IsComplete);

public sealed class VehicleUpsertRequest
{
    [Required, MaxLength(60)]
    public string Make { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string Model { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string EngineDisplay { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string FuelType { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string TransmissionType { get; set; } = string.Empty;

    [Range(1, 8)]
    public int DoorsCount { get; set; } = 4;

    [Required, MaxLength(40)]
    public string CargoCapacityDisplay { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string ConsumptionDisplay { get; set; } = string.Empty;

    public bool HasAirConditioning { get; set; } = true;

    [Required, MaxLength(30)]
    public string LicensePlate { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int Mileage { get; set; }

    [Range(typeof(decimal), "0.01", "1000000")]
    public decimal DailyRate { get; set; }

    public bool IsAvailable { get; set; } = true;

    [Range(1000, 200000)]
    public int ServiceIntervalKm { get; set; } = 10000;

    [MaxLength(500)]
    public string? PhotoPath { get; set; }
}

public sealed class UpdateVehicleRateRequest
{
    [Range(typeof(decimal), "0.01", "1000000")]
    public decimal DailyRate { get; set; }
}

public sealed record VehicleDto(
    int Id,
    string Make,
    string Model,
    string EngineDisplay,
    string FuelType,
    string TransmissionType,
    int DoorsCount,
    string CargoCapacityDisplay,
    string ConsumptionDisplay,
    bool HasAirConditioning,
    string LicensePlate,
    int Mileage,
    decimal DailyRate,
    bool IsAvailable,
    int ServiceIntervalKm,
    string? PhotoPath);

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
    int EmployeeId,
    string EmployeeName,
    DateTime StartDate,
    DateTime EndDate,
    string PickupLocation,
    string ReturnLocation,
    int StartMileage,
    int? EndMileage,
    RentalStatus Status,
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
    DateTime? ReturnInspectionCompletedAtUtc,
    int? ReturnFuelPercent,
    string? ReturnInspectionNotes);

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

    [Range(typeof(decimal), "0.01", "1000000")]
    public decimal Amount { get; set; }

    [EnumDataType(typeof(PaymentMethod))]
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    [EnumDataType(typeof(PaymentDirection))]
    public PaymentDirection Direction { get; set; } = PaymentDirection.Incoming;

    [MaxLength(300)]
    public string Notes { get; set; } = string.Empty;
}

public sealed record PaymentDto(
    int Id,
    int RentalId,
    int EmployeeId,
    decimal Amount,
    PaymentMethod Method,
    PaymentDirection Direction,
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

    [Range(typeof(decimal), "0.01", "1000000")]
    public decimal RepairCost { get; set; }

    [MaxLength(500)]
    public string? PhotoPath { get; set; }

    public bool AutoChargeToRental { get; set; }
}

public sealed record DamageDto(
    int Id,
    int VehicleId,
    string VehicleName,
    int? RentalId,
    string? ContractNumber,
    string Description,
    DateTime DateReported,
    decimal RepairCost,
    string? PhotoPath,
    string ActNumber,
    decimal ChargedAmount,
    bool IsChargedToClient,
    DamageStatus Status);

public sealed class AddMaintenanceRecordRequest
{
    [Range(1, int.MaxValue)]
    public int VehicleId { get; set; }

    public DateTime ServiceDate { get; set; }

    [Range(0, int.MaxValue)]
    public int MileageAtService { get; set; }

    [Required, MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "1000000")]
    public decimal Cost { get; set; }

    [Range(1, int.MaxValue)]
    public int NextServiceMileage { get; set; }
}

public sealed record MaintenanceRecordDto(
    int Id,
    int VehicleId,
    string VehicleName,
    DateTime ServiceDate,
    int MileageAtService,
    string Description,
    decimal Cost,
    int NextServiceMileage);

public sealed record MaintenanceDueDto(
    int VehicleId,
    string Vehicle,
    int CurrentMileage,
    int NextServiceMileage,
    int OverdueByKm);

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
