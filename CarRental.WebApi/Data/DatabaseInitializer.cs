using CarRental.WebApi.Infrastructure;
using CarRental.WebApi.Models;
using CarRental.WebApi.Services.Security;
using System.IO;

namespace CarRental.WebApi.Data;

public static class DatabaseInitializer
{
    public static SeedCredentials? Seed(RentalDbContext dbContext)
    {
        SeedCredentials? seededCredentials = null;

        if (!dbContext.Employees.Any())
        {
            var adminPassword = ResolvePassword("CAR_RENTAL_ADMIN_PASSWORD", "admin123");
            var managerPassword = ResolvePassword("CAR_RENTAL_MANAGER_PASSWORD", "manager123");

            dbContext.Employees.AddRange(
                new Employee
                {
                    FullName = "Системний адміністратор",
                    Login = "admin",
                    PasswordHash = PasswordHasher.HashPassword(adminPassword.Password),
                    Role = UserRole.Admin,
                    IsActive = true,
                    PasswordChangedAtUtc = DateTime.UtcNow
                },
                new Employee
                {
                    FullName = "Менеджер прокату",
                    Login = "manager",
                    PasswordHash = PasswordHasher.HashPassword(managerPassword.Password),
                    Role = UserRole.Manager,
                    IsActive = true,
                    PasswordChangedAtUtc = DateTime.UtcNow
                });

            seededCredentials = new SeedCredentials(
                "admin",
                adminPassword.Password,
                adminPassword.IsGenerated,
                "manager",
                managerPassword.Password,
                managerPassword.IsGenerated);
        }

        if (!dbContext.Clients.Any())
        {
            dbContext.Clients.AddRange(
                new Client
                {
                    FullName = "Ivan Petrenko",
                    PassportData = "KV123456",
                    DriverLicense = "AB123456",
                    Phone = "+380501112233",
                    Blacklisted = false
                },
                new Client
                {
                    FullName = "Olena Shevchenko",
                    PassportData = "MK654321",
                    DriverLicense = "CD654321",
                    Phone = "+380677778899",
                    Blacklisted = false
                });
        }

        if (!dbContext.Vehicles.Any())
        {
            var camry = new Vehicle
            {
                Make = "Toyota",
                Model = "Camry",
                LicensePlate = "AA1234TX",
                Mileage = 56000,
                DailyRate = 70m,
                IsAvailable = true,
                ServiceIntervalKm = 10000
            };
            var octavia = new Vehicle
            {
                Make = "Skoda",
                Model = "Octavia",
                LicensePlate = "KA5678BH",
                Mileage = 91000,
                DailyRate = 55m,
                IsAvailable = true,
                ServiceIntervalKm = 12000
            };
            var duster = new Vehicle
            {
                Make = "Renault",
                Model = "Duster",
                LicensePlate = "AX4411KK",
                Mileage = 43000,
                DailyRate = 62m,
                IsAvailable = true,
                ServiceIntervalKm = 9000
            };

            ApplyCatalogSeed(camry, VehicleCatalogSeeds.TryFindByVehicle(camry.Make, camry.Model));
            ApplyCatalogSeed(octavia, VehicleCatalogSeeds.TryFindByVehicle(octavia.Make, octavia.Model));
            ApplyCatalogSeed(duster, VehicleCatalogSeeds.TryFindByVehicle(duster.Make, duster.Model));

            dbContext.Vehicles.AddRange(camry, octavia, duster);
        }

        EnsureVehicleCatalogSync(dbContext);

        if (!dbContext.ContractSequences.Any(sequence => sequence.Year == DateTime.UtcNow.Year))
        {
            dbContext.ContractSequences.Add(new ContractSequence
            {
                Year = DateTime.UtcNow.Year,
                LastNumber = 0
            });
        }

        dbContext.SaveChanges();
        return seededCredentials;
    }

    private static void EnsureVehicleCatalogSync(RentalDbContext dbContext)
    {
        var vehicles = dbContext.Vehicles.ToList();
        var existingPlates = vehicles
            .Select(vehicle => vehicle.LicensePlate.ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);
        var existingCatalogKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var vehicle in vehicles)
        {
            var seed = VehicleCatalogSeeds.TryFindByVehicle(vehicle.Make, vehicle.Model);
            if (seed is null)
            {
                continue;
            }

            ApplyCatalogSeed(vehicle, seed);
            existingCatalogKeys.Add(seed.FullName);

            var resolvedPhoto = TryResolveCatalogPhotoPath(seed.Make, seed.Model);
            if (!string.IsNullOrWhiteSpace(resolvedPhoto))
            {
                vehicle.PhotoPath = resolvedPhoto;
            }
        }

        var addedCount = 0;
        foreach (var seed in VehicleCatalogSeeds.All.OrderBy(item => item.PopularityRank))
        {
            if (existingCatalogKeys.Contains(seed.FullName))
            {
                continue;
            }

            var plate = GenerateCatalogPlate(seed.PopularityRank, existingPlates);
            var vehicle = new Vehicle
            {
                Make = seed.Make,
                Model = seed.Model,
                LicensePlate = plate,
                Mileage = 12000 + (seed.PopularityRank * 850),
                DailyRate = seed.DailyRate,
                IsAvailable = true,
                ServiceIntervalKm = 10000,
                PhotoPath = TryResolveCatalogPhotoPath(seed.Make, seed.Model)
            };
            ApplyCatalogSeed(vehicle, seed);

            dbContext.Vehicles.Add(vehicle);
            vehicles.Add(vehicle);
            existingCatalogKeys.Add(seed.FullName);
            existingPlates.Add(plate);
            addedCount++;
        }

        if (addedCount > 0)
        {
            Console.WriteLine($"[Seed] Синхронізовано авто каталогу: додано {addedCount}.");
        }

        EnsureCatalogPhotoPaths(vehicles);
    }

    private static void EnsureCatalogPhotoPaths(IEnumerable<Vehicle> vehicles)
    {
        var updatedCount = 0;
        foreach (var vehicle in vehicles)
        {
            if (VehiclePhotoCatalog.TryResolveStoredPhotoPath(vehicle.PhotoPath, out _))
            {
                continue;
            }

            var seed = VehicleCatalogSeeds.TryFindByVehicle(vehicle.Make, vehicle.Model);
            var resolved = seed is null
                ? TryResolveCatalogPhotoPath(vehicle.Make, vehicle.Model)
                : TryResolveCatalogPhotoPath(seed.Make, seed.Model);
            if (string.IsNullOrWhiteSpace(resolved))
            {
                continue;
            }

            vehicle.PhotoPath = resolved;
            updatedCount++;
        }

        if (updatedCount > 0)
        {
            Console.WriteLine($"[Seed] Додано шляхи до фото каталогу: {updatedCount}.");
        }
    }

    private static string? TryResolveCatalogPhotoPath(string make, string model)
    {
        return VehiclePhotoCatalog.TryBuildCatalogPhotoPath(make, model);
    }

    private static (string Make, string Model) SplitMakeAndModel(string fullName)
    {
        var normalized = fullName.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return ("Unknown", "Model");
        }

        if (normalized.StartsWith("Land Rover ", StringComparison.OrdinalIgnoreCase))
        {
            return ("Land Rover", normalized["Land Rover ".Length..].Trim());
        }

        var parts = normalized.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            return (parts[0], "Model");
        }

        return (parts[0], parts[1]);
    }

    private static string BuildVehicleKey(string make, string model)
    {
        return NormalizeAlphaNumeric(make) + "|" + NormalizeAlphaNumeric(model);
    }

    private static string NormalizeAlphaNumeric(string value)
    {
        var normalized = value.ToLowerInvariant().Where(char.IsLetterOrDigit);
        return string.Concat(normalized);
    }

    private static string GenerateCatalogPlate(int index, ISet<string> existingPlates)
    {
        var candidateIndex = index;
        while (true)
        {
            var plate = $"CAT{candidateIndex:0000}UA";
            if (!existingPlates.Contains(plate))
            {
                return plate;
            }

            candidateIndex++;
        }
    }

    private static void ApplyCatalogSeed(Vehicle vehicle, VehicleCatalogSeeds.CatalogVehicleSeed? seed)
    {
        if (seed is null)
        {
            return;
        }

        vehicle.EngineDisplay = seed.EngineDisplay;
        vehicle.FuelType = seed.FuelType;
        vehicle.TransmissionType = seed.TransmissionType;
        vehicle.DoorsCount = seed.DoorsCount;
        vehicle.CargoCapacityDisplay = seed.CargoCapacityDisplay;
        vehicle.ConsumptionDisplay = seed.ConsumptionDisplay;
        vehicle.HasAirConditioning = seed.HasAirConditioning;
    }

    private static PasswordSeed ResolvePassword(string environmentVariable, string fallbackPassword)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariable);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return new PasswordSeed(value.Trim(), false);
        }

        return new PasswordSeed(fallbackPassword, false);
    }

    private sealed record PasswordSeed(string Password, bool IsGenerated);

    public sealed record SeedCredentials(
        string AdminLogin,
        string AdminPassword,
        bool AdminPasswordGenerated,
        string ManagerLogin,
        string ManagerPassword,
        bool ManagerPasswordGenerated);
}
