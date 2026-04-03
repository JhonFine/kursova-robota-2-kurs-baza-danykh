namespace CarRental.Desktop.Models;

public sealed class VehicleMake : IAuditableEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<VehicleModel> Models { get; set; } = new List<VehicleModel>();

    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}

