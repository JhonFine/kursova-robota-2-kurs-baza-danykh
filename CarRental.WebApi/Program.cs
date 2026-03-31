using System.Text;
using System.Text.Json.Serialization;
using CarRental.WebApi.Auth;
using CarRental.WebApi.Data;
using CarRental.WebApi.Middleware;
using CarRental.WebApi.Services.Auth;
using CarRental.WebApi.Services.Damages;
using CarRental.WebApi.Services.Documents;
using CarRental.WebApi.Services.Maintenance;
using CarRental.WebApi.Services.Payments;
using CarRental.WebApi.Services.Rentals;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration section is missing.");
var signingKeyFromEnvironment = Environment.GetEnvironmentVariable("CAR_RENTAL_JWT_SIGNING_KEY");
if (!string.IsNullOrWhiteSpace(signingKeyFromEnvironment))
{
    jwtOptions.SigningKey = signingKeyFromEnvironment.Trim();
}

if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
{
    throw new InvalidOperationException("JWT signing key must be at least 32 characters.");
}

if (IsInsecureJwtSigningKey(jwtOptions.SigningKey))
{
    throw new InvalidOperationException(
        "JWT signing key is insecure. Provide a strong key via configuration or CAR_RENTAL_JWT_SIGNING_KEY.");
}

builder.Services.AddSingleton<IOptions<JwtOptions>>(Options.Create(jwtOptions));

var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'Postgres' is missing.");

builder.Services.AddDbContextPool<RentalDbContext>(options =>
    options.UseNpgsql(postgresConnectionString));

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IContractNumberService, ContractNumberService>();
builder.Services.AddScoped<IRentalService, RentalService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IDamageService, DamageService>();
builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = true;
        options.SaveToken = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(ApiAuthorization.ConfigurePolicies);

builder.Services.AddCors(options =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    options.AddPolicy(ApiAuthorization.FrontendCorsPolicy, policy =>
    {
        if (origins.Length > 0)
        {
            policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
            return;
        }

        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod |
                            HttpLoggingFields.RequestPath |
                            HttpLoggingFields.ResponseStatusCode |
                            HttpLoggingFields.Duration;
});
builder.Services.AddProblemDetails();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "CarRental Web API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    });
    options.AddSecurityRequirement(new()
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpLogging();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors(ApiAuthorization.FrontendCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await EnsureDatabaseAsync(app);

await app.RunAsync();

static async Task EnsureDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseBootstrap");
    var dbContext = scope.ServiceProvider.GetRequiredService<RentalDbContext>();
    logger.LogInformation("Applying database migrations.");
    await dbContext.Database.MigrateAsync();
    DatabaseInitializer.Seed(dbContext);
    logger.LogInformation("Database is ready.");
}

static bool IsInsecureJwtSigningKey(string signingKey)
{
    return signingKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase) ||
           signingKey.Contains("DEV_ONLY", StringComparison.OrdinalIgnoreCase) ||
           signingKey.Contains("__SET", StringComparison.OrdinalIgnoreCase);
}

internal static class ApiAuthorization
{
    public const string FrontendCorsPolicy = "FrontendCors";
    public const string ManageRentals = "ManageRentals";
    public const string ManagePayments = "ManagePayments";
    public const string ManageClients = "ManageClients";
    public const string ManageFleet = "ManageFleet";
    public const string ManagePricing = "ManagePricing";
    public const string ManageEmployees = "ManageEmployees";
    public const string ManageMaintenance = "ManageMaintenance";
    public const string ManageDamages = "ManageDamages";
    public const string ExportReports = "ExportReports";
    public const string DeleteRecords = "DeleteRecords";

    public static void ConfigurePolicies(AuthorizationOptions options)
    {
        options.AddPolicy(ManageRentals, policy =>
            policy.RequireRole("User", "Manager", "Admin"));
        options.AddPolicy(ManagePayments, policy =>
            policy.RequireRole("Manager", "Admin"));
        options.AddPolicy(ManageClients, policy =>
            policy.RequireRole("Manager", "Admin"));
        options.AddPolicy(ManageFleet, policy =>
            policy.RequireRole("Manager", "Admin"));
        options.AddPolicy(ManagePricing, policy =>
            policy.RequireRole("Admin"));
        options.AddPolicy(ManageEmployees, policy =>
            policy.RequireRole("Admin"));
        options.AddPolicy(ManageMaintenance, policy =>
            policy.RequireRole("Manager", "Admin"));
        options.AddPolicy(ManageDamages, policy =>
            policy.RequireRole("Manager", "Admin"));
        options.AddPolicy(ExportReports, policy =>
            policy.RequireRole("Manager", "Admin"));
        options.AddPolicy(DeleteRecords, policy =>
            policy.RequireRole("Admin"));
    }
}

public partial class Program;
