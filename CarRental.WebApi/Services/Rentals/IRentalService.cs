using CarRental.WebApi.Models;

namespace CarRental.WebApi.Services.Rentals;

public interface IRentalService
{
    Task<bool> HasDateConflictAsync(
        int vehicleId,
        DateTime startDate,
        DateTime endDate,
        int? excludeRentalId = null,
        CancellationToken cancellationToken = default);

    Task<CreateRentalResult> CreateRentalAsync(
        CreateRentalRequest request,
        CancellationToken cancellationToken = default);

    Task<CreateRentalResult> CreateRentalWithPaymentAsync(
        CreateRentalWithPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<CloseRentalResult> CloseRentalAsync(
        CloseRentalRequest request,
        CancellationToken cancellationToken = default);

    Task<CancelRentalResult> CancelRentalAsync(
        CancelRentalRequest request,
        CancellationToken cancellationToken = default);

    Task<RescheduleRentalResult> RescheduleRentalAsync(
        RescheduleRentalRequest request,
        CancellationToken cancellationToken = default);

    Task<SettleRentalBalanceResult> SettleRentalBalanceAsync(
        SettleRentalBalanceRequest request,
        CancellationToken cancellationToken = default);

    Task<PickupInspectionResult> CompletePickupInspectionAsync(
        PickupInspectionRequest request,
        CancellationToken cancellationToken = default);

    Task RefreshStatusesAsync(CancellationToken cancellationToken = default);
}

public sealed record CreateRentalRequest(
    int ClientId,
    int VehicleId,
    int EmployeeId,
    DateTime StartDate,
    DateTime EndDate,
    string PickupLocation = "",
    string ReturnLocation = "");

public sealed record CreateRentalWithPaymentRequest(
    int ClientId,
    int VehicleId,
    int EmployeeId,
    DateTime StartDate,
    DateTime EndDate,
    string PickupLocation = "",
    string ReturnLocation = "",
    PaymentMethod Method = PaymentMethod.Cash,
    PaymentDirection Direction = PaymentDirection.Incoming,
    string Notes = "");

public sealed record CreateRentalResult(
    bool Success,
    string Message,
    int RentalId = 0,
    string ContractNumber = "",
    decimal TotalAmount = 0m);

public sealed record CloseRentalRequest(
    int RentalId,
    DateTime ActualEndDate,
    int EndMileage,
    int? ReturnFuelPercent = null,
    string ReturnInspectionNotes = "");

public sealed record CloseRentalResult(
    bool Success,
    string Message,
    decimal TotalAmount = 0m);

public sealed record CancelRentalRequest(int RentalId, string Reason);

public sealed record CancelRentalResult(bool Success, string Message);

public sealed record RescheduleRentalRequest(
    int RentalId,
    DateTime StartDate,
    DateTime EndDate,
    int EmployeeId);

public sealed record RescheduleRentalResult(
    bool Success,
    string Message,
    decimal TotalAmount = 0m,
    decimal Balance = 0m);

public sealed record SettleRentalBalanceRequest(
    int RentalId,
    int EmployeeId,
    string Notes);

public sealed record SettleRentalBalanceResult(
    bool Success,
    string Message,
    decimal Amount = 0m);

public sealed record PickupInspectionRequest(
    int RentalId,
    int FuelPercent,
    string Notes);

public sealed record PickupInspectionResult(bool Success, string Message);

