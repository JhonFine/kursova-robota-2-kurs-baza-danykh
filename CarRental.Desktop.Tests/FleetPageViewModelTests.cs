using CarRental.Desktop.Data;
using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Auth;
using CarRental.Desktop.ViewModels;
using FluentAssertions;

namespace CarRental.Desktop.Tests;

public sealed class FleetPageViewModelTests
{
    [Fact]
    public async Task PrepareForVehicleAsync_ShouldSelectVehicle_AndOpenDetails()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        var employee = await SeedFleetContextAsync(dbContext);
        var viewModel = new FleetPageViewModel(
            dbContext,
            new StubAuthorizationService(),
            new PageRefreshCoordinator(_ => Task.CompletedTask),
            employee);

        viewModel.SearchText = "anything";
        viewModel.SelectedMakeFilter = "Toyota";

        await viewModel.PrepareForVehicleAsync(2);

        viewModel.SelectedVehicle.Should().NotBeNull();
        viewModel.SelectedVehicle!.Id.Should().Be(2);
        viewModel.IsVehicleDetailsDialogOpen.Should().BeTrue();
        viewModel.SelectedSearchField.Key.Should().Be("id");
        viewModel.SearchText.Should().Be("2");
    }

    private static async Task<Employee> SeedFleetContextAsync(RentalDbContext dbContext)
    {
        TestLookupSeed.SeedVehicleLookups(dbContext);

        var account = new Account
        {
            Login = $"fleet-{Guid.NewGuid():N}",
            PasswordHash = "x",
            IsActive = true
        };
        var employee = new Employee
        {
            FullName = "Fleet Manager",
            RoleId = UserRole.Manager,
            Account = account
        };

        dbContext.Accounts.Add(account);
        dbContext.Employees.Add(employee);
        dbContext.Vehicles.AddRange(
            TestLookupSeed.CreateVehicle(
                dbContext,
                "Toyota",
                "Camry",
                "AA3333AA",
                "PETROL",
                "AUTO",
                2m,
                500m,
                7m,
                16000,
                70m,
                id: 1),
            TestLookupSeed.CreateVehicle(
                dbContext,
                "Audi",
                "A6",
                "AA4444BB",
                "PETROL",
                "AUTO",
                2m,
                500m,
                7m,
                22000,
                95m,
                id: 2));

        await dbContext.SaveChangesAsync();
        return employee;
    }

    private sealed class StubAuthorizationService : IAuthorizationService
    {
        public bool HasPermission(Employee employee, EmployeePermission permission) => true;
    }
}
