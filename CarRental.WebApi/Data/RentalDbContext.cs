using CarRental.Shared.ReferenceData;
using CarRental.WebApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.WebApi.Data;

public sealed partial class RentalDbContext(DbContextOptions<RentalDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeRoleLookup> EmployeeRoles => Set<EmployeeRoleLookup>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientDocument> ClientDocuments => Set<ClientDocument>();
    public DbSet<ClientDocumentTypeLookup> ClientDocumentTypes => Set<ClientDocumentTypeLookup>();
    public DbSet<VehicleMake> VehicleMakes => Set<VehicleMake>();
    public DbSet<VehicleModel> VehicleModels => Set<VehicleModel>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehiclePhoto> VehiclePhotos => Set<VehiclePhoto>();
    public DbSet<VehicleStatusLookup> VehicleStatuses => Set<VehicleStatusLookup>();
    public DbSet<FuelTypeLookup> FuelTypes => Set<FuelTypeLookup>();
    public DbSet<TransmissionTypeLookup> TransmissionTypes => Set<TransmissionTypeLookup>();
    public DbSet<Rental> Rentals => Set<Rental>();
    public DbSet<RentalStatusLookup> RentalStatuses => Set<RentalStatusLookup>();
    public DbSet<RentalStatusHistory> RentalStatusHistory => Set<RentalStatusHistory>();
    public DbSet<RentalInspection> RentalInspections => Set<RentalInspection>();
    public DbSet<InspectionTypeLookup> InspectionTypes => Set<InspectionTypeLookup>();
    public DbSet<Damage> Damages => Set<Damage>();
    public DbSet<DamagePhoto> DamagePhotos => Set<DamagePhoto>();
    public DbSet<DamageStatusLookup> DamageStatuses => Set<DamageStatusLookup>();
    public DbSet<DamageStatusHistory> DamageStatusHistory => Set<DamageStatusHistory>();
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
}
