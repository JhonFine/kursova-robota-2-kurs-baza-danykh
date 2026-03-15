using CarRental.WebApi.Data;
using CarRental.WebApi.Models;
using CarRental.WebApi.Services.Damages;
using CarRental.WebApi.Services.Documents;
using CarRental.WebApi.Services.Payments;
using CarRental.WebApi.Services.Rentals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CarRental.WebApi.Tests;

public sealed class DatabaseIntegrityTests
{
    [Fact]
    public async Task RefreshStatusesAsync_ShouldKeepVehicleUnavailable_WhenActiveAndBookedRentalsExist()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);

        var now = DateTime.Now;
        dbContext.Rentals.AddRange(
            new Rental
            {
                ClientId = 1,
                VehicleId = 1,
                EmployeeId = 1,
                ContractNumber = "CR-2026-000001",
                StartDate = now.AddHours(-2),
                EndDate = now.AddHours(4),
                StartMileage = 1000,
                TotalAmount = 100m,
                Status = RentalStatus.Active,
                IsClosed = false,
                CreatedAtUtc = DateTime.UtcNow
            },
            new Rental
            {
                ClientId = 1,
                VehicleId = 1,
                EmployeeId = 1,
                ContractNumber = "CR-2026-000002",
                StartDate = now.AddDays(2),
                EndDate = now.AddDays(3),
                StartMileage = 1000,
                TotalAmount = 120m,
                Status = RentalStatus.Booked,
                IsClosed = false,
                CreatedAtUtc = DateTime.UtcNow
            });
        await dbContext.SaveChangesAsync();

        var service = new RentalService(dbContext, new StubContractNumberService("CR-2026-000099"));
        await service.RefreshStatusesAsync();

        var vehicle = await dbContext.Vehicles.AsNoTracking().SingleAsync(item => item.Id == 1);
        vehicle.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task AddDamageAsync_ShouldGenerateDifferentActNumbers_WhenCalledBackToBack()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);

        var service = new DamageService(dbContext);
        var first = await service.AddDamageAsync(new DamageRequest(
            VehicleId: 1,
            RentalId: null,
            Description: "Front bumper scratch",
            RepairCost: 500m,
            PhotoPath: null,
            AutoChargeToRental: false));
        var second = await service.AddDamageAsync(new DamageRequest(
            VehicleId: 1,
            RentalId: null,
            Description: "Rear bumper scratch",
            RepairCost: 650m,
            PhotoPath: null,
            AutoChargeToRental: false));

        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();

        var numbers = await dbContext.Damages
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .Select(item => item.ActNumber)
            .ToListAsync();
        numbers.Should().HaveCount(2);
        numbers[0].Should().NotBe(numbers[1]);
    }

    [Fact]
    public async Task AddPaymentAsync_ShouldRejectNonPositiveAmount_AndModelKeepsActNumberUniqueIndex()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);

        var rental = new Rental
        {
            ClientId = 1,
            VehicleId = 1,
            EmployeeId = 1,
            ContractNumber = "CR-2026-000010",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1),
            StartMileage = 1000,
            TotalAmount = 150m,
            Status = RentalStatus.Active,
            IsClosed = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.Rentals.Add(rental);
        await dbContext.SaveChangesAsync();

        var paymentService = new PaymentService(dbContext);
        var result = await paymentService.AddPaymentAsync(new PaymentRequest(
            RentalId: rental.Id,
            EmployeeId: 1,
            Amount: 0m,
            Method: PaymentMethod.Cash,
            Direction: PaymentDirection.Incoming,
            Notes: "invalid"));
        result.Success.Should().BeFalse();

        var damageType = dbContext.Model.FindEntityType(typeof(Damage));
        damageType.Should().NotBeNull();
        damageType!.GetIndexes()
            .Any(index => index.IsUnique && index.Properties.Count == 1 && index.Properties[0].Name == nameof(Damage.ActNumber))
            .Should()
            .BeTrue();
    }

    [Fact]
    public async Task GetRentalBalanceAsync_ShouldCalculateBalanceOnPostgres()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);

        var rental = new Rental
        {
            ClientId = 1,
            VehicleId = 1,
            EmployeeId = 1,
            ContractNumber = "CR-2026-000011",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1),
            StartMileage = 1000,
            TotalAmount = 100m,
            Status = RentalStatus.Active,
            IsClosed = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.Rentals.Add(rental);
        await dbContext.SaveChangesAsync();

        dbContext.Payments.AddRange(
            new Payment
            {
                RentalId = rental.Id,
                EmployeeId = 1,
                Amount = 40m,
                Method = PaymentMethod.Card,
                Direction = PaymentDirection.Incoming,
                Notes = "incoming",
                CreatedAtUtc = DateTime.UtcNow
            },
            new Payment
            {
                RentalId = rental.Id,
                EmployeeId = 1,
                Amount = 10m,
                Method = PaymentMethod.Card,
                Direction = PaymentDirection.Refund,
                Notes = "refund",
                CreatedAtUtc = DateTime.UtcNow
            });
        await dbContext.SaveChangesAsync();

        var paymentService = new PaymentService(dbContext);
        var balance = await paymentService.GetRentalBalanceAsync(rental.Id);

        balance.Should().Be(70m);
    }

    [Fact]
    public async Task CancelRentalAsync_ShouldRefundPaidBookedRental_AndZeroBalance()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);
        var service = new RentalService(dbContext, new StubContractNumberService("CR-2026-000012"));

        var start = DateTime.Today.AddDays(2);
        var createResult = await service.CreateRentalWithPaymentAsync(new CreateRentalWithPaymentRequest(
            ClientId: 1,
            VehicleId: 1,
            EmployeeId: 1,
            StartDate: start,
            EndDate: start.AddDays(2),
            PickupLocation: "Київ",
            Method: PaymentMethod.Card,
            Direction: PaymentDirection.Incoming,
            Notes: "Initial payment"));
        createResult.Success.Should().BeTrue();

        var cancelResult = await service.CancelRentalAsync(new CancelRentalRequest(
            createResult.RentalId,
            "Client canceled"));
        cancelResult.Success.Should().BeTrue();

        var rental = await dbContext.Rentals.AsNoTracking().SingleAsync(item => item.Id == createResult.RentalId);
        rental.Status.Should().Be(RentalStatus.Canceled);
        rental.TotalAmount.Should().Be(0m);

        var payments = await dbContext.Payments
            .AsNoTracking()
            .Where(item => item.RentalId == createResult.RentalId)
            .OrderBy(item => item.Id)
            .ToListAsync();
        payments.Should().HaveCount(2);
        payments[0].Direction.Should().Be(PaymentDirection.Incoming);
        payments[1].Direction.Should().Be(PaymentDirection.Refund);
        payments[1].Amount.Should().Be(payments[0].Amount);

        var paymentService = new PaymentService(dbContext);
        var balance = await paymentService.GetRentalBalanceAsync(createResult.RentalId);
        balance.Should().Be(0m);
    }

    [Fact]
    public async Task RescheduleRentalAsync_ShouldRefundOverpayment_WhenRentalBecomesCheaper()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);
        var service = new RentalService(dbContext, new StubContractNumberService("CR-2026-000013"));

        var start = DateTime.Today.AddDays(3);
        var createResult = await service.CreateRentalWithPaymentAsync(new CreateRentalWithPaymentRequest(
            ClientId: 1,
            VehicleId: 1,
            EmployeeId: 1,
            StartDate: start,
            EndDate: start.AddDays(3),
            PickupLocation: "Київ",
            Method: PaymentMethod.Card,
            Direction: PaymentDirection.Incoming,
            Notes: "Initial payment"));
        createResult.Success.Should().BeTrue();
        createResult.TotalAmount.Should().Be(210m);

        var rescheduleResult = await service.RescheduleRentalAsync(new RescheduleRentalRequest(
            createResult.RentalId,
            start,
            start.AddDays(1),
            1));

        rescheduleResult.Success.Should().BeTrue();
        rescheduleResult.TotalAmount.Should().Be(70m);
        rescheduleResult.Balance.Should().Be(0m);

        var rental = await dbContext.Rentals.AsNoTracking().SingleAsync(item => item.Id == createResult.RentalId);
        rental.TotalAmount.Should().Be(70m);

        var refund = await dbContext.Payments
            .AsNoTracking()
            .Where(item => item.RentalId == createResult.RentalId && item.Direction == PaymentDirection.Refund)
            .SingleAsync();
        refund.Amount.Should().Be(140m);
        refund.Method.Should().Be(PaymentMethod.Card);
    }

    [Fact]
    public async Task SettleRentalBalanceAsync_ShouldCreateIncomingPayment_ForOutstandingBalance()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);
        var service = new RentalService(dbContext, new StubContractNumberService("CR-2026-000014"));

        var start = DateTime.Today.AddDays(2);
        var createResult = await service.CreateRentalAsync(new CreateRentalRequest(
            ClientId: 1,
            VehicleId: 1,
            EmployeeId: 1,
            StartDate: start,
            EndDate: start.AddDays(2),
            PickupLocation: "Київ"));
        createResult.Success.Should().BeTrue();

        var settleResult = await service.SettleRentalBalanceAsync(new SettleRentalBalanceRequest(
            createResult.RentalId,
            1,
            "Card **** 4242"));

        settleResult.Success.Should().BeTrue();
        settleResult.Amount.Should().Be(createResult.TotalAmount);

        var payment = await dbContext.Payments.AsNoTracking().SingleAsync(item => item.RentalId == createResult.RentalId);
        payment.Direction.Should().Be(PaymentDirection.Incoming);
        payment.Method.Should().Be(PaymentMethod.Card);
        payment.Amount.Should().Be(createResult.TotalAmount);
        payment.Notes.Should().Be("Card **** 4242");
    }

    [Fact]
    public async Task CompletePickupInspectionAsync_ShouldStoreInspectionAndActivateDueBookedRental()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);

        dbContext.Rentals.Add(new Rental
        {
            ClientId = 1,
            VehicleId = 1,
            EmployeeId = 1,
            ContractNumber = "CR-2026-000015",
            StartDate = DateTime.Now.AddMinutes(-15),
            EndDate = DateTime.Now.AddDays(1),
            PickupLocation = "Київ",
            ReturnLocation = "Київ",
            StartMileage = 1000,
            TotalAmount = 70m,
            Status = RentalStatus.Booked,
            IsClosed = false,
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var rentalId = await dbContext.Rentals.Select(item => item.Id).SingleAsync();
        var service = new RentalService(dbContext, new StubContractNumberService("CR-2026-000016"));

        var result = await service.CompletePickupInspectionAsync(new PickupInspectionRequest(
            rentalId,
            85,
            "Ready for pickup"));

        result.Success.Should().BeTrue();

        var rental = await dbContext.Rentals.AsNoTracking().SingleAsync(item => item.Id == rentalId);
        rental.Status.Should().Be(RentalStatus.Active);
        rental.PickupFuelPercent.Should().Be(85);
        rental.PickupInspectionNotes.Should().Be("Ready for pickup");
        rental.PickupInspectionCompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task CloseRentalAsync_ShouldAllowSameDayClose_WhenDatesAreEqual()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);
        var service = new RentalService(dbContext, new StubContractNumberService("CR-2026-000050"));

        var createResult = await service.CreateRentalAsync(new CreateRentalRequest(
            ClientId: 1,
            VehicleId: 1,
            EmployeeId: 1,
            StartDate: DateTime.Today,
            EndDate: DateTime.Today.AddDays(2),
            PickupLocation: "Київ"));
        createResult.Success.Should().BeTrue();

        var closeResult = await service.CloseRentalAsync(new CloseRentalRequest(
            RentalId: createResult.RentalId,
            ActualEndDate: DateTime.Today,
            EndMileage: 1200));

        closeResult.Success.Should().BeTrue();
        var rental = await dbContext.Rentals.AsNoTracking().SingleAsync(item => item.Id == createResult.RentalId);
        rental.EndDate.Date.Should().Be(rental.StartDate.Date);
    }

    [Fact]
    public async Task CloseRentalAsync_ShouldKeepDateRangeValid_WhenStartDateHasTimeComponent()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);
        var service = new RentalService(dbContext, new StubContractNumberService("CR-2026-000051"));

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
            EndMileage: 1250));

        closeResult.Success.Should().BeTrue();
        var rental = await dbContext.Rentals.AsNoTracking().SingleAsync(item => item.Id == createResult.RentalId);
        rental.EndDate.Should().BeOnOrAfter(rental.StartDate);
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
            IsAvailable = true,
            ServiceIntervalKm = 10000
        });
        dbContext.SaveChanges();
    }

    private sealed class StubContractNumberService(string value) : IContractNumberService
    {
        public Task<string> NextNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult(value);
    }
}
