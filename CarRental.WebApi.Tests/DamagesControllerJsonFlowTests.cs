using System.Security.Claims;
using CarRental.WebApi.Contracts;
using CarRental.WebApi.Controllers;
using CarRental.WebApi.Data;
using CarRental.WebApi.Models;
using CarRental.WebApi.Services.Damages;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRental.WebApi.Tests;

public sealed class DamagesControllerJsonFlowTests
{
    [Fact]
    public async Task Add_ShouldCreateDamageWithoutRental_AndReturnCreatedDto()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedDamageScenario(dbContext);

        var controller = CreateDamagesController(dbContext);
        var result = await controller.Add(
            new AddDamageRequest
            {
                VehicleId = 1,
                Description = "Mirror scratch",
                RepairCost = 350m,
                AutoChargeToRental = false
            },
            CancellationToken.None);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var dto = created.Value.Should().BeOfType<DamageDto>().Subject;
        dto.VehicleId.Should().Be(1);
        dto.RentalId.Should().BeNull();
        dto.ContractNumber.Should().BeNull();
        dto.Photos.Should().BeEmpty();

        var persisted = await dbContext.Damages.AsNoTracking().SingleAsync();
        persisted.Description.Should().Be("Mirror scratch");
        persisted.RentalId.Should().BeNull();
    }

    [Fact]
    public async Task Add_ShouldCreateDamageWithRental_AndGetAllShouldReturnCreatedDamage()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedDamageScenario(dbContext);

        dbContext.Rentals.Add(new Rental
        {
            Id = 10,
            ClientId = 1,
            VehicleId = 1,
            EmployeeId = 1,
            ContractNumber = "CR-2026-000035",
            StartDate = DateTime.UtcNow.AddDays(-2),
            EndDate = DateTime.UtcNow.AddDays(1),
            PickupLocation = "Kyiv",
            ReturnLocation = "Kyiv",
            StartMileage = 1000,
            TotalAmount = 500m,
            Status = RentalStatus.Closed,
            IsClosed = true,
            ClosedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var controller = CreateDamagesController(dbContext);
        var createResult = await controller.Add(
            new AddDamageRequest
            {
                VehicleId = 1,
                RentalId = 10,
                Description = "Door scratch",
                RepairCost = 1200m,
                AutoChargeToRental = false
            },
            CancellationToken.None);

        var created = createResult.Should().BeOfType<CreatedAtActionResult>().Subject;
        var dto = created.Value.Should().BeOfType<DamageDto>().Subject;
        dto.RentalId.Should().Be(10);
        dto.ContractNumber.Should().Be("CR-2026-000035");

        var getAllResult = await controller.GetAll(1, 25, CancellationToken.None);
        var ok = getAllResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var items = ok.Value.Should().BeAssignableTo<IReadOnlyList<DamageDto>>().Subject;
        items.Should().ContainSingle(item =>
            item.Id == dto.Id &&
            item.VehicleId == 1 &&
            item.RentalId == 10 &&
            item.ContractNumber == "CR-2026-000035");
    }

    [Fact]
    public async Task Add_ShouldReturnBadRequest_WhenKnownPersistenceFailureOccurs()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedDamageScenario(dbContext);

        var openStatus = await dbContext.DamageStatuses.SingleAsync(item => item.Id == DamageStatus.Open);
        dbContext.DamageStatuses.Remove(openStatus);
        await dbContext.SaveChangesAsync();

        var controller = CreateDamagesController(dbContext);
        var result = await controller.Add(
            new AddDamageRequest
            {
                VehicleId = 1,
                Description = "Backend FK failure",
                RepairCost = 200m,
                AutoChargeToRental = false
            },
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeEquivalentTo(new
        {
            message = "Не вдалося зберегти акт через неузгоджені довідники статусів пошкоджень. Перезапустіть API та спробуйте ще раз."
        });
        (await dbContext.Damages.CountAsync()).Should().Be(0);
    }

    private static DamagesController CreateDamagesController(RentalDbContext dbContext)
    {
        var controller = new DamagesController(
            dbContext,
            new DamageService(dbContext));
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

    private static void SeedDamageScenario(RentalDbContext dbContext)
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
        dbContext.Clients.Add(new Client
        {
            Id = 1,
            FullName = "Client",
            PassportData = "PP-001",
            DriverLicense = "DL-001",
            PassportExpirationDate = DateTime.UtcNow.AddYears(5),
            DriverLicenseExpirationDate = DateTime.UtcNow.AddYears(5),
            Phone = "+380500000001",
            Blacklisted = false
        });
        dbContext.Vehicles.Add(new Vehicle
        {
            Id = 1,
            Make = "Dacia",
            Model = "Duster",
            FuelType = "Бензин",
            TransmissionType = "Автомат",
            PowertrainCapacityValue = 2m,
            PowertrainCapacityUnit = "L",
            CargoCapacityValue = 500m,
            CargoCapacityUnit = "L",
            ConsumptionValue = 7m,
            ConsumptionUnit = "L_PER_100KM",
            LicensePlate = "BC6709CE",
            Mileage = 1000,
            DailyRate = 70m,
            IsBookable = true,
            IsAvailable = true,
            ServiceIntervalKm = 10000
        });
        dbContext.SaveChanges();
    }
}
