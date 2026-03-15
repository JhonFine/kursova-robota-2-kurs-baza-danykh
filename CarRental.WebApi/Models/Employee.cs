namespace CarRental.WebApi.Models;

public sealed class Employee
{
    public int Id { get; set; }

    public int? ClientId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Login { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public bool IsActive { get; set; } = true;

    public int FailedLoginAttempts { get; set; }

    public DateTime? LockoutUntilUtc { get; set; }

    public DateTime? LastLoginUtc { get; set; }

    public DateTime PasswordChangedAtUtc { get; set; } = DateTime.UtcNow;

    public Client? Client { get; set; }

    public ICollection<Rental> Rentals { get; set; } = new List<Rental>();

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

