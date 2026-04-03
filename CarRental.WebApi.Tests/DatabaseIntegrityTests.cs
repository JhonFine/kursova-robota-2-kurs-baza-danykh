using CarRental.WebApi.Data;
using CarRental.WebApi.Models;
using CarRental.WebApi.Services.Damages;
using CarRental.WebApi.Services.Documents;
using CarRental.WebApi.Services.Payments;
using CarRental.WebApi.Services.Rentals;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CarRental.WebApi.Tests;

// Набір доменних regression-тестів, який перевіряє, що runtime-сервіси не ламають ключові бізнес-інваріанти БД.
public sealed class DatabaseIntegrityTests
{
    [Fact]
    public async Task RefreshStatusesAsync_ShouldKeepVehicleUnavailable_WhenActiveAndBookedRentalsExist()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);

        var now = DateTime.UtcNow;
        dbContext.Rentals.AddRange(
            new Rental
            {
                ClientId = 1,
                VehicleId = 1,
                CreatedByEmployeeId = 1,
                ContractNumber = "CR-2026-000001",
                StartDate = now.AddHours(-2),
                EndDate = now.AddHours(4),
                StartMileage = 1000,
                TotalAmount = 100m,
                StatusId = RentalStatus.Active,
                IsClosed = false,
                CreatedAtUtc = DateTime.UtcNow
            },
            new Rental
            {
                ClientId = 1,
                VehicleId = 1,
                CreatedByEmployeeId = 1,
                ContractNumber = "CR-2026-000002",
                StartDate = now.AddDays(2),
                EndDate = now.AddDays(3),
                StartMileage = 1000,
                TotalAmount = 120m,
                StatusId = RentalStatus.Booked,
                IsClosed = false,
                CreatedAtUtc = DateTime.UtcNow
            });
        await dbContext.SaveChangesAsync();

        var service = new RentalService(dbContext, new StubContractNumberService("CR-2026-000099"));
        await service.RefreshStatusesAsync();

        // Тест повторює те саме обчислення availability, яке очікує UI після refresh статусів.
        var hasActiveRental = await dbContext.Rentals
            .AsNoTracking()
            .AnyAsync(item => item.VehicleId == 1 && item.StatusId == RentalStatus.Active);
        var vehicle = await dbContext.Vehicles.AsNoTracking().SingleAsync(item => item.Id == 1);
        var isAvailable = !vehicle.IsDeleted && vehicle.IsBookable && !hasActiveRental;
        isAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CreateRentalAsync_ShouldRejectNonBookableVehicle()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);

        var vehicle = await dbContext.Vehicles.SingleAsync(item => item.Id == 1);
        vehicle.IsBookable = false;
        vehicle.IsAvailable = false;
        await dbContext.SaveChangesAsync();

        var service = new RentalService(dbContext, new StubContractNumberService("CR-2026-000090"));
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
    public async Task CompletePickupInspectionAsync_ShouldRejectNonBookableVehicle()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);

        var start = DateTime.UtcNow.AddMinutes(20);
        dbContext.Rentals.Add(new Rental
        {
            ClientId = 1,
            VehicleId = 1,
            CreatedByEmployeeId = 1,
            ContractNumber = "CR-2026-000091",
            StartDate = start,
            EndDate = start.AddDays(1),
            PickupLocation = "Kyiv",
            ReturnLocation = "Kyiv",
            StartMileage = 1000,
            TotalAmount = 70m,
            StatusId = RentalStatus.Booked,
            IsClosed = false,
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var vehicle = await dbContext.Vehicles.SingleAsync(item => item.Id == 1);
        vehicle.IsBookable = false;
        vehicle.IsAvailable = false;
        await dbContext.SaveChangesAsync();

        var rentalId = await dbContext.Rentals.Select(item => item.Id).SingleAsync();
        var service = new RentalService(dbContext, new StubContractNumberService("CR-2026-000092"));
        var result = await service.CompletePickupInspectionAsync(new PickupInspectionRequest(
            rentalId,
            80,
            "blocked"));

        result.Success.Should().BeFalse();
        (await dbContext.RentalInspections.CountAsync()).Should().Be(0);
        (await dbContext.Rentals.AsNoTracking().SingleAsync(item => item.Id == rentalId)).StatusId.Should().Be(RentalStatus.Booked);
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
            .Select(item => item.DamageActNumber)
            .ToListAsync();
        numbers.Should().HaveCount(2);
        numbers[0].Should().NotBe(numbers[1]);
    }

    [Fact]
    public async Task AddDamageAsync_ShouldRejectRentalForDifferentVehicle()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);

        dbContext.Vehicles.Add(TestLookupSeed.CreateVehicle(
            dbContext,
            make: "BMW",
            model: "M5",
            licensePlate: "AA0022AA",
            fuelTypeCode: "Бензин",
            transmissionTypeCode: "Автомат",
            powertrainCapacityValue: 4.4m,
            cargoCapacityValue: 530m,
            consumptionValue: 11m,
            mileage: 1500,
            dailyRate: 120m,
            id: 2));
        dbContext.Rentals.Add(new Rental
        {
            ClientId = 1,
            VehicleId = 2,
            CreatedByEmployeeId = 1,
            ContractNumber = "CR-2026-000079",
            StartDate = DateTime.UtcNow.AddDays(-2),
            EndDate = DateTime.UtcNow.AddDays(1),
            PickupLocation = "Kyiv",
            ReturnLocation = "Kyiv",
            StartMileage = 1500,
            TotalAmount = 500m,
            StatusId = RentalStatus.Active,
            IsClosed = false,
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var rentalId = await dbContext.Rentals
            .AsNoTracking()
            .Where(item => item.VehicleId == 2)
            .Select(item => item.Id)
            .SingleAsync();

        var service = new DamageService(dbContext);
        var result = await service.AddDamageAsync(new DamageRequest(
            VehicleId: 1,
            RentalId: rentalId,
            Description: "Wrong rental for vehicle",
            RepairCost: 450m,
            PhotoPath: null,
            AutoChargeToRental: false));

        result.Success.Should().BeFalse();
        (await dbContext.Damages.CountAsync()).Should().Be(0);
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
            CreatedByEmployeeId = 1,
            ContractNumber = "CR-2026-000010",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1),
            StartMileage = 1000,
            TotalAmount = 150m,
            StatusId = RentalStatus.Active,
            IsClosed = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.Rentals.Add(rental);
        await dbContext.SaveChangesAsync();

        var paymentService = new PaymentService(dbContext);
        var result = await paymentService.AddPaymentAsync(new PaymentRequest(
            RentalId: rental.Id,
            RecordedByEmployeeId: 1,
            Amount: 0m,
            MethodId: PaymentMethod.Cash,
            DirectionId: PaymentDirection.Incoming,
            Notes: "invalid"));
        result.Success.Should().BeFalse();

        var damageType = dbContext.Model.FindEntityType(typeof(Damage));
        damageType.Should().NotBeNull();
        damageType!.GetIndexes()
            .Any(index => index.IsUnique && index.Properties.Count == 1 && index.Properties[0].Name == nameof(Damage.DamageActNumber))
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
            CreatedByEmployeeId = 1,
            ContractNumber = "CR-2026-000011",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1),
            StartMileage = 1000,
            TotalAmount = 100m,
            StatusId = RentalStatus.Active,
            IsClosed = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.Rentals.Add(rental);
        await dbContext.SaveChangesAsync();

        dbContext.Payments.AddRange(
            new Payment
            {
                RentalId = rental.Id,
                RecordedByEmployeeId = 1,
                Amount = 40m,
                MethodId = PaymentMethod.Card,
                DirectionId = PaymentDirection.Incoming,
                Notes = "incoming",
                CreatedAtUtc = DateTime.UtcNow
            },
            new Payment
            {
                RentalId = rental.Id,
                RecordedByEmployeeId = 1,
                Amount = 10m,
                MethodId = PaymentMethod.Card,
                DirectionId = PaymentDirection.Refund,
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

        var start = DateTime.UtcNow.AddDays(2);
        var createResult = await service.CreateRentalWithPaymentAsync(new CreateRentalWithPaymentRequest(
            ClientId: 1,
            VehicleId: 1,
            CreatedByEmployeeId: 1,
            StartDate: start,
            EndDate: start.AddDays(2),
            PickupLocation: "Київ",
            MethodId: PaymentMethod.Card,
            DirectionId: PaymentDirection.Incoming,
            Notes: "Initial payment"));
        createResult.Success.Should().BeTrue();

        var cancelResult = await service.CancelRentalAsync(new CancelRentalRequest(
            createResult.RentalId,
            "Client canceled"));
        cancelResult.Success.Should().BeTrue();

        var rental = await dbContext.Rentals.AsNoTracking().SingleAsync(item => item.Id == createResult.RentalId);
        rental.StatusId.Should().Be(RentalStatus.Canceled);
        rental.TotalAmount.Should().Be(0m);

        var payments = await dbContext.Payments
            .AsNoTracking()
            .Where(item => item.RentalId == createResult.RentalId)
            .OrderBy(item => item.Id)
            .ToListAsync();
        payments.Should().HaveCount(2);
        payments[0].DirectionId.Should().Be(PaymentDirection.Incoming);
        payments[1].DirectionId.Should().Be(PaymentDirection.Refund);
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

        var start = DateTime.UtcNow.AddDays(3);
        var createResult = await service.CreateRentalWithPaymentAsync(new CreateRentalWithPaymentRequest(
            ClientId: 1,
            VehicleId: 1,
            CreatedByEmployeeId: 1,
            StartDate: start,
            EndDate: start.AddDays(3),
            PickupLocation: "Київ",
            MethodId: PaymentMethod.Card,
            DirectionId: PaymentDirection.Incoming,
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
            .Where(item => item.RentalId == createResult.RentalId && item.DirectionId == PaymentDirection.Refund)
            .SingleAsync();
        refund.Amount.Should().Be(140m);
        refund.MethodId.Should().Be(PaymentMethod.Card);
    }

    [Fact]
    public async Task SettleRentalBalanceAsync_ShouldCreateIncomingPayment_ForOutstandingBalance()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);
        var service = new RentalService(dbContext, new StubContractNumberService("CR-2026-000014"));

        var start = DateTime.UtcNow.AddDays(2);
        var createResult = await service.CreateRentalAsync(new CreateRentalRequest(
            ClientId: 1,
            VehicleId: 1,
            CreatedByEmployeeId: 1,
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
        payment.DirectionId.Should().Be(PaymentDirection.Incoming);
        payment.MethodId.Should().Be(PaymentMethod.Card);
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
            CreatedByEmployeeId = 1,
            ContractNumber = "CR-2026-000015",
            StartDate = DateTime.UtcNow.AddMinutes(15),
            EndDate = DateTime.UtcNow.AddDays(1),
            PickupLocation = "Київ",
            ReturnLocation = "Київ",
            StartMileage = 1000,
            TotalAmount = 70m,
            StatusId = RentalStatus.Booked,
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
        rental.StatusId.Should().Be(RentalStatus.Active);

        var inspection = await dbContext.RentalInspections
            .AsNoTracking()
            .SingleAsync(item => item.RentalId == rentalId && item.TypeId == RentalInspectionType.Pickup);
        inspection.FuelPercent.Should().Be(85);
        inspection.Notes.Should().Be("Ready for pickup");
        inspection.CompletedAtUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task CompletePickupInspectionAsync_ShouldRejectLatePickup()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);

        dbContext.Rentals.Add(new Rental
        {
            ClientId = 1,
            VehicleId = 1,
            CreatedByEmployeeId = 1,
            ContractNumber = "CR-2026-000016",
            StartDate = DateTime.UtcNow.AddMinutes(-15),
            EndDate = DateTime.UtcNow.AddDays(1),
            PickupLocation = "Kyiv",
            ReturnLocation = "Kyiv",
            StartMileage = 1000,
            TotalAmount = 70m,
            StatusId = RentalStatus.Booked,
            IsClosed = false,
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var rentalId = await dbContext.Rentals.Select(item => item.Id).SingleAsync();
        var service = new RentalService(dbContext, new StubContractNumberService("CR-2026-000017"));

        var result = await service.CompletePickupInspectionAsync(new PickupInspectionRequest(
            rentalId,
            85,
            "Late pickup"));

        result.Success.Should().BeFalse();

        var rental = await dbContext.Rentals.AsNoTracking().SingleAsync(item => item.Id == rentalId);
        rental.StatusId.Should().Be(RentalStatus.Booked);

        var hasPickupInspection = await dbContext.RentalInspections
            .AsNoTracking()
            .AnyAsync(item => item.RentalId == rentalId && item.TypeId == RentalInspectionType.Pickup);
        hasPickupInspection.Should().BeFalse();
    }

    [Fact]
    public async Task CloseRentalAsync_ShouldAllowSameDayClose_WhenDatesAreEqual()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);
        var service = new RentalService(dbContext, new StubContractNumberService("CR-2026-000050"));

        var start = DateTime.UtcNow.AddMinutes(30);
        var createResult = await service.CreateRentalAsync(new CreateRentalRequest(
            ClientId: 1,
            VehicleId: 1,
            CreatedByEmployeeId: 1,
            StartDate: start,
            EndDate: start.AddDays(2),
            PickupLocation: "Київ"));
        createResult.Success.Should().BeTrue();

        var closeResult = await service.CloseRentalAsync(new CloseRentalRequest(
            RentalId: createResult.RentalId,
            ActualEndDate: start.Date,
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

        var start = DateTime.UtcNow.AddHours(2);
        var createResult = await service.CreateRentalAsync(new CreateRentalRequest(
            ClientId: 1,
            VehicleId: 1,
            CreatedByEmployeeId: 1,
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
        TestLookupSeed.SeedVehicleLookups(dbContext);

        dbContext.Employees.Add(new Employee
        {
            Id = 1,
            FullName = "Admin",
            RoleId = UserRole.Admin,
            Account = new Account
            {
                Login = "admin",
                PasswordHash = "x",
                IsActive = true,
                PasswordChangedAtUtc = DateTime.UtcNow
            }
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
            make: "Toyota",
            model: "Camry",
            licensePlate: "AA0011AA",
            fuelTypeCode: "Бензин",
            transmissionTypeCode: "Автомат",
            powertrainCapacityValue: 2m,
            cargoCapacityValue: 500m,
            consumptionValue: 7m,
            mileage: 1000,
            dailyRate: 70m,
            id: 1));
        dbContext.SaveChanges();
    }
    private sealed class StubContractNumberService(string value) : IContractNumberService
    {
        public Task<string> NextNumberAsync(CancellationToken cancellationToken = default) => Task.FromResult(value);
    }
}





