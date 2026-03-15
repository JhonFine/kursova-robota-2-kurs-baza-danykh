namespace CarRental.WebApi.Models;

public sealed class Payment
{
    public int Id { get; set; }

    public int RentalId { get; set; }

    public int EmployeeId { get; set; }

    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    public PaymentDirection Direction { get; set; } = PaymentDirection.Incoming;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public string Notes { get; set; } = string.Empty;

    public Rental? Rental { get; set; }

    public Employee? Employee { get; set; }
}

