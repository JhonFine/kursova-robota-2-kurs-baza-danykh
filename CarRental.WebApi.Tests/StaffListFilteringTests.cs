using System.Security.Claims;
using CarRental.WebApi.Controllers;
using CarRental.WebApi.Data;
using CarRental.WebApi.Models;
using CarRental.WebApi.Services.Documents;
using CarRental.WebApi.Services.Rentals;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.WebApi.Tests;

public sealed class StaffListFilteringTests
{
    [Theory]
    [InlineData("Iryna", 10)]
    [InlineData("+380500000002", 11)]
    [InlineData("DL-003", 12)]
    public async Task Clients_GetAll_ShouldSearchAcrossKeyFields(string search, int expectedClientId)
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedClients(dbContext);

        var controller = CreateClientsController(dbContext);

        var actionResult = await controller.GetAll(
            search: search,
            blacklisted: null,
            page: 1,
            pageSize: 10,
            cancellationToken: CancellationToken.None);

        var ok = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var clients = ok.Value.Should().BeAssignableTo<IReadOnlyList<CarRental.WebApi.Contracts.ClientDto>>().Subject;
        clients.Should().ContainSingle(item => item.Id == expectedClientId);
        controller.Response.Headers["X-Total-Count"].ToString().Should().Be("1");
    }

    [Fact]
    public async Task Clients_GetAll_ShouldFilterByBlacklistState()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedClients(dbContext);

        var controller = CreateClientsController(dbContext);

        var actionResult = await controller.GetAll(
            search: null,
            blacklisted: true,
            page: 1,
            pageSize: 10,
            cancellationToken: CancellationToken.None);

        var ok = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var clients = ok.Value.Should().BeAssignableTo<IReadOnlyList<CarRental.WebApi.Contracts.ClientDto>>().Subject;
        clients.Select(item => item.Id).Should().Equal(11);
        controller.Response.Headers["X-Total-Count"].ToString().Should().Be("1");
    }

    [Fact]
    public async Task Vehicles_GetAll_ShouldFilterBySearchAvailabilityAndClass()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedVehicles(dbContext);

        var controller = CreateVehiclesController(dbContext);

        var actionResult = await controller.GetAll(
            search: "TES",
            availability: true,
            vehicleClass: "Premium",
            sortBy: "name",
            sortDir: "asc",
            page: 1,
            pageSize: 10,
            cancellationToken: CancellationToken.None);

        var ok = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var vehicles = ok.Value.Should().BeAssignableTo<IReadOnlyList<CarRental.WebApi.Contracts.VehicleDto>>().Subject;
        vehicles.Select(item => item.Id).Should().Equal(22);
        controller.Response.Headers["X-Total-Count"].ToString().Should().Be("1");
    }

    [Fact]
    public async Task Vehicles_GetAll_ShouldSortByDailyRateDescending()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedVehicles(dbContext);

        var controller = CreateVehiclesController(dbContext);

        var actionResult = await controller.GetAll(
            search: null,
            availability: true,
            vehicleClass: null,
            sortBy: "dailyRate",
            sortDir: "desc",
            page: 1,
            pageSize: 10,
            cancellationToken: CancellationToken.None);

        var ok = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var vehicles = ok.Value.Should().BeAssignableTo<IReadOnlyList<CarRental.WebApi.Contracts.VehicleDto>>().Subject;
        vehicles.Select(item => item.Id).Should().Equal(22, 21, 20);
    }

    [Theory]
    [InlineData("CR-2026-300001", 100)]
    [InlineData("Acme Client", 100)]
    [InlineData("AA9090TT", 100)]
    public async Task Rentals_GetAll_ShouldSearchAcrossContractClientAndVehicleFields(string search, int expectedRentalId)
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedRentalsScenario(dbContext);

        var controller = CreateRentalsController(dbContext);

        var actionResult = await controller.GetAll(
            status: null,
            vehicleId: null,
            clientId: null,
            fromDate: null,
            toDate: null,
            search: search,
            page: 1,
            pageSize: 10,
            cancellationToken: CancellationToken.None);

        var ok = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var rentals = ok.Value.Should().BeAssignableTo<IReadOnlyList<CarRental.WebApi.Contracts.RentalDto>>().Subject;
        rentals.Should().ContainSingle(item => item.Id == expectedRentalId);
        controller.Response.Headers["X-Total-Count"].ToString().Should().Be("1");
    }

    private static ClientsController CreateClientsController(RentalDbContext dbContext)
    {
        var controller = new ClientsController(dbContext);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    private static VehiclesController CreateVehiclesController(RentalDbContext dbContext)
    {
        var controller = new VehiclesController(dbContext);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    private static RentalsController CreateRentalsController(RentalDbContext dbContext)
    {
        var controller = new RentalsController(dbContext, new RentalService(dbContext, new StubContractNumberService()));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "1"),
                    new Claim("account_id", "1"),
                    new Claim("employee_id", "1"),
                    new Claim(ClaimTypes.Role, UserRole.Manager.ToString())
                ], "TestAuth"))
            }
        };

        return controller;
    }

    private static void SeedClients(RentalDbContext dbContext)
    {
        SeedManager(dbContext);

        dbContext.Clients.AddRange(
            new Client
            {
                Id = 10,
                FullName = "Iryna Kovalenko",
                PassportData = "PP-001",
                DriverLicense = "DL-001",
                Phone = "+380500000001",
                IsBlacklisted = false
            },
            new Client
            {
                Id = 11,
                FullName = "Maksym Bondar",
                PassportData = "PP-002",
                DriverLicense = "DL-002",
                Phone = "+380500000002",
                IsBlacklisted = true,
                BlacklistReason = "Repeated violations",
                BlacklistedAtUtc = DateTime.UtcNow,
                BlacklistedByEmployeeId = 1
            },
            new Client
            {
                Id = 12,
                FullName = "Olena Hnat",
                PassportData = "PP-003",
                DriverLicense = "DL-003",
                Phone = "+380500000003",
                IsBlacklisted = false
            });

        dbContext.SaveChanges();
    }

    private static void SeedVehicles(RentalDbContext dbContext)
    {
        TestLookupSeed.SeedVehicleLookups(dbContext);

        dbContext.Vehicles.AddRange(
            TestLookupSeed.CreateVehicle(
                dbContext,
                make: "Toyota",
                model: "Yaris",
                licensePlate: "AA1000AA",
                fuelTypeCode: "Р‘РµРЅР·РёРЅ",
                transmissionTypeCode: "РђРІС‚РѕРјР°С‚",
                powertrainCapacityValue: 1.5m,
                cargoCapacityValue: 286m,
                consumptionValue: 5.5m,
                mileage: 50000,
                dailyRate: 1200m,
                id: 20),
            TestLookupSeed.CreateVehicle(
                dbContext,
                make: "Audi",
                model: "A4",
                licensePlate: "AA2000AA",
                fuelTypeCode: "Р‘РµРЅР·РёРЅ",
                transmissionTypeCode: "РђРІС‚РѕРјР°С‚",
                powertrainCapacityValue: 2m,
                cargoCapacityValue: 460m,
                consumptionValue: 7.2m,
                mileage: 25000,
                dailyRate: 2200m,
                id: 21),
            TestLookupSeed.CreateVehicle(
                dbContext,
                make: "Tesla",
                model: "Model 3",
                licensePlate: "TES123",
                fuelTypeCode: "Р•Р»РµРєС‚СЂРѕ",
                transmissionTypeCode: "РђРІС‚РѕРјР°С‚",
                powertrainCapacityValue: 75m,
                cargoCapacityValue: 425m,
                consumptionValue: 16m,
                mileage: 12000,
                dailyRate: 3000m,
                id: 22),
            TestLookupSeed.CreateVehicle(
                dbContext,
                make: "BMW",
                model: "320",
                licensePlate: "AA3000AA",
                fuelTypeCode: "Р”РёР·РµР»СЊ",
                transmissionTypeCode: "РђРІС‚РѕРјР°С‚",
                powertrainCapacityValue: 2m,
                cargoCapacityValue: 480m,
                consumptionValue: 6.3m,
                mileage: 32000,
                dailyRate: 2600m,
                id: 23));

        dbContext.Vehicles.Single(item => item.Id == 23).IsBookable = false;
        dbContext.SaveChanges();
    }

    private static void SeedRentalsScenario(RentalDbContext dbContext)
    {
        TestLookupSeed.SeedVehicleLookups(dbContext);
        SeedManager(dbContext);

        dbContext.Clients.AddRange(
            new Client
            {
                Id = 30,
                FullName = "Acme Client",
                PassportData = "PP-030",
                DriverLicense = "DL-030",
                Phone = "+380500000030",
                IsBlacklisted = false
            },
            new Client
            {
                Id = 31,
                FullName = "Other Client",
                PassportData = "PP-031",
                DriverLicense = "DL-031",
                Phone = "+380500000031",
                IsBlacklisted = false
            });

        dbContext.Vehicles.AddRange(
            TestLookupSeed.CreateVehicle(
                dbContext,
                make: "Skoda",
                model: "Octavia",
                licensePlate: "AA9090TT",
                fuelTypeCode: "Р‘РµРЅР·РёРЅ",
                transmissionTypeCode: "РђРІС‚РѕРјР°С‚",
                powertrainCapacityValue: 1.8m,
                cargoCapacityValue: 600m,
                consumptionValue: 6.4m,
                mileage: 15000,
                dailyRate: 55m,
                id: 40),
            TestLookupSeed.CreateVehicle(
                dbContext,
                make: "Ford",
                model: "Focus",
                licensePlate: "BB8080KK",
                fuelTypeCode: "Р‘РµРЅР·РёРЅ",
                transmissionTypeCode: "РњРµС…Р°РЅС–РєР°",
                powertrainCapacityValue: 1.6m,
                cargoCapacityValue: 375m,
                consumptionValue: 6.1m,
                mileage: 18000,
                dailyRate: 50m,
                id: 41));

        dbContext.Rentals.AddRange(
            new Rental
            {
                Id = 100,
                ClientId = 30,
                VehicleId = 40,
                CreatedByEmployeeId = 1,
                ContractNumber = "CR-2026-300001",
                StartDate = DateTime.UtcNow.AddDays(1),
                EndDate = DateTime.UtcNow.AddDays(2),
                StartMileage = 15000,
                TotalAmount = 160m,
                StatusId = RentalStatus.Booked,
                CreatedAtUtc = DateTime.UtcNow
            },
            new Rental
            {
                Id = 101,
                ClientId = 31,
                VehicleId = 41,
                CreatedByEmployeeId = 1,
                ContractNumber = "CR-2026-300002",
                StartDate = DateTime.UtcNow.AddDays(3),
                EndDate = DateTime.UtcNow.AddDays(4),
                StartMileage = 18000,
                TotalAmount = 140m,
                StatusId = RentalStatus.Active,
                CreatedAtUtc = DateTime.UtcNow
            });

        dbContext.SaveChanges();
    }

    private static void SeedManager(RentalDbContext dbContext)
    {
        if (dbContext.Employees.Any(item => item.Id == 1))
        {
            return;
        }

        dbContext.Employees.Add(new Employee
        {
            Id = 1,
            FullName = "Manager User",
            RoleId = UserRole.Manager,
            Account = new Account
            {
                Id = 1,
                Login = "manager",
                PasswordHash = "x",
                IsActive = true,
                PasswordChangedAtUtc = DateTime.UtcNow
            }
        });
    }

    private sealed class StubContractNumberService : IContractNumberService
    {
        public Task<string> NextNumberAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("CR-2026-999998");
    }
}
