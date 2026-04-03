namespace CarRental.Desktop.Models;

public sealed class PaymentMethodLookup
{
    public PaymentMethod Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

