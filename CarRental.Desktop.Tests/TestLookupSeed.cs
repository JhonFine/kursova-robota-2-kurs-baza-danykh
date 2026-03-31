using CarRental.Desktop.Data;
using CarRental.Desktop.Models;

namespace CarRental.Desktop.Tests;

internal static class TestLookupSeed
{
    public static void SeedVehicleLookups(RentalDbContext dbContext)
    {
        if (!dbContext.FuelTypes.Any())
        {
            dbContext.FuelTypes.AddRange(
                new FuelTypeLookup { Code = "Бензин", DisplayName = "Бензин" },
                new FuelTypeLookup { Code = "Дизель", DisplayName = "Дизель" },
                new FuelTypeLookup { Code = "Електро", DisplayName = "Електро" });
        }

        if (!dbContext.TransmissionTypes.Any())
        {
            dbContext.TransmissionTypes.AddRange(
                new TransmissionTypeLookup { Code = "Автомат", DisplayName = "Автомат" },
                new TransmissionTypeLookup { Code = "Механіка", DisplayName = "Механіка" });
        }

        dbContext.SaveChanges();
    }
}
