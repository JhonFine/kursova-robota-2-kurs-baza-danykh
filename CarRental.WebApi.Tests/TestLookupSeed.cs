using CarRental.WebApi.Data;
using CarRental.WebApi.Models;

namespace CarRental.WebApi.Tests;

internal static class TestLookupSeed
{
    public static void SeedVehicleLookups(RentalDbContext dbContext)
    {
        var now = DateTime.UtcNow;

        if (!dbContext.FuelTypes.Any())
        {
            dbContext.FuelTypes.AddRange(
                new FuelTypeLookup { Code = "PETROL", DisplayName = "Petrol", CreatedAtUtc = now, UpdatedAtUtc = now },
                new FuelTypeLookup { Code = "DIESEL", DisplayName = "Diesel", CreatedAtUtc = now, UpdatedAtUtc = now },
                new FuelTypeLookup { Code = "EV", DisplayName = "Electric", CreatedAtUtc = now, UpdatedAtUtc = now });
        }

        if (!dbContext.TransmissionTypes.Any())
        {
            dbContext.TransmissionTypes.AddRange(
                new TransmissionTypeLookup { Code = "AUTO", DisplayName = "Automatic", CreatedAtUtc = now, UpdatedAtUtc = now },
                new TransmissionTypeLookup { Code = "MANUAL", DisplayName = "Manual", CreatedAtUtc = now, UpdatedAtUtc = now });
        }

        if (!dbContext.VehicleStatuses.Any())
        {
            dbContext.VehicleStatuses.AddRange(
                new VehicleStatusLookup { Code = "READY", DisplayName = "Ready", CreatedAtUtc = now, UpdatedAtUtc = now },
                new VehicleStatusLookup { Code = "INACTIVE", DisplayName = "Inactive", CreatedAtUtc = now, UpdatedAtUtc = now },
                new VehicleStatusLookup { Code = "MAINTENANCE", DisplayName = "Maintenance", CreatedAtUtc = now, UpdatedAtUtc = now });
        }

        dbContext.SaveChanges();
    }

    public static (int MakeId, int ModelId) EnsureVehicleModel(RentalDbContext dbContext, string makeName, string modelName)
    {
        SeedVehicleLookups(dbContext);

        var normalizedMake = NormalizeLookupName(makeName);
        var make = dbContext.VehicleMakes.FirstOrDefault(item => item.NormalizedName == normalizedMake);
        if (make is null)
        {
            make = new VehicleMake
            {
                Name = makeName.Trim(),
                NormalizedName = normalizedMake,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            dbContext.VehicleMakes.Add(make);
            dbContext.SaveChanges();
        }

        var normalizedModel = NormalizeLookupName(modelName);
        var model = dbContext.VehicleModels.FirstOrDefault(item => item.MakeId == make.Id && item.NormalizedName == normalizedModel);
        if (model is null)
        {
            model = new VehicleModel
            {
                MakeId = make.Id,
                Name = modelName.Trim(),
                NormalizedName = normalizedModel,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            dbContext.VehicleModels.Add(model);
            dbContext.SaveChanges();
        }

        return (make.Id, model.Id);
    }

    public static Vehicle CreateVehicle(
        RentalDbContext dbContext,
        string make,
        string model,
        string licensePlate,
        string fuelTypeCode,
        string transmissionTypeCode,
        decimal powertrainCapacityValue,
        decimal cargoCapacityValue,
        decimal consumptionValue,
        int mileage,
        decimal dailyRate,
        int? id = null)
    {
        var (makeId, modelId) = EnsureVehicleModel(dbContext, make, model);

        return new Vehicle
        {
            Id = id ?? default,
            MakeId = makeId,
            ModelId = modelId,
            FuelTypeCode = fuelTypeCode,
            TransmissionTypeCode = transmissionTypeCode,
            PowertrainCapacityValue = powertrainCapacityValue,
            PowertrainCapacityUnit = "L",
            CargoCapacityValue = cargoCapacityValue,
            CargoCapacityUnit = "L",
            ConsumptionValue = consumptionValue,
            ConsumptionUnit = "L_PER_100KM",
            LicensePlate = licensePlate,
            Mileage = mileage,
            DailyRate = dailyRate,
            IsBookable = true,
            ServiceIntervalKm = 10000
        };
    }

    private static string NormalizeLookupName(string value)
        => string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
}
