namespace CarRental.WebApi.Models;

public sealed class Payment
{
    public int Id { get; set; }

    public int RentalId { get; set; }

    public int? RecordedByEmployeeId { get; set; }

    public decimal Amount { get; set; }

    public PaymentMethod MethodId { get; set; } = PaymentMethod.Cash;

    public PaymentDirection DirectionId { get; set; } = PaymentDirection.Incoming;

    public PaymentStatus StatusId { get; set; } = PaymentStatus.Completed;

    public string? ExternalTransactionId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public string Notes { get; set; } = string.Empty;

    public Rental? Rental { get; set; }

    public Employee? RecordedByEmployee { get; set; }

    public PaymentMethodLookup? MethodLookup { get; set; }

    public PaymentDirectionLookup? DirectionLookup { get; set; }

    public PaymentStatusLookup? StatusLookup { get; set; }
}
