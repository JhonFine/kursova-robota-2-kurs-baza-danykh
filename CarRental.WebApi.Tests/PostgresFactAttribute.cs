namespace CarRental.WebApi.Tests;

// Локально цей атрибут дає пропускати PostgreSQL integration tests без шуму,
// але в CI вони мають виконуватись обов'язково, навіть якщо env-var не проброшений.
public sealed class PostgresFactAttribute : FactAttribute
{
    private const string ConnectionStringEnvVar = "CAR_RENTAL_TEST_POSTGRES_CONNECTION";
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=car_rental;Username=postgres;Password=postgres";

    public PostgresFactAttribute()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
        if (!string.IsNullOrWhiteSpace(connectionString) || IsReachable(DefaultConnectionString))
        {
            return;
        }

        var isCi =
            string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Environment.GetEnvironmentVariable("TF_BUILD"), "True", StringComparison.OrdinalIgnoreCase);
        if (isCi)
        {
            // In CI we require PostgreSQL checks to run. Missing variable should fail during test execution.
            return;
        }

        Skip = $"{ConnectionStringEnvVar} is not set and local PostgreSQL is not reachable on localhost:5432.";
    }

    private static bool IsReachable(string connectionString)
    {
        try
        {
            var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString)
            {
                Timeout = 2,
                CommandTimeout = 2
            };
            using var connection = new Npgsql.NpgsqlConnection(builder.ConnectionString);
            connection.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
