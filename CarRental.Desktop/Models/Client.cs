namespace CarRental.Desktop.Models;

public sealed class Client
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string PassportData { get; set; } = string.Empty;

    public string DriverLicense { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public bool Blacklisted { get; set; }

    public ICollection<Rental> Rentals { get; set; } = new List<Rental>();
}
