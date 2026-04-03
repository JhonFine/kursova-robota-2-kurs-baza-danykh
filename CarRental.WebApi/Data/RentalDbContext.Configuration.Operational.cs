using CarRental.Shared.ReferenceData;
using CarRental.WebApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRental.WebApi.Data;

public sealed partial class RentalDbContext
{
    private static void ConfigureVehicles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VehicleMake>(entity =>
        {
            entity.Property(v => v.Name).HasMaxLength(60).IsRequired();
            entity.Property(v => v.NormalizedName).HasMaxLength(60).IsRequired();
            UseUtcTimestamp(entity.Property(v => v.CreatedAtUtc));
            UseUtcTimestamp(entity.Property(v => v.UpdatedAtUtc));
            entity.HasIndex(v => v.NormalizedName).IsUnique();
        });

        modelBuilder.Entity<VehicleModel>(entity =>
        {
            entity.Property(v => v.Name).HasMaxLength(80).IsRequired();
            entity.Property(v => v.NormalizedName).HasMaxLength(80).IsRequired();
            UseUtcTimestamp(entity.Property(v => v.CreatedAtUtc));
            UseUtcTimestamp(entity.Property(v => v.UpdatedAtUtc));
            entity.HasAlternateKey(v => new { v.Id, v.MakeId });
            entity.HasIndex(v => new { v.MakeId, v.NormalizedName }).IsUnique();

            entity.HasOne(v => v.Make)
                .WithMany(m => m.Models)
                .HasForeignKey(v => v.MakeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasQueryFilter(v => !v.IsDeleted);
            entity.Ignore(v => v.IsAvailable);
            entity.Ignore(v => v.EngineDisplay);
            entity.Ignore(v => v.CargoCapacityDisplay);
            entity.Ignore(v => v.ConsumptionDisplay);
            entity.Ignore(v => v.FuelType);
            entity.Ignore(v => v.TransmissionType);
            entity.Ignore(v => v.IsBookable);
            entity.Ignore(v => v.PhotoPath);
            entity.Ignore(v => v.MakeName);
            entity.Ignore(v => v.ModelName);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Vehicles_Mileage_NonNegative", "\"Mileage\" >= 0");
                table.HasCheckConstraint("CK_Vehicles_DailyRate_Positive", "\"DailyRate\" > 0");
                table.HasCheckConstraint("CK_Vehicles_ServiceIntervalKm_Positive", "\"ServiceIntervalKm\" > 0");
                table.HasCheckConstraint("CK_Vehicles_DoorsCount_Range", "\"DoorsCount\" BETWEEN 1 AND 8");
                table.HasCheckConstraint("CK_Vehicles_PowertrainCapacity_Positive", "\"PowertrainCapacityValue\" > 0");
                table.HasCheckConstraint("CK_Vehicles_CargoCapacity_Positive", "\"CargoCapacityValue\" > 0");
                table.HasCheckConstraint("CK_Vehicles_Consumption_Positive", "\"ConsumptionValue\" > 0");
                table.HasCheckConstraint("CK_Vehicles_PowertrainCapacityUnit_Allowed", "\"PowertrainCapacityUnit\" IN ('L', 'KWH')");
                table.HasCheckConstraint("CK_Vehicles_CargoCapacityUnit_Allowed", "\"CargoCapacityUnit\" IN ('L', 'KG', 'SEATS')");
                table.HasCheckConstraint("CK_Vehicles_ConsumptionUnit_Allowed", "\"ConsumptionUnit\" IN ('L_PER_100KM', 'KWH_PER_100KM')");
            });
            entity.Property(v => v.PowertrainCapacityValue).HasPrecision(10, 2);
            entity.Property(v => v.PowertrainCapacityUnit).HasMaxLength(16).IsRequired();
            entity.Property(v => v.FuelTypeCode).HasMaxLength(30).HasDefaultValue(string.Empty).IsRequired();
            entity.Property(v => v.TransmissionTypeCode).HasMaxLength(30).HasDefaultValue(string.Empty).IsRequired();
            entity.Property(v => v.VehicleStatusCode).HasMaxLength(20).HasDefaultValue(CarRental.Shared.ReferenceData.VehicleStatuses.Ready).IsRequired();
            entity.Property(v => v.DoorsCount).HasDefaultValue(4);
            entity.Property(v => v.CargoCapacityValue).HasPrecision(10, 2);
            entity.Property(v => v.CargoCapacityUnit).HasMaxLength(16).IsRequired();
            entity.Property(v => v.ConsumptionValue).HasPrecision(10, 2);
            entity.Property(v => v.ConsumptionUnit).HasMaxLength(24).IsRequired();
            entity.Property(v => v.HasAirConditioning).HasDefaultValue(true);
            entity.Property(v => v.LicensePlate).HasMaxLength(30).IsRequired();
            entity.Property(v => v.DailyRate).HasPrecision(10, 2);
            entity.Property(v => v.ServiceIntervalKm).HasDefaultValue(10000);
            UseUtcTimestamp(entity.Property(v => v.CreatedAtUtc));
            UseUtcTimestamp(entity.Property(v => v.UpdatedAtUtc));
            entity.HasIndex(v => v.LicensePlate)
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
            entity.HasIndex(v => new { v.VehicleStatusCode, v.MakeId, v.ModelId });
            entity.HasIndex(v => v.IsDeleted);
            entity.Navigation(v => v.Photos).AutoInclude();
            entity.Navigation(v => v.MakeLookup).AutoInclude();
            entity.Navigation(v => v.ModelLookup).AutoInclude();

            entity.HasOne(v => v.MakeLookup)
                .WithMany(m => m.Vehicles)
                .HasForeignKey(v => v.MakeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(v => v.ModelLookup)
                .WithMany(m => m.Vehicles)
                .HasForeignKey(v => new { v.ModelId, v.MakeId })
                .HasPrincipalKey(m => new { m.Id, m.MakeId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(v => v.FuelTypeLookup)
                .WithMany(f => f.Vehicles)
                .HasForeignKey(v => v.FuelTypeCode)
                .HasPrincipalKey(f => f.Code)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(v => v.TransmissionTypeLookup)
                .WithMany(t => t.Vehicles)
                .HasForeignKey(v => v.TransmissionTypeCode)
                .HasPrincipalKey(t => t.Code)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(v => v.VehicleStatus)
                .WithMany(s => s.Vehicles)
                .HasForeignKey(v => v.VehicleStatusCode)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<VehiclePhoto>(entity =>
        {
            entity.HasQueryFilter(v => !v.IsDeleted);
            entity.Property(v => v.StoredPath).HasMaxLength(500).IsRequired();
            UseUtcTimestamp(entity.Property(v => v.CreatedAtUtc));
            UseUtcTimestamp(entity.Property(v => v.UpdatedAtUtc));
            entity.HasIndex(v => new { v.VehicleId, v.SortOrder })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");

            entity.HasOne(v => v.Vehicle)
                .WithMany(v => v.Photos)
                .HasForeignKey(v => v.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureRentals(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Rental>(entity =>
        {
            entity.Ignore(r => r.IsClosed);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Rentals_DateRange", "\"StartDate\" < \"EndDate\"");
                table.HasCheckConstraint("CK_Rentals_StartMileage_NonNegative", "\"StartMileage\" >= 0");
                table.HasCheckConstraint("CK_Rentals_EndMileage_Valid", "\"EndMileage\" IS NULL OR \"EndMileage\" >= \"StartMileage\"");
                table.HasCheckConstraint("CK_Rentals_OverageFee_NonNegative", "\"OverageFee\" >= 0");
                table.HasCheckConstraint("CK_Rentals_TotalAmount_NonNegative", "\"TotalAmount\" >= 0");
                table.HasCheckConstraint("CK_Rentals_Closed_Lifecycle", "\"StatusId\" <> 3 OR (\"ClosedAtUtc\" IS NOT NULL AND \"CanceledAtUtc\" IS NULL AND \"CancellationReason\" IS NULL)");
                table.HasCheckConstraint("CK_Rentals_Canceled_Lifecycle", "\"StatusId\" <> 4 OR (\"ClosedAtUtc\" IS NULL AND \"CanceledAtUtc\" IS NOT NULL AND length(btrim(COALESCE(\"CancellationReason\", ''))) > 0)");
                table.HasCheckConstraint("CK_Rentals_Open_Lifecycle", "\"StatusId\" IN (3, 4) OR (\"ClosedAtUtc\" IS NULL AND \"CanceledAtUtc\" IS NULL AND \"CancellationReason\" IS NULL)");
            });
            entity.Property(r => r.ContractNumber).HasMaxLength(40).IsRequired();
            UseLocalTimestamp(entity.Property(r => r.StartDate));
            UseLocalTimestamp(entity.Property(r => r.EndDate));
            entity.Property(r => r.PickupLocation).HasMaxLength(80).IsRequired();
            entity.Property(r => r.ReturnLocation).HasMaxLength(80).IsRequired();
            entity.Property(r => r.TotalAmount).HasPrecision(10, 2);
            entity.Property(r => r.OverageFee).HasPrecision(10, 2);
            entity.Property(r => r.CancellationReason).HasMaxLength(400);
            entity.Property(r => r.StatusId).HasConversion<int>();
            UseUtcTimestamp(entity.Property(r => r.CreatedAtUtc));
            UseUtcTimestamp(entity.Property(r => r.ClosedAtUtc));
            UseUtcTimestamp(entity.Property(r => r.CanceledAtUtc));
            entity.HasAlternateKey(r => new { r.Id, r.VehicleId });
            entity.HasIndex(r => r.ContractNumber).IsUnique();
            entity.HasIndex(r => new { r.VehicleId, r.StartDate, r.EndDate, r.StatusId });
            entity.HasIndex(r => new { r.ClientId, r.StatusId });
            entity.HasIndex(r => new { r.CreatedByEmployeeId, r.CreatedAtUtc });
            entity.HasIndex(r => r.CreatedAtUtc);

            entity.HasOne(r => r.Client)
                .WithMany(c => c.Rentals)
                .HasForeignKey(r => r.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Vehicle)
                .WithMany(v => v.Rentals)
                .HasForeignKey(r => r.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.CreatedByEmployee)
                .WithMany(e => e.CreatedRentals)
                .HasForeignKey(r => r.CreatedByEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.ClosedByEmployee)
                .WithMany(e => e.ClosedRentals)
                .HasForeignKey(r => r.ClosedByEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.CanceledByEmployee)
                .WithMany(e => e.CanceledRentals)
                .HasForeignKey(r => r.CanceledByEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.StatusLookup)
                .WithMany(s => s.Rentals)
                .HasForeignKey(r => r.StatusId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RentalStatusHistory>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_RentalStatusHistory_Source_Allowed",
                    "\"ChangedBySource\" IN ('EMPLOYEE', 'CLIENT', 'SYSTEM')");
            });
            entity.Property(v => v.FromStatusId).HasConversion<int?>();
            entity.Property(v => v.ToStatusId).HasConversion<int>();
            entity.Property(v => v.ChangedBySource).HasMaxLength(16).IsRequired();
            UseUtcTimestamp(entity.Property(v => v.ChangedAtUtc));
            entity.HasIndex(v => new { v.RentalId, v.ChangedAtUtc });

            entity.HasOne(v => v.Rental)
                .WithMany(r => r.StatusHistory)
                .HasForeignKey(v => v.RentalId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(v => v.ChangedByEmployee)
                .WithMany()
                .HasForeignKey(v => v.ChangedByEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureRentalInspections(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RentalInspection>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_RentalInspections_FuelPercent_Range", "\"FuelPercent\" IS NULL OR \"FuelPercent\" BETWEEN 0 AND 100");
            });
            entity.Property(i => i.TypeId).HasConversion<int>();
            UseUtcTimestamp(entity.Property(i => i.CompletedAtUtc));
            entity.Property(i => i.Notes).HasMaxLength(500);
            UseUtcTimestamp(entity.Property(i => i.CreatedAtUtc));
            UseUtcTimestamp(entity.Property(i => i.UpdatedAtUtc));
            entity.HasIndex(i => new { i.RentalId, i.TypeId }).IsUnique();

            entity.HasOne(i => i.Rental)
                .WithMany(r => r.Inspections)
                .HasForeignKey(i => i.RentalId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(i => i.PerformedByEmployee)
                .WithMany(e => e.Inspections)
                .HasForeignKey(i => i.PerformedByEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(i => i.TypeLookup)
                .WithMany(t => t.Inspections)
                .HasForeignKey(i => i.TypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
