namespace CarRental.WebApi.Models;

public sealed class Rental
{
    public int Id { get; set; }

    public int ClientId { get; set; }

    public int VehicleId { get; set; }

    public int EmployeeId { get; set; }

    public string ContractNumber { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public string PickupLocation { get; set; } = string.Empty;

    public string ReturnLocation { get; set; } = string.Empty;

    public int StartMileage { get; set; }

    public int? EndMileage { get; set; }

    public decimal OverageFee { get; set; }

    public decimal TotalAmount { get; set; }

    public bool IsClosed { get; set; }

    public RentalStatus Status { get; set; } = RentalStatus.Booked;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ClosedAtUtc { get; set; }

    public DateTime? CanceledAtUtc { get; set; }

    public string? CancellationReason { get; set; }

    public DateTime? PickupInspectionCompletedAtUtc { get; set; }

    public int? PickupFuelPercent { get; set; }

    public string? PickupInspectionNotes { get; set; }

    public DateTime? ReturnInspectionCompletedAtUtc { get; set; }

    public int? ReturnFuelPercent { get; set; }

    public string? ReturnInspectionNotes { get; set; }

    public Client? Client { get; set; }

    public Vehicle? Vehicle { get; set; }

    public Employee? Employee { get; set; }

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public ICollection<Damage> Damages { get; set; } = new List<Damage>();
}

