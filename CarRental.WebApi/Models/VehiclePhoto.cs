namespace CarRental.WebApi.Models;

public sealed class VehiclePhoto : IAuditableEntity, ISoftDeletableEntity
{
    public int Id { get; set; }

    public int VehicleId { get; set; }

    public string StoredPath { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsPrimary { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; }

    public Vehicle? Vehicle { get; set; }
}
