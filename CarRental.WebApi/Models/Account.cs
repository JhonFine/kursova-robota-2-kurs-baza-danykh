namespace CarRental.WebApi.Models;

public sealed class Account : IAuditableEntity
{
    public int Id { get; set; }

    public string Login { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int FailedLoginAttempts { get; set; }

    public DateTime? LockoutUntilUtc { get; set; }

    public DateTime? LastLoginUtc { get; set; }

    public DateTime PasswordChangedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public Employee? Employee { get; set; }

    public Client? Client { get; set; }
}
