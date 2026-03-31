using CarRental.Desktop.Data;
using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Damages;
using CarRental.Desktop.ViewModels;
using FluentAssertions;

namespace CarRental.Desktop.Tests;

public sealed class DamagesPageViewModelTests
{
    [Fact]
    public async Task RefreshAsync_ShouldKeepRentalSelectionDisabled_UntilVehicleIsChosen()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);

        var viewModel = CreateViewModel(dbContext);

        await viewModel.RefreshAsync();

        viewModel.SelectedVehicle.Should().BeNull();
        viewModel.CanSelectRental.Should().BeFalse();
        viewModel.Rentals.Should().ContainSingle();
        viewModel.SelectedRental.Should().Be(viewModel.Rentals[0]);
        viewModel.Rentals[0].Id.Should().BeNull();
        viewModel.Rentals[0].Display.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SelectingVehicle_ShouldFilterRentalsAndResetInvalidRental()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);

        dbContext.Vehicles.Add(new Vehicle
        {
            Id = 2,
            Make = "BMW",
            Model = "M5 F90",
            FuelType = "Бензин",
            TransmissionType = "Автомат",
            PowertrainCapacityValue = 4.4m,
            PowertrainCapacityUnit = "L",
            CargoCapacityValue = 530m,
            CargoCapacityUnit = "L",
            ConsumptionValue = 11m,
            ConsumptionUnit = "L_PER_100KM",
            LicensePlate = "AA0022AA",
            Mileage = 2500,
            DailyRate = 120m,
            IsBookable = true,
            IsAvailable = true,
            ServiceIntervalKm = 10000
        });
        dbContext.Rentals.AddRange(
            new Rental
            {
                ClientId = 1,
                VehicleId = 1,
                EmployeeId = 1,
                ContractNumber = "CR-2026-000079",
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow.AddDays(2),
                PickupLocation = "Kyiv",
                ReturnLocation = "Kyiv",
                StartMileage = 1000,
                TotalAmount = 300m,
                Status = RentalStatus.Active,
                IsClosed = false,
                CreatedAtUtc = DateTime.UtcNow
            },
            new Rental
            {
                ClientId = 1,
                VehicleId = 1,
                EmployeeId = 1,
                ContractNumber = "CR-2026-000078",
                StartDate = DateTime.UtcNow.AddDays(-5),
                EndDate = DateTime.UtcNow.AddDays(-2),
                PickupLocation = "Kyiv",
                ReturnLocation = "Kyiv",
                StartMileage = 900,
                TotalAmount = 250m,
                Status = RentalStatus.Closed,
                IsClosed = true,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-5)
            },
            new Rental
            {
                ClientId = 1,
                VehicleId = 2,
                EmployeeId = 1,
                ContractNumber = "CR-2026-000077",
                StartDate = DateTime.UtcNow.AddDays(-2),
                EndDate = DateTime.UtcNow.AddDays(1),
                PickupLocation = "Lviv",
                ReturnLocation = "Lviv",
                StartMileage = 2400,
                TotalAmount = 700m,
                Status = RentalStatus.Active,
                IsClosed = false,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-2)
            });
        await dbContext.SaveChangesAsync();

        var viewModel = CreateViewModel(dbContext);

        await viewModel.RefreshAsync();

        viewModel.SelectedVehicle = viewModel.Vehicles.Single(item => item.Id == 1);
        await WaitForAsync(() => viewModel.Rentals.Count == 3);

        viewModel.CanSelectRental.Should().BeTrue();
        viewModel.Rentals.Select(item => item.Display).Should().Contain(item => item.Contains("CR-2026-000079"));
        viewModel.Rentals.Select(item => item.Display).Should().Contain(item => item.Contains("CR-2026-000078"));
        viewModel.Rentals.Select(item => item.Display).Should().NotContain(item => item.Contains("CR-2026-000077"));

        viewModel.SelectedRental = viewModel.Rentals.Single(item => item.Id.HasValue && item.Display.Contains("CR-2026-000079"));
        viewModel.SelectedVehicle = viewModel.Vehicles.Single(item => item.Id == 2);

        await WaitForAsync(() => viewModel.Rentals.Count == 2);

        viewModel.Rentals.Select(item => item.Display).Should().Contain(item => item.Contains("CR-2026-000077"));
        viewModel.Rentals.Select(item => item.Display).Should().NotContain(item => item.Contains("CR-2026-000079"));
        viewModel.SelectedRental.Should().NotBeNull();
        viewModel.SelectedRental!.Id.Should().BeNull();
        viewModel.SelectedRental.Display.Should().NotBeNullOrWhiteSpace();
    }

    private static DamagesPageViewModel CreateViewModel(RentalDbContext dbContext)
    {
        return new DamagesPageViewModel(
            dbContext,
            new StubDamageService(),
            new PageRefreshCoordinator(_ => Task.CompletedTask));
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var startedAt = DateTime.UtcNow;
        while (DateTime.UtcNow - startedAt < TimeSpan.FromSeconds(5))
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("Timed out waiting for rental options to refresh.");
    }

    private static void SeedMinimalData(RentalDbContext dbContext)
    {
        TestLookupSeed.SeedVehicleLookups(dbContext);

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
            PassportExpirationDate = DateTime.UtcNow.AddYears(5),
            DriverLicenseExpirationDate = DateTime.UtcNow.AddYears(5),
            Phone = "123",
            Blacklisted = false
        });
        dbContext.Vehicles.Add(new Vehicle
        {
            Id = 1,
            Make = "Audi",
            Model = "A4",
            FuelType = "Бензин",
            TransmissionType = "Автомат",
            PowertrainCapacityValue = 2m,
            PowertrainCapacityUnit = "L",
            CargoCapacityValue = 480m,
            CargoCapacityUnit = "L",
            ConsumptionValue = 7m,
            ConsumptionUnit = "L_PER_100KM",
            LicensePlate = "AA0011AA",
            Mileage = 1000,
            DailyRate = 70m,
            IsBookable = true,
            IsAvailable = true,
            ServiceIntervalKm = 10000
        });
        dbContext.SaveChanges();
    }

    private sealed class StubDamageService : IDamageService
    {
        public Task<DamageResult> AddDamageAsync(DamageRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new DamageResult(true, "ok"));
    }
}
