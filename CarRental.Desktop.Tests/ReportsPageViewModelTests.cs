using CarRental.Desktop.Data;
using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Analytics;
using CarRental.Desktop.ViewModels;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Desktop.Tests;

public sealed class ReportsPageViewModelTests
{
    [Fact]
    public async Task RefreshAsync_ShouldLoadSummaryWithoutPostgresDecimalSumFailure()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);

        dbContext.Rentals.Add(new Rental
        {
            ClientId = 900,
            VehicleId = 1,
            EmployeeId = 900,
            ContractNumber = "CR-2026-000090",
            StartDate = DateTime.UtcNow.Date.AddDays(-2),
            EndDate = DateTime.UtcNow.Date.AddDays(-1),
            StartMileage = 1000,
            TotalAmount = 120m,
            Status = RentalStatus.Closed,
            IsClosed = true,
            ClosedAtUtc = DateTime.UtcNow
        });
        dbContext.Damages.Add(new Damage
        {
            VehicleId = 1,
            Description = "Bumper",
            RepairCost = 45m,
            ActNumber = "ACT-20260305-0001"
        });
        await dbContext.SaveChangesAsync();

        var viewModel = new ReportsPageViewModel(dbContext, new StubAnalyticsExportService());
        var refresh = () => viewModel.RefreshAsync();

        await refresh.Should().NotThrowAsync();
        viewModel.TotalRevenue.Should().Be(120m);
        viewModel.TotalDamageCost.Should().Be(45m);
    }

    [Fact]
    public async Task RefreshAsync_ShouldExcludeClientCompatibilityEmployeesFromEmployeeFilters()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);

        var staffAccount = new Account
        {
            Login = "manager.staff",
            PasswordHash = "x",
            IsActive = true
        };
        var staffEmployee = new Employee
        {
            FullName = "Manager Staff",
            Role = UserRole.Manager,
            IsActive = true
        };

        var portalAccount = new Account
        {
            Login = "portal.user",
            PasswordHash = "x",
            IsActive = true
        };
        var portalClient = new Client
        {
            FullName = "Portal User",
            Phone = "+380501112233",
            PassportData = "PP2",
            DriverLicense = "DL2",
            PassportExpirationDate = DateTime.UtcNow.AddYears(5),
            DriverLicenseExpirationDate = DateTime.UtcNow.AddYears(5)
        };
        var compatibilityEmployee = new Employee
        {
            FullName = "Portal User",
            Role = UserRole.User,
            IsActive = true
        };

        dbContext.Accounts.Add(staffAccount);
        dbContext.Accounts.Add(portalAccount);
        await dbContext.SaveChangesAsync();

        staffEmployee.AccountId = staffAccount.Id;
        portalClient.AccountId = portalAccount.Id;
        compatibilityEmployee.AccountId = portalAccount.Id;

        dbContext.Employees.Add(staffEmployee);
        dbContext.Clients.Add(portalClient);
        dbContext.Employees.Add(compatibilityEmployee);
        await dbContext.SaveChangesAsync();
        var viewModel = new ReportsPageViewModel(dbContext, new StubAnalyticsExportService());

        await viewModel.RefreshAsync();

        var labels = viewModel.EmployeeFilters.Select(item => item.Label).ToList();
        labels.Should().Contain("Усі співробітники");
        labels.Should().Contain("Manager Staff");
        labels.Should().NotContain("Portal User");
    }

    private static void SeedMinimalData(RentalDbContext dbContext)
    {
        TestLookupSeed.SeedVehicleLookups(dbContext);

        var adminAccount = new Account
        {
            Id = 900,
            Login = "admin-seed",
            PasswordHash = "x",
            IsActive = true
        };

        dbContext.Accounts.Add(adminAccount);
        dbContext.Employees.Add(new Employee
        {
            Id = 900,
            FullName = "Admin",
            Role = UserRole.Admin,
            Account = adminAccount
        });
        dbContext.Clients.Add(new Client
        {
            Id = 900,
            FullName = "Client",
            PassportData = "PP1",
            DriverLicense = "DL1",
            PassportExpirationDate = DateTime.UtcNow.AddYears(5),
            DriverLicenseExpirationDate = DateTime.UtcNow.AddYears(5),
            Phone = "123",
            Blacklisted = false
        });
        dbContext.Vehicles.Add(new Vehicle
        {
            Id = 1,
            Make = "Toyota",
            Model = "Camry",
            FuelType = "Бензин",
            TransmissionType = "Автомат",
            PowertrainCapacityValue = 2m,
            PowertrainCapacityUnit = "L",
            CargoCapacityValue = 500m,
            CargoCapacityUnit = "L",
            ConsumptionValue = 7m,
            ConsumptionUnit = "L_PER_100KM",
            LicensePlate = "AA0011AA",
            Mileage = 1000,
            DailyRate = 70m,
            IsAvailable = true,
            ServiceIntervalKm = 10000
        });
        dbContext.SaveChanges();
    }

    private sealed class StubAnalyticsExportService : IAnalyticsExportService
    {
        public Task<string> ExportRentalsCsvAsync(ExportRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult("dummy.csv");

        public Task<string> ExportRentalsExcelAsync(ExportRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult("dummy.xlsx");
    }
}
