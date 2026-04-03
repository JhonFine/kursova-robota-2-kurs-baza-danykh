namespace CarRental.Desktop.Models;

public sealed class DamagePhoto : IAuditableEntity, ISoftDeletableEntity
{
    public int Id { get; set; }

    public int DamageId { get; set; }

    public string StoredPath { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; }

    public Damage? Damage { get; set; }
}

