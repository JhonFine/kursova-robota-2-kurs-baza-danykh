using System.ComponentModel.DataAnnotations.Schema;

namespace CarRental.Desktop.Models;

public sealed class Rental
{
    public int Id { get; set; }

    public int ClientId { get; set; }

    public int VehicleId { get; set; }

    public int CreatedByEmployeeId { get; set; }

    public int? ClosedByEmployeeId { get; set; }

    public int? CanceledByEmployeeId { get; set; }

    public string ContractNumber { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public string PickupLocation { get; set; } = string.Empty;

    public string ReturnLocation { get; set; } = string.Empty;

    public int StartMileage { get; set; }

    public int? EndMileage { get; set; }

    public decimal OverageFee { get; set; }

    public decimal TotalAmount { get; set; }

    public RentalStatus Status { get; set; } = RentalStatus.Booked;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ClosedAtUtc { get; set; }

    public DateTime? CanceledAtUtc { get; set; }

    public string? CancellationReason { get; set; }

    [NotMapped]
    public int EmployeeId
    {
        get => CreatedByEmployeeId;
        set => CreatedByEmployeeId = value;
    }

    [NotMapped]
    public bool IsClosed
    {
        get => Status == RentalStatus.Closed;
        set
        {
            if (!value)
            {
                if (Status == RentalStatus.Closed)
                {
                    Status = RentalStatus.Booked;
                }

                ClosedAtUtc = null;
                return;
            }

            Status = RentalStatus.Closed;
            ClosedAtUtc ??= DateTime.UtcNow;
            CanceledAtUtc = null;
            CancellationReason = null;
        }
    }

    public Client? Client { get; set; }

    public Vehicle? Vehicle { get; set; }

    public Employee? CreatedByEmployee { get; set; }

    public Employee? ClosedByEmployee { get; set; }

    public Employee? CanceledByEmployee { get; set; }

    [NotMapped]
    public Employee? Employee
    {
        get => CreatedByEmployee;
        set => CreatedByEmployee = value;
    }

    public RentalStatusLookup? StatusLookup { get; set; }

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public ICollection<Damage> Damages { get; set; } = new List<Damage>();

    public ICollection<RentalInspection> Inspections { get; set; } = new List<RentalInspection>();
}
