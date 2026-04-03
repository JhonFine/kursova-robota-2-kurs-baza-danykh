using CarRental.Desktop.Data;
using CarRental.Desktop.Services.Auth;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Desktop.Tests;

public sealed class DesktopSharedSchemaSmokeTests
{
    [Fact]
    public async Task DesktopSchemaBootstrapper_ShouldApplyCanonicalSchemaBeforeSeed()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateEmptyAsync();

        PostgresSchemaBootstrapper.EnsureCanonicalSchema(testDatabase.ConnectionString);

        await using var dbContext = testDatabase.CreateDbContext();
        PostgresSchemaBootstrapper.VerifyRequiredArtifacts(dbContext);
        DatabaseInitializer.Seed(dbContext);

        (await dbContext.VehicleMakes.CountAsync()).Should().BeGreaterThan(0);
        (await dbContext.VehicleModels.CountAsync()).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DesktopSeedAndAuth_ShouldWorkAgainstWebApiMigratedSchema()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        DatabaseInitializer.SeedCredentials? credentials;
        await using (var bootstrapDbContext = testDatabase.CreateDbContext())
        {
            var requiredChecks = new[]
            {
                "SELECT 1 FROM \"Accounts\" LIMIT 1;",
                "SELECT \"RoleId\" FROM \"Employees\" LIMIT 1;",
                "SELECT 1 FROM \"ClientDocuments\" LIMIT 1;",
                "SELECT 1 FROM \"VehicleMakes\" LIMIT 1;",
                "SELECT 1 FROM \"VehicleModels\" LIMIT 1;",
                "SELECT 1 FROM \"VehiclePhotos\" LIMIT 1;",
                "SELECT \"MakeId\", \"ModelId\" FROM \"Vehicles\" LIMIT 1;",
                "SELECT \"CreatedByEmployeeId\" FROM \"Rentals\" LIMIT 1;",
                "SELECT \"VehicleStatusCode\" FROM \"Vehicles\" LIMIT 1;"
            };

            foreach (var sql in requiredChecks)
            {
                bootstrapDbContext.Database.ExecuteSqlRaw(sql);
            }

            credentials = DatabaseInitializer.Seed(bootstrapDbContext);
            credentials.Should().NotBeNull();
        }

        await using var authDbContext = testDatabase.CreateDbContext();
        var authService = new AuthService(authDbContext);

        var loginResult = await authService.AuthenticateAsync(credentials!.AdminLogin, credentials.AdminPassword);

        loginResult.Success.Should().BeTrue();
        loginResult.Employee.Should().NotBeNull();
        loginResult.Employee!.Account.Should().NotBeNull();
        (await authDbContext.ClientDocuments.CountAsync()).Should().BeGreaterOrEqualTo(0);
    }
}
