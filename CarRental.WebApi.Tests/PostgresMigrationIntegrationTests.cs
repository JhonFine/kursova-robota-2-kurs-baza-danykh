using CarRental.WebApi.Data;
using CarRental.WebApi.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CarRental.WebApi.Tests;

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

        var hasChargeConsistencyConstraint = await dbContext.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*)::int AS "Value"
                FROM pg_constraint
                WHERE conname = 'CK_Damages_ChargeFlag_Consistency'
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
        hasAvailabilityTrigger.Should().Be(1);

        var hasVehicleSpecificationColumns = await dbContext.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*)::int AS "Value"
                FROM information_schema.columns
                WHERE table_schema = current_schema()
                  AND table_name = 'Vehicles'
                  AND column_name IN (
                      'EngineDisplay',
                      'FuelType',
                      'TransmissionType',
                      'DoorsCount',
                      'CargoCapacityDisplay',
                      'ConsumptionDisplay',
                      'HasAirConditioning'
                  )
                """)
            .SingleAsync();
        hasVehicleSpecificationColumns.Should().Be(7);

        var now = DateTime.UtcNow;
        var employee = new Employee
        {
            FullName = "Integration Admin",
            Login = "integration_admin",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            IsActive = true,
            PasswordChangedAtUtc = now
        };
        var client = new Client
        {
            FullName = "Integration Client",
            PassportData = "PP999999",
            DriverLicense = "DL999999",
            Phone = "+10000000000",
            Blacklisted = false
        };
        var vehicle = new Vehicle
        {
            Make = "Toyota",
            Model = "Corolla",
            LicensePlate = "IT-0001",
            Mileage = 10000,
            DailyRate = 90m,
            IsAvailable = true,
            ServiceIntervalKm = 10000
        };

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
        lockedVehicle.IsAvailable.Should().BeFalse();

        rental.Status = RentalStatus.Closed;
        rental.IsClosed = true;
        rental.EndDate = now;
        rental.ClosedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        var releasedVehicle = await dbContext.Vehicles.AsNoTracking().SingleAsync(item => item.Id == vehicle.Id);
        releasedVehicle.IsAvailable.Should().BeTrue();
    }

    [PostgresFact]
    public async Task PostgresDamagesChargeConsistencyConstraint_ShouldRejectInvalidRows()
    {
        await using var testDatabase = await PostgresTestDatabase.TryCreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();

        var now = DateTime.UtcNow;
        var employee = new Employee
        {
            FullName = "Integration Manager",
            Login = "integration_manager",
            PasswordHash = "hash",
            Role = UserRole.Manager,
            IsActive = true,
            PasswordChangedAtUtc = now
        };
        var client = new Client
        {
            FullName = "Constraint Client",
            PassportData = "PP100000",
            DriverLicense = "DL100000",
            Phone = "+10000000001",
            Blacklisted = false
        };
        var vehicle = new Vehicle
        {
            Make = "Skoda",
            Model = "Octavia",
            LicensePlate = "IT-0002",
            Mileage = 8000,
            DailyRate = 70m,
            IsAvailable = true,
            ServiceIntervalKm = 10000
        };

        dbContext.Employees.Add(employee);
        dbContext.Clients.Add(client);
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
            IsChargedToClient = false,
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
        var employee = new Employee
        {
            FullName = "Integration Index User",
            Login = "integration_index_user",
            PasswordHash = "hash",
            Role = UserRole.Manager,
            IsActive = true,
            PasswordChangedAtUtc = now
        };
        var client = new Client
        {
            FullName = "Index Client",
            PassportData = "PP200000",
            DriverLicense = "DL200000",
            Phone = "+10000000002",
            Blacklisted = false
        };
        var vehicle = new Vehicle
        {
            Make = "Renault",
            Model = "Logan",
            LicensePlate = "IT-0003",
            Mileage = 7000,
            DailyRate = 60m,
            IsAvailable = true,
            ServiceIntervalKm = 10000
        };

        dbContext.Employees.Add(employee);
        dbContext.Clients.Add(client);
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
            IsChargedToClient = false,
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
            IsChargedToClient = false,
            Status = DamageStatus.Open
        });

        var action = async () => await dbContext.SaveChangesAsync();
        var exception = await action.Should().ThrowAsync<DbUpdateException>();
        exception.Which.InnerException.Should().BeOfType<PostgresException>();
        ((PostgresException)exception.Which.InnerException!).SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
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
