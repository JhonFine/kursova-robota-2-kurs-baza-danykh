using CarRental.WebApi.Models;

namespace CarRental.WebApi.Services.Payments;

public interface IPaymentService
{
    Task<PaymentResult> AddPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Payment>> GetRentalPaymentsAsync(int rentalId, CancellationToken cancellationToken = default);

    Task<decimal> GetRentalBalanceAsync(int rentalId, CancellationToken cancellationToken = default);
}

public sealed record PaymentRequest(
    int RentalId,
    int EmployeeId,
    decimal Amount,
    PaymentMethod Method,
    PaymentDirection Direction,
    string Notes = "",
    PaymentStatus Status = PaymentStatus.Completed,
    string? ExternalTransactionId = null);

public sealed record PaymentResult(bool Success, string Message, int PaymentId = 0);

