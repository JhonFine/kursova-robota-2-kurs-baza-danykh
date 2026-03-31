using CarRental.Desktop.Infrastructure;
using CarRental.Desktop.Models;
using CarRental.Shared.ReferenceData;
using CarRental.Desktop.Services.Security;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Desktop.Data;

public static class DatabaseInitializer
{
    private static readonly int SeedRandomValue = DemoSeedReferenceData.SeedRandomValue;
    private static readonly int TotalClients = DemoSeedReferenceData.TotalClients;
    private static readonly int TotalClosedRentals = DemoSeedReferenceData.TotalClosedRentals;
    private static readonly int TotalActiveRentals = DemoSeedReferenceData.TotalActiveRentals;
    private static readonly int TotalCanceledRentals = DemoSeedReferenceData.TotalCanceledRentals;
    private static readonly int TotalDamages = DemoSeedReferenceData.TotalDamages;
    private static readonly string PlateAlphabet = DemoSeedReferenceData.PlateAlphabet;

    private static readonly (int Days, int Count)[] RentalDurationDistribution =
    [
        .. DemoSeedReferenceData.RentalDurationDistribution.Select(item => (item.Days, item.Count))
    ];

    private static readonly LocationSeed[] LocationSeeds =
    [
        .. DemoSeedReferenceData.LocationSeeds.Select(item => new LocationSeed(item.City, item.Weight, item.PrimaryPrefix, item.SecondaryPrefix))
    ];

    private static readonly string[] MaleFirstNames = [.. DemoSeedReferenceData.MaleFirstNames];
    private static readonly string[] FemaleFirstNames = [.. DemoSeedReferenceData.FemaleFirstNames];
    private static readonly string[] FamilyNames = [.. DemoSeedReferenceData.FamilyNames];
    private static readonly string[] PhonePrefixes = [.. DemoSeedReferenceData.PhonePrefixes];
    private static readonly string[] PassportSeries = [.. DemoSeedReferenceData.PassportSeries];
    private static readonly string[] DriverLicenseSeries = [.. DemoSeedReferenceData.DriverLicenseSeries];
    private static readonly string[] CancellationReasons = [.. DemoSeedReferenceData.CancellationReasons];
    private static readonly string[] DamageDescriptions = [.. DemoSeedReferenceData.DamageDescriptions];
    private static readonly string[] MaintenanceDescriptions = [.. DemoSeedReferenceData.MaintenanceDescriptions];
    private static readonly decimal[] DamageCostTemplate = [.. DemoSeedReferenceData.DamageCostTemplate];
    private static readonly IReadOnlySet<int> ChargedDamageIndices = DemoSeedReferenceData.ChargedDamageIndices;
    private static readonly IReadOnlyList<PlateLocation> WeightedPlateLocations = BuildPlateLocations();

    public static SeedCredentials? Seed(RentalDbContext dbContext)
    {
        var seededCredentials = EnsureEmployees(dbContext);
        EnsureVehicleLookups(dbContext);
        EnsureClients(dbContext);
        EnsureVehicleCatalogSync(dbContext);
        dbContext.SaveChanges();

        if (!dbContext.Rentals.Any() &&
            !dbContext.Payments.Any() &&
            !dbContext.Damages.Any() &&
            !dbContext.MaintenanceRecords.Any())
        {
            SeedOperationalHistory(dbContext);
            dbContext.SaveChanges();
        }

        EnsureContractSequences(dbContext);
        dbContext.SaveChanges();
        return seededCredentials;
    }

    private static SeedCredentials? EnsureEmployees(RentalDbContext dbContext)
    {
        if (dbContext.Employees.Any())
        {
            return null;
        }

        var adminPassword = ResolvePassword("CAR_RENTAL_ADMIN_PASSWORD", DemoSeedReferenceData.AdminFallbackPassword);
        var managerPassword = ResolvePassword("CAR_RENTAL_MANAGER_PASSWORD", DemoSeedReferenceData.ManagerFallbackPassword);
        var passwordChangedAt = DateTime.UtcNow.Date.AddHours(8);

        dbContext.Employees.AddRange(
            new Employee
            {
                FullName = "РЎРёСЃС‚РµРјРЅРёР№ Р°РґРјС–РЅС–СЃС‚СЂР°С‚РѕСЂ",
                Login = DemoSeedReferenceData.AdminLogin,
                PasswordHash = PasswordHasher.HashPassword(adminPassword.Password),
                Role = UserRole.Admin,
                IsActive = true,
                PasswordChangedAtUtc = passwordChangedAt
            },
            new Employee
            {
                FullName = "РњРµРЅРµРґР¶РµСЂ РїСЂРѕРєР°С‚Сѓ",
                Login = DemoSeedReferenceData.ManagerLogin,
                PasswordHash = PasswordHasher.HashPassword(managerPassword.Password),
                Role = UserRole.Manager,
                IsActive = true,
                PasswordChangedAtUtc = passwordChangedAt
            });

        foreach (var employee in dbContext.ChangeTracker.Entries<Employee>()
                     .Where(entry => entry.State == EntityState.Added && entry.Entity.Account is null)
                     .Select(entry => entry.Entity))
        {
            if (employee.Role == UserRole.Admin)
            {
                employee.Account = CreateSeedAccount(DemoSeedReferenceData.AdminLogin, adminPassword.Password, passwordChangedAt);
            }
            else if (employee.Role == UserRole.Manager)
            {
                employee.Account = CreateSeedAccount(DemoSeedReferenceData.ManagerLogin, managerPassword.Password, passwordChangedAt);
            }
        }

        return new SeedCredentials(
            DemoSeedReferenceData.AdminLogin,
            adminPassword.Password,
            adminPassword.IsGenerated,
            DemoSeedReferenceData.ManagerLogin,
            managerPassword.Password,
            managerPassword.IsGenerated);
    }

    private static void EnsureClients(RentalDbContext dbContext)
    {
        var existingDriverLicenses = dbContext.ClientDocuments
            .Where(document => !document.IsDeleted && document.DocumentTypeCode == ClientDocumentTypes.DriverLicense)
            .Select(document => document.DocumentNumber)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingPhones = dbContext.Clients
            .Select(client => client.Phone)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingPassportData = dbContext.ClientDocuments
            .Where(document => !document.IsDeleted && document.DocumentTypeCode == ClientDocumentTypes.Passport)
            .Select(document => document.DocumentNumber)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var currentCount = dbContext.Clients.Count();
        if (currentCount >= TotalClients)
        {
            return;
        }

        foreach (var client in BuildClientSeeds().Skip(currentCount).Take(TotalClients - currentCount))
        {
            if (existingDriverLicenses.Contains(client.DriverLicense) ||
                existingPhones.Contains(client.Phone) ||
                existingPassportData.Contains(client.PassportData))
            {
                continue;
            }

            dbContext.Clients.Add(client);
            existingDriverLicenses.Add(client.DriverLicense);
            existingPhones.Add(client.Phone);
            existingPassportData.Add(client.PassportData);
        }
    }

    private static IEnumerable<Client> BuildClientSeeds()
    {
        var generated = 0;
        var blacklistedIndexes = DemoSeedReferenceData.BlacklistedClientIndexes;

        foreach (var familyName in FamilyNames)
        {
            foreach (var firstName in MaleFirstNames)
            {
                yield return CreateClientSeed(generated, $"{firstName} {familyName}", blacklistedIndexes.Contains(generated));
                generated++;
                if (generated == TotalClients / 2)
                {
                    goto buildFemaleClients;
                }
            }
        }

    buildFemaleClients:
        foreach (var familyName in FamilyNames)
        {
            foreach (var firstName in FemaleFirstNames)
            {
                yield return CreateClientSeed(generated, $"{firstName} {familyName}", blacklistedIndexes.Contains(generated));
                generated++;
                if (generated == TotalClients)
                {
                    yield break;
                }
            }
        }
    }

    private static Client CreateClientSeed(int index, string fullName, bool blacklisted)
    {
        var passportSeries = PassportSeries[index % PassportSeries.Length];
        var driverSeries = DriverLicenseSeries[index % DriverLicenseSeries.Length];
        var phonePrefix = PhonePrefixes[index % PhonePrefixes.Length];
        var phoneSuffix = 1_000_000 + (index * 37);
        var issuedAt = DateTime.UtcNow.Date.AddYears(-6).AddDays(index % 90);

        return new Client
        {
            FullName = fullName,
            PassportData = $"{passportSeries}{200000 + index:000000}",
            PassportExpirationDate = issuedAt.AddYears(10),
            DriverLicense = $"{driverSeries}{500000 + index:000000}",
            DriverLicenseExpirationDate = issuedAt.AddYears(5),
            Phone = $"+380{phonePrefix}{phoneSuffix:0000000}",
            Blacklisted = blacklisted
        };
    }

    private static void EnsureVehicleLookups(RentalDbContext dbContext)
    {
        var existingFuelCodes = dbContext.FuelTypes
            .Select(item => item.Code)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var fuelType in VehicleCatalogSeeds.All
                     .Select(item => item.FuelType)
                     .Where(item => !string.IsNullOrWhiteSpace(item))
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(item => item, StringComparer.Ordinal))
        {
            if (existingFuelCodes.Contains(fuelType))
            {
                continue;
            }

            dbContext.FuelTypes.Add(new FuelTypeLookup
            {
                Code = fuelType,
                DisplayName = fuelType
            });
            existingFuelCodes.Add(fuelType);
        }

        var existingTransmissionCodes = dbContext.TransmissionTypes
            .Select(item => item.Code)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var transmissionType in VehicleCatalogSeeds.All
                     .Select(item => item.TransmissionType)
                     .Where(item => !string.IsNullOrWhiteSpace(item))
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(item => item, StringComparer.Ordinal))
        {
            if (existingTransmissionCodes.Contains(transmissionType))
            {
                continue;
            }

            dbContext.TransmissionTypes.Add(new TransmissionTypeLookup
            {
                Code = transmissionType,
                DisplayName = transmissionType
            });
            existingTransmissionCodes.Add(transmissionType);
        }
    }

    private static void EnsureVehicleCatalogSync(RentalDbContext dbContext)
    {
        var hasRentalHistory = dbContext.Rentals.Any();
        var vehicles = dbContext.Vehicles.ToList();
        var existingPlates = vehicles
            .Select(vehicle => VehicleDomainRules.NormalizeLicensePlate(vehicle.LicensePlate))
            .Where(plate => !string.IsNullOrWhiteSpace(plate))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        var existingCatalogKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var vehicle in vehicles)
        {
            var seed = VehicleCatalogSeeds.TryFindByVehicle(vehicle.Make, vehicle.Model);
            if (seed is null)
            {
                continue;
            }

            existingCatalogKeys.Add(seed.FullName);
            ApplyCatalogSeed(vehicle, seed);
            vehicle.DailyRate = seed.DailyRate;
            vehicle.ServiceIntervalKm = ResolveServiceInterval(seed);
            vehicle.Mileage = Math.Max(vehicle.Mileage, CalculateInitialMileage(seed));

            if (!hasRentalHistory && !VehicleDomainRules.IsValidLicensePlate(vehicle.LicensePlate))
            {
                vehicle.LicensePlate = GenerateCatalogPlate(seed.PopularityRank, existingPlates);
                existingPlates.Add(vehicle.LicensePlate);
            }

            var resolvedPhoto = TryResolveCatalogPhotoPath(seed.Make, seed.Model);
            if (!string.IsNullOrWhiteSpace(resolvedPhoto))
            {
                vehicle.PhotoPath = resolvedPhoto;
            }
        }

        foreach (var seed in VehicleCatalogSeeds.All.OrderBy(item => item.PopularityRank))
        {
            if (existingCatalogKeys.Contains(seed.FullName))
            {
                continue;
            }

            var plate = GenerateCatalogPlate(seed.PopularityRank, existingPlates);
            existingPlates.Add(plate);

            var vehicle = new Vehicle
            {
                Make = seed.Make,
                Model = seed.Model,
                LicensePlate = plate,
                Mileage = CalculateInitialMileage(seed),
                DailyRate = seed.DailyRate,
                IsBookable = true,
                ServiceIntervalKm = ResolveServiceInterval(seed),
                PhotoPath = TryResolveCatalogPhotoPath(seed.Make, seed.Model)
            };
            ApplyCatalogSeed(vehicle, seed);

            dbContext.Vehicles.Add(vehicle);
            vehicles.Add(vehicle);
            existingCatalogKeys.Add(seed.FullName);
        }

        EnsureCatalogPhotoPaths(vehicles);
    }

    private static void SeedOperationalHistory(RentalDbContext dbContext)
    {
        var random = new Random(SeedRandomValue);
        var today = DateTime.UtcNow.Date;
        var periodStart = today.AddDays(-365);

        var employees = dbContext.Employees
            .OrderBy(employee => employee.Role)
            .ThenBy(employee => employee.Id)
            .ToList();
        var manager = employees.FirstOrDefault(employee => employee.Role == UserRole.Manager) ?? employees.First();
        var admin = employees.First();

        var clients = dbContext.Clients
            .Include(client => client.Documents)
            .Where(client => !client.IsBlacklisted)
            .OrderBy(client => client.Id)
            .ToList();
        var vehicleStates = dbContext.Vehicles
            .ToList()
            .Select(vehicle =>
            {
                var seed = VehicleCatalogSeeds.TryFindByVehicle(vehicle.Make, vehicle.Model);
                return seed is null ? null : CreateVehicleSeedState(vehicle, seed);
            })
            .Where(state => state is not null)
            .Cast<VehicleSeedState>()
            .OrderBy(state => state.CatalogSeed.PopularityRank)
            .ToList();

        if (clients.Count == 0 || vehicleStates.Count == 0)
        {
            return;
        }

        foreach (var vehicleState in vehicleStates)
        {
            vehicleState.Vehicle.Mileage = vehicleState.InitialMileage;
            vehicleState.Vehicle.IsBookable = true;
        }

        var durations = BuildRentalDurations(random);
        var scheduledRentals = new List<ScheduledRental>(durations.Count);

        for (var index = 0; index < TotalClosedRentals; index++)
        {
            scheduledRentals.Add(ScheduleRental(random, vehicleStates, clients, manager, admin, periodStart, today, durations[index], RentalStatus.Closed, index, false));
        }

        for (var index = 0; index < TotalActiveRentals; index++)
        {
            scheduledRentals.Add(ScheduleRental(random, vehicleStates, clients, manager, admin, periodStart, today, durations[TotalClosedRentals + index], RentalStatus.Active, TotalClosedRentals + index, true));
        }

        for (var index = 0; index < TotalCanceledRentals; index++)
        {
            scheduledRentals.Add(ScheduleRental(random, vehicleStates, clients, manager, admin, periodStart, today, durations[TotalClosedRentals + TotalActiveRentals + index], RentalStatus.Canceled, TotalClosedRentals + TotalActiveRentals + index, false));
        }

        AssignContractNumbers(scheduledRentals);
        PopulateMileageAndPaymentPlans(scheduledRentals, vehicleStates, random, today);
        AttachDamages(scheduledRentals, random);

        var rentals = new List<Rental>(scheduledRentals.Count);
        var payments = new List<Payment>();
        var damages = new List<Damage>();
        var inspections = new List<RentalInspection>();
        var maintenanceRecords = vehicleStates.SelectMany(state => state.MaintenanceRecords).ToList();

        foreach (var scheduledRental in scheduledRentals.OrderBy(item => item.StartDate).ThenBy(item => item.ContractNumber, StringComparer.Ordinal))
        {
            var rental = new Rental
            {
                Client = scheduledRental.Client,
                Vehicle = scheduledRental.VehicleState.Vehicle,
                Employee = scheduledRental.Employee,
                ContractNumber = scheduledRental.ContractNumber,
                StartDate = scheduledRental.StartDate,
                EndDate = scheduledRental.EndDate,
                PickupLocation = scheduledRental.PickupLocation,
                ReturnLocation = scheduledRental.ReturnLocation,
                StartMileage = scheduledRental.StartMileage,
                EndMileage = scheduledRental.EndMileage,
                OverageFee = scheduledRental.OverageFee,
                TotalAmount = scheduledRental.TotalAmount,
                Status = scheduledRental.Status,
                CreatedAtUtc = scheduledRental.CreatedAtUtc,
                ClosedAtUtc = scheduledRental.ClosedAtUtc,
                CanceledAtUtc = scheduledRental.CanceledAtUtc,
                CancellationReason = scheduledRental.CancellationReason
            };
            rentals.Add(rental);

            if (scheduledRental.PickupInspectionCompletedAtUtc.HasValue)
            {
                inspections.Add(new RentalInspection
                {
                    Rental = rental,
                    Type = RentalInspectionType.Pickup,
                    CompletedAtUtc = scheduledRental.PickupInspectionCompletedAtUtc.Value,
                    FuelPercent = scheduledRental.PickupFuelPercent,
                    Notes = scheduledRental.PickupInspectionNotes,
                    CreatedAtUtc = scheduledRental.PickupInspectionCompletedAtUtc.Value,
                    UpdatedAtUtc = scheduledRental.PickupInspectionCompletedAtUtc.Value
                });
            }

            if (scheduledRental.ReturnInspectionCompletedAtUtc.HasValue)
            {
                inspections.Add(new RentalInspection
                {
                    Rental = rental,
                    Type = RentalInspectionType.Return,
                    CompletedAtUtc = scheduledRental.ReturnInspectionCompletedAtUtc.Value,
                    FuelPercent = scheduledRental.ReturnFuelPercent,
                    Notes = scheduledRental.ReturnInspectionNotes,
                    CreatedAtUtc = scheduledRental.ReturnInspectionCompletedAtUtc.Value,
                    UpdatedAtUtc = scheduledRental.ReturnInspectionCompletedAtUtc.Value
                });
            }

            foreach (var paymentPlan in scheduledRental.Payments)
            {
                payments.Add(new Payment
                {
                    Rental = rental,
                    EmployeeId = paymentPlan.EmployeeId,
                    Amount = paymentPlan.Amount,
                    Method = paymentPlan.Method,
                    Direction = paymentPlan.Direction,
                    CreatedAtUtc = paymentPlan.CreatedAtUtc,
                    Notes = paymentPlan.Notes
                });
            }

            foreach (var damagePlan in scheduledRental.Damages)
            {
                damages.Add(new Damage
                {
                    Rental = rental,
                    VehicleId = scheduledRental.VehicleState.Vehicle.Id,
                    Description = damagePlan.Description,
                    DateReported = damagePlan.DateReported,
                    RepairCost = damagePlan.RepairCost,
                    ActNumber = damagePlan.ActNumber,
                    ChargedAmount = damagePlan.ChargedAmount,
                    Status = damagePlan.Status
                });
            }
        }

        dbContext.Rentals.AddRange(rentals);
        dbContext.RentalInspections.AddRange(inspections);
        dbContext.Payments.AddRange(payments);
        dbContext.Damages.AddRange(damages);
        dbContext.MaintenanceRecords.AddRange(maintenanceRecords);

        foreach (var vehicleState in vehicleStates)
        {
            vehicleState.Vehicle.Mileage = vehicleState.CurrentMileage;
            vehicleState.Vehicle.IsBookable = !vehicleState.Vehicle.IsDeleted;
        }
    }

    private static VehicleSeedState CreateVehicleSeedState(Vehicle vehicle, VehicleCatalogSeeds.CatalogVehicleSeed seed)
    {
        var homeLocation = ResolvePlateLocation(seed.PopularityRank);
        var initialMileage = CalculateInitialMileage(seed);
        var serviceInterval = ResolveServiceInterval(seed);
        return new VehicleSeedState(
            vehicle,
            seed,
            homeLocation.City,
            initialMileage,
            initialMileage,
            ResolveNextServiceMileage(initialMileage, serviceInterval),
            serviceInterval);
    }

    private static List<int> BuildRentalDurations(Random random)
    {
        var durations = new List<int>(TotalClosedRentals + TotalActiveRentals + TotalCanceledRentals);
        foreach (var (days, count) in RentalDurationDistribution)
        {
            for (var index = 0; index < count; index++)
            {
                durations.Add(days);
            }
        }

        for (var index = durations.Count - 1; index > 0; index--)
        {
            var swapIndex = random.Next(index + 1);
            (durations[index], durations[swapIndex]) = (durations[swapIndex], durations[index]);
        }

        return durations;
    }

    private static ScheduledRental ScheduleRental(
        Random random,
        IReadOnlyList<VehicleSeedState> vehicleStates,
        IReadOnlyList<Client> clients,
        Employee manager,
        Employee admin,
        DateTime periodStart,
        DateTime today,
        int durationDays,
        RentalStatus status,
        int sequenceIndex,
        bool requireDistinctVehicle)
    {
        for (var attempt = 0; attempt < 5000; attempt++)
        {
            var vehicleState = PickVehicleState(random, vehicleStates, requireDistinctVehicle);
            var (startDate, endDate) = ResolveRentalPeriod(random, periodStart, today, durationDays, status);

            if (status != RentalStatus.Canceled && HasOverlap(vehicleState.OccupiedIntervals, startDate, endDate))
            {
                continue;
            }

            var client = clients[(sequenceIndex * 7 + attempt + random.Next(clients.Count)) % clients.Count];
            var employee = sequenceIndex % 5 == 0 ? admin : manager;
            var pickupLocation = ResolvePickupLocation(random, vehicleState.HomeCity);
            var returnLocation = random.Next(100) < 85 ? pickupLocation : ResolveDifferentLocation(random, pickupLocation);
            var createdAtUtc = ResolveCreatedAt(random, periodStart, startDate, status);

            var scheduledRental = new ScheduledRental
            {
                VehicleState = vehicleState,
                Client = client,
                Employee = employee,
                Status = status,
                DurationDays = durationDays,
                StartDate = startDate,
                EndDate = endDate,
                PickupLocation = pickupLocation,
                ReturnLocation = returnLocation,
                CreatedAtUtc = createdAtUtc,
                IsOneWay = !string.Equals(pickupLocation, returnLocation, StringComparison.Ordinal)
            };

            if (status == RentalStatus.Canceled)
            {
                scheduledRental.CanceledAtUtc = ResolveCanceledAt(random, createdAtUtc, startDate);
                scheduledRental.CancellationReason = CancellationReasons[sequenceIndex % CancellationReasons.Length];
            }
            else
            {
                vehicleState.OccupiedIntervals.Add(new DateInterval(startDate, endDate));
                if (status == RentalStatus.Active)
                {
                    vehicleState.HasActiveRental = true;
                }
            }

            return scheduledRental;
        }

        throw new InvalidOperationException($"Failed to schedule a rental with status {status} and duration {durationDays}.");
    }

    private static VehicleSeedState PickVehicleState(Random random, IReadOnlyList<VehicleSeedState> vehicleStates, bool requireDistinctVehicle)
    {
        var candidates = requireDistinctVehicle
            ? vehicleStates.Where(state => !state.HasActiveRental).ToList()
            : vehicleStates.ToList();

        var totalWeight = 0d;
        var weights = new double[candidates.Count];
        for (var index = 0; index < candidates.Count; index++)
        {
            var state = candidates[index];
            var popularityWeight = Math.Max(10, 80 - state.CatalogSeed.PopularityRank);
            var rateWeight = (double)(VehicleDomainRules.MaxDailyRate - state.CatalogSeed.DailyRate + 300m) / 50d;
            var premiumPenalty = state.CatalogSeed.DailyRate >= 3000m ? 0.7d : 1d;
            var weight = (popularityWeight + rateWeight) * premiumPenalty;
            weights[index] = weight;
            totalWeight += weight;
        }

        var threshold = random.NextDouble() * totalWeight;
        var cumulative = 0d;
        for (var index = 0; index < candidates.Count; index++)
        {
            cumulative += weights[index];
            if (threshold <= cumulative)
            {
                return candidates[index];
            }
        }

        return candidates[^1];
    }

    private static (DateTime StartDate, DateTime EndDate) ResolveRentalPeriod(
        Random random,
        DateTime periodStart,
        DateTime today,
        int durationDays,
        RentalStatus status)
    {
        return status switch
        {
            RentalStatus.Active => (today.AddDays(-durationDays).AddHours(10), today.AddHours(10)),
            RentalStatus.Canceled => BuildRandomPastPeriod(random, periodStart, today.AddDays(-durationDays), durationDays),
            _ => BuildRandomPastPeriod(random, periodStart, today.AddDays(-(durationDays + 1)), durationDays)
        };
    }

    private static (DateTime StartDate, DateTime EndDate) BuildRandomPastPeriod(Random random, DateTime periodStart, DateTime latestStartDate, int durationDays)
    {
        var latestDate = latestStartDate.Date;
        var earliestDate = periodStart.Date;
        var availableDays = Math.Max(0, (latestDate - earliestDate).Days);
        var dayOffset = availableDays == 0 ? 0 : random.Next(availableDays + 1);
        var startDate = earliestDate.AddDays(dayOffset).AddHours(10);
        return (startDate, startDate.AddDays(durationDays));
    }

    private static DateTime ResolveCreatedAt(Random random, DateTime periodStart, DateTime startDate, RentalStatus status)
    {
        var minCreatedAt = periodStart.AddHours(8);
        var maxLeadDays = status == RentalStatus.Active ? 14 : 28;
        var createdAt = startDate.AddDays(-random.Next(1, maxLeadDays + 1)).AddHours(-random.Next(1, 9));
        return createdAt < minCreatedAt ? minCreatedAt : createdAt;
    }

    private static DateTime ResolveCanceledAt(Random random, DateTime createdAtUtc, DateTime startDate)
    {
        var latestCanceledAt = startDate.AddHours(-3);
        var canceledAt = createdAtUtc.AddHours(random.Next(12, 120));
        return canceledAt > latestCanceledAt ? latestCanceledAt : canceledAt;
    }

    private static bool HasOverlap(IEnumerable<DateInterval> intervals, DateTime startDate, DateTime endDate)
    {
        return intervals.Any(interval => interval.StartDate < endDate && startDate < interval.EndDate);
    }

    private static string ResolvePickupLocation(Random random, string homeCity)
    {
        return random.Next(100) < 70 ? homeCity : ResolveWeightedLocation(random);
    }

    private static string ResolveDifferentLocation(Random random, string excludedCity)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var city = ResolveWeightedLocation(random);
            if (!string.Equals(city, excludedCity, StringComparison.Ordinal))
            {
                return city;
            }
        }

        return LocationSeeds.First(seed => !string.Equals(seed.City, excludedCity, StringComparison.Ordinal)).City;
    }

    private static string ResolveWeightedLocation(Random random)
    {
        var threshold = random.Next(100);
        var cumulative = 0;
        foreach (var location in LocationSeeds)
        {
            cumulative += location.Weight;
            if (threshold < cumulative)
            {
                return location.City;
            }
        }

        return LocationSeeds[^1].City;
    }

    private static void AssignContractNumbers(IReadOnlyList<ScheduledRental> scheduledRentals)
    {
        var counters = new Dictionary<int, int>();
        foreach (var rental in scheduledRentals.OrderBy(item => item.StartDate).ThenBy(item => item.CreatedAtUtc).ThenBy(item => item.VehicleState.CatalogSeed.PopularityRank))
        {
            var year = rental.StartDate.Year;
            counters.TryGetValue(year, out var current);
            current++;
            counters[year] = current;
            rental.ContractNumber = $"CR-{year}-{current:000000}";
        }
    }

    private static void PopulateMileageAndPaymentPlans(
        IReadOnlyList<ScheduledRental> scheduledRentals,
        IReadOnlyList<VehicleSeedState> vehicleStates,
        Random random,
        DateTime today)
    {
        foreach (var group in scheduledRentals.GroupBy(rental => rental.VehicleState.Vehicle.Id))
        {
            var vehicleState = vehicleStates.First(item => item.Vehicle.Id == group.Key);
            foreach (var rental in group.OrderBy(item => item.StartDate).ThenBy(item => item.CreatedAtUtc).ThenBy(item => item.ContractNumber, StringComparer.Ordinal))
            {
                rental.StartMileage = vehicleState.CurrentMileage;
                rental.BaseAmount = rental.VehicleState.Vehicle.DailyRate * rental.DurationDays;

                if (rental.Status == RentalStatus.Canceled)
                {
                    rental.EndMileage = null;
                    rental.OverageFee = 0m;
                    rental.TotalAmount = 0m;
                    PlanCanceledPayments(rental);
                    continue;
                }

                rental.PickupInspectionCompletedAtUtc = rental.StartDate.AddMinutes(25);
                rental.PickupFuelPercent = 75 + random.Next(21);
                rental.PickupInspectionNotes = "РђРІС‚Рѕ С‡РёСЃС‚Рµ, РґРѕРєСѓРјРµРЅС‚Рё С‚Р° РєР»СЋС‡С– РїРµСЂРµРґР°РЅС– РєР»С–С”РЅС‚Сѓ.";

                var distance = EstimateDistance(random, rental.VehicleState.CatalogSeed, rental.DurationDays, rental.IsOneWay);

                if (rental.Status == RentalStatus.Closed)
                {
                    rental.EndMileage = rental.StartMileage + distance;
                    rental.OverageFee = ResolveOverageFee(random, rental.DurationDays);
                    rental.TotalAmount = rental.BaseAmount + rental.OverageFee;
                    rental.ClosedAtUtc = rental.EndDate.AddHours(2);
                    rental.ReturnInspectionCompletedAtUtc = rental.EndDate.AddMinutes(35);
                    rental.ReturnFuelPercent = Math.Max(30, rental.PickupFuelPercent.Value - random.Next(8, 36));
                    rental.ReturnInspectionNotes = "РџСЂРѕР±С–Рі Р·РІС–СЂРµРЅРѕ, РІС–Р·СѓР°Р»СЊРЅРёР№ РѕРіР»СЏРґ РІРёРєРѕРЅР°РЅРѕ Р±РµР· РєСЂРёС‚РёС‡РЅРёС… Р·Р°СѓРІР°Р¶РµРЅСЊ.";
                    PlanClosedPayments(rental, random);

                    vehicleState.CurrentMileage = rental.EndMileage.Value;
                    MaybeCreateMaintenanceRecord(vehicleState, rental, random, today);
                }
                else
                {
                    rental.EndMileage = null;
                    rental.OverageFee = 0m;
                    rental.TotalAmount = rental.BaseAmount;
                    PlanActivePayments(rental, random);
                }
            }
        }
    }

    private static int EstimateDistance(Random random, VehicleCatalogSeeds.CatalogVehicleSeed seed, int durationDays, bool oneWay)
    {
        var perDayBase = ResolveBaseDailyMileage(seed);
        var distance = durationDays * (perDayBase + random.Next(-15, 36));
        if (oneWay)
        {
            distance += random.Next(80, 181);
        }

        return Math.Max(durationDays * 80, distance);
    }

    private static int ResolveBaseDailyMileage(VehicleCatalogSeeds.CatalogVehicleSeed seed)
    {
        var normalizedModel = seed.Model.ToLowerInvariant();
        if (seed.FuelType.Contains("Р•Р»РµРєС‚СЂРѕ", StringComparison.OrdinalIgnoreCase))
        {
            return 95;
        }

        if (normalizedModel.Contains("transit", StringComparison.Ordinal) ||
            normalizedModel.Contains("sprinter", StringComparison.Ordinal) ||
            normalizedModel.Contains("vivaro", StringComparison.Ordinal))
        {
            return 140;
        }

        if (normalizedModel.Contains("trafi", StringComparison.Ordinal) ||
            normalizedModel.Contains("multivan", StringComparison.Ordinal) ||
            normalizedModel.Contains("kangoo", StringComparison.Ordinal))
        {
            return 125;
        }

        if (normalizedModel.Contains("prado", StringComparison.Ordinal) ||
            normalizedModel.Contains("x5", StringComparison.Ordinal) ||
            normalizedModel.Contains("q7", StringComparison.Ordinal) ||
            normalizedModel.Contains("gle", StringComparison.Ordinal) ||
            normalizedModel.Contains("cayenne", StringComparison.Ordinal) ||
            normalizedModel.Contains("wrangler", StringComparison.Ordinal) ||
            normalizedModel.Contains("discovery", StringComparison.Ordinal))
        {
            return 130;
        }

        return 105;
    }

    private static decimal ResolveOverageFee(Random random, int durationDays)
    {
        if (durationDays >= 6 || random.Next(100) >= 12)
        {
            return 0m;
        }

        var feeOptions = new[] { 200m, 400m, 600m, 800m };
        return feeOptions[random.Next(feeOptions.Length)];
    }

    private static void PlanClosedPayments(ScheduledRental rental, Random random)
    {
        rental.Payments.Clear();
        var useSplitPayment = rental.TotalAmount >= 5000m && random.Next(100) < 65;
        if (!useSplitPayment)
        {
            rental.Payments.Add(new ScheduledPayment(rental.Employee.Id, rental.TotalAmount, PaymentMethod.Card, PaymentDirection.Incoming, rental.CreatedAtUtc.AddHours(2), "РћРїР»Р°С‚Р° РѕСЂРµРЅРґРё"));
            return;
        }

        var deposit = RoundToNearest50(rental.TotalAmount * 0.4m);
        if (deposit <= 0m || deposit >= rental.TotalAmount)
        {
            deposit = RoundToNearest50(rental.TotalAmount / 2m);
        }

        rental.Payments.Add(new ScheduledPayment(rental.Employee.Id, deposit, PaymentMethod.Card, PaymentDirection.Incoming, rental.CreatedAtUtc.AddHours(2), "РџРµСЂРµРґРїР»Р°С‚Р° Р·Р° РѕСЂРµРЅРґСѓ"));
        rental.Payments.Add(new ScheduledPayment(rental.Employee.Id, rental.TotalAmount - deposit, random.Next(100) < 75 ? PaymentMethod.Card : PaymentMethod.Cash, PaymentDirection.Incoming, rental.EndDate.AddHours(1), "Р¤С–РЅР°Р»СЊРЅРёР№ СЂРѕР·СЂР°С…СѓРЅРѕРє Р·Р° РѕСЂРµРЅРґСѓ"));
    }

    private static void PlanActivePayments(ScheduledRental rental, Random random)
    {
        rental.Payments.Clear();
        var prepaidShare = 0.35m + (decimal)random.Next(0, 21) / 100m;
        var prepaidAmount = RoundToNearest50(rental.TotalAmount * prepaidShare);
        prepaidAmount = Math.Clamp(prepaidAmount, 500m, rental.TotalAmount);

        rental.Payments.Add(new ScheduledPayment(rental.Employee.Id, prepaidAmount, PaymentMethod.Card, PaymentDirection.Incoming, rental.CreatedAtUtc.AddHours(3), "РџРµСЂРµРґРїР»Р°С‚Р° Р·Р° Р°РєС‚РёРІРЅСѓ РѕСЂРµРЅРґСѓ"));
    }

    private static void PlanCanceledPayments(ScheduledRental rental)
    {
        rental.Payments.Clear();
        var reservedAmount = RoundToNearest50(Math.Max(rental.BaseAmount * 0.25m, 500m));
        rental.Payments.Add(new ScheduledPayment(rental.Employee.Id, reservedAmount, PaymentMethod.Card, PaymentDirection.Incoming, rental.CreatedAtUtc.AddHours(2), "РћРїР»Р°С‚Р° Р±СЂРѕРЅСЋРІР°РЅРЅСЏ"));
        rental.Payments.Add(new ScheduledPayment(rental.Employee.Id, reservedAmount, PaymentMethod.Card, PaymentDirection.Refund, rental.CanceledAtUtc ?? rental.StartDate.AddHours(-2), "РџРѕРІРµСЂРЅРµРЅРЅСЏ РѕРїР»Р°С‚Рё Р·Р° СЃРєР°СЃРѕРІР°РЅРµ Р±СЂРѕРЅСЋРІР°РЅРЅСЏ"));
    }

    private static void MaybeCreateMaintenanceRecord(VehicleSeedState vehicleState, ScheduledRental rental, Random random, DateTime today)
    {
        if (!rental.EndMileage.HasValue)
        {
            return;
        }

        while (vehicleState.MaintenanceRecords.Count < 2 && rental.EndMileage.Value >= vehicleState.NextServiceMileage)
        {
            var serviceDate = rental.EndDate.AddDays(1).AddHours(11 + vehicleState.MaintenanceRecords.Count);
            if (serviceDate > today.AddHours(18))
            {
                return;
            }

            var description = MaintenanceDescriptions[(vehicleState.MaintenanceRecords.Count + vehicleState.CatalogSeed.PopularityRank) % MaintenanceDescriptions.Length];
            var cost = vehicleState.MaintenanceRecords.Count == 0
                ? RoundToNearest50(3200m + random.Next(0, 9) * 250m)
                : RoundToNearest50(5200m + random.Next(0, 11) * 300m);

            vehicleState.MaintenanceRecords.Add(new MaintenanceRecord
            {
                Vehicle = vehicleState.Vehicle,
                ServiceDate = serviceDate,
                MileageAtService = vehicleState.NextServiceMileage,
                Description = description,
                Cost = cost,
                NextServiceMileage = vehicleState.NextServiceMileage + vehicleState.ServiceIntervalKm
            });

            vehicleState.NextServiceMileage += vehicleState.ServiceIntervalKm;
        }
    }

    private static void AttachDamages(IReadOnlyList<ScheduledRental> scheduledRentals, Random random)
    {
        var closedRentals = scheduledRentals
            .Where(rental => rental.Status == RentalStatus.Closed)
            .OrderByDescending(rental => rental.DurationDays)
            .ThenBy(rental => rental.StartDate)
            .ToList();

        var candidateRentals = closedRentals.OrderBy(_ => random.Next()).Take(TotalDamages).ToList();
        var chargedIndexes = ChargedDamageIndices.ToHashSet();

        for (var index = 0; index < candidateRentals.Count; index++)
        {
            var rental = candidateRentals[index];
            var repairCost = DamageCostTemplate[index];
            var isChargedToClient = chargedIndexes.Contains(index);
            var chargedAmount = isChargedToClient ? repairCost : 0m;
            var dateReported = rental.EndDate.AddHours(4 + (index % 7));

            rental.Damages.Add(new ScheduledDamage(
                DamageDescriptions[index % DamageDescriptions.Length],
                dateReported,
                repairCost,
                chargedAmount,
                isChargedToClient,
                isChargedToClient ? DamageStatus.Charged : DamageStatus.Resolved,
                $"ACT-{dateReported.Year}-{index + 1:0000}"));

            if (!isChargedToClient)
            {
                continue;
            }

            rental.TotalAmount += repairCost;
            rental.Payments.Add(new ScheduledPayment(rental.Employee.Id, repairCost, PaymentMethod.Card, PaymentDirection.Incoming, rental.ClosedAtUtc?.AddHours(6) ?? rental.EndDate.AddHours(6), "РљРѕРјРїРµРЅСЃР°С†С–СЏ Р·Р° РїРѕС€РєРѕРґР¶РµРЅРЅСЏ"));
        }
    }

    private static void EnsureContractSequences(RentalDbContext dbContext)
    {
        var rentalsByYear = dbContext.Rentals
            .AsNoTracking()
            .Select(rental => rental.ContractNumber)
            .AsEnumerable()
            .Select(ParseContractSequence)
            .Where(item => item is not null)
            .Cast<(int Year, int Number)>()
            .GroupBy(item => item.Year)
            .ToDictionary(group => group.Key, group => group.Max(item => item.Number));

        var sequences = dbContext.ContractSequences.ToList();
        foreach (var (year, lastNumber) in rentalsByYear)
        {
            var sequence = sequences.FirstOrDefault(item => item.Year == year);
            if (sequence is null)
            {
                sequence = new ContractSequence { Year = year, LastNumber = lastNumber };
                dbContext.ContractSequences.Add(sequence);
                sequences.Add(sequence);
                continue;
            }

            sequence.LastNumber = Math.Max(sequence.LastNumber, lastNumber);
        }

        var currentYear = DateTime.UtcNow.Year;
        if (!sequences.Any(sequence => sequence.Year == currentYear))
        {
            dbContext.ContractSequences.Add(new ContractSequence
            {
                Year = currentYear,
                LastNumber = rentalsByYear.TryGetValue(currentYear, out var currentYearLastNumber) ? currentYearLastNumber : 0
            });
        }
    }

    private static (int Year, int Number)? ParseContractSequence(string contractNumber)
    {
        var parts = contractNumber.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || !int.TryParse(parts[1], out var year) || !int.TryParse(parts[2], out var number))
        {
            return null;
        }

        return (year, number);
    }

    private static PlateLocation ResolvePlateLocation(int popularityRank)
    {
        return WeightedPlateLocations[(popularityRank - 1) % WeightedPlateLocations.Count];
    }

    private static List<PlateLocation> BuildPlateLocations()
    {
        var locations = new List<PlateLocation>(VehicleCatalogSeeds.All.Count);
        foreach (var seed in LocationSeeds)
        {
            var count = seed.Weight switch
            {
                40 => 24,
                20 => 12,
                16 => 10,
                14 => 8,
                _ => 6
            };

            for (var index = 0; index < count; index++)
            {
                locations.Add(new PlateLocation(seed.City, index % 2 == 0 ? seed.PrimaryPrefix : seed.SecondaryPrefix));
            }
        }

        return locations;
    }

    private static string GenerateCatalogPlate(int index, ISet<string> existingPlates)
    {
        var candidateIndex = index;
        while (true)
        {
            var location = WeightedPlateLocations[(candidateIndex - 1) % WeightedPlateLocations.Count];
            var numericPart = 1000 + ((candidateIndex * 173) % 9000);
            var suffixFirst = PlateAlphabet[(candidateIndex / PlateAlphabet.Length) % PlateAlphabet.Length];
            var suffixSecond = PlateAlphabet[(candidateIndex * 7) % PlateAlphabet.Length];
            var plate = $"{location.Prefix}{numericPart:0000}{suffixFirst}{suffixSecond}";
            if (!existingPlates.Contains(plate))
            {
                return plate;
            }

            candidateIndex++;
        }
    }

    private static int CalculateInitialMileage(VehicleCatalogSeeds.CatalogVehicleSeed seed)
    {
        var baseMileage = 18_000 + (seed.PopularityRank * 1_150);
        var rateAdjustment = (int)((seed.DailyRate - VehicleDomainRules.MinDailyRate) / 50m) * 80;
        return baseMileage + rateAdjustment;
    }

    private static int ResolveServiceInterval(VehicleCatalogSeeds.CatalogVehicleSeed seed)
    {
        if (seed.FuelType.Contains("Р•Р»РµРєС‚СЂРѕ", StringComparison.OrdinalIgnoreCase))
        {
            return 15000;
        }

        if (seed.Model.Contains("Transit", StringComparison.OrdinalIgnoreCase) ||
            seed.Model.Contains("Sprinter", StringComparison.OrdinalIgnoreCase) ||
            seed.Model.Contains("Vivaro", StringComparison.OrdinalIgnoreCase))
        {
            return 12000;
        }

        return 10000;
    }

    private static int ResolveNextServiceMileage(int currentMileage, int serviceIntervalKm)
    {
        return ((currentMileage / serviceIntervalKm) + 1) * serviceIntervalKm;
    }

    private static decimal RoundToNearest50(decimal value)
    {
        return Math.Round(value / 50m, MidpointRounding.AwayFromZero) * 50m;
    }

    private static void EnsureCatalogPhotoPaths(IEnumerable<Vehicle> vehicles)
    {
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
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                vehicle.PhotoPath = resolved;
            }
        }
    }

    private static string? TryResolveCatalogPhotoPath(string make, string model)
    {
        return VehiclePhotoCatalog.TryBuildCatalogPhotoPath(make, model);
    }

    private static void ApplyCatalogSeed(Vehicle vehicle, VehicleCatalogSeeds.CatalogVehicleSeed? seed)
    {
        if (seed is null)
        {
            return;
        }

        vehicle.PowertrainCapacityValue = seed.PowertrainCapacityValue;
        vehicle.PowertrainCapacityUnit = seed.PowertrainCapacityUnit;
        vehicle.FuelType = seed.FuelType;
        vehicle.TransmissionType = seed.TransmissionType;
        vehicle.DoorsCount = seed.DoorsCount;
        vehicle.CargoCapacityValue = seed.CargoCapacityValue;
        vehicle.CargoCapacityUnit = seed.CargoCapacityUnit;
        vehicle.ConsumptionValue = seed.ConsumptionValue;
        vehicle.ConsumptionUnit = seed.ConsumptionUnit;
        vehicle.HasAirConditioning = seed.HasAirConditioning;
    }

    private static Account CreateSeedAccount(string login, string password, DateTime passwordChangedAt)
    {
        return new Account
        {
            Login = login,
            PasswordHash = PasswordHasher.HashPassword(password),
            IsActive = true,
            PasswordChangedAtUtc = passwordChangedAt
        };
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
    private sealed record LocationSeed(string City, int Weight, string PrimaryPrefix, string SecondaryPrefix);
    private sealed record PlateLocation(string City, string Prefix);
    private sealed record DateInterval(DateTime StartDate, DateTime EndDate);

    private sealed class VehicleSeedState(
        Vehicle vehicle,
        VehicleCatalogSeeds.CatalogVehicleSeed catalogSeed,
        string homeCity,
        int initialMileage,
        int currentMileage,
        int nextServiceMileage,
        int serviceIntervalKm)
    {
        public Vehicle Vehicle { get; } = vehicle;
        public VehicleCatalogSeeds.CatalogVehicleSeed CatalogSeed { get; } = catalogSeed;
        public string HomeCity { get; } = homeCity;
        public int InitialMileage { get; } = initialMileage;
        public int CurrentMileage { get; set; } = currentMileage;
        public int NextServiceMileage { get; set; } = nextServiceMileage;
        public int ServiceIntervalKm { get; } = serviceIntervalKm;
        public bool HasActiveRental { get; set; }
        public List<DateInterval> OccupiedIntervals { get; } = [];
        public List<MaintenanceRecord> MaintenanceRecords { get; } = [];
    }

    private sealed class ScheduledRental
    {
        public required VehicleSeedState VehicleState { get; init; }
        public required Client Client { get; init; }
        public required Employee Employee { get; init; }
        public required RentalStatus Status { get; init; }
        public required int DurationDays { get; init; }
        public required DateTime StartDate { get; init; }
        public required DateTime EndDate { get; init; }
        public required string PickupLocation { get; init; }
        public required string ReturnLocation { get; init; }
        public required DateTime CreatedAtUtc { get; init; }
        public required bool IsOneWay { get; init; }
        public string ContractNumber { get; set; } = string.Empty;
        public int StartMileage { get; set; }
        public int? EndMileage { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal OverageFee { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime? ClosedAtUtc { get; set; }
        public DateTime? CanceledAtUtc { get; set; }
        public string? CancellationReason { get; set; }
        public DateTime? PickupInspectionCompletedAtUtc { get; set; }
        public int? PickupFuelPercent { get; set; }
        public string? PickupInspectionNotes { get; set; }
        public DateTime? ReturnInspectionCompletedAtUtc { get; set; }
        public int? ReturnFuelPercent { get; set; }
        public string? ReturnInspectionNotes { get; set; }
        public List<ScheduledPayment> Payments { get; } = [];
        public List<ScheduledDamage> Damages { get; } = [];
    }

    private sealed record ScheduledPayment(int EmployeeId, decimal Amount, PaymentMethod Method, PaymentDirection Direction, DateTime CreatedAtUtc, string Notes);
    private sealed record ScheduledDamage(string Description, DateTime DateReported, decimal RepairCost, decimal ChargedAmount, bool IsChargedToClient, DamageStatus Status, string ActNumber);

    public sealed record SeedCredentials(
        string AdminLogin,
        string AdminPassword,
        bool AdminPasswordGenerated,
        string ManagerLogin,
        string ManagerPassword,
        bool ManagerPasswordGenerated);
}
