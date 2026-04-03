using CarRental.Desktop.Data;
using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Maintenance;
using FluentAssertions;

namespace CarRental.Desktop.Tests;

public sealed class MaintenanceServiceTests
{
    [Fact]
    public async Task GetDueItemsAsync_ShouldReturnOverdueAndSoonForecasts_WithLocalizedNotes()
    {
        var today = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Unspecified);

        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        var employee = await SeedMaintenanceContextAsync(dbContext);

        dbContext.MaintenanceRecords.AddRange(
            new MaintenanceRecord
            {
                VehicleId = 1,
                PerformedByEmployeeId = employee.Id,
                ServiceDate = today.AddDays(-30),
                MileageAtService = 19000,
                NextServiceMileage = 20000,
                MaintenanceTypeCode = "SCHEDULED",
                Description = "Past due service",
                Cost = 2500m
            },
            new MaintenanceRecord
            {
                VehicleId = 2,
                PerformedByEmployeeId = employee.Id,
                ServiceDate = today.AddDays(-15),
                MileageAtService = 19600,
                NextServiceMileage = 20500,
                MaintenanceTypeCode = "SCHEDULED",
                Description = "Upcoming service",
                Cost = 1900m
            });

        dbContext.Rentals.Add(new Rental
        {
            ClientId = 1,
            VehicleId = 2,
            CreatedByEmployeeId = employee.Id,
            ClosedByEmployeeId = employee.Id,
            ContractNumber = "CR-2026-200001",
            StartDate = today.AddDays(-12),
            EndDate = today.AddDays(-6),
            PickupLocation = "Kyiv",
            ReturnLocation = "Kyiv",
            StartMileage = 19000,
            EndMileage = 19600,
            TotalAmount = 100m,
            StatusId = RentalStatus.Closed,
            ClosedAtUtc = DateTime.SpecifyKind(today.AddDays(-6).AddHours(2), DateTimeKind.Utc)
        });

        await dbContext.SaveChangesAsync();

        var service = new MaintenanceService(dbContext);
        var dueItems = await service.GetDueItemsAsync();

        dueItems.Should().HaveCount(2);

        var overdueItem = dueItems.Single(item => item.VehicleId == 1);
        overdueItem.ForecastStatus.Should().Be(MaintenanceForecastStatus.Overdue);
        overdueItem.DistanceToNextServiceKm.Should().Be(-500);
        overdueItem.ForecastNotes.Should().Be("Заплановано за останнім сервісним записом");

        var soonItem = dueItems.Single(item => item.VehicleId == 2);
        soonItem.ForecastStatus.Should().Be(MaintenanceForecastStatus.Soon);
        soonItem.DistanceToNextServiceKm.Should().Be(900);
        soonItem.DaysToNextService.Should().Be(9);
    }

    [Fact]
    public async Task GetDueItemsAsync_ShouldUseNextServiceDateAsPrimaryDaysSource()
    {
        var today = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Unspecified);

        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        var employee = await SeedMaintenanceContextAsync(dbContext);

        dbContext.MaintenanceRecords.Add(new MaintenanceRecord
        {
            VehicleId = 1,
            PerformedByEmployeeId = employee.Id,
            ServiceDate = today.AddDays(-10),
            MileageAtService = 15000,
            NextServiceMileage = 26000,
            NextServiceDate = today.AddDays(7),
            MaintenanceTypeCode = "SCHEDULED",
            Description = "Date-based planning",
            Cost = 1700m
        });

        await dbContext.SaveChangesAsync();

        var service = new MaintenanceService(dbContext);
        var dueItem = (await service.GetDueItemsAsync()).Single();

        dueItem.ForecastStatus.Should().Be(MaintenanceForecastStatus.Soon);
        dueItem.DaysToNextService.Should().Be(7);
        dueItem.DistanceToNextServiceKm.Should().BeGreaterThan(1000);
    }

    private static async Task<Employee> SeedMaintenanceContextAsync(RentalDbContext dbContext)
    {
        TestLookupSeed.SeedVehicleLookups(dbContext);

        var account = new Account
        {
            Login = $"maintenance-{Guid.NewGuid():N}",
            PasswordHash = "x",
            IsActive = true
        };
        var employee = new Employee
        {
            FullName = "Maintenance Manager",
            RoleId = UserRole.Manager,
            Account = account
        };

        dbContext.Accounts.Add(account);
        dbContext.Employees.Add(employee);
        dbContext.Clients.Add(new Client
        {
            Id = 1,
            FullName = "Client",
            PassportData = "PP-001",
            DriverLicense = "DL-001",
            PassportExpirationDate = DateTime.UtcNow.AddYears(5),
            DriverLicenseExpirationDate = DateTime.UtcNow.AddYears(5),
            Phone = "+380630000001",
            IsBlacklisted = false
        });

        dbContext.Vehicles.AddRange(
            TestLookupSeed.CreateVehicle(
                dbContext,
                "Toyota",
                "Corolla",
                "AA0101AA",
                "PETROL",
                "AUTO",
                1.8m,
                430m,
                6.3m,
                20500,
                60m,
                id: 1),
            TestLookupSeed.CreateVehicle(
                dbContext,
                "BMW",
                "320d",
                "AA0202BB",
                "DIESEL",
                "AUTO",
                2m,
                480m,
                5.9m,
                19600,
                90m,
                id: 2));

        await dbContext.SaveChangesAsync();
        return employee;
    }
}
