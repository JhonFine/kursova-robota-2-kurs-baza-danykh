using CarRental.WebApi.Data;
using CarRental.WebApi.Models;

namespace CarRental.WebApi.Tests;

internal static class TestLookupSeed
{
    public static void SeedVehicleLookups(RentalDbContext dbContext)
    {
        if (dbContext.FuelTypes.Any() || dbContext.TransmissionTypes.Any())
        {
            return;
        }

        var now = DateTime.UtcNow;
        dbContext.FuelTypes.AddRange(
            new FuelTypeLookup { Code = "Бензин", DisplayName = "Бензин", CreatedAtUtc = now, UpdatedAtUtc = now },
            new FuelTypeLookup { Code = "Дизель", DisplayName = "Дизель", CreatedAtUtc = now, UpdatedAtUtc = now },
            new FuelTypeLookup { Code = "Електро", DisplayName = "Електро", CreatedAtUtc = now, UpdatedAtUtc = now });

        dbContext.TransmissionTypes.AddRange(
            new TransmissionTypeLookup { Code = "Автомат", DisplayName = "Автомат", CreatedAtUtc = now, UpdatedAtUtc = now },
            new TransmissionTypeLookup { Code = "Механіка", DisplayName = "Механіка", CreatedAtUtc = now, UpdatedAtUtc = now });
    }
}
