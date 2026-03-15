using CarRental.Desktop.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Desktop.Data;

public sealed class RentalDbContext(DbContextOptions<RentalDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<Client> Clients => Set<Client>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<Rental> Rentals => Set<Rental>();

    public DbSet<Damage> Damages => Set<Damage>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();

    public DbSet<ContractSequence> ContractSequences => Set<ContractSequence>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UnspecifiedDateTimeConverter>()
            .HaveColumnType("timestamp without time zone");
        configurationBuilder.Properties<DateTime?>()
            .HaveConversion<NullableUnspecifiedDateTimeConverter>()
            .HaveColumnType("timestamp without time zone");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Employees_Role_Range", "\"Role\" BETWEEN 1 AND 3");
                table.HasCheckConstraint("CK_Employees_FailedLoginAttempts_NonNegative", "\"FailedLoginAttempts\" >= 0");
            });
            entity.Property(e => e.FullName).HasMaxLength(120).IsRequired();
            entity.Property(e => e.Login).HasMaxLength(60).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(e => e.FailedLoginAttempts).HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.HasIndex(e => e.Login).IsUnique();
            entity.HasIndex(e => e.ClientId).IsUnique();

            entity.HasOne(e => e.Client)
                .WithOne()
                .HasForeignKey<Employee>(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Client>(entity =>
        {
            entity.Property(c => c.FullName).HasMaxLength(120).IsRequired();
            entity.Property(c => c.PassportData).HasMaxLength(120).IsRequired();
            entity.Property(c => c.DriverLicense).HasMaxLength(80).IsRequired();
            entity.Property(c => c.Phone).HasMaxLength(40).IsRequired();
            entity.HasIndex(c => c.DriverLicense).IsUnique();
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Vehicles_Mileage_NonNegative", "\"Mileage\" >= 0");
                table.HasCheckConstraint("CK_Vehicles_DailyRate_Positive", "\"DailyRate\" > 0");
                table.HasCheckConstraint("CK_Vehicles_ServiceIntervalKm_Positive", "\"ServiceIntervalKm\" > 0");
                table.HasCheckConstraint("CK_Vehicles_DoorsCount_Range", "\"DoorsCount\" BETWEEN 1 AND 8");
            });
            entity.Property(v => v.Make).HasMaxLength(60).IsRequired();
            entity.Property(v => v.Model).HasMaxLength(80).IsRequired();
            entity.Property(v => v.EngineDisplay).HasMaxLength(40).HasDefaultValue(string.Empty).IsRequired();
            entity.Property(v => v.FuelType).HasMaxLength(30).HasDefaultValue(string.Empty).IsRequired();
            entity.Property(v => v.TransmissionType).HasMaxLength(30).HasDefaultValue(string.Empty).IsRequired();
            entity.Property(v => v.DoorsCount).HasDefaultValue(4);
            entity.Property(v => v.CargoCapacityDisplay).HasMaxLength(40).HasDefaultValue(string.Empty).IsRequired();
            entity.Property(v => v.ConsumptionDisplay).HasMaxLength(40).HasDefaultValue(string.Empty).IsRequired();
            entity.Property(v => v.HasAirConditioning).HasDefaultValue(true);
            entity.Property(v => v.LicensePlate).HasMaxLength(30).IsRequired();
            entity.Property(v => v.DailyRate).HasPrecision(10, 2);
            entity.Property(v => v.ServiceIntervalKm).HasDefaultValue(10000);
            entity.Property(v => v.PhotoPath).HasMaxLength(500);
            entity.HasIndex(v => v.LicensePlate).IsUnique();
        });

        modelBuilder.Entity<Rental>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Rentals_DateRange", "\"StartDate\" <= \"EndDate\"");
                table.HasCheckConstraint("CK_Rentals_StartMileage_NonNegative", "\"StartMileage\" >= 0");
                table.HasCheckConstraint("CK_Rentals_EndMileage_Valid", "\"EndMileage\" IS NULL OR \"EndMileage\" >= \"StartMileage\"");
                table.HasCheckConstraint("CK_Rentals_OverageFee_NonNegative", "\"OverageFee\" >= 0");
                table.HasCheckConstraint("CK_Rentals_TotalAmount_NonNegative", "\"TotalAmount\" >= 0");
                table.HasCheckConstraint("CK_Rentals_Status_Range", "\"Status\" BETWEEN 1 AND 4");
                table.HasCheckConstraint("CK_Rentals_PickupFuelPercent_Range", "\"PickupFuelPercent\" IS NULL OR \"PickupFuelPercent\" BETWEEN 0 AND 100");
                table.HasCheckConstraint("CK_Rentals_ReturnFuelPercent_Range", "\"ReturnFuelPercent\" IS NULL OR \"ReturnFuelPercent\" BETWEEN 0 AND 100");
                table.HasCheckConstraint(
                    "CK_Rentals_IsClosed_MatchesStatus",
                    "(\"Status\" = 3) = \"IsClosed\"");
            });
            entity.Property(r => r.ContractNumber).HasMaxLength(40).IsRequired();
            entity.Property(r => r.PickupLocation).HasMaxLength(80).IsRequired();
            entity.Property(r => r.ReturnLocation).HasMaxLength(80).IsRequired();
            entity.Property(r => r.PickupInspectionNotes).HasMaxLength(500);
            entity.Property(r => r.ReturnInspectionNotes).HasMaxLength(500);
            entity.Property(r => r.TotalAmount).HasPrecision(10, 2);
            entity.Property(r => r.OverageFee).HasPrecision(10, 2);
            entity.Property(r => r.CancellationReason).HasMaxLength(400);
            entity.Property(r => r.Status).HasConversion<int>();
            entity.HasIndex(r => r.ContractNumber).IsUnique();

            entity.HasOne(r => r.Client)
                .WithMany(c => c.Rentals)
                .HasForeignKey(r => r.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Vehicle)
                .WithMany(v => v.Rentals)
                .HasForeignKey(r => r.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Employee)
                .WithMany(e => e.Rentals)
                .HasForeignKey(r => r.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Damage>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Damages_RepairCost_Positive", "\"RepairCost\" > 0");
                table.HasCheckConstraint("CK_Damages_ChargedAmount_NonNegative", "\"ChargedAmount\" >= 0");
                table.HasCheckConstraint("CK_Damages_ChargedAmount_LteRepairCost", "\"ChargedAmount\" <= \"RepairCost\"");
                if (Database.IsNpgsql())
                {
                    table.HasCheckConstraint(
                        "CK_Damages_ChargeFlag_Consistency",
                        "(\"IsChargedToClient\" AND \"ChargedAmount\" > 0) OR (NOT \"IsChargedToClient\" AND \"ChargedAmount\" = 0)");
                }
                table.HasCheckConstraint("CK_Damages_Status_Range", "\"Status\" BETWEEN 1 AND 3");
            });
            entity.Property(d => d.Description).HasMaxLength(500).IsRequired();
            entity.Property(d => d.ActNumber).HasMaxLength(60).IsRequired();
            entity.Property(d => d.PhotoPath).HasMaxLength(500);
            entity.Property(d => d.RepairCost).HasPrecision(10, 2);
            entity.Property(d => d.ChargedAmount).HasPrecision(10, 2);
            entity.Property(d => d.Status).HasConversion<int>();
            entity.HasIndex(d => d.ActNumber).IsUnique();

            entity.HasOne(d => d.Vehicle)
                .WithMany(v => v.Damages)
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Rental)
                .WithMany(r => r.Damages)
                .HasForeignKey(d => d.RentalId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Payments_Amount_Positive", "\"Amount\" > 0");
                table.HasCheckConstraint("CK_Payments_Method_Range", "\"Method\" BETWEEN 1 AND 2");
                table.HasCheckConstraint("CK_Payments_Direction_Range", "\"Direction\" BETWEEN 1 AND 2");
            });
            entity.Property(p => p.Amount).HasPrecision(10, 2);
            entity.Property(p => p.Method).HasConversion<int>();
            entity.Property(p => p.Direction).HasConversion<int>();
            entity.Property(p => p.Notes).HasMaxLength(300);

            entity.HasOne(p => p.Rental)
                .WithMany(r => r.Payments)
                .HasForeignKey(p => p.RentalId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.Employee)
                .WithMany(e => e.Payments)
                .HasForeignKey(p => p.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MaintenanceRecord>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_MaintenanceRecords_MileageAtService_NonNegative", "\"MileageAtService\" >= 0");
                table.HasCheckConstraint("CK_MaintenanceRecords_Cost_NonNegative", "\"Cost\" >= 0");
                table.HasCheckConstraint("CK_MaintenanceRecords_NextServiceMileage_Positive", "\"NextServiceMileage\" > 0");
                table.HasCheckConstraint(
                    "CK_MaintenanceRecords_NextServiceMileage_GteCurrent",
                    "\"NextServiceMileage\" >= \"MileageAtService\"");
            });
            entity.Property(m => m.Description).HasMaxLength(500).IsRequired();
            entity.Property(m => m.Cost).HasPrecision(10, 2);

            entity.HasOne(m => m.Vehicle)
                .WithMany(v => v.MaintenanceRecords)
                .HasForeignKey(m => m.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContractSequence>(entity =>
        {
            entity.HasIndex(c => c.Year).IsUnique();
        });
    }
}
