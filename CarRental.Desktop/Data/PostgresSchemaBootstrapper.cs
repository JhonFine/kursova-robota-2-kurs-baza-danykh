extern alias WebApiRuntime;

using Microsoft.EntityFrameworkCore;
using DesktopRentalDbContext = CarRental.Desktop.Data.RentalDbContext;
using WebApiRentalDbContext = WebApiRuntime::CarRental.WebApi.Data.RentalDbContext;

namespace CarRental.Desktop.Data;

public static class PostgresSchemaBootstrapper
{
    private static readonly string[] RequiredSchemaChecks =
    [
        "SELECT 1 FROM \"Accounts\" LIMIT 1;",
        "SELECT 1 FROM \"Employees\" LIMIT 1;",
        "SELECT \"RoleId\" FROM \"Employees\" LIMIT 1;",
        "SELECT 1 FROM \"Clients\" LIMIT 1;",
        "SELECT 1 FROM \"ClientDocuments\" LIMIT 1;",
        "SELECT 1 FROM \"VehicleMakes\" LIMIT 1;",
        "SELECT 1 FROM \"VehicleModels\" LIMIT 1;",
        "SELECT 1 FROM \"Vehicles\" LIMIT 1;",
        "SELECT 1 FROM \"VehiclePhotos\" LIMIT 1;",
        "SELECT 1 FROM \"Damages\" LIMIT 1;",
        "SELECT 1 FROM \"DamagePhotos\" LIMIT 1;",
        "SELECT 1 FROM \"Payments\" LIMIT 1;",
        "SELECT 1 FROM \"MaintenanceRecords\" LIMIT 1;",
        "SELECT \"MakeId\" FROM \"VehicleModels\" LIMIT 1;",
        "SELECT \"MakeId\", \"ModelId\", \"VehicleStatusCode\" FROM \"Vehicles\" LIMIT 1;",
        "SELECT \"CreatedByEmployeeId\" FROM \"Rentals\" LIMIT 1;",
        "SELECT \"ReportedByEmployeeId\" FROM \"Damages\" LIMIT 1;"
    ];

    public static void EnsureCanonicalSchema(string connectionString)
    {
        try
        {
            var webApiContextOptions = new DbContextOptionsBuilder<WebApiRentalDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            using (var webApiDbContext = new WebApiRentalDbContext(webApiContextOptions))
            {
                webApiDbContext.Database.Migrate();
            }

            var desktopContextOptions = new DbContextOptionsBuilder<DesktopRentalDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            using var desktopDbContext = new DesktopRentalDbContext(desktopContextOptions);
            VerifyRequiredArtifacts(desktopDbContext);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Desktop could not synchronize PostgreSQL schema to the current canonical version. Ensure the configured database exists and the account can apply migrations.",
                exception);
        }
    }

    public static void VerifyRequiredArtifacts(DesktopRentalDbContext dbContext)
    {
        foreach (var sql in RequiredSchemaChecks)
        {
            dbContext.Database.ExecuteSqlRaw(sql);
        }
    }
}
