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

        dbContext.Vehicles.Add(TestLookupSeed.CreateVehicle(
            dbContext,
            "BMW",
            "M5 F90",
            "AA0022AA",
            "PETROL",
            "AUTO",
            4.4m,
            530m,
            11m,
            2500,
            120m,
            id: 2));
        dbContext.Rentals.AddRange(
            new Rental
            {
                ClientId = 1,
                VehicleId = 1,
                CreatedByEmployeeId = 1,
                ContractNumber = "CR-2026-000079",
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow.AddDays(2),
                PickupLocation = "Kyiv",
                ReturnLocation = "Kyiv",
                StartMileage = 1000,
                TotalAmount = 300m,
                StatusId = RentalStatus.Active,
                CreatedAtUtc = DateTime.UtcNow
            },
            new Rental
            {
                ClientId = 1,
                VehicleId = 1,
                CreatedByEmployeeId = 1,
                ClosedByEmployeeId = 1,
                ContractNumber = "CR-2026-000078",
                StartDate = DateTime.UtcNow.AddDays(-5),
                EndDate = DateTime.UtcNow.AddDays(-2),
                PickupLocation = "Kyiv",
                ReturnLocation = "Kyiv",
                StartMileage = 900,
                TotalAmount = 250m,
                StatusId = RentalStatus.Closed,
                ClosedAtUtc = DateTime.UtcNow.AddDays(-2),
                CreatedAtUtc = DateTime.UtcNow.AddDays(-5)
            },
            new Rental
            {
                ClientId = 1,
                VehicleId = 2,
                CreatedByEmployeeId = 1,
                ContractNumber = "CR-2026-000077",
                StartDate = DateTime.UtcNow.AddDays(-2),
                EndDate = DateTime.UtcNow.AddDays(1),
                PickupLocation = "Lviv",
                ReturnLocation = "Lviv",
                StartMileage = 2400,
                TotalAmount = 700m,
                StatusId = RentalStatus.Active,
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

        var account = new Account
        {
            Id = 1,
            Login = "admin",
            PasswordHash = "x",
            IsActive = true
        };

        dbContext.Accounts.Add(account);
        dbContext.Employees.Add(new Employee
        {
            Id = 1,
            FullName = "Admin",
            RoleId = UserRole.Admin,
            Account = account
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
            IsBlacklisted = false
        });
        dbContext.Vehicles.Add(TestLookupSeed.CreateVehicle(
            dbContext,
            "Audi",
            "A4",
            "AA0011AA",
            "PETROL",
            "AUTO",
            2m,
            480m,
            7m,
            1000,
            70m,
            id: 1));
        dbContext.SaveChanges();
    }

    private sealed class StubDamageService : IDamageService
    {
        public Task<DamageResult> AddDamageAsync(DamageRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new DamageResult(true, "ok"));
    }
}
