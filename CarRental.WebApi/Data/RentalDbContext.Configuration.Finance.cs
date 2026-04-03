using CarRental.Shared.ReferenceData;
using CarRental.WebApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRental.WebApi.Data;

public sealed partial class RentalDbContext
{
    private static void ConfigureDamages(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Damage>(entity =>
        {
            entity.Ignore(d => d.IsChargedToClient);
            entity.Ignore(d => d.PhotoPath);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Damages_RepairCost_Positive", "\"RepairCost\" > 0");
                table.HasCheckConstraint("CK_Damages_ChargedAmount_NonNegative", "\"ChargedAmount\" >= 0");
                table.HasCheckConstraint("CK_Damages_ChargedAmount_LteRepairCost", "\"ChargedAmount\" <= \"RepairCost\"");
                table.HasCheckConstraint("CK_Damages_Status_ChargeConsistency", "(\"StatusId\" <> 1 OR \"ChargedAmount\" = 0) AND (\"StatusId\" <> 2 OR \"ChargedAmount\" > 0)");
            });
            entity.Property(d => d.Description).HasMaxLength(500).IsRequired();
            UseLocalTimestamp(entity.Property(d => d.DateReported));
            entity.Property(d => d.DamageActNumber).HasMaxLength(60).IsRequired();
            entity.Property(d => d.RepairCost).HasPrecision(10, 2);
            entity.Property(d => d.ChargedAmount).HasPrecision(10, 2);
            entity.Property(d => d.StatusId).HasConversion<int>();
            UseUtcTimestamp(entity.Property(d => d.CreatedAtUtc));
            UseUtcTimestamp(entity.Property(d => d.UpdatedAtUtc));
            entity.HasIndex(d => d.DamageActNumber).IsUnique();
            entity.HasIndex(d => new { d.VehicleId, d.DateReported });
            entity.HasIndex(d => d.RentalId);
            entity.Navigation(d => d.Photos).AutoInclude();

            entity.HasOne(d => d.Vehicle)
                .WithMany(v => v.Damages)
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Rental)
                .WithMany(r => r.Damages)
                .HasForeignKey(d => new { d.RentalId, d.VehicleId })
                .HasPrincipalKey(r => new { r.Id, r.VehicleId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.ReportedByEmployee)
                .WithMany(e => e.ReportedDamages)
                .HasForeignKey(d => d.ReportedByEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.StatusLookup)
                .WithMany(s => s.Damages)
                .HasForeignKey(d => d.StatusId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DamageStatusHistory>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_DamageStatusHistory_Source_Allowed",
                    "\"ChangedBySource\" IN ('EMPLOYEE', 'CLIENT', 'SYSTEM')");
            });
            entity.Property(v => v.FromStatusId).HasConversion<int?>();
            entity.Property(v => v.ToStatusId).HasConversion<int>();
            entity.Property(v => v.ChangedBySource).HasMaxLength(16).IsRequired();
            UseUtcTimestamp(entity.Property(v => v.ChangedAtUtc));
            entity.HasIndex(v => new { v.DamageId, v.ChangedAtUtc });

            entity.HasOne(v => v.Damage)
                .WithMany(d => d.StatusHistory)
                .HasForeignKey(v => v.DamageId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(v => v.ChangedByEmployee)
                .WithMany()
                .HasForeignKey(v => v.ChangedByEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DamagePhoto>(entity =>
        {
            entity.HasQueryFilter(d => !d.IsDeleted);
            entity.Property(d => d.StoredPath).HasMaxLength(500).IsRequired();
            UseUtcTimestamp(entity.Property(d => d.CreatedAtUtc));
            UseUtcTimestamp(entity.Property(d => d.UpdatedAtUtc));
            entity.HasIndex(d => new { d.DamageId, d.SortOrder })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");

            entity.HasOne(d => d.Damage)
                .WithMany(d => d.Photos)
                .HasForeignKey(d => d.DamageId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurePayments(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Payments_Amount_Positive", "\"Amount\" > 0");
            });
            entity.Property(p => p.Amount).HasPrecision(10, 2);
            entity.Property(p => p.MethodId).HasConversion<int>();
            entity.Property(p => p.DirectionId).HasConversion<int>();
            entity.Property(p => p.StatusId).HasConversion<int>();
            entity.Property(p => p.ExternalTransactionId).HasMaxLength(120);
            UseUtcTimestamp(entity.Property(p => p.CreatedAtUtc));
            entity.Property(p => p.Notes).HasMaxLength(300);
            entity.HasIndex(p => new { p.RentalId, p.CreatedAtUtc });
            entity.HasIndex(p => new { p.RecordedByEmployeeId, p.CreatedAtUtc });
            entity.HasIndex(p => p.ExternalTransactionId).IsUnique();

            entity.HasOne(p => p.Rental)
                .WithMany(r => r.Payments)
                .HasForeignKey(p => p.RentalId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.RecordedByEmployee)
                .WithMany(e => e.RecordedPayments)
                .HasForeignKey(p => p.RecordedByEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.MethodLookup)
                .WithMany(m => m.Payments)
                .HasForeignKey(p => p.MethodId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.DirectionLookup)
                .WithMany(d => d.Payments)
                .HasForeignKey(p => p.DirectionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.StatusLookup)
                .WithMany(s => s.Payments)
                .HasForeignKey(p => p.StatusId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureMaintenanceRecords(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MaintenanceRecord>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_MaintenanceRecords_MileageAtService_NonNegative", "\"MileageAtService\" >= 0");
                table.HasCheckConstraint("CK_MaintenanceRecords_Cost_NonNegative", "\"Cost\" >= 0");
                table.HasCheckConstraint("CK_MaintenanceRecords_NextServiceMileage_Positive", "\"NextServiceMileage\" IS NULL OR \"NextServiceMileage\" > 0");
                table.HasCheckConstraint("CK_MaintenanceRecords_NextServiceMileage_GteCurrent", "\"NextServiceMileage\" IS NULL OR \"NextServiceMileage\" >= \"MileageAtService\"");
                table.HasCheckConstraint("CK_MaintenanceRecords_NextService_Planning", "\"NextServiceMileage\" IS NOT NULL OR \"NextServiceDate\" IS NOT NULL");
                table.HasCheckConstraint(
                    "CK_MaintenanceRecords_ServiceActor_Xor",
                    "(\"PerformedByEmployeeId\" IS NOT NULL AND length(btrim(COALESCE(\"ServiceProviderName\", ''))) = 0) " +
                    "OR (\"PerformedByEmployeeId\" IS NULL AND length(btrim(COALESCE(\"ServiceProviderName\", ''))) > 0)");
            });
            UseDate(entity.Property(m => m.ServiceDate));
            UseDate(entity.Property(m => m.NextServiceDate));
            entity.Property(m => m.Description).HasMaxLength(500).IsRequired();
            entity.Property(m => m.Cost).HasPrecision(10, 2);
            entity.Property(m => m.MaintenanceTypeCode).HasMaxLength(30).IsRequired();
            entity.Property(m => m.ServiceProviderName).HasMaxLength(120);
            entity.HasIndex(m => new { m.VehicleId, m.ServiceDate });

            entity.HasOne(m => m.Vehicle)
                .WithMany(v => v.MaintenanceRecords)
                .HasForeignKey(m => m.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.PerformedByEmployee)
                .WithMany(e => e.MaintenanceRecords)
                .HasForeignKey(m => m.PerformedByEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.MaintenanceType)
                .WithMany(t => t.Records)
                .HasForeignKey(m => m.MaintenanceTypeCode)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureContractSequences(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ContractSequence>(entity =>
        {
            entity.HasIndex(c => c.Year).IsUnique();
        });
    }
}
