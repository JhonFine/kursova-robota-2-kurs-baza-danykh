using CarRental.Desktop.Data;
using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Payments;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Desktop.Tests;

public sealed class PaymentServiceTests
{
    [Fact]
    public async Task GetRentalBalanceAsync_ShouldCalculateBalanceOnPostgres()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);

        var rental = new Rental
        {
            Id = 1,
            ClientId = 1,
            VehicleId = 1,
            CreatedByEmployeeId = 1,
            ContractNumber = "CR-2026-000080",
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddDays(1),
            StartMileage = 1000,
            TotalAmount = 100m,
            StatusId = RentalStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.Rentals.Add(rental);
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

        var service = new PaymentService(dbContext);
        var balance = await service.GetRentalBalanceAsync(rental.Id);

        balance.Should().Be(70m);
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
            1000,
            70m,
            id: 1));
        dbContext.SaveChanges();
    }
}
