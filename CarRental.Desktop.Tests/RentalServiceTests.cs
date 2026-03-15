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

        var start = DateTime.Today.AddDays(2);
        var end = DateTime.Today.AddDays(4);

        var result = await service.CreateRentalAsync(new CreateRentalRequest(
            ClientId: 1,
            VehicleId: 1,
            EmployeeId: 1,
            StartDate: start,
            EndDate: end,
            PickupLocation: "Київ"));

        result.Success.Should().BeTrue();
        result.ContractNumber.Should().Be("CR-2026-000001");
        var rental = await dbContext.Rentals.FirstAsync();
        rental.Status.Should().Be(RentalStatus.Booked);
        rental.ContractNumber.Should().Be("CR-2026-000001");
    }

    [Fact]
    public async Task CreateRentalWithPaymentAsync_ShouldCreateRentalAndPayment()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);
        var service = new RentalService(dbContext, new StubContractNumberService("CR-2026-000010"));

        var start = DateTime.Today.AddDays(2);
        var end = DateTime.Today.AddDays(4);

        var result = await service.CreateRentalWithPaymentAsync(new CreateRentalWithPaymentRequest(
            ClientId: 1,
            VehicleId: 1,
            EmployeeId: 1,
            StartDate: start,
            EndDate: end,
            PickupLocation: "Київ",
            Method: PaymentMethod.Card,
            Direction: PaymentDirection.Incoming,
            Notes: "Оплата карткою"));

        result.Success.Should().BeTrue();
        var rental = await dbContext.Rentals.FirstAsync();
        var payment = await dbContext.Payments.FirstAsync();

        rental.ContractNumber.Should().Be("CR-2026-000010");
        payment.RentalId.Should().Be(rental.Id);
        payment.EmployeeId.Should().Be(1);
        payment.Amount.Should().Be(rental.TotalAmount);
        payment.Method.Should().Be(PaymentMethod.Card);
        payment.Direction.Should().Be(PaymentDirection.Incoming);
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
            EmployeeId: 1,
            StartDate: DateTime.Today.AddDays(2),
            EndDate: DateTime.Today.AddDays(4),
            PickupLocation: "Київ"));

        var result = await service.CreateRentalWithPaymentAsync(new CreateRentalWithPaymentRequest(
            ClientId: 1,
            VehicleId: 1,
            EmployeeId: 1,
            StartDate: DateTime.Today.AddDays(3),
            EndDate: DateTime.Today.AddDays(5),
            PickupLocation: "Київ",
            Method: PaymentMethod.Card,
            Direction: PaymentDirection.Incoming,
            Notes: "Оплата карткою"));

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
        var createResult = await service.CreateRentalAsync(new CreateRentalRequest(
            ClientId: 1,
            VehicleId: 1,
            EmployeeId: 1,
            StartDate: DateTime.Today,
            EndDate: DateTime.Today.AddDays(1),
            PickupLocation: "Київ"));

        var closeResult = await service.CloseRentalAsync(new CloseRentalRequest(
            RentalId: createResult.RentalId,
            ActualEndDate: DateTime.Today,
            EndMileage: 56550));

        closeResult.Success.Should().BeTrue();
        var rental = await dbContext.Rentals.Include(item => item.Vehicle).FirstAsync();
        rental.Status.Should().Be(RentalStatus.Closed);
        rental.IsClosed.Should().BeTrue();
        rental.Vehicle!.Mileage.Should().Be(56550);
        rental.OverageFee.Should().Be(0m);
        rental.TotalAmount.Should().Be(70m);
    }

    [Fact]
    public async Task CloseRentalAsync_ShouldKeepDateRangeValid_WhenStartDateHasTimeComponent()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);
        var service = new RentalService(dbContext, new StubContractNumberService("CR-2026-000004"));

        var start = DateTime.Today.AddHours(10);
        var createResult = await service.CreateRentalAsync(new CreateRentalRequest(
            ClientId: 1,
            VehicleId: 1,
            EmployeeId: 1,
            StartDate: start,
            EndDate: start.AddDays(2),
            PickupLocation: "Київ"));

        createResult.Success.Should().BeTrue();

        var closeResult = await service.CloseRentalAsync(new CloseRentalRequest(
            RentalId: createResult.RentalId,
            ActualEndDate: start.Date,
            EndMileage: 56600));

        closeResult.Success.Should().BeTrue();
        var rental = await dbContext.Rentals.AsNoTracking().SingleAsync(item => item.Id == createResult.RentalId);
        rental.EndDate.Should().BeOnOrAfter(rental.StartDate);
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
            EmployeeId: 1,
            StartDate: DateTime.Today.AddDays(1),
            EndDate: DateTime.Today.AddDays(3),
            PickupLocation: "Київ"));

        var hasConflict = await service.HasDateConflictAsync(
            vehicleId: 1,
            startDate: DateTime.Today.AddDays(2),
            endDate: DateTime.Today.AddDays(4));

        hasConflict.Should().BeTrue();
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
            Mileage = 56000,
            DailyRate = 70m,
            IsAvailable = true
        });
        dbContext.SaveChanges();
    }

    private sealed class StubContractNumberService(string value) : IContractNumberService
    {
        public Task<string> NextNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult(value);
    }
}
