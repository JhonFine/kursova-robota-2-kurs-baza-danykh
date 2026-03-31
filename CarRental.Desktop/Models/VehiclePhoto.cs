namespace CarRental.Desktop.Models;

public sealed class VehiclePhoto
{
    public int Id { get; set; }

    public int VehicleId { get; set; }

    public string StoredPath { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsPrimary { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Vehicle? Vehicle { get; set; }
}
