namespace CarRental.Desktop.Models;

public sealed class PaymentDirectionLookup
{
    public PaymentDirection Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
