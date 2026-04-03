using CarRental.Desktop.Data;
using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Auth;
using CarRental.Desktop.Services.Documents;
using CarRental.Desktop.Services.Payments;
using CarRental.Desktop.Services.Rentals;
using CarRental.Desktop.ViewModels;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Desktop.Tests;

public sealed class RentalsPageViewModelTests
{
    [Fact]
    public async Task OpenCreateRentalDialogCommand_ShouldOpenDialog_WithDefaultLocations()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        var currentEmployee = await SeedRentalContextAsync(dbContext);
        var viewModel = CreateViewModel(dbContext, currentEmployee);

        await viewModel.OpenCreateRentalDialogCommand.ExecuteAsync(null);

        viewModel.IsCreateRentalDialogOpen.Should().BeTrue();
        viewModel.CreateDraft.PickupLocation.Should().NotBeNullOrWhiteSpace();
        viewModel.CreateDraft.ReturnLocation.Should().Be(viewModel.CreateDraft.PickupLocation);
        viewModel.Clients.Should().NotBeEmpty();
        viewModel.Vehicles.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SubmitCreateRentalCommand_ShouldCreateRental_WithSelectedLocations_AndSelectIt()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        var currentEmployee = await SeedRentalContextAsync(dbContext);
        var printService = new StubPrintService();
        var viewModel = CreateViewModel(dbContext, currentEmployee, printService: printService);

        await viewModel.OpenCreateRentalDialogCommand.ExecuteAsync(null);
        viewModel.CreateDraft.SelectedClient = viewModel.Clients.First();
        viewModel.CreateDraft.SelectedVehicle = viewModel.Vehicles.First();
        viewModel.CreateDraft.StartDate = DateTime.Today.AddDays(1);
        viewModel.CreateDraft.EndDate = DateTime.Today.AddDays(2);
        viewModel.CreateDraft.PickupLocation = "Kyiv";
        viewModel.CreateDraft.ReturnLocation = "Lviv";
        viewModel.CreateDraft.AutoPrintContract = true;

        await viewModel.SubmitCreateRentalCommand.ExecuteAsync(null);

        var rental = await dbContext.Rentals.SingleAsync();
        rental.PickupLocation.Should().Be("Kyiv");
        rental.ReturnLocation.Should().Be("Lviv");
        rental.StatusId.Should().Be(RentalStatus.Booked);

        viewModel.IsCreateRentalDialogOpen.Should().BeFalse();
        viewModel.SelectedRental.Should().NotBeNull();
        viewModel.SelectedRental!.Id.Should().Be(rental.Id);
        viewModel.SelectedRental.RouteDisplay.Should().Be("Kyiv -> Lviv");
        viewModel.StatusMessage.Should().Contain("Оренду створено");
        printService.LastPrintedPath.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SubmitCreateRentalCommand_ShouldCreateInitialPayment_WithRequestedAmount()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        var currentEmployee = await SeedRentalContextAsync(dbContext);
        var viewModel = CreateViewModel(dbContext, currentEmployee);

        await viewModel.OpenCreateRentalDialogCommand.ExecuteAsync(null);
        viewModel.CreateDraft.SelectedClient = viewModel.Clients.First();
        viewModel.CreateDraft.SelectedVehicle = viewModel.Vehicles.First();
        viewModel.CreateDraft.StartDate = DateTime.Today.AddDays(1);
        viewModel.CreateDraft.EndDate = DateTime.Today.AddDays(2);
        viewModel.CreateDraft.PickupLocation = "Kyiv";
        viewModel.CreateDraft.ReturnLocation = "Kyiv";
        viewModel.CreateDraft.CreateInitialPayment = true;
        viewModel.CreateDraft.InitialPaymentAmountInput = "50";
        viewModel.CreateDraft.PaymentMethod = PaymentMethod.Card;
        viewModel.CreateDraft.PaymentDirection = PaymentDirection.Incoming;
        viewModel.CreateDraft.PaymentNotes = "Initial payment";

        await viewModel.SubmitCreateRentalCommand.ExecuteAsync(null);

        var rental = await dbContext.Rentals.AsNoTracking().SingleAsync();
        var payment = await dbContext.Payments.AsNoTracking().SingleAsync();

        payment.RentalId.Should().Be(rental.Id);
        payment.Amount.Should().Be(50m);
        payment.MethodId.Should().Be(PaymentMethod.Card);
        payment.DirectionId.Should().Be(PaymentDirection.Incoming);
        payment.Notes.Should().Be("Initial payment");
        viewModel.SelectedRental.Should().NotBeNull();
        viewModel.SelectedRental!.Balance.Should().Be(rental.TotalAmount - 50m);
    }

    [Fact]
    public async Task SubmitCreateRentalCommand_ShouldBlockExpiredDriverLicense_BeforeServiceCall()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        var currentEmployee = await SeedRentalContextAsync(dbContext);

        var expiredClient = new Client
        {
            FullName = "Expired Client",
            PassportData = "PP-EXP",
            DriverLicense = "DL-EXP",
            PassportExpirationDate = DateTime.UtcNow.AddYears(2),
            DriverLicenseExpirationDate = DateTime.UtcNow.AddDays(-1),
            Phone = "+380633333333",
            IsBlacklisted = false
        };
        dbContext.Clients.Add(expiredClient);
        await dbContext.SaveChangesAsync();

        var viewModel = CreateViewModel(dbContext, currentEmployee);

        await viewModel.OpenCreateRentalDialogCommand.ExecuteAsync(null);
        viewModel.CreateDraft.SelectedClient = viewModel.Clients.Single(item => item.Id == expiredClient.Id);
        viewModel.CreateDraft.SelectedVehicle = viewModel.Vehicles.First();
        viewModel.CreateDraft.StartDate = DateTime.Today.AddDays(1);
        viewModel.CreateDraft.EndDate = DateTime.Today.AddDays(2);

        viewModel.CreateClientValidationMessage.Should().Contain("Посвідчення водія прострочене");

        await viewModel.SubmitCreateRentalCommand.ExecuteAsync(null);

        viewModel.IsCreateRentalDialogOpen.Should().BeTrue();
        viewModel.CreateDraft.FormMessage.Should().Contain("Посвідчення водія прострочене");
        (await dbContext.Rentals.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OpenCreateRentalDialogCommand_ShouldFilterOutVehicles_WithBookedConflicts_OnSelectedPeriod()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        var currentEmployee = await SeedRentalContextAsync(dbContext);

        dbContext.Rentals.Add(new Rental
        {
            ClientId = 1,
            VehicleId = 1,
            CreatedByEmployeeId = currentEmployee.Id,
            ContractNumber = "CR-2026-009999",
            StartDate = DateTime.Today.AddDays(1).AddHours(10),
            EndDate = DateTime.Today.AddDays(3).AddHours(10),
            PickupLocation = "Kyiv",
            ReturnLocation = "Kyiv",
            StartMileage = 56000,
            TotalAmount = 140m,
            StatusId = RentalStatus.Booked
        });
        await dbContext.SaveChangesAsync();

        var viewModel = CreateViewModel(dbContext, currentEmployee);

        await viewModel.OpenCreateRentalDialogCommand.ExecuteAsync(null);
        viewModel.CreateDraft.StartDate = DateTime.Today.AddDays(1);
        viewModel.CreateDraft.EndDate = DateTime.Today.AddDays(2);
        await Task.Delay(300);

        viewModel.Vehicles.Should().NotContain(item => item.Id == 1);

        viewModel.CreateDraft.SelectedClient = viewModel.Clients.First();
        viewModel.CreateDraft.SelectedVehicle = new RentalsPageViewModel.VehicleOption(
            1,
            "Toyota Camry [AA0011AA] - конфлікт на обрані дати",
            70m,
            false,
            "Обране авто недоступне на обрані дати.");

        await viewModel.SubmitCreateRentalCommand.ExecuteAsync(null);

        viewModel.IsCreateRentalDialogOpen.Should().BeTrue();
        viewModel.CreateVehicleValidationMessage.Should().Contain("недоступне");
        viewModel.CreateDraft.FormMessage.Should().Contain("недоступне");
        (await dbContext.Rentals.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SubmitCreateRentalCommand_ShouldRejectInitialPayment_ThatExceedsEstimatedTotal()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        var currentEmployee = await SeedRentalContextAsync(dbContext);
        var viewModel = CreateViewModel(dbContext, currentEmployee);

        await viewModel.OpenCreateRentalDialogCommand.ExecuteAsync(null);
        viewModel.CreateDraft.SelectedClient = viewModel.Clients.First();
        viewModel.CreateDraft.SelectedVehicle = viewModel.Vehicles.First();
        viewModel.CreateDraft.StartDate = DateTime.Today.AddDays(1);
        viewModel.CreateDraft.EndDate = DateTime.Today.AddDays(2);
        viewModel.CreateDraft.CreateInitialPayment = true;
        viewModel.CreateDraft.InitialPaymentAmountInput = "1000";

        viewModel.CreatePaymentValidationMessage.Should().Contain("не може перевищувати");

        await viewModel.SubmitCreateRentalCommand.ExecuteAsync(null);

        viewModel.IsCreateRentalDialogOpen.Should().BeTrue();
        viewModel.CreateDraft.FormMessage.Should().Contain("не може перевищувати");
        (await dbContext.Rentals.CountAsync()).Should().Be(0);
        (await dbContext.Payments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task PrepareForClientAsync_ShouldOpenDialog_AndSelectPreferredClient()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        var currentEmployee = await SeedRentalContextAsync(dbContext);

        var secondClient = await dbContext.Clients
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .Skip(1)
            .FirstAsync();

        var viewModel = CreateViewModel(dbContext, currentEmployee);

        await viewModel.PrepareForClientAsync(secondClient.Id);

        viewModel.IsCreateRentalDialogOpen.Should().BeTrue();
        viewModel.CreateDraft.SelectedClient.Should().NotBeNull();
        viewModel.CreateDraft.SelectedClient!.Id.Should().Be(secondClient.Id);
    }

    private static RentalsPageViewModel CreateViewModel(
        RentalDbContext dbContext,
        Employee currentEmployee,
        StubDocumentGenerator? documentGenerator = null,
        StubPrintService? printService = null)
    {
        return new RentalsPageViewModel(
            dbContext,
            new RentalService(dbContext, new StubContractNumberService()),
            documentGenerator ?? new StubDocumentGenerator(),
            printService ?? new StubPrintService(),
            new PaymentService(dbContext),
            new StubAuthorizationService(),
            new PageRefreshCoordinator(_ => Task.CompletedTask),
            currentEmployee);
    }

    private static async Task<Employee> SeedRentalContextAsync(RentalDbContext dbContext)
    {
        TestLookupSeed.SeedVehicleLookups(dbContext);

        var account = new Account
        {
            Login = $"manager-{Guid.NewGuid():N}",
            PasswordHash = "x",
            IsActive = true
        };
        var employee = new Employee
        {
            FullName = "Test Manager",
            RoleId = UserRole.Manager,
            Account = account
        };

        dbContext.Accounts.Add(account);
        dbContext.Employees.Add(employee);

        dbContext.Clients.AddRange(
            new Client
            {
                FullName = "Client One",
                PassportData = "PP-001",
                DriverLicense = "DL-001",
                PassportExpirationDate = DateTime.UtcNow.AddYears(5),
                DriverLicenseExpirationDate = DateTime.UtcNow.AddYears(5),
                Phone = "+380631111111",
                IsBlacklisted = false
            },
            new Client
            {
                FullName = "Client Two",
                PassportData = "PP-002",
                DriverLicense = "DL-002",
                PassportExpirationDate = DateTime.UtcNow.AddYears(5),
                DriverLicenseExpirationDate = DateTime.UtcNow.AddYears(5),
                Phone = "+380632222222",
                IsBlacklisted = false
            });

        dbContext.Vehicles.AddRange(
            TestLookupSeed.CreateVehicle(
                dbContext,
                "Toyota",
                "Camry",
                "AA0011AA",
                "PETROL",
                "AUTO",
                2m,
                500m,
                7m,
                56000,
                70m,
                id: 1),
            TestLookupSeed.CreateVehicle(
                dbContext,
                "Audi",
                "A4",
                "AA0022BB",
                "PETROL",
                "AUTO",
                2m,
                480m,
                7m,
                44000,
                90m,
                id: 2));

        await dbContext.SaveChangesAsync();
        return employee;
    }

    private sealed class StubAuthorizationService : IAuthorizationService
    {
        public bool HasPermission(Employee employee, EmployeePermission permission) => true;
    }

    private sealed class StubContractNumberService : IContractNumberService
    {
        private int _counter = 1;

        public Task<string> NextNumberAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult($"CR-2026-{_counter++.ToString("D6")}");
        }
    }

    private sealed class StubDocumentGenerator : IDocumentGenerator
    {
        public Task<GeneratedContractFiles> GenerateRentalContractAsync(ContractData data, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new GeneratedContractFiles("contract.txt", "contract.docx", "contract.pdf"));
        }
    }

    private sealed class StubPrintService : IPrintService
    {
        public string? LastPrintedPath { get; private set; }

        public bool TryPrint(string filePath, out string message)
        {
            LastPrintedPath = filePath;
            message = "OK";
            return true;
        }
    }
}
