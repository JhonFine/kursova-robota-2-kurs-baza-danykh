using CarRental.Desktop.Data;
using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Documents;
using CarRental.Desktop.Services.Rentals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Desktop.Tests;

public sealed class RentalServiceTests
{
    [Fact]
    public async Task CreateRentalAsync_ShouldCreateBookedRental_WithContractNumber()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);
        var service = new RentalService(dbContext, new StubContractNumberService("CR-2026-000001"));

        var start = DateTime.UtcNow.AddDays(2);
        var end = start.AddDays(2);

        var result = await service.CreateRentalAsync(new CreateRentalRequest(
            ClientId: 1,
            VehicleId: 1,
            CreatedByEmployeeId: 1,
            StartDate: start,
            EndDate: end,
            PickupLocation: "Kyiv"));

        result.Success.Should().BeTrue();
        result.ContractNumber.Should().Be("CR-2026-000001");
        var rental = await dbContext.Rentals.FirstAsync();
        rental.StatusId.Should().Be(RentalStatus.Booked);
        rental.ContractNumber.Should().Be("CR-2026-000001");
    }

    [Fact]
    public async Task CreateRentalWithPaymentAsync_ShouldCreateRentalAndPayment_WithRequestedAmount()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);
        var service = new RentalService(dbContext, new StubContractNumberService("CR-2026-000010"));

        var start = DateTime.UtcNow.AddDays(2);
        var end = start.AddDays(2);

        var result = await service.CreateRentalWithPaymentAsync(new CreateRentalWithPaymentRequest(
            ClientId: 1,
            VehicleId: 1,
            CreatedByEmployeeId: 1,
            StartDate: start,
            EndDate: end,
            PickupLocation: "Kyiv",
            Amount: 90m,
            MethodId: PaymentMethod.Card,
            DirectionId: PaymentDirection.Incoming,
            Notes: "Initial payment"));

        result.Success.Should().BeTrue();
        var rental = await dbContext.Rentals.FirstAsync();
        var payment = await dbContext.Payments.FirstAsync();

        rental.ContractNumber.Should().Be("CR-2026-000010");
        payment.RentalId.Should().Be(rental.Id);
        payment.RecordedByEmployeeId.Should().Be(1);
        payment.Amount.Should().Be(90m);
        payment.MethodId.Should().Be(PaymentMethod.Card);
        payment.DirectionId.Should().Be(PaymentDirection.Incoming);
        payment.Notes.Should().Be("Initial payment");
    }

    [Fact]
    public async Task CreateRentalWithPaymentAsync_ShouldRejectPaymentAmount_WhenItExceedsRentalTotal()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);
        var service = new RentalService(dbContext, new StubContractNumberService("CR-2026-000015"));

        var start = DateTime.UtcNow.AddDays(2);
        var end = start.AddDays(1);

        var result = await service.CreateRentalWithPaymentAsync(new CreateRentalWithPaymentRequest(
            ClientId: 1,
            VehicleId: 1,
            CreatedByEmployeeId: 1,
            StartDate: start,
            EndDate: end,
            PickupLocation: "Kyiv",
            Amount: 1000m,
            MethodId: PaymentMethod.Card,
            DirectionId: PaymentDirection.Incoming,
            Notes: "Too much"));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Початковий платіж");
        (await dbContext.Rentals.CountAsync()).Should().Be(0);
        (await dbContext.Payments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateRentalAsync_ShouldRejectNonBookableVehicle()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);

        var vehicle = await dbContext.Vehicles.SingleAsync(item => item.Id == 1);
        vehicle.IsBookable = false;
        vehicle.IsAvailable = false;
        await dbContext.SaveChangesAsync();

        var service = new RentalService(dbContext, new StubContractNumberService("CR-2026-000099"));
        var start = DateTime.UtcNow.AddDays(2);
        var result = await service.CreateRentalAsync(new CreateRentalRequest(
            ClientId: 1,
            VehicleId: 1,
            CreatedByEmployeeId: 1,
            StartDate: start,
            EndDate: start.AddDays(1),
            PickupLocation: "Kyiv"));

        result.Success.Should().BeFalse();
        (await dbContext.Rentals.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateRentalWithPaymentAsync_ShouldNotCreatePayment_WhenRentalCreationFails()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);
        var service = new RentalService(dbContext, new StubContractNumberService("CR-2026-000011"));

        await service.CreateRentalAsync(new CreateRentalRequest(
            ClientId: 1,
            VehicleId: 1,
            CreatedByEmployeeId: 1,
            StartDate: DateTime.UtcNow.AddDays(2),
            EndDate: DateTime.UtcNow.AddDays(4),
            PickupLocation: "Kyiv"));

        var result = await service.CreateRentalWithPaymentAsync(new CreateRentalWithPaymentRequest(
            ClientId: 1,
            VehicleId: 1,
            CreatedByEmployeeId: 1,
            StartDate: DateTime.UtcNow.AddDays(3),
            EndDate: DateTime.UtcNow.AddDays(5),
            PickupLocation: "Kyiv",
            Amount: 90m,
            MethodId: PaymentMethod.Card,
            DirectionId: PaymentDirection.Incoming,
            Notes: "Initial payment"));

        result.Success.Should().BeFalse();
        (await dbContext.Rentals.CountAsync()).Should().Be(1);
        (await dbContext.Payments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CloseRentalAsync_ShouldUpdateMileage_StatusAndTotals()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);
        var service = new RentalService(dbContext, new StubContractNumberService("CR-2026-000002"));
        var start = DateTime.UtcNow.AddMinutes(30);
        var createResult = await service.CreateRentalAsync(new CreateRentalRequest(
            ClientId: 1,
            VehicleId: 1,
            CreatedByEmployeeId: 1,
            StartDate: start,
            EndDate: start.AddDays(1),
            PickupLocation: "Kyiv"));

        var closeResult = await service.CloseRentalAsync(new CloseRentalRequest(
            RentalId: createResult.RentalId,
            ActualEndDate: start.Date,
            EndMileage: 56550));

        closeResult.Success.Should().BeTrue();
        var rental = await dbContext.Rentals.Include(item => item.Vehicle).FirstAsync();
        rental.StatusId.Should().Be(RentalStatus.Closed);
        rental.IsClosed.Should().BeTrue();
        rental.Vehicle!.Mileage.Should().Be(56550);
        rental.OverageFee.Should().Be(0m);
        rental.TotalAmount.Should().Be(70m);
    }

    [Fact]
    public async Task CloseRentalAsync_ShouldKeepDateRangeStrictlyValid_WhenStartDateHasTimeComponent()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);
        var service = new RentalService(dbContext, new StubContractNumberService("CR-2026-000004"));

        var start = DateTime.UtcNow.AddHours(2);
        var createResult = await service.CreateRentalAsync(new CreateRentalRequest(
            ClientId: 1,
            VehicleId: 1,
            CreatedByEmployeeId: 1,
            StartDate: start,
            EndDate: start.AddDays(2),
            PickupLocation: "Kyiv"));

        createResult.Success.Should().BeTrue();

        var closeResult = await service.CloseRentalAsync(new CloseRentalRequest(
            RentalId: createResult.RentalId,
            ActualEndDate: start.Date,
            EndMileage: 56600));

        closeResult.Success.Should().BeTrue();
        var rental = await dbContext.Rentals.AsNoTracking().SingleAsync(item => item.Id == createResult.RentalId);
        rental.EndDate.Should().BeAfter(rental.StartDate);
    }

    [Fact]
    public async Task HasDateConflictAsync_ShouldReturnTrue_WhenRangesIntersect()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);
        var service = new RentalService(dbContext, new StubContractNumberService("CR-2026-000003"));

        await service.CreateRentalAsync(new CreateRentalRequest(
            ClientId: 1,
            VehicleId: 1,
            CreatedByEmployeeId: 1,
            StartDate: DateTime.UtcNow.Date.AddDays(1),
            EndDate: DateTime.UtcNow.Date.AddDays(3),
            PickupLocation: "Kyiv"));

        var hasConflict = await service.HasDateConflictAsync(
            vehicleId: 1,
            startDate: DateTime.UtcNow.Date.AddDays(2),
            endDate: DateTime.UtcNow.Date.AddDays(4));

        hasConflict.Should().BeTrue();
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
            id: 1));
        dbContext.SaveChanges();
    }

    private sealed class StubContractNumberService(string value) : IContractNumberService
    {
        public Task<string> NextNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult(value);
    }
}
