using CarRental.Shared.ReferenceData;
using CarRental.WebApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.WebApi.Data;

public sealed partial class RentalDbContext
{
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
            entity.Property(e => e.RoleId).HasConversion<int>();
            UseUtcTimestamp(entity.Property(e => e.CreatedAtUtc));
            UseUtcTimestamp(entity.Property(e => e.UpdatedAtUtc));
            entity.HasIndex(e => e.AccountId).IsUnique();
            entity.HasIndex(e => new { e.RoleId, e.FullName });
            entity.Navigation(e => e.Account).AutoInclude();

            entity.HasOne(e => e.Account)
                .WithOne(a => a.Employee)
                .HasForeignKey<Employee>(e => e.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.RoleLookup)
                .WithMany(r => r.Employees)
                .HasForeignKey(e => e.RoleId)
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
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_Clients_Blacklist_Consistency",
                    "(NOT \"IsBlacklisted\" AND \"BlacklistReason\" IS NULL AND \"BlacklistedAtUtc\" IS NULL AND \"BlacklistedByEmployeeId\" IS NULL) " +
                    "OR (\"IsBlacklisted\" AND length(btrim(COALESCE(\"BlacklistReason\", ''))) > 0 AND \"BlacklistedAtUtc\" IS NOT NULL AND \"BlacklistedByEmployeeId\" IS NOT NULL)");
            });
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
            entity.HasIndex(c => c.BlacklistedByEmployeeId);
            entity.HasIndex(c => c.IsDeleted);
            entity.Navigation(c => c.Account).AutoInclude();
            entity.Navigation(c => c.Documents).AutoInclude();

            entity.HasOne(c => c.Account)
                .WithOne(a => a.Client)
                .HasForeignKey<Client>(c => c.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.BlacklistedByEmployee)
                .WithMany(e => e.BlacklistedClients)
                .HasForeignKey(c => c.BlacklistedByEmployeeId)
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
}
