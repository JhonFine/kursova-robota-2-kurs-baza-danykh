using System.ComponentModel.DataAnnotations.Schema;

namespace CarRental.WebApi.Models;

public sealed class Employee : IAuditableEntity
{
    private string? _legacyLogin;
    private string? _legacyPasswordHash;
    private bool? _legacyIsActive;
    private int? _legacyFailedLoginAttempts;
    private DateTime? _legacyLockoutUntilUtc;
    private DateTime? _legacyLastLoginUtc;
    private DateTime _legacyPasswordChangedAtUtc = DateTime.UtcNow;

    public int Id { get; set; }

    public int AccountId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public UserRole RoleId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public string Login
    {
        get => Account?.Login ?? _legacyLogin ?? string.Empty;
        set
        {
            _legacyLogin = value;
            if (Account is not null)
            {
                Account.Login = value;
            }
        }
    }

    [NotMapped]
    public string PasswordHash
    {
        get => Account?.PasswordHash ?? _legacyPasswordHash ?? string.Empty;
        set
        {
            _legacyPasswordHash = value;
            if (Account is not null)
            {
                Account.PasswordHash = value;
            }
        }
    }

    [NotMapped]
    public bool IsActive
    {
        get => Account?.IsActive ?? _legacyIsActive ?? false;
        set
        {
            _legacyIsActive = value;
            if (Account is not null)
            {
                Account.IsActive = value;
            }
        }
    }

    [NotMapped]
    public int FailedLoginAttempts
    {
        get => Account?.FailedLoginAttempts ?? _legacyFailedLoginAttempts ?? 0;
        set
        {
            _legacyFailedLoginAttempts = value;
            if (Account is not null)
            {
                Account.FailedLoginAttempts = value;
            }
        }
    }

    [NotMapped]
    public DateTime? LockoutUntilUtc
    {
        get => Account?.LockoutUntilUtc ?? _legacyLockoutUntilUtc;
        set
        {
            _legacyLockoutUntilUtc = value;
            if (Account is not null)
            {
                Account.LockoutUntilUtc = value;
            }
        }
    }

    [NotMapped]
    public DateTime? LastLoginUtc
    {
        get => Account?.LastLoginUtc ?? _legacyLastLoginUtc;
        set
        {
            _legacyLastLoginUtc = value;
            if (Account is not null)
            {
                Account.LastLoginUtc = value;
            }
        }
    }

    [NotMapped]
    public DateTime PasswordChangedAtUtc
    {
        get => Account?.PasswordChangedAtUtc ?? _legacyPasswordChangedAtUtc;
        set
        {
            _legacyPasswordChangedAtUtc = value;
            if (Account is not null)
            {
                Account.PasswordChangedAtUtc = value;
            }
        }
    }

    public Account? Account { get; set; }

    public EmployeeRoleLookup? RoleLookup { get; set; }

    public ICollection<Rental> CreatedRentals { get; set; } = new List<Rental>();

    public ICollection<Rental> ClosedRentals { get; set; } = new List<Rental>();

    public ICollection<Rental> CanceledRentals { get; set; } = new List<Rental>();

    public ICollection<Payment> RecordedPayments { get; set; } = new List<Payment>();

    public ICollection<RentalInspection> Inspections { get; set; } = new List<RentalInspection>();

    public ICollection<Damage> ReportedDamages { get; set; } = new List<Damage>();

    public ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = new List<MaintenanceRecord>();

    public ICollection<Client> BlacklistedClients { get; set; } = new List<Client>();
}
