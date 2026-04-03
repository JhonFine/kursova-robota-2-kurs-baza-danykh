using System.ComponentModel.DataAnnotations.Schema;
using CarRental.Shared.ReferenceData;

namespace CarRental.Desktop.Models;

public sealed class Vehicle : IAuditableEntity, ISoftDeletableEntity
{
    public int Id { get; set; }

    public int MakeId { get; set; }

    public int ModelId { get; set; }

    public decimal PowertrainCapacityValue { get; set; }

    public string PowertrainCapacityUnit { get; set; } = VehicleSpecificationUnits.Liters;

    public string FuelTypeCode { get; set; } = string.Empty;

    public string TransmissionTypeCode { get; set; } = string.Empty;

    public string VehicleStatusCode { get; set; } = VehicleStatuses.Ready;

    public int DoorsCount { get; set; } = 4;

    public decimal CargoCapacityValue { get; set; }

    public string CargoCapacityUnit { get; set; } = VehicleSpecificationUnits.Liters;

    public decimal ConsumptionValue { get; set; }

    public string ConsumptionUnit { get; set; } = VehicleSpecificationUnits.LitersPer100Km;

    public bool HasAirConditioning { get; set; } = true;

    public string LicensePlate { get; set; } = string.Empty;

    public int Mileage { get; set; }

    public decimal DailyRate { get; set; }

    public bool IsDeleted { get; set; }

    public int ServiceIntervalKm { get; set; } = 10000;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public bool IsAvailable
    {
        get => !IsDeleted &&
               string.Equals(VehicleStatusCode, VehicleStatuses.Ready, StringComparison.OrdinalIgnoreCase) &&
               Rentals.All(rental => rental.StatusId != RentalStatus.Active);
        set => IsBookable = value;
    }

    [NotMapped]
    public string EngineDisplay => VehicleSpecifications.FormatPowertrain(PowertrainCapacityValue, PowertrainCapacityUnit);

    [NotMapped]
    public string CargoCapacityDisplay => VehicleSpecifications.FormatCargoCapacity(CargoCapacityValue, CargoCapacityUnit);

    [NotMapped]
    public string ConsumptionDisplay => VehicleSpecifications.FormatConsumption(ConsumptionValue, ConsumptionUnit);

    [NotMapped]
    public string FuelType
    {
        get => FuelTypeCode;
        set => FuelTypeCode = value;
    }

    [NotMapped]
    public string TransmissionType
    {
        get => TransmissionTypeCode;
        set => TransmissionTypeCode = value;
    }

    [NotMapped]
    public bool IsBookable
    {
        get => string.Equals(VehicleStatusCode, VehicleStatuses.Ready, StringComparison.OrdinalIgnoreCase);
        set => VehicleStatusCode = value ? VehicleStatuses.Ready : VehicleStatuses.Inactive;
    }

    [NotMapped]
    public string? PhotoPath
    {
        get => Photos
            .Where(item => !item.IsDeleted)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.SortOrder)
            .Select(item => item.StoredPath)
            .FirstOrDefault();
        set
        {
            var nextPath = value?.Trim();
            ReconcilePhotos(string.IsNullOrWhiteSpace(nextPath)
                ? Array.Empty<(string StoredPath, int SortOrder, bool IsPrimary)>()
                : [(nextPath!, 0, true)]);
        }
    }

    [NotMapped]
    public string MakeName => MakeLookup?.Name ?? string.Empty;

    [NotMapped]
    public string ModelName => ModelLookup?.Name ?? string.Empty;

    public VehicleMake? MakeLookup { get; set; }

    public VehicleModel? ModelLookup { get; set; }

    public FuelTypeLookup? FuelTypeLookup { get; set; }

    public TransmissionTypeLookup? TransmissionTypeLookup { get; set; }

    public VehicleStatusLookup? VehicleStatus { get; set; }

    public ICollection<Rental> Rentals { get; set; } = new List<Rental>();

    public ICollection<Damage> Damages { get; set; } = new List<Damage>();

    public ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = new List<MaintenanceRecord>();

    public ICollection<VehiclePhoto> Photos { get; set; } = new List<VehiclePhoto>();

    public void ReconcilePhotos(IEnumerable<(string StoredPath, int SortOrder, bool IsPrimary)> desiredPhotos)
    {
        var utcNow = DateTime.UtcNow;
        var desired = desiredPhotos
            .Where(item => !string.IsNullOrWhiteSpace(item.StoredPath))
            .Select(item => (StoredPath: item.StoredPath.Trim(), item.SortOrder, item.IsPrimary))
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
            existing.IsPrimary = match.IsPrimary;
            existing.IsDeleted = false;
            existing.UpdatedAtUtc = utcNow;
        }

        foreach (var missing in desired.Where(item =>
                     Photos.All(existing => !string.Equals(existing.StoredPath, item.StoredPath, StringComparison.OrdinalIgnoreCase))))
        {
            Photos.Add(new VehiclePhoto
            {
                VehicleId = Id,
                StoredPath = missing.StoredPath,
                SortOrder = missing.SortOrder,
                IsPrimary = missing.IsPrimary,
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = utcNow
            });
        }
    }
}

