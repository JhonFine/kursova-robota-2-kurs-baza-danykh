using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CarRental.Shared.ReferenceData;
using CarRental.WebApi.Contracts;
using CarRental.WebApi.Data;
using CarRental.WebApi.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CarRental.WebApi.Tests;

[Collection(nameof(CultureSensitiveHttpCollection))]
public sealed class HttpValidationRegressionTests
{
    private static readonly JsonSerializerOptions ApiJsonOptions = CreateApiJsonOptions();

    [PostgresFact]
    public async Task AddDamageJson_ShouldCreateDamage_AndAppearInJournal_UnderUkUaCulture()
    {
        using var cultureScope = CultureScope.Use("uk-UA");
        using var jwtScope = EnvironmentVariableScope.Use(
            "CAR_RENTAL_JWT_SIGNING_KEY",
            "test-signing-key-with-sufficient-length-1234567890");
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        using var connectionScope = EnvironmentVariableScope.Use(
            "ConnectionStrings__Postgres",
            testDatabase.ConnectionString);
        using var factory = new ApiHostFactory();
        using var client = factory.CreateClient();

        var scenario = await CreateRentalScenarioAsync(testDatabase);
        var token = await LoginAsync(client, "manager", "manager123");

        var description = $"damage-json-{Guid.NewGuid():N}";
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/damages")
        {
            Content = JsonContent.Create(new
            {
                vehicleId = scenario.VehicleId,
                rentalId = scenario.RentalId,
                description,
                repairCost = 5000,
                autoChargeToRental = true
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdDamage = await response.Content.ReadFromJsonAsync<DamageDto>(ApiJsonOptions);
        createdDamage.Should().NotBeNull();
        createdDamage!.Description.Should().Be(description);
        createdDamage.VehicleId.Should().Be(scenario.VehicleId);
        createdDamage.RentalId.Should().Be(scenario.RentalId);

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, "/api/damages?page=1&pageSize=200");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var journalResponse = await client.SendAsync(getRequest);
        journalResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var journal = await journalResponse.Content.ReadFromJsonAsync<List<DamageDto>>(ApiJsonOptions);
        journal.Should().NotBeNull();
        journal!.Should().Contain(item => item.Id == createdDamage.Id && item.Description == description);
    }

    [PostgresFact]
    public async Task AddDamageMultipart_ShouldCreateDamage_UnderUkUaCulture()
    {
        using var cultureScope = CultureScope.Use("uk-UA");
        using var jwtScope = EnvironmentVariableScope.Use(
            "CAR_RENTAL_JWT_SIGNING_KEY",
            "test-signing-key-with-sufficient-length-1234567890");
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        using var connectionScope = EnvironmentVariableScope.Use(
            "ConnectionStrings__Postgres",
            testDatabase.ConnectionString);
        using var factory = new ApiHostFactory();
        using var client = factory.CreateClient();

        var scenario = await CreateRentalScenarioAsync(testDatabase);
        var token = await LoginAsync(client, "manager", "manager123");

        var description = $"damage-multipart-{Guid.NewGuid():N}";
        using var content = new MultipartFormDataContent
        {
            { new StringContent(scenario.VehicleId.ToString(CultureInfo.InvariantCulture)), "VehicleId" },
            { new StringContent(scenario.RentalId.ToString(CultureInfo.InvariantCulture)), "RentalId" },
            { new StringContent(description), "Description" },
            { new StringContent("5000"), "RepairCost" },
            { new StringContent(bool.FalseString), "AutoChargeToRental" }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/damages")
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdDamage = await response.Content.ReadFromJsonAsync<DamageDto>(ApiJsonOptions);
        createdDamage.Should().NotBeNull();
        createdDamage!.Description.Should().Be(description);
    }

    [PostgresFact]
    public async Task AddDamageJson_WithZeroRepairCost_ShouldReturnBadRequest_NotServerError()
    {
        using var cultureScope = CultureScope.Use("uk-UA");
        using var jwtScope = EnvironmentVariableScope.Use(
            "CAR_RENTAL_JWT_SIGNING_KEY",
            "test-signing-key-with-sufficient-length-1234567890");
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        using var connectionScope = EnvironmentVariableScope.Use(
            "ConnectionStrings__Postgres",
            testDatabase.ConnectionString);
        using var factory = new ApiHostFactory();
        using var client = factory.CreateClient();

        var scenario = await CreateRentalScenarioAsync(testDatabase);
        var token = await LoginAsync(client, "manager", "manager123");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/damages")
        {
            Content = JsonContent.Create(new
            {
                vehicleId = scenario.VehicleId,
                description = $"damage-invalid-{Guid.NewGuid():N}",
                repairCost = 0,
                autoChargeToRental = false
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().NotContain("The server failed to process the request.");
    }

    [PostgresFact]
    public async Task UpdateVehicleRate_ShouldSucceed_UnderUkUaCulture()
    {
        using var cultureScope = CultureScope.Use("uk-UA");
        using var jwtScope = EnvironmentVariableScope.Use(
            "CAR_RENTAL_JWT_SIGNING_KEY",
            "test-signing-key-with-sufficient-length-1234567890");
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        using var connectionScope = EnvironmentVariableScope.Use(
            "ConnectionStrings__Postgres",
            testDatabase.ConnectionString);
        using var factory = new ApiHostFactory();
        using var client = factory.CreateClient();

        var scenario = await CreateRentalScenarioAsync(testDatabase);
        var token = await LoginAsync(client, "admin", "admin123");

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/vehicles/{scenario.VehicleId}/rate")
        {
            Content = JsonContent.Create(new
            {
                dailyRate = 500
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var vehicle = await response.Content.ReadFromJsonAsync<VehicleDto>(ApiJsonOptions);
        vehicle.Should().NotBeNull();
        vehicle!.Id.Should().Be(scenario.VehicleId);
        vehicle.DailyRate.Should().Be(500);
    }

    private static async Task<RentalScenario> CreateRentalScenarioAsync(WebApiPostgresTestDatabase testDatabase)
    {
        await using var dbContext = testDatabase.CreateDbContext();

        var managerId = await dbContext.Employees
            .AsNoTracking()
            .Where(item => item.RoleId == UserRole.Manager)
            .Select(item => item.Id)
            .FirstAsync();

        var fuelTypeCode = await dbContext.FuelTypes
            .AsNoTracking()
            .Select(item => item.Code)
            .FirstAsync();

        var transmissionTypeCode = await dbContext.TransmissionTypes
            .AsNoTracking()
            .Select(item => item.Code)
            .FirstAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var vehicle = TestLookupSeed.CreateVehicle(
            dbContext,
            make: "Test",
            model: $"Vehicle {suffix}",
            licensePlate: $"T{suffix[..7]}",
            fuelTypeCode: fuelTypeCode,
            transmissionTypeCode: transmissionTypeCode,
            powertrainCapacityValue: 2m,
            cargoCapacityValue: 450m,
            consumptionValue: 7.2m,
            mileage: 12000,
            dailyRate: 150m);

        var client = new Client
        {
            FullName = $"Test Client {suffix}",
            Phone = $"+380{suffix.Select(ch => (int)ch % 10).Aggregate(string.Empty, (current, digit) => current + digit)}"
        };

        dbContext.Vehicles.Add(vehicle);
        dbContext.Clients.Add(client);
        await dbContext.SaveChangesAsync();

        var rental = new Rental
        {
            ClientId = client.Id,
            VehicleId = vehicle.Id,
            CreatedByEmployeeId = managerId,
            ContractNumber = $"CR-TST-{suffix}",
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(1),
            PickupLocation = "Київ",
            ReturnLocation = "Київ",
            StartMileage = vehicle.Mileage,
            TotalAmount = 300m,
            StatusId = RentalStatus.Active
        };

        dbContext.Rentals.Add(rental);
        await dbContext.SaveChangesAsync();

        return new RentalScenario(vehicle.Id, rental.Id);
    }

    private static async Task<string> LoginAsync(HttpClient client, string login, string password)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Login = login,
                Password = password
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var responseStream = await response.Content.ReadAsStreamAsync();
        using var payload = await JsonDocument.ParseAsync(responseStream);
        var accessToken = payload.RootElement.GetProperty("accessToken").GetString();
        accessToken.Should().NotBeNullOrWhiteSpace();
        return accessToken!;
    }

    private static JsonSerializerOptions CreateApiJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class ApiHostFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
        }
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo previousCulture;
        private readonly CultureInfo previousUiCulture;
        private readonly CultureInfo? previousDefaultCulture;
        private readonly CultureInfo? previousDefaultUiCulture;

        private CultureScope(CultureInfo culture)
        {
            previousCulture = CultureInfo.CurrentCulture;
            previousUiCulture = CultureInfo.CurrentUICulture;
            previousDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;
            previousDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }

        public static CultureScope Use(string cultureName)
            => new(new CultureInfo(cultureName));

        public void Dispose()
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
            CultureInfo.DefaultThreadCurrentCulture = previousDefaultCulture;
            CultureInfo.DefaultThreadCurrentUICulture = previousDefaultUiCulture;
        }
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string name;
        private readonly string? previousValue;

        private EnvironmentVariableScope(string name, string value)
        {
            this.name = name;
            previousValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public static EnvironmentVariableScope Use(string name, string value)
            => new(name, value);

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(name, previousValue);
        }
    }

    private sealed record RentalScenario(int VehicleId, int RentalId);
}

[CollectionDefinition(nameof(CultureSensitiveHttpCollection), DisableParallelization = true)]
public sealed class CultureSensitiveHttpCollection;
