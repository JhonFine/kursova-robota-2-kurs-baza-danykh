namespace CarRental.WebApi.Models;

public sealed class VehicleModel : IAuditableEntity
{
    public int Id { get; set; }

    public int MakeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public VehicleMake? Make { get; set; }

    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
