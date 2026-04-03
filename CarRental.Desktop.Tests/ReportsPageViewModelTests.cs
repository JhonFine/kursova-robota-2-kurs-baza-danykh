using CarRental.Desktop.Data;
using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Analytics;
using CarRental.Desktop.ViewModels;
using FluentAssertions;

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
            CreatedByEmployeeId = 900,
            ClosedByEmployeeId = 900,
            ContractNumber = "CR-2026-000090",
            StartDate = DateTime.UtcNow.Date.AddDays(-2),
            EndDate = DateTime.UtcNow.Date.AddDays(-1),
            StartMileage = 1000,
            TotalAmount = 120m,
            StatusId = RentalStatus.Closed,
            ClosedAtUtc = DateTime.UtcNow
        });
        dbContext.Damages.Add(new Damage
        {
            VehicleId = 1,
            ReportedByEmployeeId = 900,
            Description = "Bumper",
            RepairCost = 45m,
            DamageActNumber = "ACT-20260305-0001",
            StatusId = DamageStatus.Open
        });
        await dbContext.SaveChangesAsync();

        var viewModel = new ReportsPageViewModel(dbContext, new StubAnalyticsExportService());
        var refresh = () => viewModel.RefreshAsync();

        await refresh.Should().NotThrowAsync();
        viewModel.TotalRevenue.Should().Be(120m);
        viewModel.TotalDamageCost.Should().Be(45m);
    }

    [Fact]
    public async Task RefreshAsync_ShouldLoadAllEmployeesIntoFilters()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);

        var managerAccount = new Account
        {
            Login = "manager.staff",
            PasswordHash = "x",
            IsActive = true
        };

        dbContext.Accounts.Add(managerAccount);
        dbContext.Employees.Add(new Employee
        {
            FullName = "Manager Staff",
            RoleId = UserRole.Manager,
            Account = managerAccount
        });
        await dbContext.SaveChangesAsync();

        var viewModel = new ReportsPageViewModel(dbContext, new StubAnalyticsExportService());

        await viewModel.RefreshAsync();

        var labels = viewModel.EmployeeFilters.Select(item => item.Label).ToList();
        labels.Should().Contain("РЈСЃС– СЃРїС–РІСЂРѕР±С–С‚РЅРёРєРё");
        labels.Should().Contain("Admin");
        labels.Should().Contain("Manager Staff");
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
            RoleId = UserRole.Admin,
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
            IsBlacklisted = false
        });
        dbContext.Vehicles.Add(TestLookupSeed.CreateVehicle(
            dbContext,
            "Toyota",
            "Camry",
            "AA0011AA",
            "PETROL",
            "AUTO",
            2m,
            500m,
            7m,
            1000,
            70m,
            id: 1));
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
