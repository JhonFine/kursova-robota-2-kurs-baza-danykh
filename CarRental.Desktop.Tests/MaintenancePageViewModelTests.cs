using CarRental.Desktop.Data;
using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Maintenance;
using CarRental.Desktop.ViewModels;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Desktop.Tests;

public sealed class MaintenancePageViewModelTests
{
    [Fact]
    public async Task RefreshAsync_ShouldPopulateVehicleOptions_WithLocalizedStatuses()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        var employee = await SeedMaintenanceContextAsync(dbContext);

        dbContext.Rentals.Add(new Rental
        {
            ClientId = 1,
            VehicleId = 1,
            CreatedByEmployeeId = employee.Id,
            ContractNumber = "CR-2026-300001",
            StartDate = DateTime.Today.AddDays(-1),
            EndDate = DateTime.Today.AddDays(1),
            PickupLocation = "Kyiv",
            ReturnLocation = "Kyiv",
            StartMileage = 12000,
            TotalAmount = 100m,
            StatusId = RentalStatus.Active
        });
        await dbContext.SaveChangesAsync();

        var viewModel = CreateViewModel(dbContext, employee);

        await viewModel.RefreshAsync();

        viewModel.Vehicles.Should().Contain(item => item.VehicleStatusDisplay == "Активна оренда");
        viewModel.Vehicles.Should().Contain(item => item.VehicleStatusDisplay == "На ремонті");
    }

    [Fact]
    public async Task ApplyHistoryFiltersCommand_ShouldFilterRecords_ByVehiclePeriodTypeAndSearch()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        var employee = await SeedMaintenanceContextAsync(dbContext);

        dbContext.MaintenanceRecords.AddRange(
            new MaintenanceRecord
            {
                VehicleId = 1,
                PerformedByEmployeeId = employee.Id,
                ServiceDate = new DateTime(2026, 3, 10),
                MileageAtService = 12000,
                NextServiceMileage = 22000,
                MaintenanceTypeCode = "SCHEDULED",
                Description = "Заміна масла та фільтрів",
                Cost = 4200m
            },
            new MaintenanceRecord
            {
                VehicleId = 1,
                PerformedByEmployeeId = employee.Id,
                ServiceDate = new DateTime(2026, 2, 10),
                MileageAtService = 11000,
                NextServiceMileage = 21000,
                MaintenanceTypeCode = "REPAIR",
                Description = "Ремонт ходової",
                Cost = 7500m
            },
            new MaintenanceRecord
            {
                VehicleId = 2,
                PerformedByEmployeeId = employee.Id,
                ServiceDate = new DateTime(2026, 3, 12),
                MileageAtService = 9000,
                NextServiceMileage = 19000,
                MaintenanceTypeCode = "SCHEDULED",
                Description = "Заміна масла",
                Cost = 3100m
            });
        await dbContext.SaveChangesAsync();

        var viewModel = CreateViewModel(dbContext, employee);
        await viewModel.RefreshAsync();

        viewModel.SelectedHistoryVehicleId = 1;
        viewModel.HistoryDateFrom = new DateTime(2026, 3, 1);
        viewModel.HistoryDateTo = new DateTime(2026, 3, 31);
        viewModel.SelectedHistoryMaintenanceType = "SCHEDULED";
        viewModel.HistorySearchText = "масла";

        await viewModel.ApplyHistoryFiltersCommand.ExecuteAsync(null);

        viewModel.Records.Should().ContainSingle();
        var record = viewModel.Records.Single();
        record.VehicleId.Should().Be(1);
        record.MaintenanceTypeCode.Should().Be("SCHEDULED");
        record.MileageDisplay.Should().Contain("км");
        record.CostDisplay.Should().Contain("грн");
    }

    [Fact]
    public async Task OpenVehicleFromMaintenanceCommand_ShouldInvokeNavigationCallback()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        var employee = await SeedMaintenanceContextAsync(dbContext);
        var viewModel = CreateViewModel(dbContext, employee);

        var capturedVehicleId = 0;
        var openDetails = false;
        viewModel.OpenVehicleRequestedAsync = (vehicleId, openCard) =>
        {
            capturedVehicleId = vehicleId;
            openDetails = openCard;
            return Task.CompletedTask;
        };

        await viewModel.OpenVehicleFromMaintenanceCommand.ExecuteAsync(2);

        capturedVehicleId.Should().Be(2);
        openDetails.Should().BeTrue();
    }

    [Fact]
    public async Task AddRecordCommand_ShouldRefreshHistoryAndDueItems()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        var employee = await SeedMaintenanceContextAsync(dbContext);
        var viewModel = CreateViewModel(dbContext, employee);

        await viewModel.RefreshAsync();
        viewModel.SelectedVehicle = viewModel.Vehicles.First(item => item.Id == 1);
        viewModel.MileageInput = "12000";
        viewModel.NextMileageInput = "12500";
        viewModel.CostInput = "1800";
        viewModel.Description = "Планове ТО";

        await viewModel.AddRecordCommand.ExecuteAsync(null);

        viewModel.Records.Should().Contain(item => item.VehicleId == 1 && item.Description == "Планове ТО");
        viewModel.DueItems.Should().Contain(item => item.VehicleId == 1);
    }

    private static MaintenancePageViewModel CreateViewModel(RentalDbContext dbContext, Employee employee)
    {
        return new MaintenancePageViewModel(
            dbContext,
            new MaintenanceService(dbContext),
            new PageRefreshCoordinator(_ => Task.CompletedTask),
            employee);
    }

    private static async Task<Employee> SeedMaintenanceContextAsync(RentalDbContext dbContext)
    {
        TestLookupSeed.SeedVehicleLookups(dbContext);

        var account = new Account
        {
            Login = $"maintenance-vm-{Guid.NewGuid():N}",
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
            PassportData = "PP-100",
            DriverLicense = "DL-100",
            PassportExpirationDate = DateTime.UtcNow.AddYears(5),
            DriverLicenseExpirationDate = DateTime.UtcNow.AddYears(5),
            Phone = "+380630000100",
            IsBlacklisted = false
        });

        var activeVehicle = TestLookupSeed.CreateVehicle(
            dbContext,
            "Toyota",
            "Corolla",
            "AA1111AA",
            "PETROL",
            "AUTO",
            1.8m,
            430m,
            6.4m,
            12000,
            60m,
            id: 1);
        var maintenanceVehicle = TestLookupSeed.CreateVehicle(
            dbContext,
            "BMW",
            "320d",
            "AA2222BB",
            "DIESEL",
            "AUTO",
            2m,
            480m,
            5.9m,
            18500,
            95m,
            id: 2);
        maintenanceVehicle.VehicleStatusCode = "MAINTENANCE";

        dbContext.Vehicles.AddRange(activeVehicle, maintenanceVehicle);

        await dbContext.SaveChangesAsync();
        return employee;
    }
}
