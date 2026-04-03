using System.Text.RegularExpressions;
using CarRental.WebApi.Data;
using CarRental.WebApi.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CarRental.WebApi.Tests;

public sealed class SeedDataIntegrationTests
{
    [PostgresFact]
    public async Task DatabaseInitializer_ShouldSeedRealisticUkrainianDemoData()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();

        DatabaseInitializer.Seed(dbContext);

        var today = DateTime.UtcNow.Date;
        var periodStart = today.AddDays(-365);
        var plateRegex = new Regex("^[ABCEHIKMOPTX]{2}\\d{4}[ABCEHIKMOPTX]{2}$", RegexOptions.CultureInvariant);

        var clientsCount = await dbContext.Clients.CountAsync();
        var vehicles = await dbContext.Vehicles.AsNoTracking().OrderBy(item => item.Id).ToListAsync();
        var rentals = await dbContext.Rentals.AsNoTracking().OrderBy(item => item.VehicleId).ThenBy(item => item.StartDate).ToListAsync();
        var damages = await dbContext.Damages.AsNoTracking().ToListAsync();
        var paymentsCount = await dbContext.Payments.CountAsync();
        var maintenanceCount = await dbContext.MaintenanceRecords.CountAsync();

        clientsCount.Should().Be(180);
        vehicles.Should().HaveCount(60);
        rentals.Should().HaveCount(300);
        damages.Should().HaveCount(21);
        paymentsCount.Should().BeGreaterThan(0);
        maintenanceCount.Should().BeGreaterThan(0);

        vehicles.Should().OnlyContain(vehicle => vehicle.DailyRate >= 1000m && vehicle.DailyRate <= 3500m);
        vehicles.Should().OnlyContain(vehicle => plateRegex.IsMatch(vehicle.LicensePlate));

        rentals.Should().OnlyContain(rental =>
            rental.StartDate >= periodStart &&
            rental.EndDate >= periodStart &&
            rental.StartDate <= today.AddHours(23) &&
            rental.EndDate <= today.AddHours(23));

        rentals.Count(rental => rental.StatusId == RentalStatus.Closed).Should().Be(288);
        rentals.Count(rental => rental.StatusId == RentalStatus.Active).Should().Be(8);
        rentals.Count(rental => rental.StatusId == RentalStatus.Canceled).Should().Be(4);

        var groupedRentals = rentals
            .Where(rental => rental.StatusId != RentalStatus.Canceled)
            .GroupBy(rental => rental.VehicleId);
        foreach (var group in groupedRentals)
        {
            var ordered = group.OrderBy(rental => rental.StartDate).ToList();
            for (var index = 1; index < ordered.Count; index++)
            {
                ordered[index].StartDate.Should().BeOnOrAfter(ordered[index - 1].EndDate);
            }
        }

        var totalRevenue = rentals
            .Where(rental => rental.StatusId == RentalStatus.Closed)
            .Sum(rental => rental.TotalAmount);
        var totalDamageCost = damages.Sum(damage => damage.RepairCost);
        var damageRatio = totalDamageCost / totalRevenue;

        totalRevenue.Should().BeInRange(1_500_000m, 2_000_000m);
        damageRatio.Should().BeInRange(0.05m, 0.10m);
        ((decimal)damages.Count / rentals.Count).Should().BeInRange(0.05m, 0.10m);

        var currentYear = today.Year;
        var maxCurrentYearContract = rentals
            .Where(rental => rental.StartDate.Year == currentYear)
            .Select(rental => int.Parse(rental.ContractNumber.Split('-')[2]))
            .DefaultIfEmpty(0)
            .Max();
        var currentYearSequence = await dbContext.ContractSequences
            .AsNoTracking()
            .SingleAsync(item => item.Year == currentYear);

        currentYearSequence.LastNumber.Should().Be(maxCurrentYearContract);
    }
}
