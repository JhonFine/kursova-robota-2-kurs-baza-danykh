using CarRental.Shared.ReferenceData;
using CarRental.Desktop.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Desktop.Data;

public sealed partial class RentalDbContext
{
    private void ApplyAuditMetadata()
    {
        RecordStatusHistory();

        var utcNow = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries().ToList())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity is IAuditableEntity addedAuditable)
                    {
                        if (addedAuditable.CreatedAtUtc == default)
                        {
                            addedAuditable.CreatedAtUtc = utcNow;
                        }

                        addedAuditable.UpdatedAtUtc = utcNow;
                    }
                    break;

                case EntityState.Modified:
                    if (entry.Entity is IAuditableEntity modifiedAuditable)
                    {
                        modifiedAuditable.UpdatedAtUtc = utcNow;
                    }

                    if (entry.Entity is Client modifiedClient && modifiedClient.IsDeleted)
                    {
                        foreach (var document in modifiedClient.Documents)
                        {
                            document.IsDeleted = true;
                            document.UpdatedAtUtc = utcNow;
                        }
                    }
                    break;

                case EntityState.Deleted when entry.Entity is ISoftDeletableEntity softDeletable:
                    entry.State = EntityState.Modified;
                    softDeletable.IsDeleted = true;

                    if (entry.Entity is Vehicle vehicle)
                    {
                        vehicle.VehicleStatusCode = CarRental.Shared.ReferenceData.VehicleStatuses.Inactive;
                    }

                    if (entry.Entity is Client client)
                    {
                        foreach (var document in client.Documents)
                        {
                            document.IsDeleted = true;
                            document.UpdatedAtUtc = utcNow;
                        }
                    }

                    if (entry.Entity is IAuditableEntity deletedAuditable)
                    {
                        deletedAuditable.UpdatedAtUtc = utcNow;
                    }
                    break;
            }

            NormalizeLocalTimestampProperties(entry.Entity);
        }
    }

    private void RecordStatusHistory()
    {
        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Rental>()
                     .Where(item => item.State is EntityState.Added or EntityState.Modified)
                     .ToList())
        {
            var currentStatus = entry.Entity.StatusId;
            RentalStatus? previousStatus = null;
            var shouldRecord = false;

            if (entry.State == EntityState.Added)
            {
                shouldRecord = currentStatus != default;
            }
            else
            {
                var property = entry.Property(nameof(Rental.StatusId));
                shouldRecord = property.IsModified;
                previousStatus = property.OriginalValue is RentalStatus status ? status : default(RentalStatus?);
                shouldRecord &= previousStatus != currentStatus;
            }

            if (!shouldRecord || entry.Entity.StatusHistory.Any(item => item.ToStatusId == currentStatus && item.FromStatusId == previousStatus))
            {
                continue;
            }

            var changedByEmployeeId = ResolveRentalHistoryEmployeeId(entry.Entity);
            entry.Entity.StatusHistory.Add(new RentalStatusHistory
            {
                Rental = entry.Entity,
                FromStatusId = previousStatus,
                ToStatusId = currentStatus,
                ChangedAtUtc = ResolveRentalStatusChangedAt(entry.Entity, utcNow),
                ChangedByEmployeeId = changedByEmployeeId,
                ChangedBySource = changedByEmployeeId.HasValue ? ChangeSources.Employee : ChangeSources.Client
            });
        }

        foreach (var entry in ChangeTracker.Entries<Damage>()
                     .Where(item => item.State is EntityState.Added or EntityState.Modified)
                     .ToList())
        {
            var currentStatus = entry.Entity.StatusId;
            DamageStatus? previousStatus = null;
            var shouldRecord = false;

            if (entry.State == EntityState.Added)
            {
                shouldRecord = currentStatus != default;
            }
            else
            {
                var property = entry.Property(nameof(Damage.StatusId));
                shouldRecord = property.IsModified;
                previousStatus = property.OriginalValue is DamageStatus status ? status : default(DamageStatus?);
                shouldRecord &= previousStatus != currentStatus;
            }

            if (!shouldRecord || entry.Entity.StatusHistory.Any(item => item.ToStatusId == currentStatus && item.FromStatusId == previousStatus))
            {
                continue;
            }

            entry.Entity.StatusHistory.Add(new DamageStatusHistory
            {
                Damage = entry.Entity,
                FromStatusId = previousStatus,
                ToStatusId = currentStatus,
                ChangedAtUtc = entry.Entity.UpdatedAtUtc == default ? utcNow : entry.Entity.UpdatedAtUtc,
                ChangedByEmployeeId = entry.Entity.ReportedByEmployeeId > 0 ? entry.Entity.ReportedByEmployeeId : null,
                ChangedBySource = entry.Entity.ReportedByEmployeeId > 0 ? ChangeSources.Employee : ChangeSources.System
            });
        }
    }

    private static int? ResolveRentalHistoryEmployeeId(Rental rental)
    {
        if (rental.StatusId == RentalStatus.Closed)
        {
            return rental.ClosedByEmployeeId;
        }

        if (rental.StatusId == RentalStatus.Canceled)
        {
            return rental.CanceledByEmployeeId;
        }

        return rental.CreatedByEmployeeId;
    }

    private static DateTime ResolveRentalStatusChangedAt(Rental rental, DateTime utcNow)
    {
        if (rental.StatusId == RentalStatus.Closed && rental.ClosedAtUtc.HasValue)
        {
            return rental.ClosedAtUtc.Value;
        }

        if (rental.StatusId == RentalStatus.Canceled && rental.CanceledAtUtc.HasValue)
        {
            return rental.CanceledAtUtc.Value;
        }

        return rental.CreatedAtUtc == default ? utcNow : rental.CreatedAtUtc;
    }

    private static void NormalizeLocalTimestampProperties(object entity)
    {
        switch (entity)
        {
            case ClientDocument document:
                document.ExpirationDate = NormalizeDateValue(document.ExpirationDate);
                break;

            case Rental rental:
                rental.StartDate = NormalizeLocalTimestamp(rental.StartDate);
                rental.EndDate = NormalizeLocalTimestamp(rental.EndDate);
                break;

            case Damage damage:
                damage.DateReported = NormalizeLocalTimestamp(damage.DateReported);
                break;

            case MaintenanceRecord maintenanceRecord:
                maintenanceRecord.ServiceDate = NormalizeDateOnly(maintenanceRecord.ServiceDate);
                maintenanceRecord.NextServiceDate = NormalizeDateValue(maintenanceRecord.NextServiceDate);
                break;
        }
    }

    private static DateTime NormalizeLocalTimestamp(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

    private static DateTime NormalizeDateOnly(DateTime value)
        => DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified);

    private static DateTime? NormalizeDateValue(DateTime? value)
        => value.HasValue ? DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Unspecified) : null;

    private static void UseUtcTimestamp(PropertyBuilder<DateTime> propertyBuilder)
        => propertyBuilder.HasColumnType("timestamp with time zone");

    private static void UseUtcTimestamp(PropertyBuilder<DateTime?> propertyBuilder)
        => propertyBuilder.HasColumnType("timestamp with time zone");

    private static void UseLocalTimestamp(PropertyBuilder<DateTime> propertyBuilder)
        => propertyBuilder.HasColumnType("timestamp without time zone");

    private static void UseLocalTimestamp(PropertyBuilder<DateTime?> propertyBuilder)
        => propertyBuilder.HasColumnType("timestamp without time zone");

    private static void UseDate(PropertyBuilder<DateTime> propertyBuilder)
        => propertyBuilder.HasColumnType("date");

    private static void UseDate(PropertyBuilder<DateTime?> propertyBuilder)
        => propertyBuilder.HasColumnType("date");

    private static void ConfigureEnumLookup<TLookup, TEnum>(
        EntityTypeBuilder<TLookup> entity,
        IReadOnlyDictionary<TEnum, string> values)
        where TLookup : class
        where TEnum : struct, Enum
    {
        entity.HasKey("Id");
        entity.Property("Id").HasConversion<int>();
        entity.Property<string>("DisplayName").HasMaxLength(60).IsRequired();

        var seed = values
            .Select(item =>
            {
                var instance = Activator.CreateInstance<TLookup>();
                typeof(TLookup).GetProperty("Id")!.SetValue(instance, item.Key);
                typeof(TLookup).GetProperty("DisplayName")!.SetValue(instance, item.Value);
                return instance;
            })
            .ToArray();

        entity.HasData(seed);
    }
}


