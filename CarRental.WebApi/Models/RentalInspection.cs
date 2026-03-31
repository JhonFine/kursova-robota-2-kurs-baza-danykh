namespace CarRental.WebApi.Models;

public sealed class RentalInspection : IAuditableEntity
{
    public int Id { get; set; }

    public int RentalId { get; set; }

    public int PerformedByEmployeeId { get; set; }

    public RentalInspectionType Type { get; set; }

    public DateTime CompletedAtUtc { get; set; } = DateTime.UtcNow;

    public int? FuelPercent { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public Rental? Rental { get; set; }

    public Employee? PerformedByEmployee { get; set; }

    public InspectionTypeLookup? TypeLookup { get; set; }
}
