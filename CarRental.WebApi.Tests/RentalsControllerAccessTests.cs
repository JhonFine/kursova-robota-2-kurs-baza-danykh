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
using Microsoft.EntityFrameworkCore;
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
            EmployeeId = 1,
            ContractNumber = "CR-2026-100000",
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(2),
            StartMileage = 12000,
            TotalAmount = 140m,
            Status = RentalStatus.Booked,
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, employeeId: 2, role: UserRole.User);

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
            EmployeeId = 1,
            ContractNumber = "CR-2026-100001",
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(2),
            StartMileage = 12000,
            TotalAmount = 140m,
            Status = RentalStatus.Booked,
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, employeeId: 2, role: UserRole.User);

        var result = await controller.Cancel(
            101,
            new ApiCancelRentalRequest { Reason = "Скасовано клієнтом через web" },
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var rental = ok.Value.Should().BeOfType<RentalDto>().Subject;
        rental.Status.Should().Be(RentalStatus.Canceled);

        var persisted = await dbContext.Rentals.AsNoTracking().SingleAsync(item => item.Id == 101);
        persisted.Status.Should().Be(RentalStatus.Canceled);
        persisted.CancellationReason.Should().Be("Скасовано клієнтом через web");
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
            EmployeeId = 1,
            ContractNumber = "CR-2026-100002",
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(2),
            StartMileage = 12000,
            TotalAmount = 140m,
            Status = RentalStatus.Booked,
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, employeeId: 2, role: UserRole.User);

        var result = await controller.Cancel(
            102,
            new ApiCancelRentalRequest { Reason = "Скасовано клієнтом через web" },
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
            EmployeeId = 1,
            ContractNumber = "CR-2026-100003",
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
            StartMileage = 12000,
            TotalAmount = 140m,
            Status = status,
            IsClosed = status == RentalStatus.Closed,
            ClosedAtUtc = status == RentalStatus.Closed ? DateTime.UtcNow : null,
            CanceledAtUtc = status == RentalStatus.Canceled ? DateTime.UtcNow : null,
            CancellationReason = status == RentalStatus.Canceled ? "Попередня причина" : null,
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, employeeId: 2, role: UserRole.User);

        var result = await controller.Cancel(
            103,
            new ApiCancelRentalRequest { Reason = "Скасовано клієнтом через web" },
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().NotBeNull();

        var persisted = await dbContext.Rentals.AsNoTracking().SingleAsync(item => item.Id == 103);
        persisted.Status.Should().Be(status);
    }

    [Fact]
    public async Task Create_ShouldRejectPastStart_ForUser()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedControllerData(dbContext);

        var controller = CreateController(dbContext, employeeId: 2, role: UserRole.User);

        var result = await controller.Create(
            new CarRental.WebApi.Contracts.CreateRentalRequest
            {
                ClientId = 10,
                VehicleId = 20,
                StartDate = DateTime.Now.AddHours(-2),
                EndDate = DateTime.Now.AddHours(2),
                PickupLocation = "Київ",
                ReturnLocation = "Київ"
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

        var controller = CreateController(dbContext, employeeId: 2, role: UserRole.User);

        var result = await controller.Create(
            new CarRental.WebApi.Contracts.CreateRentalRequest
            {
                ClientId = 10,
                VehicleId = 20,
                StartDate = DateTime.Now.AddDays(1),
                EndDate = DateTime.Now.AddDays(2),
                PickupLocation = "Київ",
                ReturnLocation = "Київ"
            },
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    private static RentalsController CreateController(RentalDbContext dbContext, int employeeId, UserRole role)
    {
        var controller = new RentalsController(dbContext, new RentalService(dbContext, new StubContractNumberService()));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, employeeId.ToString()),
                    new Claim(ClaimTypes.Role, role.ToString())
                ], "TestAuth"))
            }
        };

        return controller;
    }

    private static void SeedControllerData(RentalDbContext dbContext)
    {
        dbContext.Employees.AddRange(
            new Employee
            {
                Id = 1,
                FullName = "Manager",
                Login = "manager",
                PasswordHash = "x",
                Role = UserRole.Manager,
                IsActive = true
            },
            new Employee
            {
                Id = 2,
                FullName = "Client User",
                Login = "client_user",
                PasswordHash = "x",
                Role = UserRole.User,
                IsActive = true,
                ClientId = 10
            });

        dbContext.Clients.AddRange(
            new Client
            {
                Id = 10,
                FullName = "Client User",
                PassportData = "EMP-000002",
                DriverLicense = "USR-000002",
                Phone = "+380000000001",
                Blacklisted = false
            },
            new Client
            {
                Id = 11,
                FullName = "Another Client",
                PassportData = "PP-OTHER",
                DriverLicense = "DL-OTHER",
                Phone = "+380000000002",
                Blacklisted = false
            });

        dbContext.Vehicles.Add(new Vehicle
        {
            Id = 20,
            Make = "Toyota",
            Model = "Camry",
            LicensePlate = "AA2020AA",
            Mileage = 12000,
            DailyRate = 70m,
            IsAvailable = true,
            ServiceIntervalKm = 10000
        });

        dbContext.SaveChanges();
    }

    private sealed class StubContractNumberService : IContractNumberService
    {
        public Task<string> NextNumberAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("CR-2026-999999");
    }
}
