using CarRental.Desktop.Models;

namespace CarRental.Desktop.Services.Payments;

public interface IPaymentService
{
    Task<PaymentResult> AddPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Payment>> GetRentalPaymentsAsync(int rentalId, CancellationToken cancellationToken = default);

    Task<decimal> GetRentalBalanceAsync(int rentalId, CancellationToken cancellationToken = default);
}

public sealed record PaymentRequest(
    int RentalId,
    int? RecordedByEmployeeId,
    decimal Amount,
    PaymentMethod MethodId,
    PaymentDirection DirectionId,
    string Notes = "",
    PaymentStatus StatusId = PaymentStatus.Completed,
    string? ExternalTransactionId = null);

public sealed record PaymentResult(bool Success, string Message, int PaymentId = 0);


