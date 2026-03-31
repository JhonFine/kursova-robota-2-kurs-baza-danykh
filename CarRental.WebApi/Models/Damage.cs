using System.ComponentModel.DataAnnotations.Schema;

namespace CarRental.WebApi.Models;

public sealed class Damage : IAuditableEntity
{
    public int Id { get; set; }

    public int VehicleId { get; set; }

    public int? RentalId { get; set; }

    public int ReportedByEmployeeId { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateTime DateReported { get; set; } = DateTime.UtcNow;

    public decimal RepairCost { get; set; }

    public string ActNumber { get; set; } = string.Empty;

    public decimal ChargedAmount { get; set; }

    public DamageStatus Status { get; set; } = DamageStatus.Open;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public bool IsChargedToClient
    {
        get => ChargedAmount > 0m;
        set
        {
            if (value)
            {
                ChargedAmount = ChargedAmount > 0m ? ChargedAmount : RepairCost;
                if (Status == DamageStatus.Open)
                {
                    Status = DamageStatus.Charged;
                }

                return;
            }

            ChargedAmount = 0m;
            if (Status == DamageStatus.Charged)
            {
                Status = DamageStatus.Open;
            }
        }
    }

    [NotMapped]
    public string? PhotoPath
    {
        get => Photos.OrderBy(item => item.SortOrder).Select(item => item.StoredPath).FirstOrDefault();
        set
        {
            Photos.Clear();
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            Photos.Add(new DamagePhoto
            {
                DamageId = Id,
                StoredPath = value,
                SortOrder = 0
            });
        }
    }

    public Vehicle? Vehicle { get; set; }

    public Rental? Rental { get; set; }

    public Employee? ReportedByEmployee { get; set; }

    public DamageStatusLookup? StatusLookup { get; set; }

    public ICollection<DamagePhoto> Photos { get; set; } = new List<DamagePhoto>();
}
