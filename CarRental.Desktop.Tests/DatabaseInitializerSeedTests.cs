using System.Text.RegularExpressions;
using CarRental.Desktop.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Desktop.Tests;

// Перевіряє, що великий demo-seed лишається внутрішньо узгодженим і не роз'їжджається після змін у reference data.
public sealed class DatabaseInitializerSeedTests
{
    [Fact]
    public async Task DatabaseInitializer_ShouldSeedConsistentRuntimeData()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();

        DatabaseInitializer.Seed(dbContext);

        var today = DateTime.UtcNow.Date;
        var periodStart = today.AddDays(-365);
        var plateRegex = new Regex("^[ABCEHIKMOPTX]{2}\\d{4}[ABCEHIKMOPTX]{2}$", RegexOptions.CultureInvariant);

        var clientsCount = await dbContext.Clients.CountAsync();
        var vehicles = await dbContext.Vehicles.AsNoTracking().ToListAsync();
        var rentals = await dbContext.Rentals.AsNoTracking().ToListAsync();
        var damagesCount = await dbContext.Damages.CountAsync();
        var maintenanceCount = await dbContext.MaintenanceRecords.CountAsync();

        clientsCount.Should().Be(180);
        vehicles.Should().HaveCount(60);
        rentals.Should().HaveCount(300);
        damagesCount.Should().Be(21);
        maintenanceCount.Should().BeGreaterThan(0);

        vehicles.Should().OnlyContain(vehicle => vehicle.DailyRate >= 1000m && vehicle.DailyRate <= 3500m);
        vehicles.Should().OnlyContain(vehicle => plateRegex.IsMatch(vehicle.LicensePlate));
        rentals.Should().OnlyContain(rental => rental.StartDate >= periodStart && rental.EndDate <= today.AddHours(23));
    }
}
