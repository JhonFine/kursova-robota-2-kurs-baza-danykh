using CarRental.Desktop.Data;
using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Analytics;
using ClosedXML.Excel;
using FluentAssertions;

namespace CarRental.Desktop.Tests;

public sealed class AnalyticsExportServiceTests
{
    [Fact]
    public async Task ExportRentalsCsvAsync_ShouldWriteUtf8BomAndUkrainianHeaders()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);

        var exportDirectory = Path.Combine(Path.GetTempPath(), "car-rental-exports-tests", Guid.NewGuid().ToString("N"));

        try
        {
            var service = new AnalyticsExportService(dbContext, exportDirectory);

            var path = await service.ExportRentalsCsvAsync(new ExportRequest(
                AsUnspecified(DateTime.Today.AddDays(-2)),
                AsUnspecified(DateTime.Today.AddDays(1))));

            File.Exists(path).Should().BeTrue();

            var bytes = await File.ReadAllBytesAsync(path);
            bytes.Take(3).Should().Equal((byte)0xEF, (byte)0xBB, (byte)0xBF);

            var text = await File.ReadAllTextAsync(path);
            text.Should().StartWith("РќРѕРјРµСЂР”РѕРіРѕРІРѕСЂСѓ,Р”Р°С‚Р°РџРѕС‡Р°С‚РєСѓ,Р”Р°С‚Р°РљС–РЅС†СЏ,РљР»С–С”РЅС‚,РђРІС‚Рѕ,РњРµРЅРµРґР¶РµСЂ,РЎС‚Р°С‚СѓСЃ,РЎСѓРјР°");
        }
        finally
        {
            if (Directory.Exists(exportDirectory))
            {
                Directory.Delete(exportDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExportRentalsExcelAsync_ShouldUseUkrainianWorksheetHeaders()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();
        SeedMinimalData(dbContext);

        var exportDirectory = Path.Combine(Path.GetTempPath(), "car-rental-exports-tests", Guid.NewGuid().ToString("N"));

        try
        {
            var service = new AnalyticsExportService(dbContext, exportDirectory);

            var path = await service.ExportRentalsExcelAsync(new ExportRequest(
                AsUnspecified(DateTime.Today.AddDays(-2)),
                AsUnspecified(DateTime.Today.AddDays(1))));

            File.Exists(path).Should().BeTrue();

            using var workbook = new XLWorkbook(path);
            var worksheet = workbook.Worksheets.Single();
            worksheet.Name.Should().Be("РћСЂРµРЅРґРё");
            worksheet.Cell(1, 1).GetString().Should().Be("Р”РѕРіРѕРІС–СЂ");
            worksheet.Cell(1, 2).GetString().Should().Be("РџРѕС‡Р°С‚РѕРє");
            worksheet.Cell(1, 3).GetString().Should().Be("РљС–РЅРµС†СЊ");
            worksheet.Cell(1, 4).GetString().Should().Be("РљР»С–С”РЅС‚");
            worksheet.Cell(1, 5).GetString().Should().Be("РђРІС‚Рѕ");
            worksheet.Cell(1, 6).GetString().Should().Be("РњРµРЅРµРґР¶РµСЂ");
            worksheet.Cell(1, 7).GetString().Should().Be("РЎС‚Р°С‚СѓСЃ");
            worksheet.Cell(1, 8).GetString().Should().Be("РЎСѓРјР°");
        }
        finally
        {
            if (Directory.Exists(exportDirectory))
            {
                Directory.Delete(exportDirectory, recursive: true);
            }
        }
    }

    private static void SeedMinimalData(RentalDbContext dbContext)
    {
        TestLookupSeed.SeedVehicleLookups(dbContext);

        var account = new Account
        {
            Id = 501,
            Login = "manager.export",
            PasswordHash = "x",
            IsActive = true
        };

        dbContext.Accounts.Add(account);
        dbContext.Employees.Add(new Employee
        {
            Id = 501,
            FullName = "Manager Export",
            RoleId = UserRole.Manager,
            Account = account
        });
        dbContext.Clients.Add(new Client
        {
            Id = 601,
            FullName = "Client Export",
            PassportData = "PP-601",
            DriverLicense = "DL-601",
            PassportExpirationDate = DateTime.UtcNow.AddYears(5),
            DriverLicenseExpirationDate = DateTime.UtcNow.AddYears(5),
            Phone = "+380500006001",
            IsBlacklisted = false
        });
        dbContext.Vehicles.Add(TestLookupSeed.CreateVehicle(
            dbContext,
            "Audi",
            "A4",
            "AA0701AA",
            "PETROL",
            "AUTO",
            2m,
            480m,
            7m,
            1000,
            100m,
            id: 701));
        var rentalStartDate = AsUnspecified(DateTime.Today.AddDays(-1));
        var rentalEndDate = AsUnspecified(DateTime.Today);

        dbContext.Rentals.Add(new Rental
        {
            Id = 801,
            ClientId = 601,
            VehicleId = 701,
            CreatedByEmployeeId = 501,
            ClosedByEmployeeId = 501,
            ContractNumber = "CR-2026-000801",
            StartDate = rentalStartDate,
            EndDate = rentalEndDate,
            StartMileage = 1000,
            TotalAmount = 100m,
            StatusId = RentalStatus.Closed,
            ClosedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        });
        dbContext.SaveChanges();
    }

    private static DateTime AsUnspecified(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
}
