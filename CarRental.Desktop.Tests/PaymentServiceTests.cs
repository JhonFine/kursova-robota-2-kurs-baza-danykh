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
            EmployeeId = 1,
            ContractNumber = "CR-2026-000080",
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(1),
            StartMileage = 1000,
            TotalAmount = 100m,
            Status = RentalStatus.Active,
            IsClosed = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.Rentals.Add(rental);
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

        var service = new PaymentService(dbContext);
        var balance = await service.GetRentalBalanceAsync(rental.Id);

        balance.Should().Be(70m);
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
            IsAvailable = true
        });
        dbContext.SaveChanges();
    }
}
