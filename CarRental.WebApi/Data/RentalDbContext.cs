using CarRental.Shared.ReferenceData;
using CarRental.WebApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.WebApi.Data;

public sealed class RentalDbContext(DbContextOptions<RentalDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeRoleLookup> EmployeeRoles => Set<EmployeeRoleLookup>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientDocument> ClientDocuments => Set<ClientDocument>();
    public DbSet<ClientDocumentTypeLookup> ClientDocumentTypes => Set<ClientDocumentTypeLookup>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehiclePhoto> VehiclePhotos => Set<VehiclePhoto>();
    public DbSet<VehicleStatusLookup> VehicleStatuses => Set<VehicleStatusLookup>();
    public DbSet<FuelTypeLookup> FuelTypes => Set<FuelTypeLookup>();
    public DbSet<TransmissionTypeLookup> TransmissionTypes => Set<TransmissionTypeLookup>();
    public DbSet<Rental> Rentals => Set<Rental>();
    public DbSet<RentalStatusLookup> RentalStatuses => Set<RentalStatusLookup>();
    public DbSet<RentalInspection> RentalInspections => Set<RentalInspection>();
    public DbSet<InspectionTypeLookup> InspectionTypes => Set<InspectionTypeLookup>();
    public DbSet<Damage> Damages => Set<Damage>();
    public DbSet<DamagePhoto> DamagePhotos => Set<DamagePhoto>();
    public DbSet<DamageStatusLookup> DamageStatuses => Set<DamageStatusLookup>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentMethodLookup> PaymentMethods => Set<PaymentMethodLookup>();
    public DbSet<PaymentDirectionLookup> PaymentDirections => Set<PaymentDirectionLookup>();
    public DbSet<PaymentStatusLookup> PaymentStatuses => Set<PaymentStatusLookup>();
    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();
    public DbSet<MaintenanceTypeLookup> MaintenanceTypes => Set<MaintenanceTypeLookup>();
    public DbSet<ContractSequence> ContractSequences => Set<ContractSequence>();

    public override int SaveChanges()
    {
        ApplyAuditMetadata();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditMetadata();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditMetadata();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyAuditMetadata();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureAccounts(modelBuilder);
        ConfigureLookups(modelBuilder);
        ConfigureEmployees(modelBuilder);
        ConfigureClients(modelBuilder);
        ConfigureVehicles(modelBuilder);
        ConfigureRentals(modelBuilder);
        ConfigureRentalInspections(modelBuilder);
        ConfigureDamages(modelBuilder);
        ConfigurePayments(modelBuilder);
        ConfigureMaintenanceRecords(modelBuilder);
        ConfigureContractSequences(modelBuilder);
    }

    private static void ConfigureAccounts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Accounts_FailedLoginAttempts_NonNegative", "\"FailedLoginAttempts\" >= 0");
            });

            entity.Property(a => a.Login).HasMaxLength(60).IsRequired();
            entity.Property(a => a.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(a => a.FailedLoginAttempts).HasDefaultValue(0);
            entity.Property(a => a.IsActive).HasDefaultValue(true);
            UseUtcTimestamp(entity.Property(a => a.LockoutUntilUtc));
            UseUtcTimestamp(entity.Property(a => a.LastLoginUtc));
            UseUtcTimestamp(entity.Property(a => a.PasswordChangedAtUtc));
            UseUtcTimestamp(entity.Property(a => a.CreatedAtUtc));
            UseUtcTimestamp(entity.Property(a => a.UpdatedAtUtc));
            entity.HasIndex(a => a.Login).IsUnique();
            entity.Navigation(a => a.Client).AutoInclude();
            entity.Navigation(a => a.Employee).AutoInclude();
        });
    }

    private static void ConfigureLookups(ModelBuilder modelBuilder)
    {
        ConfigureEnumLookup(modelBuilder.Entity<EmployeeRoleLookup>(), new Dictionary<UserRole, string>
        {
            [UserRole.Admin] = "Admin",
            [UserRole.Manager] = "Manager",
            [UserRole.User] = "User"
        });

        ConfigureEnumLookup(modelBuilder.Entity<RentalStatusLookup>(), new Dictionary<RentalStatus, string>
        {
            [RentalStatus.Booked] = "Booked",
            [RentalStatus.Active] = "Active",
            [RentalStatus.Closed] = "Closed",
            [RentalStatus.Canceled] = "Canceled"
        });

        ConfigureEnumLookup(modelBuilder.Entity<PaymentMethodLookup>(), new Dictionary<PaymentMethod, string>
        {
            [PaymentMethod.Cash] = "Cash",
            [PaymentMethod.Card] = "Card"
        });

        ConfigureEnumLookup(modelBuilder.Entity<PaymentDirectionLookup>(), new Dictionary<PaymentDirection, string>
        {
            [PaymentDirection.Incoming] = "Incoming",
            [PaymentDirection.Refund] = "Refund"
        });

        ConfigureEnumLookup(modelBuilder.Entity<PaymentStatusLookup>(), new Dictionary<PaymentStatus, string>
        {
            [PaymentStatus.Pending] = "Pending",
            [PaymentStatus.Completed] = "Completed",
            [PaymentStatus.Canceled] = "Canceled",
            [PaymentStatus.Refunded] = "Refunded"
        });

        ConfigureEnumLookup(modelBuilder.Entity<DamageStatusLookup>(), new Dictionary<DamageStatus, string>
        {
            [DamageStatus.Open] = "Open",
            [DamageStatus.Charged] = "Charged",
            [DamageStatus.Resolved] = "Resolved"
        });

        ConfigureEnumLookup(modelBuilder.Entity<InspectionTypeLookup>(), new Dictionary<RentalInspectionType, string>
        {
            [RentalInspectionType.Pickup] = "Pickup",
            [RentalInspectionType.Return] = "Return"
        });

        modelBuilder.Entity<VehicleStatusLookup>(entity =>
        {
            entity.HasKey(v => v.Code);
            entity.Property(v => v.Code).HasMaxLength(20).IsRequired();
            entity.Property(v => v.DisplayName).HasMaxLength(60).IsRequired();
            entity.HasData(
                new VehicleStatusLookup { Code = CarRental.Shared.ReferenceData.VehicleStatuses.Ready, DisplayName = "Ready" },
                new VehicleStatusLookup { Code = CarRental.Shared.ReferenceData.VehicleStatuses.Rented, DisplayName = "Rented" },
                new VehicleStatusLookup { Code = CarRental.Shared.ReferenceData.VehicleStatuses.Maintenance, DisplayName = "Maintenance" },
                new VehicleStatusLookup { Code = CarRental.Shared.ReferenceData.VehicleStatuses.Damaged, DisplayName = "Damaged" },
                new VehicleStatusLookup { Code = CarRental.Shared.ReferenceData.VehicleStatuses.Inactive, DisplayName = "Inactive" });
        });

        modelBuilder.Entity<ClientDocumentTypeLookup>(entity =>
        {
            entity.HasKey(v => v.Code);
            entity.Property(v => v.Code).HasMaxLength(30).IsRequired();
            entity.Property(v => v.DisplayName).HasMaxLength(60).IsRequired();
            entity.HasData(
                new ClientDocumentTypeLookup { Code = CarRental.Shared.ReferenceData.ClientDocumentTypes.Passport, DisplayName = "Passport" },
                new ClientDocumentTypeLookup { Code = CarRental.Shared.ReferenceData.ClientDocumentTypes.DriverLicense, DisplayName = "Driver license" });
        });

        modelBuilder.Entity<MaintenanceTypeLookup>(entity =>
        {
            entity.HasKey(v => v.Code);
            entity.Property(v => v.Code).HasMaxLength(30).IsRequired();
            entity.Property(v => v.DisplayName).HasMaxLength(60).IsRequired();
            entity.HasData(
                new MaintenanceTypeLookup { Code = CarRental.Shared.ReferenceData.MaintenanceTypes.Scheduled, DisplayName = "Scheduled service" },
                new MaintenanceTypeLookup { Code = CarRental.Shared.ReferenceData.MaintenanceTypes.Repair, DisplayName = "Repair" },
                new MaintenanceTypeLookup { Code = CarRental.Shared.ReferenceData.MaintenanceTypes.Tires, DisplayName = "Tires" },
                new MaintenanceTypeLookup { Code = CarRental.Shared.ReferenceData.MaintenanceTypes.Inspection, DisplayName = "Inspection" });
        });

        modelBuilder.Entity<FuelTypeLookup>(entity =>
        {
            entity.HasKey(f => f.Code);
            entity.Property(f => f.Code).HasMaxLength(30).IsRequired();
            entity.Property(f => f.DisplayName).HasMaxLength(60).IsRequired();
            UseUtcTimestamp(entity.Property(f => f.CreatedAtUtc));
            UseUtcTimestamp(entity.Property(f => f.UpdatedAtUtc));
        });

        modelBuilder.Entity<TransmissionTypeLookup>(entity =>
        {
            entity.HasKey(t => t.Code);
            entity.Property(t => t.Code).HasMaxLength(30).IsRequired();
            entity.Property(t => t.DisplayName).HasMaxLength(60).IsRequired();
            UseUtcTimestamp(entity.Property(t => t.CreatedAtUtc));
            UseUtcTimestamp(entity.Property(t => t.UpdatedAtUtc));
        });
    }

    private static void ConfigureEmployees(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>(entity =>
        {
            entity.Ignore(e => e.Login);
            entity.Ignore(e => e.PasswordHash);
            entity.Ignore(e => e.IsActive);
            entity.Ignore(e => e.FailedLoginAttempts);
            entity.Ignore(e => e.LockoutUntilUtc);
            entity.Ignore(e => e.LastLoginUtc);
            entity.Ignore(e => e.PasswordChangedAtUtc);

            entity.Property(e => e.FullName).HasMaxLength(120).IsRequired();
            entity.Property(e => e.Role).HasConversion<int>();
            UseUtcTimestamp(entity.Property(e => e.CreatedAtUtc));
            UseUtcTimestamp(entity.Property(e => e.UpdatedAtUtc));
            entity.HasIndex(e => e.AccountId).IsUnique();
            entity.HasIndex(e => new { e.Role, e.FullName });
            entity.Navigation(e => e.Account).AutoInclude();

            entity.HasOne(e => e.Account)
                .WithOne(a => a.Employee)
                .HasForeignKey<Employee>(e => e.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.RoleLookup)
                .WithMany(r => r.Employees)
                .HasForeignKey(e => e.Role)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureClients(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>(entity =>
        {
            entity.HasQueryFilter(c => !c.IsDeleted);
            entity.Ignore(c => c.PassportData);
            entity.Ignore(c => c.PassportExpirationDate);
            entity.Ignore(c => c.PassportPhotoPath);
            entity.Ignore(c => c.DriverLicense);
            entity.Ignore(c => c.DriverLicenseExpirationDate);
            entity.Ignore(c => c.DriverLicensePhotoPath);
            entity.Ignore(c => c.Blacklisted);
            entity.Property(c => c.FullName).HasMaxLength(120).IsRequired();
            entity.Property(c => c.Phone).HasMaxLength(40).IsRequired();
            entity.Property(c => c.BlacklistReason).HasMaxLength(400);
            UseUtcTimestamp(entity.Property(c => c.BlacklistedAtUtc));
            UseUtcTimestamp(entity.Property(c => c.CreatedAtUtc));
            UseUtcTimestamp(entity.Property(c => c.UpdatedAtUtc));
            entity.HasIndex(c => c.AccountId).IsUnique();
            entity.HasIndex(c => c.Phone)
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
            entity.HasIndex(c => c.IsDeleted);
            entity.Navigation(c => c.Account).AutoInclude();
            entity.Navigation(c => c.Documents).AutoInclude();

            entity.HasOne(c => c.Account)
                .WithOne(a => a.Client)
                .HasForeignKey<Client>(c => c.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ClientDocument>(entity =>
        {
            entity.HasQueryFilter(d => !d.IsDeleted);
            entity.Property(d => d.DocumentTypeCode).HasMaxLength(30).IsRequired();
            entity.Property(d => d.DocumentNumber).HasMaxLength(120).IsRequired();
            entity.Property(d => d.StoredPath).HasMaxLength(500);
            UseDate(entity.Property(d => d.ExpirationDate));
            UseUtcTimestamp(entity.Property(d => d.CreatedAtUtc));
            UseUtcTimestamp(entity.Property(d => d.UpdatedAtUtc));
            entity.HasIndex(d => new { d.ClientId, d.DocumentTypeCode })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
            entity.HasIndex(d => new { d.DocumentTypeCode, d.DocumentNumber })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");

            entity.HasOne(d => d.Client)
                .WithMany(c => c.Documents)
                .HasForeignKey(d => d.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.DocumentType)
                .WithMany(t => t.Documents)
                .HasForeignKey(d => d.DocumentTypeCode)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureVehicles(ModelBuilder modelBuilder)
    {
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
            entity.Property(v => v.Make).HasMaxLength(60).IsRequired();
            entity.Property(v => v.Model).HasMaxLength(80).IsRequired();
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
            entity.HasIndex(v => new { v.VehicleStatusCode, v.Make, v.Model });
            entity.HasIndex(v => v.IsDeleted);
            entity.Navigation(v => v.Photos).AutoInclude();

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
            entity.Property(v => v.StoredPath).HasMaxLength(500).IsRequired();
            UseUtcTimestamp(entity.Property(v => v.CreatedAtUtc));
            entity.HasIndex(v => new { v.VehicleId, v.SortOrder }).IsUnique();

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
            entity.Ignore(r => r.EmployeeId);
            entity.Ignore(r => r.Employee);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Rentals_DateRange", "\"StartDate\" <= \"EndDate\"");
                table.HasCheckConstraint("CK_Rentals_StartMileage_NonNegative", "\"StartMileage\" >= 0");
                table.HasCheckConstraint("CK_Rentals_EndMileage_Valid", "\"EndMileage\" IS NULL OR \"EndMileage\" >= \"StartMileage\"");
                table.HasCheckConstraint("CK_Rentals_OverageFee_NonNegative", "\"OverageFee\" >= 0");
                table.HasCheckConstraint("CK_Rentals_TotalAmount_NonNegative", "\"TotalAmount\" >= 0");
                table.HasCheckConstraint("CK_Rentals_Closed_Lifecycle", "\"Status\" <> 3 OR (\"ClosedAtUtc\" IS NOT NULL AND \"CanceledAtUtc\" IS NULL AND \"CancellationReason\" IS NULL)");
                table.HasCheckConstraint("CK_Rentals_Canceled_Lifecycle", "\"Status\" <> 4 OR (\"ClosedAtUtc\" IS NULL AND \"CanceledAtUtc\" IS NOT NULL AND length(btrim(COALESCE(\"CancellationReason\", ''))) > 0)");
                table.HasCheckConstraint("CK_Rentals_Open_Lifecycle", "\"Status\" IN (3, 4) OR (\"ClosedAtUtc\" IS NULL AND \"CanceledAtUtc\" IS NULL AND \"CancellationReason\" IS NULL)");
            });
            entity.Property(r => r.ContractNumber).HasMaxLength(40).IsRequired();
            UseLocalTimestamp(entity.Property(r => r.StartDate));
            UseLocalTimestamp(entity.Property(r => r.EndDate));
            entity.Property(r => r.PickupLocation).HasMaxLength(80).IsRequired();
            entity.Property(r => r.ReturnLocation).HasMaxLength(80).IsRequired();
            entity.Property(r => r.TotalAmount).HasPrecision(10, 2);
            entity.Property(r => r.OverageFee).HasPrecision(10, 2);
            entity.Property(r => r.CancellationReason).HasMaxLength(400);
            entity.Property(r => r.Status).HasConversion<int>();
            UseUtcTimestamp(entity.Property(r => r.CreatedAtUtc));
            UseUtcTimestamp(entity.Property(r => r.ClosedAtUtc));
            UseUtcTimestamp(entity.Property(r => r.CanceledAtUtc));
            entity.HasAlternateKey(r => new { r.Id, r.VehicleId });
            entity.HasIndex(r => r.ContractNumber).IsUnique();
            entity.HasIndex(r => new { r.VehicleId, r.StartDate, r.EndDate, r.Status });
            entity.HasIndex(r => new { r.ClientId, r.Status });
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
                .HasForeignKey(r => r.Status)
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
            entity.Property(i => i.Type).HasConversion<int>();
            UseUtcTimestamp(entity.Property(i => i.CompletedAtUtc));
            entity.Property(i => i.Notes).HasMaxLength(500);
            UseUtcTimestamp(entity.Property(i => i.CreatedAtUtc));
            UseUtcTimestamp(entity.Property(i => i.UpdatedAtUtc));
            entity.HasIndex(i => new { i.RentalId, i.Type }).IsUnique();

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
                .HasForeignKey(i => i.Type)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

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
                table.HasCheckConstraint("CK_Damages_Status_ChargeConsistency", "(\"Status\" <> 1 OR \"ChargedAmount\" = 0) AND (\"Status\" <> 2 OR \"ChargedAmount\" > 0)");
            });
            entity.Property(d => d.Description).HasMaxLength(500).IsRequired();
            UseLocalTimestamp(entity.Property(d => d.DateReported));
            entity.Property(d => d.ActNumber).HasMaxLength(60).IsRequired();
            entity.Property(d => d.RepairCost).HasPrecision(10, 2);
            entity.Property(d => d.ChargedAmount).HasPrecision(10, 2);
            entity.Property(d => d.Status).HasConversion<int>();
            UseUtcTimestamp(entity.Property(d => d.CreatedAtUtc));
            UseUtcTimestamp(entity.Property(d => d.UpdatedAtUtc));
            entity.HasIndex(d => d.ActNumber).IsUnique();
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
                .HasForeignKey(d => d.Status)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DamagePhoto>(entity =>
        {
            entity.Property(d => d.StoredPath).HasMaxLength(500).IsRequired();
            UseUtcTimestamp(entity.Property(d => d.CreatedAtUtc));
            entity.HasIndex(d => new { d.DamageId, d.SortOrder }).IsUnique();

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
            entity.Property(p => p.Method).HasConversion<int>();
            entity.Property(p => p.Direction).HasConversion<int>();
            entity.Property(p => p.Status).HasConversion<int>();
            entity.Property(p => p.ExternalTransactionId).HasMaxLength(120);
            UseUtcTimestamp(entity.Property(p => p.CreatedAtUtc));
            entity.Property(p => p.Notes).HasMaxLength(300);
            entity.HasIndex(p => new { p.RentalId, p.CreatedAtUtc });
            entity.HasIndex(p => new { p.EmployeeId, p.CreatedAtUtc });
            entity.HasIndex(p => p.ExternalTransactionId).IsUnique();

            entity.HasOne(p => p.Rental)
                .WithMany(r => r.Payments)
                .HasForeignKey(p => p.RentalId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.Employee)
                .WithMany(e => e.Payments)
                .HasForeignKey(p => p.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.MethodLookup)
                .WithMany(m => m.Payments)
                .HasForeignKey(p => p.Method)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.DirectionLookup)
                .WithMany(d => d.Payments)
                .HasForeignKey(p => p.Direction)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.StatusLookup)
                .WithMany(s => s.Payments)
                .HasForeignKey(p => p.Status)
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
                table.HasCheckConstraint("CK_MaintenanceRecords_NextServiceMileage_Positive", "\"NextServiceMileage\" > 0");
                table.HasCheckConstraint("CK_MaintenanceRecords_NextServiceMileage_GteCurrent", "\"NextServiceMileage\" >= \"MileageAtService\"");
            });
            UseLocalTimestamp(entity.Property(m => m.ServiceDate));
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

    private void ApplyAuditMetadata()
    {
        EnsureLegacyIdentityCompatibility();
        EnsureLegacyOperationalCompatibility();

        var utcNow = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
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

    private void EnsureLegacyIdentityCompatibility()
    {
        var employees = ChangeTracker.Entries<Employee>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .Select(entry => entry.Entity)
            .ToList();

        foreach (var employee in employees)
        {
            EnsureEmployeeAccount(employee);
        }

        foreach (var employee in employees)
        {
            if (!employee.PendingPortalClientId.HasValue || employee.Account is null)
            {
                continue;
            }

            var client = ChangeTracker.Entries<Client>()
                .Select(entry => entry.Entity)
                .FirstOrDefault(item => item.Id == employee.PendingPortalClientId.Value);

            if (client is null)
            {
                continue;
            }

            client.Account ??= employee.Account;
            client.AccountId = employee.Account.Id;
        }
    }

    private void EnsureEmployeeAccount(Employee employee)
    {
        if (employee.Account is not null)
        {
            if (employee.AccountId == default && employee.Account.Id > 0)
            {
                employee.AccountId = employee.Account.Id;
            }

            return;
        }

        if (employee.AccountId > 0)
        {
            var trackedAccount = ChangeTracker.Entries<Account>()
                .Select(entry => entry.Entity)
                .FirstOrDefault(account => account.Id == employee.AccountId);
            if (trackedAccount is not null)
            {
                employee.Account = trackedAccount;
                return;
            }
        }

        var normalizedLogin = string.IsNullOrWhiteSpace(employee.Login)
            ? $"employee-{(employee.Id > 0 ? employee.Id : Guid.NewGuid().ToString("N"))}"
            : employee.Login.Trim().ToLowerInvariant();

        var trackedByLogin = ChangeTracker.Entries<Account>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(account => string.Equals(account.Login, normalizedLogin, StringComparison.OrdinalIgnoreCase));
        if (trackedByLogin is not null)
        {
            employee.Account = trackedByLogin;
            employee.AccountId = trackedByLogin.Id;
            return;
        }

        var account = new Account
        {
            Id = employee.Id > 0 &&
                 !ChangeTracker.Entries<Account>().Any(entry => entry.Entity.Id == employee.Id)
                ? employee.Id
                : default,
            Login = normalizedLogin,
            PasswordHash = string.IsNullOrWhiteSpace(employee.PasswordHash) ? "legacy-password" : employee.PasswordHash,
            IsActive = employee.IsActive || employee.Account is not null,
            FailedLoginAttempts = employee.FailedLoginAttempts,
            LockoutUntilUtc = employee.LockoutUntilUtc,
            LastLoginUtc = employee.LastLoginUtc,
            PasswordChangedAtUtc = employee.PasswordChangedAtUtc == default ? DateTime.UtcNow : employee.PasswordChangedAtUtc
        };

        Accounts.Add(account);
        employee.Account = account;
        employee.AccountId = account.Id;
    }

    private void EnsureLegacyOperationalCompatibility()
    {
        int? fallbackEmployeeId = null;

        foreach (var rental in ChangeTracker.Entries<Rental>()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
                     .Select(entry => entry.Entity))
        {
            if (rental.CreatedByEmployeeId <= 0 && rental.CreatedByEmployee is not null)
            {
                rental.CreatedByEmployeeId = rental.CreatedByEmployee.Id;
            }

            if (rental.Status == RentalStatus.Closed && !rental.ClosedByEmployeeId.HasValue)
            {
                rental.ClosedByEmployeeId = rental.CreatedByEmployeeId > 0
                    ? rental.CreatedByEmployeeId
                    : ResolveFallbackEmployeeId(ref fallbackEmployeeId);
            }

            if (rental.Status == RentalStatus.Canceled && !rental.CanceledByEmployeeId.HasValue)
            {
                rental.CanceledByEmployeeId = rental.CreatedByEmployeeId > 0
                    ? rental.CreatedByEmployeeId
                    : ResolveFallbackEmployeeId(ref fallbackEmployeeId);
            }
        }

        foreach (var inspection in ChangeTracker.Entries<RentalInspection>()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
                     .Select(entry => entry.Entity))
        {
            if (inspection.PerformedByEmployeeId > 0)
            {
                continue;
            }

            inspection.PerformedByEmployeeId = ResolveRentalActorEmployeeId(inspection.RentalId, inspection.Rental, ref fallbackEmployeeId);
        }

        foreach (var damage in ChangeTracker.Entries<Damage>()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
                     .Select(entry => entry.Entity))
        {
            if (damage.ReportedByEmployeeId <= 0)
            {
                damage.ReportedByEmployeeId = ResolveRentalActorEmployeeId(damage.RentalId, damage.Rental, ref fallbackEmployeeId);
            }
        }

        foreach (var payment in ChangeTracker.Entries<Payment>()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
                     .Select(entry => entry.Entity))
        {
            if (payment.Status == default)
            {
                payment.Status = PaymentStatus.Completed;
            }
        }
    }

    private int ResolveRentalActorEmployeeId(int? rentalId, Rental? rental, ref int? fallbackEmployeeId)
    {
        if (rental?.CreatedByEmployeeId > 0)
        {
            return rental.CreatedByEmployeeId;
        }

        if (rentalId.HasValue)
        {
            var trackedRental = ChangeTracker.Entries<Rental>()
                .Select(entry => entry.Entity)
                .FirstOrDefault(item => item.Id == rentalId.Value);
            if (trackedRental?.CreatedByEmployeeId > 0)
            {
                return trackedRental.CreatedByEmployeeId;
            }
        }

        return ResolveFallbackEmployeeId(ref fallbackEmployeeId);
    }

    private int ResolveFallbackEmployeeId(ref int? fallbackEmployeeId)
    {
        if (fallbackEmployeeId.HasValue && fallbackEmployeeId.Value > 0)
        {
            return fallbackEmployeeId.Value;
        }

        fallbackEmployeeId = ChangeTracker.Entries<Employee>()
            .Where(entry => entry.Entity.Id > 0)
            .Select(entry => (int?)entry.Entity.Id)
            .FirstOrDefault();

        if (fallbackEmployeeId.HasValue && fallbackEmployeeId.Value > 0)
        {
            return fallbackEmployeeId.Value;
        }

        fallbackEmployeeId = Employees
            .AsNoTracking()
            .OrderBy(entry => entry.Role == UserRole.Admin ? 0 : 1)
            .ThenBy(entry => entry.Id)
            .Select(entry => (int?)entry.Id)
            .FirstOrDefault();

        return fallbackEmployeeId ?? 1;
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
                maintenanceRecord.ServiceDate = NormalizeLocalTimestamp(maintenanceRecord.ServiceDate);
                break;
        }
    }

    private static DateTime NormalizeLocalTimestamp(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

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
