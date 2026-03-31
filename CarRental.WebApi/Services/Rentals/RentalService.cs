using System.Data;
using CarRental.Shared.ReferenceData;
using CarRental.WebApi.Data;
using CarRental.WebApi.Models;
using CarRental.WebApi.Services.Documents;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CarRental.WebApi.Services.Rentals;

public sealed class RentalService(
    RentalDbContext dbContext,
    IContractNumberService contractNumberService) : IRentalService
{
    private const string AutoCancellationRefundNote = "System refund after rental cancellation";
    private const string AutoRescheduleRefundNote = "System refund after rental reschedule";
    private const string AutoBalanceSettlementNote = "Self-service balance settlement";

    // Конфлікт перевіряємо у "бізнес-часі": усі дати попередньо нормалізуються,
    // щоб порівняння не залежали від випадкових timezone-конвертацій клієнта.
    public async Task<bool> HasDateConflictAsync(
        int vehicleId,
        DateTime startDate,
        DateTime endDate,
        int? excludeRentalId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedStart = NormalizeBusinessTimestamp(startDate);
        var normalizedEnd = NormalizeBusinessTimestamp(endDate);

        return await dbContext.Rentals
            .AsNoTracking()
            .AnyAsync(
                rental =>
                    rental.VehicleId == vehicleId &&
                    (!excludeRentalId.HasValue || rental.Id != excludeRentalId.Value) &&
                    rental.Status != RentalStatus.Closed &&
                    rental.Status != RentalStatus.Canceled &&
                    rental.StartDate <= normalizedEnd &&
                    normalizedStart <= rental.EndDate,
                cancellationToken);
    }

    public async Task<CreateRentalResult> CreateRentalAsync(
        CreateRentalRequest request,
        CancellationToken cancellationToken = default)
        => await CreateRentalInternalAsync(request, pendingPayment: null, cancellationToken);

    public async Task<CreateRentalResult> CreateRentalWithPaymentAsync(
        CreateRentalWithPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        return await CreateRentalInternalAsync(
            new CreateRentalRequest(
                request.ClientId,
                request.VehicleId,
                request.CreatedByEmployeeId,
                request.StartDate,
                request.EndDate,
                request.PickupLocation,
                request.ReturnLocation),
            new PendingPayment(request.Method, request.Direction, request.Notes),
            cancellationToken);
    }

    // Закриття оренди не лише змінює статус, а й добудовує фінальний розрахунок:
    // повернення переплати, фінальний inspection і оновлення пробігу авто.
    public async Task<CloseRentalResult> CloseRentalAsync(
        CloseRentalRequest request,
        CancellationToken cancellationToken = default)
    {
        var rental = await dbContext.Rentals
            .Include(item => item.Vehicle)
            .Include(item => item.Damages)
            .Include(item => item.Payments)
            .Include(item => item.Inspections)
            .FirstOrDefaultAsync(item => item.Id == request.RentalId, cancellationToken);
        if (rental is null)
        {
            return new CloseRentalResult(false, "Оренду не знайдено.");
        }

        if (rental.Status == RentalStatus.Closed)
        {
            return new CloseRentalResult(false, "Оренду вже закрито.");
        }

        if (rental.Status == RentalStatus.Canceled)
        {
            return new CloseRentalResult(false, "Скасовану оренду не можна закрити.");
        }

        var normalizedActualEndDate = NormalizeBusinessTimestamp(request.ActualEndDate);
        if (normalizedActualEndDate.Date == rental.StartDate.Date &&
            normalizedActualEndDate < rental.StartDate)
        {
            normalizedActualEndDate = rental.StartDate;
        }

        if (normalizedActualEndDate < rental.StartDate)
        {
            return new CloseRentalResult(false, "Некоректна дата повернення.");
        }

        if (request.EndMileage < rental.StartMileage)
        {
            return new CloseRentalResult(false, "Кінцевий пробіг не може бути меншим за початковий.");
        }

        if (rental.Vehicle is null)
        {
            return new CloseRentalResult(false, "Для цієї оренди не знайдено авто.");
        }

        var rentalDays = Math.Max(1, (normalizedActualEndDate.Date - rental.StartDate.Date).Days + 1);
        var baseAmount = rentalDays * rental.Vehicle.DailyRate;
        var damageCharges = rental.Damages.Sum(item => item.ChargedAmount);
        var totalAmount = baseAmount + damageCharges;

        var paidAmount = CalculateNetPaid(rental.Payments);
        var refundAmount = paidAmount - totalAmount;

        if (refundAmount > 0m)
        {
            var refundPayment = new Payment
            {
                RentalId = rental.Id,
                EmployeeId = request.ClosedByEmployeeId,
                Amount = refundAmount,
                Method = ResolveRefundMethod(rental.Payments),
                Direction = PaymentDirection.Refund,
                Notes = "Автоматичне повернення переплати при закритті оренди.",
                CreatedAtUtc = DateTime.UtcNow
            };
            dbContext.Payments.Add(refundPayment);
        }

        rental.EndDate = normalizedActualEndDate;
        rental.EndMileage = request.EndMileage;
        rental.OverageFee = 0m;
        rental.TotalAmount = totalAmount;
        rental.Status = RentalStatus.Closed;
        rental.ClosedByEmployeeId = request.ClosedByEmployeeId;
        rental.CanceledByEmployeeId = null;
        rental.ClosedAtUtc = DateTime.UtcNow;
        rental.CanceledAtUtc = null;
        rental.CancellationReason = null;
        UpsertInspection(
            rental,
            request.ClosedByEmployeeId,
            RentalInspectionType.Return,
            request.ReturnFuelPercent,
            request.ReturnInspectionNotes);

        rental.Vehicle.Mileage = request.EndMileage;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CloseRentalResult(
            true,
            "Оренду закрито. Пробіг авто оновлено.",
            totalAmount);
    }

    // Скасування booked-оренди може автоматично повернути передоплату,
    // тому тут живе і статусна логіка, і фінансовий side effect.
    public async Task<CancelRentalResult> CancelRentalAsync(
        CancelRentalRequest request,
        CancellationToken cancellationToken = default)
    {
        var rental = await dbContext.Rentals
            .Include(item => item.Vehicle)
            .Include(item => item.Payments)
            .FirstOrDefaultAsync(item => item.Id == request.RentalId, cancellationToken);
        if (rental is null)
        {
            return new CancelRentalResult(false, "Оренду не знайдено.");
        }

        if (rental.Status == RentalStatus.Closed)
        {
            return new CancelRentalResult(false, "Закриту оренду не можна скасувати.");
        }

        if (rental.Status == RentalStatus.Active)
        {
            return new CancelRentalResult(false, "Активну оренду не можна скасувати.");
        }

        if (rental.Status == RentalStatus.Canceled)
        {
            return new CancelRentalResult(false, "Оренду вже скасовано.");
        }

        var normalizedReason = request.Reason.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason))
        {
            return new CancelRentalResult(false, "Вкажіть причину скасування.");
        }

        var originalStatus = rental.Status;
        if (originalStatus == RentalStatus.Booked)
        {
            var netPaid = CalculateNetPaid(rental.Payments);
            if (netPaid > 0m)
            {
                dbContext.Payments.Add(new Payment
                {
                    RentalId = rental.Id,
                    EmployeeId = request.CanceledByEmployeeId,
                    Amount = netPaid,
                    Method = ResolveRefundMethod(rental.Payments),
                    Direction = PaymentDirection.Refund,
                    Notes = BuildSystemNote(AutoCancellationRefundNote, normalizedReason),
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            rental.TotalAmount = 0m;
            rental.OverageFee = 0m;
        }

        rental.Status = RentalStatus.Canceled;
        rental.ClosedByEmployeeId = null;
        rental.CanceledByEmployeeId = request.CanceledByEmployeeId;
        rental.ClosedAtUtc = null;
        rental.CanceledAtUtc = DateTime.UtcNow;
        rental.CancellationReason = normalizedReason;

        await dbContext.SaveChangesAsync(cancellationToken);
        return new CancelRentalResult(true, "Оренду скасовано.");
    }

    // Перенесення дозволене лише для booked-оренди і водночас перераховує
    // різницю в оплаті, щоб після зсуву дат баланс не залишився "старим".
    public async Task<RescheduleRentalResult> RescheduleRentalAsync(
        RescheduleRentalRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = NormalizeBusinessTimestamp(DateTime.UtcNow);
        var normalizedStart = NormalizeBusinessTimestamp(request.StartDate);
        var normalizedEnd = NormalizeBusinessTimestamp(request.EndDate);
        var rental = await dbContext.Rentals
            .Include(item => item.Vehicle)
            .Include(item => item.Payments)
            .FirstOrDefaultAsync(item => item.Id == request.RentalId, cancellationToken);
        if (rental is null)
        {
            return new RescheduleRentalResult(false, "Оренду не знайдено.");
        }

        if (rental.Status != RentalStatus.Booked)
        {
            return new RescheduleRentalResult(false, "Перенесення доступне лише для заброньованих оренд.");
        }

        if (!await dbContext.Employees.AnyAsync(item => item.Id == request.UpdatedByEmployeeId, cancellationToken))
        {
            return new RescheduleRentalResult(false, "Працівника не знайдено.");
        }

        if (request.StartDate >= request.EndDate)
        {
            return new RescheduleRentalResult(false, "Дата повернення має бути пізнішою за дату отримання.");
        }

        if (normalizedStart < now)
        {
            return new RescheduleRentalResult(false, "Початок оренди не може бути в минулому.");
        }

        if (normalizedEnd <= now)
        {
            return new RescheduleRentalResult(false, "Новий період оренди вже завершився.");
        }

        if (rental.Vehicle is null)
        {
            return new RescheduleRentalResult(false, "Для цієї оренди не знайдено авто.");
        }

        var hasConflict = await HasDateConflictAsync(
            rental.VehicleId,
            normalizedStart,
            normalizedEnd,
            rental.Id,
            cancellationToken);
        if (hasConflict)
        {
            return new RescheduleRentalResult(false, "Обрані дати перетинаються з іншим бронюванням.");
        }

        var totalAmount = CalculateRentalAmount(rental.Vehicle.DailyRate, normalizedStart, normalizedEnd);
        var netPaid = CalculateNetPaid(rental.Payments);
        var refundAmount = Math.Max(0m, netPaid - totalAmount);
        if (refundAmount > 0m)
        {
            dbContext.Payments.Add(new Payment
            {
                RentalId = rental.Id,
                EmployeeId = request.UpdatedByEmployeeId,
                Amount = refundAmount,
                Method = ResolveRefundMethod(rental.Payments),
                Direction = PaymentDirection.Refund,
                Notes = AutoRescheduleRefundNote,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        rental.StartDate = normalizedStart;
        rental.EndDate = normalizedEnd;
        rental.TotalAmount = totalAmount;
        rental.Status = RentalStatus.Booked;
        rental.ClosedByEmployeeId = null;
        rental.CanceledByEmployeeId = null;
        rental.ClosedAtUtc = null;
        rental.CanceledAtUtc = null;
        rental.CancellationReason = null;

        await dbContext.SaveChangesAsync(cancellationToken);

        var adjustedNetPaid = netPaid - refundAmount;
        var balance = Math.Max(0m, totalAmount - adjustedNetPaid);

        return new RescheduleRentalResult(
            true,
            "Період оренди оновлено.",
            totalAmount,
            balance);
    }

    // Окремий сценарій self-service доплати: клієнт може лише занулити поточний борг,
    // але не змінити довільно суму чи напрям платежу.
    public async Task<SettleRentalBalanceResult> SettleRentalBalanceAsync(
        SettleRentalBalanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var rental = await dbContext.Rentals
            .Include(item => item.Payments)
            .FirstOrDefaultAsync(item => item.Id == request.RentalId, cancellationToken);
        if (rental is null)
        {
            return new SettleRentalBalanceResult(false, "Оренду не знайдено.");
        }

        if (!await dbContext.Employees.AnyAsync(item => item.Id == request.EmployeeId, cancellationToken))
        {
            return new SettleRentalBalanceResult(false, "Працівника не знайдено.");
        }

        var balance = Math.Max(0m, rental.TotalAmount - CalculateNetPaid(rental.Payments));
        if (balance <= 0m)
        {
            return new SettleRentalBalanceResult(false, "Для цієї оренди немає позитивного балансу.");
        }

        dbContext.Payments.Add(new Payment
        {
            RentalId = rental.Id,
            EmployeeId = request.EmployeeId,
            Amount = balance,
            Method = PaymentMethod.Card,
            Direction = PaymentDirection.Incoming,
            Notes = string.IsNullOrWhiteSpace(request.Notes)
                ? AutoBalanceSettlementNote
                : request.Notes.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new SettleRentalBalanceResult(true, "Баланс оренди сплачено.", balance);
    }

    public async Task<PickupInspectionResult> CompletePickupInspectionAsync(
        PickupInspectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = NormalizeBusinessTimestamp(DateTime.UtcNow);
        var rental = await dbContext.Rentals
            .Include(item => item.Vehicle)
            .Include(item => item.Inspections)
            .FirstOrDefaultAsync(item => item.Id == request.RentalId, cancellationToken);
        if (rental is null)
        {
            return new PickupInspectionResult(false, "Оренду не знайдено.");
        }

        if (rental.Status == RentalStatus.Closed || rental.Status == RentalStatus.Canceled)
        {
            return new PickupInspectionResult(false, "Для цієї оренди огляд видачі недоступний.");
        }

        if (rental.Status == RentalStatus.Booked && now > rental.StartDate)
        {
            return new PickupInspectionResult(false, "Час видачі за цим бронюванням уже минув. Перенесіть або скасуйте бронювання.");
        }

        if (!await dbContext.Employees.AnyAsync(item => item.Id == request.PerformedByEmployeeId, cancellationToken))
        {
            return new PickupInspectionResult(false, "Працівника не знайдено.");
        }

        if (rental.Vehicle is not null &&
            !string.Equals(rental.Vehicle.VehicleStatusCode, VehicleStatuses.Ready, StringComparison.OrdinalIgnoreCase))
        {
            return new PickupInspectionResult(false, "Vehicle is temporarily unavailable for pickup.");
        }

        UpsertInspection(rental, request.PerformedByEmployeeId, RentalInspectionType.Pickup, request.FuelPercent, request.Notes);

        if (rental.Status == RentalStatus.Booked)
        {
            rental.Status = RentalStatus.Active;
            rental.ClosedByEmployeeId = null;
            rental.CanceledByEmployeeId = null;
            rental.ClosedAtUtc = null;
            rental.CanceledAtUtc = null;
            rental.CancellationReason = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new PickupInspectionResult(true, "Огляд видачі збережено.");
    }

    public Task RefreshStatusesAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    // Бронювання з payment і без нього сходяться в один internal flow,
    // щоб правила дат, конфліктів і стартових сум були ідентичними.
    private async Task<CreateRentalResult> CreateRentalInternalAsync(
        CreateRentalRequest request,
        PendingPayment? pendingPayment,
        CancellationToken cancellationToken)
    {
        var now = NormalizeBusinessTimestamp(DateTime.UtcNow);
        var normalizedStart = NormalizeBusinessTimestamp(request.StartDate);
        var normalizedEnd = NormalizeBusinessTimestamp(request.EndDate);
        if (request.StartDate >= request.EndDate)
        {
            return new CreateRentalResult(false, "Дата початку не може бути пізніше дати завершення.");
        }

        if (normalizedStart < now)
        {
            return new CreateRentalResult(false, "Початок оренди не може бути в минулому.");
        }

        var pickupLocation = NormalizeLocation(request.PickupLocation);
        if (string.IsNullOrWhiteSpace(pickupLocation))
        {
            return new CreateRentalResult(false, "Вкажіть локацію отримання.");
        }

        var returnLocation = NormalizeLocation(request.ReturnLocation, pickupLocation);

        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                if (dbContext.Database.IsNpgsql())
                {
                    await dbContext.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT pg_advisory_xact_lock({request.VehicleId});",
                        cancellationToken);
                }

                var vehicle = await dbContext.Vehicles.FirstOrDefaultAsync(
                    item => item.Id == request.VehicleId,
                    cancellationToken);
                if (vehicle is null)
                {
                    return new CreateRentalResult(false, "Авто не знайдено.");
                }

                if (!string.Equals(vehicle.VehicleStatusCode, VehicleStatuses.Ready, StringComparison.OrdinalIgnoreCase))
                {
                    return new CreateRentalResult(false, "Vehicle is temporarily unavailable for booking.");
                }

                var client = await dbContext.Clients.FirstOrDefaultAsync(
                    item => item.Id == request.ClientId,
                    cancellationToken);
                if (client is null)
                {
                    return new CreateRentalResult(false, "Клієнта не знайдено.");
                }

                if (client.IsBlacklisted)
                {
                    return new CreateRentalResult(false, "Клієнт у чорному списку.");
                }

                if (!client.DriverLicenseExpirationDate.HasValue ||
                    client.DriverLicenseExpirationDate.Value.Date < normalizedStart.Date)
                {
                    return new CreateRentalResult(false, "Driver license is missing or expired for the rental period.");
                }

                var employeeExists = await dbContext.Employees
                    .AnyAsync(employee => employee.Id == request.CreatedByEmployeeId, cancellationToken);
                if (!employeeExists)
                {
                    return new CreateRentalResult(false, "Працівника не знайдено.");
                }

                var hasConflict = await HasDateConflictAsync(
                    request.VehicleId,
                    normalizedStart,
                    normalizedEnd,
                    cancellationToken: cancellationToken);
                if (hasConflict)
                {
                    return new CreateRentalResult(false, "Обрані дати перетинаються з іншим бронюванням.");
                }

                var totalAmount = CalculateRentalAmount(vehicle.DailyRate, normalizedStart, normalizedEnd);
                var contractNumber = await contractNumberService.NextNumberAsync(cancellationToken);

                var rental = new Rental
                {
                    ClientId = request.ClientId,
                    VehicleId = request.VehicleId,
                    CreatedByEmployeeId = request.CreatedByEmployeeId,
                    ContractNumber = contractNumber,
                    StartDate = normalizedStart,
                    EndDate = normalizedEnd,
                    PickupLocation = pickupLocation,
                    ReturnLocation = returnLocation,
                    StartMileage = vehicle.Mileage,
                    TotalAmount = totalAmount,
                    Status = RentalStatus.Booked,
                    CreatedAtUtc = DateTime.UtcNow
                };

                dbContext.Rentals.Add(rental);

                if (pendingPayment is not null)
                {
                    dbContext.Payments.Add(new Payment
                    {
                        Rental = rental,
                        EmployeeId = request.CreatedByEmployeeId,
                        Amount = totalAmount,
                        Method = pendingPayment.Method,
                        Direction = pendingPayment.Direction,
                        Notes = pendingPayment.Notes?.Trim() ?? string.Empty,
                        CreatedAtUtc = DateTime.UtcNow
                    });
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new CreateRentalResult(
                    true,
                    "Оренду створено.",
                    rental.Id,
                    contractNumber,
                    totalAmount);
            }
            catch (DbUpdateException exception) when (IsRetriableCreateConflict(exception))
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                if (attempt == maxAttempts)
                {
                    return new CreateRentalResult(false, "Concurrent update detected. Please retry rental creation.");
                }
            }
            catch (PostgresException exception) when (
                exception.SqlState == PostgresErrorCodes.SerializationFailure ||
                exception.SqlState == PostgresErrorCodes.DeadlockDetected)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                if (attempt == maxAttempts)
                {
                    return new CreateRentalResult(false, "Concurrent update detected. Please retry rental creation.");
                }
            }
        }

        return new CreateRentalResult(false, "Rental creation failed due to concurrency conflict.");
    }

    private static decimal CalculateRentalAmount(decimal dailyRate, DateTime startDate, DateTime endDate)
    {
        var rentalHours = (decimal)(endDate - startDate).TotalHours;
        if (rentalHours <= 0m)
        {
            return 0m;
        }

        return decimal.Round(
            dailyRate * (rentalHours / 24m),
            2,
            MidpointRounding.AwayFromZero);
    }

    private static decimal CalculateNetPaid(IEnumerable<Payment> payments)
    {
        return payments.Sum(payment =>
            payment.Direction == PaymentDirection.Incoming
                ? payment.Amount
                : payment.Direction == PaymentDirection.Refund
                    ? -payment.Amount
                    : 0m);
    }

    private static PaymentMethod ResolveRefundMethod(IEnumerable<Payment> payments)
    {
        return payments
            .Where(item => item.Direction == PaymentDirection.Incoming)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => item.Method)
            .DefaultIfEmpty(PaymentMethod.Cash)
            .First();
    }

    private static string NormalizeLocation(string? value, string? fallback = null)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = (fallback ?? string.Empty).Trim();
        }

        if (normalized.Length > 80)
        {
            normalized = normalized[..80];
        }

        return normalized;
    }

    private static string NormalizeOptionalText(string? value, int maxLength)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.Length > maxLength)
        {
            normalized = normalized[..maxLength];
        }

        return normalized;
    }

    private static string BuildSystemNote(string baseNote, string suffix)
    {
        if (string.IsNullOrWhiteSpace(suffix))
        {
            return baseNote;
        }

        return $"{baseNote}. {suffix}";
    }

    // У системі всі "операційні" DateTime зберігаються як UTC без timezone-компонента,
    // тому тут відтинаємо зайву часову інформацію до узгодженого виду.
    private static DateTime NormalizeBusinessTimestamp(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Unspecified => value,
            DateTimeKind.Utc => DateTime.SpecifyKind(value, DateTimeKind.Unspecified),
            DateTimeKind.Local => DateTime.SpecifyKind(value.ToUniversalTime(), DateTimeKind.Unspecified),
            _ => value
        };
    }

    // Inspection для видачі і повернення оновлюємо як upsert, щоб повторне
    // підтвердження не плодило дублікати, а коригувало вже існуючий запис.
    private static void UpsertInspection(
        Rental rental,
        int performedByEmployeeId,
        RentalInspectionType type,
        int? fuelPercent,
        string? notes)
    {
        var timestamp = DateTime.UtcNow;
        var inspection = rental.Inspections.FirstOrDefault(item => item.Type == type);
        if (inspection is null)
        {
            inspection = new RentalInspection
            {
                Rental = rental,
                Type = type,
                PerformedByEmployeeId = performedByEmployeeId,
                CreatedAtUtc = timestamp
            };
            rental.Inspections.Add(inspection);
        }

        inspection.PerformedByEmployeeId = performedByEmployeeId;
        inspection.CompletedAtUtc = timestamp;
        inspection.FuelPercent = fuelPercent;
        inspection.Notes = NormalizeOptionalText(notes, 500);
        inspection.UpdatedAtUtc = timestamp;
    }

    private static bool IsRetriableCreateConflict(DbUpdateException exception)
    {
        if (exception.InnerException is PostgresException postgresException)
        {
            return postgresException.SqlState == PostgresErrorCodes.UniqueViolation ||
                   postgresException.SqlState == PostgresErrorCodes.SerializationFailure ||
                   postgresException.SqlState == PostgresErrorCodes.DeadlockDetected;
        }

        return false;
    }

    private sealed record PendingPayment(
        PaymentMethod Method,
        PaymentDirection Direction,
        string Notes);
}
