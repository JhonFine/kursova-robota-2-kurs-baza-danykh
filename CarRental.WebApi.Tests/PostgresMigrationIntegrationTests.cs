using CarRental.WebApi.Data;
using CarRental.WebApi.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CarRental.WebApi.Tests;

// Migration integration tests страхують саме структуру БД:
// constraints, відсутність legacy-колонок і поведінку схеми після реальних insert/update сценаріїв.
public sealed class PostgresMigrationIntegrationTests
{
    private const string ConnectionStringEnvVar = "CAR_RENTAL_TEST_POSTGRES_CONNECTION";
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=car_rental;Username=postgres;Password=postgres";

    [PostgresFact]
    public async Task PostgresMigrations_ShouldCreateConstraintAndAvailabilityTrigger()
    {
        await using var testDatabase = await PostgresTestDatabase.TryCreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();

        // Тут перевіряємо не лише наявність артефактів міграції, а й те, що legacy механіки availability остаточно прибрані.
        var hasChargeConsistencyConstraint = await dbContext.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*)::int AS "Value"
                FROM pg_constraint
                WHERE conname = 'CK_Damages_Status_ChargeConsistency'
                """)
            .SingleAsync();
        hasChargeConsistencyConstraint.Should().Be(1);

        var hasAvailabilityTrigger = await dbContext.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*)::int AS "Value"
                FROM pg_trigger
                WHERE tgname = 'trg_rentals_sync_vehicle_availability'
                  AND NOT tgisinternal
                """)
            .SingleAsync();
        hasAvailabilityTrigger.Should().Be(0);

        var hasVehicleAvailabilityTrigger = await dbContext.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*)::int AS "Value"
                FROM pg_trigger
                WHERE tgname = 'trg_vehicles_sync_vehicle_availability'
                  AND NOT tgisinternal
                """)
            .SingleAsync();
        hasVehicleAvailabilityTrigger.Should().Be(0);

        var hasOverlapConstraint = await dbContext.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*)::int AS "Value"
                FROM pg_constraint
                WHERE conname = 'EX_Rentals_NoOverlappingActiveOrBookedPeriods'
                """)
            .SingleAsync();
        hasOverlapConstraint.Should().Be(1);

        var hasAtomicVehicleSpecColumns = await dbContext.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*)::int AS "Value"
                FROM information_schema.columns
                WHERE table_schema = current_schema()
                  AND table_name = 'Vehicles'
                  AND column_name IN (
                      'FuelTypeCode',
                      'TransmissionTypeCode',
                      'VehicleStatusCode',
                      'DoorsCount',
                      'PowertrainCapacityValue',
                      'PowertrainCapacityUnit',
                      'CargoCapacityValue',
                      'CargoCapacityUnit',
                      'ConsumptionValue',
                      'ConsumptionUnit',
                      'HasAirConditioning'
                  )
                """)
            .SingleAsync();
        hasAtomicVehicleSpecColumns.Should().Be(11);

        var hasLegacyVehicleColumns = await dbContext.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*)::int AS "Value"
                FROM information_schema.columns
                WHERE table_schema = current_schema()
                  AND table_name = 'Vehicles'
                  AND column_name IN (
                      'EngineDisplay',
                      'CargoCapacityDisplay',
                      'ConsumptionDisplay',
                      'IsAvailable',
                      'IsBookable',
                      'PhotoPath'
                  )
                """)
            .SingleAsync();
        hasLegacyVehicleColumns.Should().Be(0);

        var hasUtcTimestamps = await dbContext.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*)::int AS "Value"
                FROM information_schema.columns
                WHERE table_schema = current_schema()
                  AND (
                      (table_name = 'Vehicles' AND column_name IN ('CreatedAtUtc', 'UpdatedAtUtc'))
                      OR (table_name = 'Rentals' AND column_name IN ('CreatedAtUtc', 'ClosedAtUtc', 'CanceledAtUtc'))
                      OR (table_name = 'Damages' AND column_name IN ('CreatedAtUtc', 'UpdatedAtUtc'))
                  )
                  AND data_type = 'timestamp with time zone'
                """)
            .SingleAsync();
        hasUtcTimestamps.Should().Be(7);

        var hasDocumentUniqueIndex = await dbContext.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*)::int AS "Value"
                FROM pg_indexes
                WHERE schemaname = current_schema()
                  AND tablename = 'ClientDocuments'
                  AND indexname = 'IX_ClientDocuments_DocumentTypeCode_DocumentNumber'
                  AND indexdef LIKE '%WHERE ("IsDeleted" = false)%'
                """)
            .SingleAsync();
        hasDocumentUniqueIndex.Should().Be(1);

        var hasAccountsLoginUniqueIndex = await dbContext.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*)::int AS "Value"
                FROM pg_indexes
                WHERE schemaname = current_schema()
                  AND tablename = 'Accounts'
                  AND indexname = 'IX_Accounts_Login'
                """)
            .SingleAsync();
        hasAccountsLoginUniqueIndex.Should().Be(1);

        var now = DateTime.UtcNow;
        TestLookupSeed.SeedVehicleLookups(dbContext);
        var fuelTypeCode = dbContext.FuelTypes.Local.First().Code;
        var automaticTransmissionCode = dbContext.TransmissionTypes.Local.First().Code;

        var employee = CreateEmployee("integration_admin", now);
        var client = CreateClient("Integration Client", "PP999999", "DL999999", "+10000000000");
        var vehicle = CreateVehicle(
            "Toyota",
            "Corolla",
            "IT-0001",
            fuelTypeCode,
            automaticTransmissionCode,
            1.8m,
            470m,
            6.6m,
            10000,
            90m);

        dbContext.Employees.Add(employee);
        dbContext.Clients.Add(client);
        dbContext.Vehicles.Add(vehicle);
        await dbContext.SaveChangesAsync();

        var rental = new Rental
        {
            ClientId = client.Id,
            VehicleId = vehicle.Id,
            EmployeeId = employee.Id,
            ContractNumber = "CR-2026-900001",
            StartDate = now.AddHours(-1),
            EndDate = now.AddHours(6),
            StartMileage = vehicle.Mileage,
            TotalAmount = 180m,
            Status = RentalStatus.Active,
            IsClosed = false,
            CreatedAtUtc = now
        };
        dbContext.Rentals.Add(rental);
        await dbContext.SaveChangesAsync();

        var lockedVehicle = await dbContext.Vehicles.AsNoTracking().SingleAsync(item => item.Id == vehicle.Id);
        var lockedVehicleIsAvailable = await ComputeVehicleAvailabilityAsync(dbContext, lockedVehicle);
        lockedVehicleIsAvailable.Should().BeFalse();

        rental.Status = RentalStatus.Closed;
        rental.IsClosed = true;
        rental.EndDate = now;
        rental.ClosedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        var releasedVehicle = await dbContext.Vehicles.AsNoTracking().SingleAsync(item => item.Id == vehicle.Id);
        var releasedVehicleIsAvailable = await ComputeVehicleAvailabilityAsync(dbContext, releasedVehicle);
        releasedVehicleIsAvailable.Should().BeTrue();

        vehicle.IsBookable = false;
        await dbContext.SaveChangesAsync();

        var manuallyBlockedVehicle = await dbContext.Vehicles.AsNoTracking().SingleAsync(item => item.Id == vehicle.Id);
        var manuallyBlockedVehicleIsAvailable = await ComputeVehicleAvailabilityAsync(dbContext, manuallyBlockedVehicle);
        manuallyBlockedVehicleIsAvailable.Should().BeFalse();
    }

    [PostgresFact]
    public async Task PostgresMigrations_ShouldRejectOverlappingBookedRentals_AndAllowReuseAfterSoftDelete()
    {
        await using var testDatabase = await PostgresTestDatabase.TryCreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();

        var now = DateTime.UtcNow;
        TestLookupSeed.SeedVehicleLookups(dbContext);
        var fuelTypeCode = dbContext.FuelTypes.Local.First().Code;
        var automaticTransmissionCode = dbContext.TransmissionTypes.Local.First().Code;

        var employee = CreateEmployee("integration_soft_delete_user", now);
        var client = CreateClient("Reusable Client", "PP300000", "DL300000", "+10000000003");
        var vehicle = CreateVehicle(
            "Mazda",
            "6",
            "IT-0004",
            fuelTypeCode,
            automaticTransmissionCode,
            2m,
            480m,
            7m,
            12000,
            95m);

        dbContext.Employees.Add(employee);
        dbContext.Clients.Add(client);
        dbContext.Vehicles.Add(vehicle);
        await dbContext.SaveChangesAsync();

        dbContext.Rentals.Add(new Rental
        {
            ClientId = client.Id,
            VehicleId = vehicle.Id,
            EmployeeId = employee.Id,
            ContractNumber = "CR-2026-900010",
            StartDate = now.AddDays(2),
            EndDate = now.AddDays(4),
            StartMileage = vehicle.Mileage,
            TotalAmount = 200m,
            Status = RentalStatus.Booked,
            IsClosed = false,
            CreatedAtUtc = now
        });
        await dbContext.SaveChangesAsync();

        dbContext.Rentals.Add(new Rental
        {
            ClientId = client.Id,
            VehicleId = vehicle.Id,
            EmployeeId = employee.Id,
            ContractNumber = "CR-2026-900011",
            StartDate = now.AddDays(4),
            EndDate = now.AddDays(5),
            StartMileage = vehicle.Mileage,
            TotalAmount = 120m,
            Status = RentalStatus.Booked,
            IsClosed = false,
            CreatedAtUtc = now
        });

        var overlapAction = async () => await dbContext.SaveChangesAsync();
        var overlapException = await overlapAction.Should().ThrowAsync<DbUpdateException>();
        overlapException.Which.InnerException.Should().BeOfType<PostgresException>();
        ((PostgresException)overlapException.Which.InnerException!).SqlState.Should().Be(PostgresErrorCodes.ExclusionViolation);

        dbContext.ChangeTracker.Clear();

        var archivedClient = await dbContext.Clients.IgnoreQueryFilters().SingleAsync(item => item.Id == client.Id);
        archivedClient.IsDeleted = true;
        var archivedVehicle = await dbContext.Vehicles.IgnoreQueryFilters().SingleAsync(item => item.Id == vehicle.Id);
        archivedVehicle.IsDeleted = true;
        await dbContext.SaveChangesAsync();

        dbContext.Clients.Add(CreateClient("Reusable Client 2", "PP300001", "DL300000", "+10000000004"));
        dbContext.Vehicles.Add(CreateVehicle(
            "Mazda",
            "CX-5",
            "IT-0004",
            fuelTypeCode,
            automaticTransmissionCode,
            2.5m,
            506m,
            7.5m,
            15000,
            110m));

        var reuseAction = async () => await dbContext.SaveChangesAsync();
        await reuseAction.Should().NotThrowAsync();
    }

    [PostgresFact]
    public async Task PostgresDamagesChargeConsistencyConstraint_ShouldRejectInvalidRows()
    {
        await using var testDatabase = await PostgresTestDatabase.TryCreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();

        var now = DateTime.UtcNow;
        TestLookupSeed.SeedVehicleLookups(dbContext);
        var fuelTypeCode = dbContext.FuelTypes.Local.First().Code;
        var automaticTransmissionCode = dbContext.TransmissionTypes.Local.First().Code;

        dbContext.Employees.Add(CreateEmployee("integration_manager", now, UserRole.Manager));
        dbContext.Clients.Add(CreateClient("Constraint Client", "PP100000", "DL100000", "+10000000001"));
        var vehicle = CreateVehicle(
            "Skoda",
            "Octavia",
            "IT-0002",
            fuelTypeCode,
            automaticTransmissionCode,
            1.8m,
            600m,
            6.4m,
            8000,
            70m);
        dbContext.Vehicles.Add(vehicle);
        await dbContext.SaveChangesAsync();

        dbContext.Damages.Add(new Damage
        {
            VehicleId = vehicle.Id,
            Description = "Invalid charge consistency",
            DateReported = now,
            RepairCost = 150m,
            ActNumber = "ACT-INVALID-TEST-0001",
            ChargedAmount = 10m,
            Status = DamageStatus.Open
        });

        var action = async () => await dbContext.SaveChangesAsync();
        var exception = await action.Should().ThrowAsync<DbUpdateException>();
        exception.Which.InnerException.Should().BeOfType<PostgresException>();
        ((PostgresException)exception.Which.InnerException!).SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }

    [PostgresFact]
    public async Task PostgresDamagesActNumberUniqueIndex_ShouldRejectDuplicates()
    {
        await using var testDatabase = await PostgresTestDatabase.TryCreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();

        var now = DateTime.UtcNow;
        TestLookupSeed.SeedVehicleLookups(dbContext);
        var fuelTypeCode = dbContext.FuelTypes.Local.First().Code;
        var manualTransmissionCode = dbContext.TransmissionTypes.Local.Last().Code;

        dbContext.Employees.Add(CreateEmployee("integration_index_user", now, UserRole.Manager));
        dbContext.Clients.Add(CreateClient("Index Client", "PP200000", "DL200000", "+10000000002"));
        var vehicle = CreateVehicle(
            "Renault",
            "Logan",
            "IT-0003",
            fuelTypeCode,
            manualTransmissionCode,
            1.6m,
            510m,
            6.2m,
            7000,
            60m);
        dbContext.Vehicles.Add(vehicle);
        await dbContext.SaveChangesAsync();

        const string duplicateActNumber = "ACT-DUPLICATE-TEST-0001";
        dbContext.Damages.Add(new Damage
        {
            VehicleId = vehicle.Id,
            Description = "Damage one",
            DateReported = now,
            RepairCost = 90m,
            ActNumber = duplicateActNumber,
            ChargedAmount = 0m,
            Status = DamageStatus.Open
        });
        dbContext.Damages.Add(new Damage
        {
            VehicleId = vehicle.Id,
            Description = "Damage two",
            DateReported = now.AddMinutes(1),
            RepairCost = 120m,
            ActNumber = duplicateActNumber,
            ChargedAmount = 0m,
            Status = DamageStatus.Open
        });

        var action = async () => await dbContext.SaveChangesAsync();
        var exception = await action.Should().ThrowAsync<DbUpdateException>();
        exception.Which.InnerException.Should().BeOfType<PostgresException>();
        ((PostgresException)exception.Which.InnerException!).SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }

    private static Employee CreateEmployee(string login, DateTime now, UserRole role = UserRole.Admin)
    {
        return new Employee
        {
            FullName = $"Employee {login}",
            Login = login,
            PasswordHash = "hash",
            Role = role,
            IsActive = true,
            PasswordChangedAtUtc = now
        };
    }

    private static Client CreateClient(string fullName, string passportData, string driverLicense, string phone)
    {
        return new Client
        {
            FullName = fullName,
            PassportData = passportData,
            DriverLicense = driverLicense,
            Phone = phone,
            Blacklisted = false
        };
    }

    private static Vehicle CreateVehicle(
        string make,
        string model,
        string licensePlate,
        string fuelTypeCode,
        string transmissionTypeCode,
        decimal powertrainCapacityValue,
        decimal cargoCapacityValue,
        decimal consumptionValue,
        int mileage,
        decimal dailyRate)
    {
        return new Vehicle
        {
            Make = make,
            Model = model,
            FuelType = fuelTypeCode,
            TransmissionType = transmissionTypeCode,
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

    private static async Task<bool> ComputeVehicleAvailabilityAsync(RentalDbContext dbContext, Vehicle vehicle)
    {
        var hasActiveRental = await dbContext.Rentals
            .AsNoTracking()
            .AnyAsync(item => item.VehicleId == vehicle.Id && item.Status == RentalStatus.Active);
        return !vehicle.IsDeleted && vehicle.IsBookable && !hasActiveRental;
    }

    private sealed class PostgresTestDatabase : IAsyncDisposable
    {
        private readonly string _adminConnectionString;
        private readonly string _databaseName;
        private readonly DbContextOptions<RentalDbContext> _contextOptions;

        private PostgresTestDatabase(
            string adminConnectionString,
            string databaseName,
            DbContextOptions<RentalDbContext> contextOptions)
        {
            _adminConnectionString = adminConnectionString;
            _databaseName = databaseName;
            _contextOptions = contextOptions;
        }

        public static async Task<PostgresTestDatabase> TryCreateAsync()
        {
            var baseConnectionString = ResolveConnectionString();
            var adminConnectionString = BuildAdminConnectionString(baseConnectionString);
            var databaseName = $"it_{Guid.NewGuid():N}";

            await using (var adminConnection = new NpgsqlConnection(adminConnectionString))
            {
                await adminConnection.OpenAsync();
                await using var createDatabaseCommand = new NpgsqlCommand($"""CREATE DATABASE "{databaseName}";""", adminConnection);
                await createDatabaseCommand.ExecuteNonQueryAsync();
            }

            var builder = new NpgsqlConnectionStringBuilder(baseConnectionString)
            {
                Database = databaseName
            };
            var contextOptions = new DbContextOptionsBuilder<RentalDbContext>()
                .UseNpgsql(builder.ConnectionString)
                .Options;

            await using (var dbContext = new RentalDbContext(contextOptions))
            {
                await dbContext.Database.MigrateAsync();
            }

            return new PostgresTestDatabase(adminConnectionString, databaseName, contextOptions);
        }

        public RentalDbContext CreateDbContext() => new(_contextOptions);

        public async ValueTask DisposeAsync()
        {
            await using var adminConnection = new NpgsqlConnection(_adminConnectionString);
            await adminConnection.OpenAsync();
            await using var terminateConnectionsCommand = new NpgsqlCommand(
                """
                SELECT pg_terminate_backend(pid)
                FROM pg_stat_activity
                WHERE datname = @databaseName
                  AND pid <> pg_backend_pid();
                """,
                adminConnection);
            terminateConnectionsCommand.Parameters.AddWithValue("@databaseName", _databaseName);
            await terminateConnectionsCommand.ExecuteNonQueryAsync();

            await using var dropDatabaseCommand = new NpgsqlCommand($"""DROP DATABASE IF EXISTS "{_databaseName}";""", adminConnection);
            await dropDatabaseCommand.ExecuteNonQueryAsync();
        }

        private static string ResolveConnectionString()
        {
            var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
            return string.IsNullOrWhiteSpace(connectionString)
                ? DefaultConnectionString
                : connectionString.Trim();
        }

        private static string BuildAdminConnectionString(string connectionString)
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Database = "postgres"
            };
            return builder.ConnectionString;
        }
    }
}
