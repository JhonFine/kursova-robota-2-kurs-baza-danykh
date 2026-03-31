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

    public async Task<DamageResult> AddDamageAsync(DamageRequest request, CancellationToken cancellationToken = default)
    {
        if (request.RepairCost <= 0)
        {
            return new DamageResult(false, "Вартість ремонту має бути більшою за нуль.");
        }

        var vehicle = await dbContext.Vehicles.FirstOrDefaultAsync(item => item.Id == request.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            return new DamageResult(false, "Авто не знайдено.");
        }

        Rental? rental = null;
        if (request.RentalId.HasValue)
        {
            rental = await dbContext.Rentals.FirstOrDefaultAsync(item => item.Id == request.RentalId.Value, cancellationToken);
            if (rental is null)
            {
                return new DamageResult(false, "Оренду не знайдено.");
            }

            if (rental.VehicleId != request.VehicleId)
            {
                return new DamageResult(false, "Обрана оренда не відповідає цьому авто.");
            }
        }

        if (!await dbContext.Employees.AnyAsync(item => item.Id == request.ReportedByEmployeeId, cancellationToken))
        {
            return new DamageResult(false, "Працівника не знайдено.");
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
            ActNumber = GenerateActNumber(),
            ChargedAmount = 0m,
            Status = DamageStatus.Open,
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
            damage.Status = DamageStatus.Charged;
        }

        dbContext.Damages.Add(damage);

        for (var attempt = 1; attempt <= MaxActNumberSaveAttempts; attempt++)
        {
            try
            {
                logger.LogInformation(
                    "Persisting damage act. VehicleId={VehicleId} RentalId={RentalId} ReportedByEmployeeId={ReportedByEmployeeId} AutoChargeToRental={AutoChargeToRental} PhotoCount={PhotoCount} ActNumber={ActNumber} Attempt={Attempt}",
                    request.VehicleId,
                    request.RentalId,
                    request.ReportedByEmployeeId,
                    request.AutoChargeToRental,
                    normalizedPhotoPaths.Count,
                    damage.ActNumber,
                    attempt);

                await dbContext.SaveChangesAsync(cancellationToken);
                return new DamageResult(true, $"Пошкодження зафіксовано, акт №{damage.ActNumber}.", damage.Id);
            }
            catch (DbUpdateException exception) when (IsActNumberConflict(exception) && attempt < MaxActNumberSaveAttempts)
            {
                logger.LogWarning(
                    exception,
                    "Damage act number collision. VehicleId={VehicleId} RentalId={RentalId} PreviousActNumber={ActNumber} Attempt={Attempt}",
                    request.VehicleId,
                    request.RentalId,
                    damage.ActNumber,
                    attempt);

                damage.ActNumber = GenerateActNumber();
            }
            catch (DbUpdateException exception) when (TryMapPersistenceFailure(exception, out var errorMessage))
            {
                var postgresException = exception.InnerException as PostgresException;
                logger.LogWarning(
                    exception,
                    "Failed to persist damage act. VehicleId={VehicleId} RentalId={RentalId} ReportedByEmployeeId={ReportedByEmployeeId} AutoChargeToRental={AutoChargeToRental} PhotoCount={PhotoCount} ActNumber={ActNumber} SqlState={SqlState} ConstraintName={ConstraintName}",
                    request.VehicleId,
                    request.RentalId,
                    request.ReportedByEmployeeId,
                    request.AutoChargeToRental,
                    normalizedPhotoPaths.Count,
                    damage.ActNumber,
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
        return new DamageResult(false, "Не вдалося згенерувати унікальний номер акту. Спробуйте ще раз.");
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
        if (exception.InnerException is not PostgresException postgresException)
        {
            errorMessage = string.Empty;
            return false;
        }

        switch (postgresException.SqlState)
        {
            case PostgresErrorCodes.ForeignKeyViolation:
                errorMessage = string.Equals(postgresException.ConstraintName, "FK_Damages_DamageStatuses_Status", StringComparison.OrdinalIgnoreCase)
                    ? "Не вдалося зберегти акт через неузгоджені довідники статусів пошкоджень. Перезапустіть API та спробуйте ще раз."
                    : "Не вдалося зберегти акт через неузгоджені дані авто, оренди або співробітника. Оновіть сторінку та спробуйте ще раз.";
                return true;

            case PostgresErrorCodes.CheckViolation:
                errorMessage = "Не вдалося зберегти акт через некоректні значення вартості ремонту або статусу.";
                return true;

            case PostgresErrorCodes.UniqueViolation:
                errorMessage = IsActNumberConflict(exception)
                    ? "Не вдалося згенерувати унікальний номер акту. Спробуйте ще раз."
                    : "Не вдалося зберегти акт через конфлікт даних. Спробуйте ще раз.";
                return true;

            default:
                errorMessage = string.Empty;
                return false;
        }
    }
}
