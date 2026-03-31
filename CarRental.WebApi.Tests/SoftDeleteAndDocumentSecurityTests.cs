using System.Security.Claims;
using System.IO;
using CarRental.WebApi.Contracts;
using CarRental.WebApi.Controllers;
using CarRental.WebApi.Data;
using CarRental.WebApi.Models;
using CarRental.WebApi.Services.Damages;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRental.WebApi.Tests;

public sealed class SoftDeleteAndDocumentSecurityTests
{
    private const int MaxDamagePhotosPerAct = 5;
    private const string DamagePhotoStoragePrefix = "/protected/damages";

    [Fact]
    public async Task SoftDeletedEntities_ShouldBeHiddenFromLists_ButHistoricalRentalShouldKeepNames()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        TestLookupSeed.SeedVehicleLookups(dbContext);

        dbContext.Employees.Add(new Employee
        {
            Id = 1,
            FullName = "Manager User",
            Login = "manager",
            PasswordHash = "x",
            Role = UserRole.Manager,
            IsActive = true
        });
        dbContext.Clients.Add(new Client
        {
            Id = 10,
            FullName = "Deleted Client",
            PassportData = "PP-010",
            DriverLicense = "DL-010",
            PassportExpirationDate = DateTime.UtcNow.AddYears(5),
            DriverLicenseExpirationDate = DateTime.UtcNow.AddYears(5),
            Phone = "+380500000010",
            Blacklisted = false
        });
        dbContext.Vehicles.Add(new Vehicle
        {
            Id = 20,
            Make = "Tesla",
            Model = "Model 3",
            FuelType = "Електро",
            TransmissionType = "Автомат",
            PowertrainCapacityValue = 75m,
            PowertrainCapacityUnit = "KWH",
            CargoCapacityValue = 425m,
            CargoCapacityUnit = "L",
            ConsumptionValue = 16m,
            ConsumptionUnit = "KWH_PER_100KM",
            LicensePlate = "AA2020AA",
            Mileage = 12000,
            DailyRate = 3000m,
            IsAvailable = true,
            ServiceIntervalKm = 10000
        });
        dbContext.Rentals.Add(new Rental
        {
            Id = 100,
            ClientId = 10,
            VehicleId = 20,
            EmployeeId = 1,
            ContractNumber = "CR-2026-500001",
            StartDate = DateTime.UtcNow.AddDays(-2),
            EndDate = DateTime.UtcNow.AddDays(-1),
            StartMileage = 12000,
            TotalAmount = 3000m,
            Status = RentalStatus.Closed,
            IsClosed = true,
            ClosedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        dbContext.ChangeTracker.Clear();

        var client = await dbContext.Clients.FirstAsync(item => item.Id == 10);
        var vehicle = await dbContext.Vehicles.FirstAsync(item => item.Id == 20);
        dbContext.Clients.Remove(client);
        dbContext.Vehicles.Remove(vehicle);
        await dbContext.SaveChangesAsync();

        (await dbContext.Clients.CountAsync()).Should().Be(0);
        (await dbContext.Vehicles.CountAsync()).Should().Be(0);
        (await dbContext.Clients.IgnoreQueryFilters()
            .CountAsync(item => item.Id == 10 && item.IsDeleted)).Should().Be(1);
        (await dbContext.Vehicles.IgnoreQueryFilters()
            .CountAsync(item => item.Id == 20 && item.IsDeleted)).Should().Be(1);

        var rentalsController = CreateRentalsController(dbContext);
        var result = await rentalsController.GetById(100, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<RentalDto>().Subject;
        dto.ClientName.Should().Be("Deleted Client");
        dto.VehicleName.Should().Contain("Tesla Model 3 [AA2020AA]");
    }

    [Fact]
    public async Task ClientsCreate_ShouldRejectUnsafeDocumentPhotoPaths()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        var controller = new ClientsController(dbContext)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.Create(
            new ClientUpsertRequest
            {
                FullName = "Unsafe Client",
                PassportData = "PP-111",
                PassportPhotoPath = "C:\\secret\\passport.jpg",
                DriverLicense = "DL-111",
                DriverLicensePhotoPath = "/images/vehicles/car.jpg",
                Phone = "+380500000111",
                Blacklisted = false
            },
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await dbContext.Clients.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ClientsUploadDocumentPhoto_ShouldStoreFileOutsidePublicRoot()
    {
        var previousRoot = Environment.GetEnvironmentVariable("CAR_RENTAL_DOCUMENTS_ROOT");
        var tempRoot = Path.Combine(Path.GetTempPath(), "car-rental-docs-tests", Guid.NewGuid().ToString("N"));

        try
        {
            Environment.SetEnvironmentVariable("CAR_RENTAL_DOCUMENTS_ROOT", tempRoot);

            await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
            await using var dbContext = testDatabase.CreateDbContext();
            dbContext.Clients.Add(new Client
            {
                Id = 1,
                FullName = "Client",
                PassportData = "PP-001",
                DriverLicense = "DL-001",
                PassportExpirationDate = DateTime.UtcNow.AddYears(5),
                DriverLicenseExpirationDate = DateTime.UtcNow.AddYears(5),
                Phone = "+380500000001",
                Blacklisted = false
            });
            await dbContext.SaveChangesAsync();

            var controller = new ClientsController(dbContext)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            await using var stream = new MemoryStream([1, 2, 3, 4, 5]);
            var file = new FormFile(stream, 0, stream.Length, "file", "passport.jpg")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg"
            };

            var result = await controller.UploadDocumentPhoto(
                1,
                "passport-photo",
                file,
                CancellationToken.None);

            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            var dto = ok.Value.Should().BeOfType<ClientDto>().Subject;
            dto.PassportPhotoPath.Should().StartWith("/protected/documents/clients/1/passport/");

            var persisted = await dbContext.Clients.AsNoTracking().SingleAsync(item => item.Id == 1);
            persisted.PassportPhotoPath.Should().Be(dto.PassportPhotoPath);

            Directory.Exists(tempRoot).Should().BeTrue();
            Directory.GetFiles(tempRoot, "*", SearchOption.AllDirectories).Should().ContainSingle();
            tempRoot.ToLowerInvariant().Should().NotContain("wwwroot");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CAR_RENTAL_DOCUMENTS_ROOT", previousRoot);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DamagesAddMultipart_ShouldStoreMultiplePhotos_AndPersistRows()
    {
        var previousRoot = Environment.GetEnvironmentVariable("CAR_RENTAL_DOCUMENTS_ROOT");
        var tempRoot = Path.Combine(Path.GetTempPath(), "car-rental-damage-tests", Guid.NewGuid().ToString("N"));

        try
        {
            Environment.SetEnvironmentVariable("CAR_RENTAL_DOCUMENTS_ROOT", tempRoot);

            await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
            await using var dbContext = testDatabase.CreateDbContext();
            SeedDamageScenario(dbContext);

            var controller = CreateDamagesController(dbContext);
            await using var stream1 = new MemoryStream([1, 2, 3, 4]);
            await using var stream2 = new MemoryStream([5, 6, 7, 8]);
            var file1 = CreateImageFile(stream1, "damage-1.jpg", "image/jpeg");
            var file2 = CreateImageFile(stream2, "damage-2.png", "image/png");

            var result = await controller.AddMultipart(
                new DamagesController.AddDamageMultipartRequest
                {
                    VehicleId = 1,
                    Description = "Door scratch",
                    RepairCost = 1200m,
                    AutoChargeToRental = false,
                    Photos = [file1, file2]
                },
                CancellationToken.None);

            var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
            var dto = created.Value.Should().BeOfType<DamageDto>().Subject;
            dto.Photos.Should().HaveCount(2);
            dto.Photos.Select(item => item.StoredPath).Should().OnlyContain(path => path.StartsWith("/protected/damages/vehicles/1/"));

            var persistedDamage = await dbContext.Damages
                .AsNoTracking()
                .Include(item => item.Photos)
                .SingleAsync();
            persistedDamage.Photos.Should().HaveCount(2);

            Directory.Exists(tempRoot).Should().BeTrue();
            Directory.GetFiles(tempRoot, "*", SearchOption.AllDirectories).Should().HaveCount(2);
            tempRoot.ToLowerInvariant().Should().NotContain("wwwroot");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CAR_RENTAL_DOCUMENTS_ROOT", previousRoot);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DamagesAddMultipart_ShouldAllowCreateWithoutPhotos()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedDamageScenario(dbContext);

        var controller = CreateDamagesController(dbContext);

        var result = await controller.AddMultipart(
            new DamagesController.AddDamageMultipartRequest
            {
                VehicleId = 1,
                Description = "Mirror scratch",
                RepairCost = 350m,
                AutoChargeToRental = false
            },
            CancellationToken.None);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var dto = created.Value.Should().BeOfType<DamageDto>().Subject;
        dto.Photos.Should().BeEmpty();
        dto.PhotoPath.Should().BeNull();

        var damage = await dbContext.Damages
            .AsNoTracking()
            .Include(item => item.Photos)
            .SingleAsync();
        damage.Photos.Should().BeEmpty();
    }

    [Fact]
    public async Task DamagesAddMultipart_ShouldDeleteStoredFiles_WhenValidationFails()
    {
        var previousRoot = Environment.GetEnvironmentVariable("CAR_RENTAL_DOCUMENTS_ROOT");
        var tempRoot = Path.Combine(Path.GetTempPath(), "car-rental-damage-tests", Guid.NewGuid().ToString("N"));

        try
        {
            Environment.SetEnvironmentVariable("CAR_RENTAL_DOCUMENTS_ROOT", tempRoot);

            await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
            await using var dbContext = testDatabase.CreateDbContext();
            SeedDamageScenario(dbContext);

            dbContext.Vehicles.Add(new Vehicle
            {
                Id = 2,
                Make = "BMW",
                Model = "M5",
                FuelType = "Бензин",
                TransmissionType = "Автомат",
                PowertrainCapacityValue = 4.4m,
                PowertrainCapacityUnit = "L",
                CargoCapacityValue = 530m,
                CargoCapacityUnit = "L",
                ConsumptionValue = 11m,
                ConsumptionUnit = "L_PER_100KM",
                LicensePlate = "AA0022AA",
                Mileage = 1500,
                DailyRate = 120m,
                IsBookable = true,
                IsAvailable = true,
                ServiceIntervalKm = 10000
            });
            dbContext.Rentals.Add(new Rental
            {
                Id = 50,
                ClientId = 1,
                VehicleId = 2,
                EmployeeId = 1,
                ContractNumber = "CR-2026-000079",
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow.AddDays(1),
                PickupLocation = "Kyiv",
                ReturnLocation = "Kyiv",
                StartMileage = 1500,
                TotalAmount = 500m,
                Status = RentalStatus.Active,
                IsClosed = false,
                CreatedAtUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();

            var controller = CreateDamagesController(dbContext);
            await using var stream = new MemoryStream([1, 2, 3, 4]);
            var file = CreateImageFile(stream, "damage.jpg", "image/jpeg");

            var result = await controller.AddMultipart(
                new DamagesController.AddDamageMultipartRequest
                {
                    VehicleId = 1,
                    RentalId = 50,
                    Description = "Wrong rental for vehicle",
                    RepairCost = 450m,
                    AutoChargeToRental = false,
                    Photos = [file]
                },
                CancellationToken.None);

            result.Should().BeOfType<BadRequestObjectResult>();
            (await dbContext.Damages.CountAsync()).Should().Be(0);
            Directory.Exists(tempRoot).Should().BeTrue();
            Directory.GetFiles(tempRoot, "*", SearchOption.AllDirectories).Should().BeEmpty();
        }
        finally
        {
            Environment.SetEnvironmentVariable("CAR_RENTAL_DOCUMENTS_ROOT", previousRoot);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DamagesAddMultipart_ShouldRejectInvalidFileType()
    {
        var previousRoot = Environment.GetEnvironmentVariable("CAR_RENTAL_DOCUMENTS_ROOT");
        var tempRoot = Path.Combine(Path.GetTempPath(), "car-rental-damage-tests", Guid.NewGuid().ToString("N"));

        try
        {
            Environment.SetEnvironmentVariable("CAR_RENTAL_DOCUMENTS_ROOT", tempRoot);

            await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
            await using var dbContext = testDatabase.CreateDbContext();
            SeedDamageScenario(dbContext);

            var controller = CreateDamagesController(dbContext);
            await using var stream = new MemoryStream([1, 2, 3]);
            var file = CreateImageFile(stream, "notes.txt", "text/plain");

            var result = await controller.AddMultipart(
                new DamagesController.AddDamageMultipartRequest
                {
                    VehicleId = 1,
                    Description = "Invalid file type",
                    RepairCost = 100m,
                    AutoChargeToRental = false,
                    Photos = [file]
                },
                CancellationToken.None);

            result.Should().BeOfType<BadRequestObjectResult>();
            (await dbContext.Damages.CountAsync()).Should().Be(0);
            Directory.Exists(tempRoot).Should().BeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("CAR_RENTAL_DOCUMENTS_ROOT", previousRoot);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DamagesAddMultipart_ShouldRejectMoreThanFivePhotos()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedDamageScenario(dbContext);

        var controller = CreateDamagesController(dbContext);
        var files = new List<IFormFile>();
        var streams = new List<MemoryStream>();

        try
        {
            for (var index = 0; index < MaxDamagePhotosPerAct + 1; index++)
            {
                var stream = new MemoryStream([1, 2, 3, 4]);
                streams.Add(stream);
                files.Add(CreateImageFile(stream, $"damage-{index}.jpg", "image/jpeg"));
            }

            var result = await controller.AddMultipart(
                new DamagesController.AddDamageMultipartRequest
                {
                    VehicleId = 1,
                    Description = "Too many photos",
                    RepairCost = 500m,
                    AutoChargeToRental = false,
                    Photos = files
                },
                CancellationToken.None);

            result.Should().BeOfType<BadRequestObjectResult>();
            (await dbContext.Damages.CountAsync()).Should().Be(0);
        }
        finally
        {
            foreach (var stream in streams)
            {
                await stream.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task DamagesGetPhoto_ShouldStreamProtectedDamagePhoto()
    {
        var previousRoot = Environment.GetEnvironmentVariable("CAR_RENTAL_DOCUMENTS_ROOT");
        var tempRoot = Path.Combine(Path.GetTempPath(), "car-rental-damage-tests", Guid.NewGuid().ToString("N"));

        try
        {
            Environment.SetEnvironmentVariable("CAR_RENTAL_DOCUMENTS_ROOT", tempRoot);

            await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
            await using var dbContext = testDatabase.CreateDbContext();
            SeedDamageScenario(dbContext);

            var relativeDirectory = Path.Combine(tempRoot, "vehicles", "1");
            Directory.CreateDirectory(relativeDirectory);
            var fullPath = Path.Combine(relativeDirectory, "existing-photo.jpg");
            await File.WriteAllBytesAsync(fullPath, [1, 2, 3, 4, 5]);

            dbContext.Damages.Add(new Damage
            {
                Id = 10,
                VehicleId = 1,
                ReportedByEmployeeId = 1,
                Description = "Existing damage",
                RepairCost = 900m,
                ActNumber = "ACT-20260331000000000-100000",
                ChargedAmount = 0m,
                Status = DamageStatus.Open,
                Photos =
                [
                    new DamagePhoto
                    {
                        Id = 20,
                        StoredPath = $"{DamagePhotoStoragePrefix}/vehicles/1/existing-photo.jpg",
                        SortOrder = 0
                    }
                ]
            });
            await dbContext.SaveChangesAsync();

            var controller = CreateDamagesController(dbContext);
            var result = await controller.GetPhoto(10, 20, CancellationToken.None);

            var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
            fileResult.ContentType.Should().Be("image/jpeg");
            await fileResult.FileStream.DisposeAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable("CAR_RENTAL_DOCUMENTS_ROOT", previousRoot);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static RentalsController CreateRentalsController(RentalDbContext dbContext)
    {
        var controller = new RentalsController(
            dbContext,
            new CarRental.WebApi.Services.Rentals.RentalService(
                dbContext,
                new StubContractNumberService()));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "1"),
                    new Claim(ClaimTypes.Role, UserRole.Manager.ToString())
                ], "TestAuth"))
            }
        };

        return controller;
    }

    private static DamagesController CreateDamagesController(RentalDbContext dbContext)
    {
        var controller = new DamagesController(
            dbContext,
            new DamageService(dbContext));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "1"),
                    new Claim("account_id", "1"),
                    new Claim("employee_id", "1"),
                    new Claim(ClaimTypes.Role, UserRole.Manager.ToString())
                ], "TestAuth"))
            }
        };

        return controller;
    }

    private static FormFile CreateImageFile(MemoryStream stream, string fileName, string contentType)
    {
        return new FormFile(stream, 0, stream.Length, "photos", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static void SeedDamageScenario(RentalDbContext dbContext)
    {
        TestLookupSeed.SeedVehicleLookups(dbContext);

        dbContext.Employees.Add(new Employee
        {
            Id = 1,
            FullName = "Manager User",
            Login = "manager",
            PasswordHash = "x",
            Role = UserRole.Manager,
            IsActive = true
        });
        dbContext.Clients.Add(new Client
        {
            Id = 1,
            FullName = "Client",
            PassportData = "PP-001",
            DriverLicense = "DL-001",
            PassportExpirationDate = DateTime.UtcNow.AddYears(5),
            DriverLicenseExpirationDate = DateTime.UtcNow.AddYears(5),
            Phone = "+380500000001",
            Blacklisted = false
        });
        dbContext.Vehicles.Add(new Vehicle
        {
            Id = 1,
            Make = "Ford",
            Model = "Focus",
            FuelType = "Бензин",
            TransmissionType = "Автомат",
            PowertrainCapacityValue = 2m,
            PowertrainCapacityUnit = "L",
            CargoCapacityValue = 500m,
            CargoCapacityUnit = "L",
            ConsumptionValue = 7m,
            ConsumptionUnit = "L_PER_100KM",
            LicensePlate = "AA0011AA",
            Mileage = 1000,
            DailyRate = 70m,
            IsBookable = true,
            IsAvailable = true,
            ServiceIntervalKm = 10000
        });
        dbContext.SaveChanges();
    }

    private sealed class StubContractNumberService : CarRental.WebApi.Services.Documents.IContractNumberService
    {
        public Task<string> NextNumberAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("CR-2026-999997");
    }
}
