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
                    new Claim(ClaimTypes.Role, UserRole.Manager.ToString())
                ], "TestAuth"))
            }
        };

        return controller;
    }

    private static void SeedClients(RentalDbContext dbContext)
    {
        dbContext.Clients.AddRange(
            new Client
            {
                Id = 10,
                FullName = "Iryna Kovalenko",
                PassportData = "PP-001",
                DriverLicense = "DL-001",
                Phone = "+380500000001",
                Blacklisted = false
            },
            new Client
            {
                Id = 11,
                FullName = "Maksym Bondar",
                PassportData = "PP-002",
                DriverLicense = "DL-002",
                Phone = "+380500000002",
                Blacklisted = true
            },
            new Client
            {
                Id = 12,
                FullName = "Olena Hnat",
                PassportData = "PP-003",
                DriverLicense = "DL-003",
                Phone = "+380500000003",
                Blacklisted = false
            });

        dbContext.SaveChanges();
    }

    private static void SeedVehicles(RentalDbContext dbContext)
    {
        TestLookupSeed.SeedVehicleLookups(dbContext);

        dbContext.Vehicles.AddRange(
            new Vehicle
            {
                Id = 20,
                Make = "Toyota",
                Model = "Yaris",
                FuelType = "Бензин",
                TransmissionType = "Автомат",
                PowertrainCapacityValue = 1.5m,
                PowertrainCapacityUnit = "L",
                CargoCapacityValue = 286m,
                CargoCapacityUnit = "L",
                ConsumptionValue = 5.5m,
                ConsumptionUnit = "L_PER_100KM",
                LicensePlate = "AA1000AA",
                Mileage = 50000,
                DailyRate = 1200m,
                IsAvailable = true,
                ServiceIntervalKm = 10000
            },
            new Vehicle
            {
                Id = 21,
                Make = "Audi",
                Model = "A4",
                FuelType = "Бензин",
                TransmissionType = "Автомат",
                PowertrainCapacityValue = 2m,
                PowertrainCapacityUnit = "L",
                CargoCapacityValue = 460m,
                CargoCapacityUnit = "L",
                ConsumptionValue = 7.2m,
                ConsumptionUnit = "L_PER_100KM",
                LicensePlate = "AA2000AA",
                Mileage = 25000,
                DailyRate = 2200m,
                IsAvailable = true,
                ServiceIntervalKm = 10000
            },
            new Vehicle
            {
                Id = 22,
                Make = "Tesla",
                Model = "Model 3",
                FuelType = "Електро",
                TransmissionType = "Автомат",
                PowertrainCapacityValue = 75m,
                PowertrainCapacityUnit = "KWH",
                CargoCapacityValue = 425m,
                CargoCapacityUnit = "L",
                ConsumptionValue = 16m,
                ConsumptionUnit = "KWH_PER_100KM",
                LicensePlate = "TES123",
                Mileage = 12000,
                DailyRate = 3000m,
                IsAvailable = true,
                ServiceIntervalKm = 10000
            },
            new Vehicle
            {
                Id = 23,
                Make = "BMW",
                Model = "320",
                FuelType = "Дизель",
                TransmissionType = "Автомат",
                PowertrainCapacityValue = 2m,
                PowertrainCapacityUnit = "L",
                CargoCapacityValue = 480m,
                CargoCapacityUnit = "L",
                ConsumptionValue = 6.3m,
                ConsumptionUnit = "L_PER_100KM",
                LicensePlate = "AA3000AA",
                Mileage = 32000,
                DailyRate = 2600m,
                IsAvailable = false,
                ServiceIntervalKm = 10000
            });

        dbContext.SaveChanges();
    }

    private static void SeedRentalsScenario(RentalDbContext dbContext)
    {
        TestLookupSeed.SeedVehicleLookups(dbContext);

        dbContext.Employees.Add(new Employee
        {
            Id = 1,
            FullName = "Manager User",
            Login = "manager",
            PasswordHash = "x",
            Role = UserRole.Manager,
            IsActive = true
        });

        dbContext.Clients.AddRange(
            new Client
            {
                Id = 30,
                FullName = "Acme Client",
                PassportData = "PP-030",
                DriverLicense = "DL-030",
                Phone = "+380500000030",
                Blacklisted = false
            },
            new Client
            {
                Id = 31,
                FullName = "Other Client",
                PassportData = "PP-031",
                DriverLicense = "DL-031",
                Phone = "+380500000031",
                Blacklisted = false
            });

        dbContext.Vehicles.AddRange(
            new Vehicle
            {
                Id = 40,
                Make = "Skoda",
                Model = "Octavia",
                FuelType = "Бензин",
                TransmissionType = "Автомат",
                PowertrainCapacityValue = 1.8m,
                PowertrainCapacityUnit = "L",
                CargoCapacityValue = 600m,
                CargoCapacityUnit = "L",
                ConsumptionValue = 6.4m,
                ConsumptionUnit = "L_PER_100KM",
                LicensePlate = "AA9090TT",
                Mileage = 15000,
                DailyRate = 55m,
                IsAvailable = true,
                ServiceIntervalKm = 10000
            },
            new Vehicle
            {
                Id = 41,
                Make = "Ford",
                Model = "Focus",
                FuelType = "Бензин",
                TransmissionType = "Механіка",
                PowertrainCapacityValue = 1.6m,
                PowertrainCapacityUnit = "L",
                CargoCapacityValue = 375m,
                CargoCapacityUnit = "L",
                ConsumptionValue = 6.1m,
                ConsumptionUnit = "L_PER_100KM",
                LicensePlate = "BB8080KK",
                Mileage = 18000,
                DailyRate = 50m,
                IsAvailable = true,
                ServiceIntervalKm = 10000
            });

        dbContext.Rentals.AddRange(
            new Rental
            {
                Id = 100,
                ClientId = 30,
                VehicleId = 40,
                EmployeeId = 1,
                ContractNumber = "CR-2026-300001",
                StartDate = DateTime.UtcNow.AddDays(1),
                EndDate = DateTime.UtcNow.AddDays(2),
                StartMileage = 15000,
                TotalAmount = 160m,
                Status = RentalStatus.Booked,
                CreatedAtUtc = DateTime.UtcNow
            },
            new Rental
            {
                Id = 101,
                ClientId = 31,
                VehicleId = 41,
                EmployeeId = 1,
                ContractNumber = "CR-2026-300002",
                StartDate = DateTime.UtcNow.AddDays(3),
                EndDate = DateTime.UtcNow.AddDays(4),
                StartMileage = 18000,
                TotalAmount = 140m,
                Status = RentalStatus.Active,
                CreatedAtUtc = DateTime.UtcNow
            });

        dbContext.SaveChanges();
    }

    private sealed class StubContractNumberService : IContractNumberService
    {
        public Task<string> NextNumberAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("CR-2026-999998");
    }
}
