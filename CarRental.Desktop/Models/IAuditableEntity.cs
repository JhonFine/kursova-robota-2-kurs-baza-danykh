namespace CarRental.Desktop.Models;

public interface IAuditableEntity
{
    DateTime CreatedAtUtc { get; set; }

    DateTime UpdatedAtUtc { get; set; }
}

