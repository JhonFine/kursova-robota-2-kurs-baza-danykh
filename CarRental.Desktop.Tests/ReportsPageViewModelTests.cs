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
            ClientId = 1,
            VehicleId = 1,
            EmployeeId = 1,
            ContractNumber = "CR-2026-000090",
            StartDate = DateTime.Today.AddDays(-2),
            EndDate = DateTime.Today.AddDays(-1),
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

    private static void SeedMinimalData(RentalDbContext dbContext)
    {
        dbContext.Employees.Add(new Employee
        {
            Id = 1,
            FullName = "Admin",
            Login = "admin",
            PasswordHash = "x",
            Role = UserRole.Admin,
            IsActive = true
        });
        dbContext.Clients.Add(new Client
        {
            Id = 1,
            FullName = "Client",
            PassportData = "PP1",
            DriverLicense = "DL1",
            Phone = "123",
            Blacklisted = false
        });
        dbContext.Vehicles.Add(new Vehicle
        {
            Id = 1,
            Make = "Toyota",
            Model = "Camry",
            LicensePlate = "AA0011AA",
            Mileage = 1000,
            DailyRate = 70m,
            IsAvailable = true
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
