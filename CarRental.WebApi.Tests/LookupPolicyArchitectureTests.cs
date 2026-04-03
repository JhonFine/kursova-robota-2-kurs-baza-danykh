using CarRental.WebApi.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CarRental.WebApi.Tests;

public sealed class LookupPolicyArchitectureTests
{
    private static readonly string[] IntKeyLookups =
    [
        "EmployeeRoleLookup",
        "RentalStatusLookup",
        "PaymentMethodLookup",
        "PaymentDirectionLookup",
        "PaymentStatusLookup",
        "DamageStatusLookup",
        "InspectionTypeLookup"
    ];

    private static readonly string[] CodeKeyLookups =
    [
        "VehicleStatusLookup",
        "FuelTypeLookup",
        "TransmissionTypeLookup",
        "MaintenanceTypeLookup",
        "ClientDocumentTypeLookup"
    ];

    [Fact]
    public void LookupEntities_ShouldMatchApprovedWhitelist_AndKeyPolicy()
    {
        var options = new DbContextOptionsBuilder<RentalDbContext>()
            .UseNpgsql("Host=localhost;Database=lookup_policy_probe;Username=postgres;Password=postgres")
            .Options;

        using var dbContext = new RentalDbContext(options);
        var lookupEntities = dbContext.Model.GetEntityTypes()
            .Where(entityType => entityType.ClrType.Name.EndsWith("Lookup", StringComparison.Ordinal))
            .OrderBy(entityType => entityType.ClrType.Name, StringComparer.Ordinal)
            .ToList();

        lookupEntities.Select(entityType => entityType.ClrType.Name)
            .Should()
            .BeEquivalentTo(IntKeyLookups.Concat(CodeKeyLookups));

        foreach (var entityType in lookupEntities)
        {
            var primaryKey = entityType.FindPrimaryKey();
            primaryKey.Should().NotBeNull($"{entityType.ClrType.Name} must have a primary key.");
            primaryKey!.Properties.Should().ContainSingle($"{entityType.ClrType.Name} must use a single-column key.");

            var keyProperty = primaryKey.Properties.Single();
            if (IntKeyLookups.Contains(entityType.ClrType.Name, StringComparer.Ordinal))
            {
                keyProperty.Name.Should().Be("Id", $"{entityType.ClrType.Name} must use int Id.");
                keyProperty.ClrType.Should().Be(typeof(int), $"{entityType.ClrType.Name} must use int Id.");
                continue;
            }

            keyProperty.Name.Should().Be("Code", $"{entityType.ClrType.Name} must use string Code.");
            keyProperty.ClrType.Should().Be(typeof(string), $"{entityType.ClrType.Name} must use string Code.");
        }
    }
}
