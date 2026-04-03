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

    public string DamageActNumber { get; set; } = string.Empty;

    public decimal ChargedAmount { get; set; }

    public DamageStatus StatusId { get; set; } = DamageStatus.Open;

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
                if (StatusId == DamageStatus.Open)
                {
                    StatusId = DamageStatus.Charged;
                }

                return;
            }

            ChargedAmount = 0m;
            if (StatusId == DamageStatus.Charged)
            {
                StatusId = DamageStatus.Open;
            }
        }
    }

    [NotMapped]
    public string? PhotoPath
    {
        get => Photos
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.SortOrder)
            .Select(item => item.StoredPath)
            .FirstOrDefault();
        set
        {
            var nextPath = value?.Trim();
            ReconcilePhotos(string.IsNullOrWhiteSpace(nextPath)
                ? Array.Empty<(string StoredPath, int SortOrder)>()
                : [(nextPath!, 0)]);
        }
    }

    public Vehicle? Vehicle { get; set; }

    public Rental? Rental { get; set; }

    public Employee? ReportedByEmployee { get; set; }

    public DamageStatusLookup? StatusLookup { get; set; }

    public ICollection<DamageStatusHistory> StatusHistory { get; set; } = new List<DamageStatusHistory>();

    public ICollection<DamagePhoto> Photos { get; set; } = new List<DamagePhoto>();

    public void ReconcilePhotos(IEnumerable<(string StoredPath, int SortOrder)> desiredPhotos)
    {
        var utcNow = DateTime.UtcNow;
        var desired = desiredPhotos
            .Where(item => !string.IsNullOrWhiteSpace(item.StoredPath))
            .Select(item => (StoredPath: item.StoredPath.Trim(), item.SortOrder))
            .ToList();

        foreach (var existing in Photos)
        {
            var match = desired.FirstOrDefault(item =>
                string.Equals(item.StoredPath, existing.StoredPath, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(match.StoredPath))
            {
                existing.IsDeleted = true;
                existing.UpdatedAtUtc = utcNow;
                continue;
            }

            existing.SortOrder = match.SortOrder;
            existing.IsDeleted = false;
            existing.UpdatedAtUtc = utcNow;
        }

        foreach (var missing in desired.Where(item =>
                     Photos.All(existing => !string.Equals(existing.StoredPath, item.StoredPath, StringComparison.OrdinalIgnoreCase))))
        {
            Photos.Add(new DamagePhoto
            {
                DamageId = Id,
                StoredPath = missing.StoredPath,
                SortOrder = missing.SortOrder,
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = utcNow
            });
        }
    }
}
