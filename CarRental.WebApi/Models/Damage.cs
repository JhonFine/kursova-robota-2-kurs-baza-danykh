namespace CarRental.WebApi.Models;

public sealed class Damage
{
    public int Id { get; set; }

    public int VehicleId { get; set; }

    public int? RentalId { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateTime DateReported { get; set; } = DateTime.UtcNow;

    public decimal RepairCost { get; set; }

    public string? PhotoPath { get; set; }

    public string ActNumber { get; set; } = string.Empty;

    public decimal ChargedAmount { get; set; }

    public bool IsChargedToClient { get; set; }

    public DamageStatus Status { get; set; } = DamageStatus.Open;

    public Vehicle? Vehicle { get; set; }

    public Rental? Rental { get; set; }
}

