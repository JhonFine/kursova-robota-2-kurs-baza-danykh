using System.Security.Claims;
using CarRental.WebApi.Contracts;
using CarRental.WebApi.Controllers;
using CarRental.WebApi.Data;
using CarRental.WebApi.Models;
using CarRental.WebApi.Services.Documents;
using CarRental.WebApi.Services.Rentals;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using ApiCancelRentalRequest = CarRental.WebApi.Contracts.CancelRentalRequest;

namespace CarRental.WebApi.Tests;

public sealed class RentalsControllerAccessTests
{
    [Fact]
    public async Task GetAll_ShouldIncludeManagerCreatedRental_ForUserClientProfile()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedControllerData(dbContext);

        dbContext.Rentals.Add(new Rental
        {
            Id = 100,
            ClientId = 10,
            VehicleId = 20,
            CreatedByEmployeeId = 1,
            ContractNumber = "CR-2026-100000",
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(2),
            StartMileage = 12000,
            TotalAmount = 140m,
            StatusId = RentalStatus.Booked,
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, accountId: 2, role: UserRole.User, clientId: 10);

        var actionResult = await controller.GetAll(
            status: null,
            vehicleId: null,
            clientId: null,
            fromDate: null,
            toDate: null,
            search: null,
            page: null,
            pageSize: null,
            cancellationToken: CancellationToken.None);

        var ok = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var rentals = ok.Value.Should().BeAssignableTo<IReadOnlyList<RentalDto>>().Subject;
        rentals.Should().ContainSingle(item => item.Id == 100 && item.ClientId == 10);
    }

    [Fact]
    public async Task Cancel_ShouldAllowUserToCancelOwnBookedRental()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedControllerData(dbContext);

        dbContext.Rentals.Add(new Rental
        {
            Id = 101,
            ClientId = 10,
            VehicleId = 20,
            CreatedByEmployeeId = 1,
            ContractNumber = "CR-2026-100001",
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(2),
            StartMileage = 12000,
            TotalAmount = 140m,
            StatusId = RentalStatus.Booked,
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, accountId: 2, role: UserRole.User, clientId: 10);

        var result = await controller.Cancel(
            101,
            new ApiCancelRentalRequest { Reason = "РЎРєР°СЃРѕРІР°РЅРѕ РєР»С–С”РЅС‚РѕРј С‡РµСЂРµР· web" },
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var rental = ok.Value.Should().BeOfType<RentalDto>().Subject;
        rental.StatusId.Should().Be(RentalStatus.Canceled);

        var persisted = await dbContext.Rentals.AsNoTracking().SingleAsync(item => item.Id == 101);
        persisted.StatusId.Should().Be(RentalStatus.Canceled);
        persisted.CancellationReason.Should().Be("РЎРєР°СЃРѕРІР°РЅРѕ РєР»С–С”РЅС‚РѕРј С‡РµСЂРµР· web");
        persisted.CanceledByEmployeeId.Should().BeNull();
    }

    [Fact]
    public async Task Cancel_ShouldReturnForbidden_ForRentalOwnedByAnotherClient()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedControllerData(dbContext);

        dbContext.Rentals.Add(new Rental
        {
            Id = 102,
            ClientId = 11,
            VehicleId = 20,
            CreatedByEmployeeId = 1,
            ContractNumber = "CR-2026-100002",
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(2),
            StartMileage = 12000,
            TotalAmount = 140m,
            StatusId = RentalStatus.Booked,
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, accountId: 2, role: UserRole.User, clientId: 10);

        var result = await controller.Cancel(
            102,
            new ApiCancelRentalRequest { Reason = "РЎРєР°СЃРѕРІР°РЅРѕ РєР»С–С”РЅС‚РѕРј С‡РµСЂРµР· web" },
            CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Theory]
    [InlineData(RentalStatus.Active)]
    [InlineData(RentalStatus.Closed)]
    [InlineData(RentalStatus.Canceled)]
    public async Task Cancel_ShouldReturnBadRequest_ForOwnRentalOutsideBookedState(RentalStatus status)
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedControllerData(dbContext);

        dbContext.Rentals.Add(new Rental
        {
            Id = 103,
            ClientId = 10,
            VehicleId = 20,
            CreatedByEmployeeId = 1,
            ContractNumber = "CR-2026-100003",
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
            StartMileage = 12000,
            TotalAmount = 140m,
            StatusId = status,
            IsClosed = status == RentalStatus.Closed,
            ClosedAtUtc = status == RentalStatus.Closed ? DateTime.UtcNow : null,
            CanceledAtUtc = status == RentalStatus.Canceled ? DateTime.UtcNow : null,
            CancellationReason = status == RentalStatus.Canceled ? "РџРѕРїРµСЂРµРґРЅСЏ РїСЂРёС‡РёРЅР°" : null,
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, accountId: 2, role: UserRole.User, clientId: 10);

        var result = await controller.Cancel(
            103,
            new ApiCancelRentalRequest { Reason = "РЎРєР°СЃРѕРІР°РЅРѕ РєР»С–С”РЅС‚РѕРј С‡РµСЂРµР· web" },
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().NotBeNull();

        var persisted = await dbContext.Rentals.AsNoTracking().SingleAsync(item => item.Id == 103);
        persisted.StatusId.Should().Be(status);
    }

    [Fact]
    public async Task Create_ShouldRejectPastStart_ForUser()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedControllerData(dbContext);

        var controller = CreateController(dbContext, accountId: 2, role: UserRole.User, clientId: 10);

        var result = await controller.Create(
            new CarRental.WebApi.Contracts.CreateRentalRequest
            {
                ClientId = 10,
                VehicleId = 20,
                StartDate = DateTime.UtcNow.AddHours(-2),
                EndDate = DateTime.UtcNow.AddHours(2),
                PickupLocation = "РљРёС—РІ",
                ReturnLocation = "РљРёС—РІ"
            },
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_ShouldRejectIncompleteUserProfile()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedControllerData(dbContext);

        var controller = CreateController(dbContext, accountId: 2, role: UserRole.User, clientId: 10);

        var result = await controller.Create(
            new CarRental.WebApi.Contracts.CreateRentalRequest
            {
                ClientId = 10,
                VehicleId = 20,
                StartDate = DateTime.UtcNow.AddDays(1),
                EndDate = DateTime.UtcNow.AddDays(2),
                PickupLocation = "РљРёС—РІ",
                ReturnLocation = "РљРёС—РІ"
            },
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    private static RentalsController CreateController(
        RentalDbContext dbContext,
        int accountId,
        UserRole role,
        int? employeeId = null,
        int? clientId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, accountId.ToString()),
            new("account_id", accountId.ToString()),
            new(ClaimTypes.Role, role.ToString())
        };

        if (employeeId.HasValue)
        {
            claims.Add(new Claim("employee_id", employeeId.Value.ToString()));
        }

        if (clientId.HasValue)
        {
            claims.Add(new Claim("client_id", clientId.Value.ToString()));
        }

        var controller = new RentalsController(dbContext, new RentalService(dbContext, new StubContractNumberService()));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };

        return controller;
    }

    private static void SeedControllerData(RentalDbContext dbContext)
    {
        TestLookupSeed.SeedVehicleLookups(dbContext);

        var managerAccount = new Account
        {
            Id = 1,
            Login = "manager",
            PasswordHash = "x",
            IsActive = true,
            PasswordChangedAtUtc = DateTime.UtcNow
        };
        var clientAccount = new Account
        {
            Id = 2,
            Login = "client_user",
            PasswordHash = "x",
            IsActive = true,
            PasswordChangedAtUtc = DateTime.UtcNow
        };

        dbContext.Accounts.AddRange(managerAccount, clientAccount);
        dbContext.Employees.Add(new Employee
        {
            Id = 1,
            FullName = "Manager",
            RoleId = UserRole.Manager,
            Account = managerAccount
        });

        dbContext.Clients.AddRange(
            new Client
            {
                Id = 10,
                FullName = "Client User",
                Account = clientAccount,
                PassportData = "EMP-000002",
                DriverLicense = "USR-000002",
                Phone = "+380000000001",
                IsBlacklisted = false
            },
            new Client
            {
                Id = 11,
                FullName = "Another Client",
                PassportData = "PP-OTHER",
                DriverLicense = "DL-OTHER",
                Phone = "+380000000002",
                IsBlacklisted = false
            });

        dbContext.Vehicles.Add(TestLookupSeed.CreateVehicle(
            dbContext,
            make: "Toyota",
            model: "Camry",
            licensePlate: "AA2020AA",
            fuelTypeCode: "Р‘РµРЅР·РёРЅ",
            transmissionTypeCode: "РђРІС‚РѕРјР°С‚",
            powertrainCapacityValue: 2m,
            cargoCapacityValue: 500m,
            consumptionValue: 7m,
            mileage: 12000,
            dailyRate: 70m,
            id: 20));

        dbContext.SaveChanges();
    }

    private sealed class StubContractNumberService : IContractNumberService
    {
        public Task<string> NextNumberAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("CR-2026-999999");
    }
}
