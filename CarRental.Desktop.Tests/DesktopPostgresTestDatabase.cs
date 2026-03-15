using CarRental.Desktop.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CarRental.Desktop.Tests;

internal sealed class DesktopPostgresTestDatabase : IAsyncDisposable
{
    private const string ConnectionStringEnvVar = "CAR_RENTAL_TEST_POSTGRES_CONNECTION";
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=car_rental;Username=postgres;Password=postgres";

    private readonly string _adminConnectionString;
    private readonly string _databaseName;
    private readonly DbContextOptions<RentalDbContext> _contextOptions;

    private DesktopPostgresTestDatabase(
        string adminConnectionString,
        string databaseName,
        DbContextOptions<RentalDbContext> contextOptions)
    {
        _adminConnectionString = adminConnectionString;
        _databaseName = databaseName;
        _contextOptions = contextOptions;
    }

    public static async Task<DesktopPostgresTestDatabase> CreateAsync()
    {
        var baseConnectionString = ResolveConnectionString();
        var databaseName = $"desktop_test_{Guid.NewGuid():N}";
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
            await dbContext.Database.EnsureCreatedAsync();
        }

        return new DesktopPostgresTestDatabase(adminConnectionString, databaseName, contextOptions);
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
