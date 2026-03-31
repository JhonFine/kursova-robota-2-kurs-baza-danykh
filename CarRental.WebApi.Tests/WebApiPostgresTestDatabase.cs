using CarRental.WebApi.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CarRental.WebApi.Tests;

// WebApi integration tests працюють із одноразовою PostgreSQL-базою на тест,
// щоб migration/constraint сценарії не залежали від порядку запуску.
internal sealed class WebApiPostgresTestDatabase : IAsyncDisposable
{
    private const string ConnectionStringEnvVar = "CAR_RENTAL_TEST_POSTGRES_CONNECTION";
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=car_rental;Username=postgres;Password=postgres";

    private readonly string _adminConnectionString;
    private readonly string _databaseName;
    private readonly DbContextOptions<RentalDbContext> _contextOptions;
    private readonly string _connectionString;

    private WebApiPostgresTestDatabase(
        string adminConnectionString,
        string databaseName,
        string connectionString,
        DbContextOptions<RentalDbContext> contextOptions)
    {
        _adminConnectionString = adminConnectionString;
        _databaseName = databaseName;
        _connectionString = connectionString;
        _contextOptions = contextOptions;
    }

    public string ConnectionString => _connectionString;

    public static async Task<WebApiPostgresTestDatabase> CreateAsync(bool applyMigrations = true)
    {
        var baseConnectionString = ResolveConnectionString();
        var databaseName = $"webapi_test_{Guid.NewGuid():N}";
        var adminConnectionString = BuildAdminConnectionString(baseConnectionString);

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
            // Частина тестів перевіряє саме migrations, інша лише швидко піднімає схему через EnsureCreated.
            if (applyMigrations)
            {
                await dbContext.Database.MigrateAsync();
            }
            else
            {
                await dbContext.Database.EnsureCreatedAsync();
            }
        }

        return new WebApiPostgresTestDatabase(adminConnectionString, databaseName, builder.ConnectionString, contextOptions);
    }

    public RentalDbContext CreateDbContext() => new(_contextOptions);

    public async ValueTask DisposeAsync()
    {
        // Після тесту насильно закриваємо сесії до тимчасової БД, щоб DROP DATABASE не падав на lock.
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
