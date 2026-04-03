namespace CarRental.Desktop.Data;

public sealed class DatabaseConnectionOptions
{
    private const string DefaultPostgresConnectionString =
        "Host=localhost;Port=5432;Database=car_rental;Username=postgres;Password=postgres";

    public string PostgresConnectionString { get; init; } = DefaultPostgresConnectionString;

    public static DatabaseConnectionOptions Load()
    {
        var connectionString = Environment.GetEnvironmentVariable("CAR_RENTAL_POSTGRES_CONNECTION");
        return new DatabaseConnectionOptions
        {
            PostgresConnectionString = string.IsNullOrWhiteSpace(connectionString)
                ? DefaultPostgresConnectionString
                : connectionString.Trim()
        };
    }
}

