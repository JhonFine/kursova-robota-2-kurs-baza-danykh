using CarRental.WebApi.Data;
using CarRental.WebApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using System.Security.Cryptography;

namespace CarRental.WebApi.Services.Damages;

public sealed class DamageService(RentalDbContext dbContext, ILogger<DamageService>? logger = null) : IDamageService
{
    private readonly ILogger<DamageService> logger = logger ?? NullLogger<DamageService>.Instance;
    private const int MaxActNumberSaveAttempts = 3;

    // РђРєС‚ РїРѕС€РєРѕРґР¶РµРЅРЅСЏ РјРѕР¶Рµ С–СЃРЅСѓРІР°С‚Рё СЏРє СЃР°Рј РїРѕ СЃРѕР±С–, С‚Р°Рє С– РІ РїСЂРёРІ'СЏР·С†С– РґРѕ РѕСЂРµРЅРґРё.
    // РЇРєС‰Рѕ РІРІС–РјРєРЅРµРЅРѕ autoCharge, С‚СѓС‚ Р¶Рµ РїРµСЂРµРЅРѕСЃРёРјРѕ СЃСѓРјСѓ РІ Р±Р°Р»Р°РЅСЃ РґРѕРіРѕРІРѕСЂСѓ.
    public async Task<DamageResult> AddDamageAsync(DamageRequest request, CancellationToken cancellationToken = default)
    {
        if (request.RepairCost <= 0)
        {
            return new DamageResult(false, "Р’Р°СЂС‚С–СЃС‚СЊ СЂРµРјРѕРЅС‚Сѓ РјР°С” Р±СѓС‚Рё Р±С–Р»СЊС€РѕСЋ Р·Р° РЅСѓР»СЊ.");
        }

        var vehicle = await dbContext.Vehicles.FirstOrDefaultAsync(item => item.Id == request.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            return new DamageResult(false, "РђРІС‚Рѕ РЅРµ Р·РЅР°Р№РґРµРЅРѕ.");
        }

        Rental? rental = null;
        if (request.RentalId.HasValue)
        {
            rental = await dbContext.Rentals.FirstOrDefaultAsync(item => item.Id == request.RentalId.Value, cancellationToken);
            if (rental is null)
            {
                return new DamageResult(false, "РћСЂРµРЅРґСѓ РЅРµ Р·РЅР°Р№РґРµРЅРѕ.");
            }

            if (rental.VehicleId != request.VehicleId)
            {
                return new DamageResult(false, "РћР±СЂР°РЅР° РѕСЂРµРЅРґР° РЅРµ РІС–РґРїРѕРІС–РґР°С” С†СЊРѕРјСѓ Р°РІС‚Рѕ.");
            }
        }

        if (!await dbContext.Employees.AnyAsync(item => item.Id == request.ReportedByEmployeeId, cancellationToken))
        {
            return new DamageResult(false, "РџСЂР°С†С–РІРЅРёРєР° РЅРµ Р·РЅР°Р№РґРµРЅРѕ.");
        }

        var normalizedPhotoPaths = request.ResolvedPhotoPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .ToList();

        var damage = new Damage
        {
            VehicleId = request.VehicleId,
            RentalId = request.RentalId,
            ReportedByEmployeeId = request.ReportedByEmployeeId,
            Description = request.Description.Trim(),
            DateReported = DateTime.UtcNow,
            RepairCost = request.RepairCost,
            DamageActNumber = GenerateActNumber(),
            ChargedAmount = 0m,
            StatusId = DamageStatus.Open,
            Photos = normalizedPhotoPaths
                .Select((path, index) => new DamagePhoto
                {
                    StoredPath = path,
                    SortOrder = index
                })
                .ToList()
        };

        if (request.AutoChargeToRental && rental is not null)
        {
            rental.TotalAmount += request.RepairCost;
            damage.ChargedAmount = request.RepairCost;
            damage.StatusId = DamageStatus.Charged;
        }

        dbContext.Damages.Add(damage);

        // РќРѕРјРµСЂ Р°РєС‚Сѓ РјР°С” Р±СѓС‚Рё СѓРЅС–РєР°Р»СЊРЅРёРј, Р°Р»Рµ РіРµРЅРµСЂР°С†С–СЏ СЃРїРµС†С–Р°Р»СЊРЅРѕ Р»РёС€Р°С”С‚СЊСЃСЏ
        // РІРёРїР°РґРєРѕРІРѕСЋ, С‚РѕРјСѓ РєРѕР»С–Р·С–С— РїРµСЂРµР¶РёРІР°С”РјРѕ С‚СѓС‚ Р»РѕРєР°Р»СЊРЅРёРј retry Р·Р°РјС–СЃС‚СЊ РїР°РґС–РЅРЅСЏ РІСЃС–С”С— РѕРїРµСЂР°С†С–С—.
        for (var attempt = 1; attempt <= MaxActNumberSaveAttempts; attempt++)
        {
            try
            {
                logger.LogInformation(
                    "Persisting damage act. VehicleId={VehicleId} RentalId={RentalId} ReportedByEmployeeId={ReportedByEmployeeId} AutoChargeToRental={AutoChargeToRental} PhotoCount={PhotoCount} DamageActNumber ={ActNumber} Attempt={Attempt}",
                    request.VehicleId,
                    request.RentalId,
                    request.ReportedByEmployeeId,
                    request.AutoChargeToRental,
                    normalizedPhotoPaths.Count,
                    damage.DamageActNumber,
                    attempt);

                await dbContext.SaveChangesAsync(cancellationToken);
                return new DamageResult(true, $"РџРѕС€РєРѕРґР¶РµРЅРЅСЏ Р·Р°С„С–РєСЃРѕРІР°РЅРѕ, Р°РєС‚ в„–{damage.DamageActNumber}.", damage.Id);
            }
            catch (DbUpdateException exception) when (IsActNumberConflict(exception) && attempt < MaxActNumberSaveAttempts)
            {
                logger.LogWarning(
                    exception,
                    "Damage act number collision. VehicleId={VehicleId} RentalId={RentalId} PreviousActNumber={ActNumber} Attempt={Attempt}",
                    request.VehicleId,
                    request.RentalId,
                    damage.DamageActNumber,
                    attempt);

                damage.DamageActNumber = GenerateActNumber();
            }
            catch (DbUpdateException exception) when (TryMapPersistenceFailure(exception, out var errorMessage))
            {
                var postgresException = exception.InnerException as PostgresException;
                logger.LogWarning(
                    exception,
                    "Failed to persist damage act. VehicleId={VehicleId} RentalId={RentalId} ReportedByEmployeeId={ReportedByEmployeeId} AutoChargeToRental={AutoChargeToRental} PhotoCount={PhotoCount} DamageActNumber ={ActNumber} SqlState={SqlState} ConstraintName={ConstraintName}",
                    request.VehicleId,
                    request.RentalId,
                    request.ReportedByEmployeeId,
                    request.AutoChargeToRental,
                    normalizedPhotoPaths.Count,
                    damage.DamageActNumber,
                    postgresException?.SqlState,
                    postgresException?.ConstraintName);

                return new DamageResult(false, errorMessage);
            }
        }

        logger.LogWarning(
            "Failed to generate unique damage act number after {Attempts} attempts. VehicleId={VehicleId} RentalId={RentalId} ReportedByEmployeeId={ReportedByEmployeeId}",
            MaxActNumberSaveAttempts,
            request.VehicleId,
            request.RentalId,
            request.ReportedByEmployeeId);
        return new DamageResult(false, "РќРµ РІРґР°Р»РѕСЃСЏ Р·РіРµРЅРµСЂСѓРІР°С‚Рё СѓРЅС–РєР°Р»СЊРЅРёР№ РЅРѕРјРµСЂ Р°РєС‚Сѓ. РЎРїСЂРѕР±СѓР№С‚Рµ С‰Рµ СЂР°Р·.");
    }

    private static string GenerateActNumber()
    {
        var randomSuffix = RandomNumberGenerator.GetInt32(100000, 1000000);
        return $"ACT-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{randomSuffix}";
    }

    private static bool IsActNumberConflict(DbUpdateException exception)
        => exception.InnerException is PostgresException postgresException
           && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
           && string.Equals(postgresException.ConstraintName, "IX_Damages_ActNumber", StringComparison.OrdinalIgnoreCase);

    private static bool TryMapPersistenceFailure(DbUpdateException exception, out string errorMessage)
    {
        // РљРѕРЅРІРµСЂС‚СѓС”РјРѕ РЅРёР·СЊРєРѕСЂС–РІРЅРµРІС– PostgreSQL constraint errors Сѓ РїРѕРІС–РґРѕРјР»РµРЅРЅСЏ,
        // СЏРєС– РјРѕР¶РЅР° Р±РµР·РїРµС‡РЅРѕ РІС–РґРґР°С‚Рё UI Р±РµР· РїСЂРёРІ'СЏР·РєРё РґРѕ РІРЅСѓС‚СЂС–С€РЅС–С… РЅР°Р·РІ С‚Р°Р±Р»РёС†СЊ.
        if (exception.InnerException is not PostgresException postgresException)
        {
            errorMessage = string.Empty;
            return false;
        }

        switch (postgresException.SqlState)
        {
            case PostgresErrorCodes.ForeignKeyViolation:
                errorMessage = string.Equals(postgresException.ConstraintName, "FK_Damages_DamageStatuses_Status", StringComparison.OrdinalIgnoreCase)
                    ? "РќРµ РІРґР°Р»РѕСЃСЏ Р·Р±РµСЂРµРіС‚Рё Р°РєС‚ С‡РµСЂРµР· РЅРµСѓР·РіРѕРґР¶РµРЅС– РґРѕРІС–РґРЅРёРєРё СЃС‚Р°С‚СѓСЃС–РІ РїРѕС€РєРѕРґР¶РµРЅСЊ. РџРµСЂРµР·Р°РїСѓСЃС‚С–С‚СЊ API С‚Р° СЃРїСЂРѕР±СѓР№С‚Рµ С‰Рµ СЂР°Р·."
                    : "РќРµ РІРґР°Р»РѕСЃСЏ Р·Р±РµСЂРµРіС‚Рё Р°РєС‚ С‡РµСЂРµР· РЅРµСѓР·РіРѕРґР¶РµРЅС– РґР°РЅС– Р°РІС‚Рѕ, РѕСЂРµРЅРґРё Р°Р±Рѕ СЃРїС–РІСЂРѕР±С–С‚РЅРёРєР°. РћРЅРѕРІС–С‚СЊ СЃС‚РѕСЂС–РЅРєСѓ С‚Р° СЃРїСЂРѕР±СѓР№С‚Рµ С‰Рµ СЂР°Р·.";
                return true;

            case PostgresErrorCodes.CheckViolation:
                errorMessage = "РќРµ РІРґР°Р»РѕСЃСЏ Р·Р±РµСЂРµРіС‚Рё Р°РєС‚ С‡РµСЂРµР· РЅРµРєРѕСЂРµРєС‚РЅС– Р·РЅР°С‡РµРЅРЅСЏ РІР°СЂС‚РѕСЃС‚С– СЂРµРјРѕРЅС‚Сѓ Р°Р±Рѕ СЃС‚Р°С‚СѓСЃСѓ.";
                return true;

            case PostgresErrorCodes.UniqueViolation:
                errorMessage = IsActNumberConflict(exception)
                    ? "РќРµ РІРґР°Р»РѕСЃСЏ Р·РіРµРЅРµСЂСѓРІР°С‚Рё СѓРЅС–РєР°Р»СЊРЅРёР№ РЅРѕРјРµСЂ Р°РєС‚Сѓ. РЎРїСЂРѕР±СѓР№С‚Рµ С‰Рµ СЂР°Р·."
                    : "РќРµ РІРґР°Р»РѕСЃСЏ Р·Р±РµСЂРµРіС‚Рё Р°РєС‚ С‡РµСЂРµР· РєРѕРЅС„Р»С–РєС‚ РґР°РЅРёС…. РЎРїСЂРѕР±СѓР№С‚Рµ С‰Рµ СЂР°Р·.";
                return true;

            default:
                errorMessage = string.Empty;
                return false;
        }
    }
}


