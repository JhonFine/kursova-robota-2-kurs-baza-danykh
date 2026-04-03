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

    // РљРѕРЅС„Р»С–РєС‚ РїРµСЂРµРІС–СЂСЏС”РјРѕ Сѓ "Р±С–Р·РЅРµСЃ-С‡Р°СЃС–": СѓСЃС– РґР°С‚Рё РїРѕРїРµСЂРµРґРЅСЊРѕ РЅРѕСЂРјР°Р»С–Р·СѓСЋС‚СЊСЃСЏ,
    // С‰РѕР± РїРѕСЂС–РІРЅСЏРЅРЅСЏ РЅРµ Р·Р°Р»РµР¶Р°Р»Рё РІС–Рґ РІРёРїР°РґРєРѕРІРёС… timezone-РєРѕРЅРІРµСЂС‚Р°С†С–Р№ РєР»С–С”РЅС‚Р°.
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
                    rental.StatusId != RentalStatus.Closed &&
                    rental.StatusId != RentalStatus.Canceled &&
                    rental.StartDate < normalizedEnd &&
                    normalizedStart < rental.EndDate,
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
            new PendingPayment(request.MethodId, request.DirectionId, request.Notes),
            cancellationToken);
    }

    // Р—Р°РєСЂРёС‚С‚СЏ РѕСЂРµРЅРґРё РЅРµ Р»РёС€Рµ Р·РјС–РЅСЋС” СЃС‚Р°С‚СѓСЃ, Р° Р№ РґРѕР±СѓРґРѕРІСѓС” С„С–РЅР°Р»СЊРЅРёР№ СЂРѕР·СЂР°С…СѓРЅРѕРє:
    // РїРѕРІРµСЂРЅРµРЅРЅСЏ РїРµСЂРµРїР»Р°С‚Рё, С„С–РЅР°Р»СЊРЅРёР№ inspection С– РѕРЅРѕРІР»РµРЅРЅСЏ РїСЂРѕР±С–РіСѓ Р°РІС‚Рѕ.
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
            return new CloseRentalResult(false, "РћСЂРµРЅРґСѓ РЅРµ Р·РЅР°Р№РґРµРЅРѕ.");
        }

        if (rental.StatusId == RentalStatus.Closed)
        {
            return new CloseRentalResult(false, "РћСЂРµРЅРґСѓ РІР¶Рµ Р·Р°РєСЂРёС‚Рѕ.");
        }

        if (rental.StatusId == RentalStatus.Canceled)
        {
            return new CloseRentalResult(false, "РЎРєР°СЃРѕРІР°РЅСѓ РѕСЂРµРЅРґСѓ РЅРµ РјРѕР¶РЅР° Р·Р°РєСЂРёС‚Рё.");
        }

        var normalizedActualEndDate = NormalizeBusinessTimestamp(request.ActualEndDate);
        if (normalizedActualEndDate.Date == rental.StartDate.Date &&
            normalizedActualEndDate < rental.StartDate)
        {
            normalizedActualEndDate = rental.StartDate.AddMinutes(1);
        }

        if (normalizedActualEndDate <= rental.StartDate)
        {
            return new CloseRentalResult(false, "РќРµРєРѕСЂРµРєС‚РЅР° РґР°С‚Р° РїРѕРІРµСЂРЅРµРЅРЅСЏ.");
        }

        if (request.EndMileage < rental.StartMileage)
        {
            return new CloseRentalResult(false, "РљС–РЅС†РµРІРёР№ РїСЂРѕР±С–Рі РЅРµ РјРѕР¶Рµ Р±СѓС‚Рё РјРµРЅС€РёРј Р·Р° РїРѕС‡Р°С‚РєРѕРІРёР№.");
        }

        if (rental.Vehicle is null)
        {
            return new CloseRentalResult(false, "Р”Р»СЏ С†С–С”С— РѕСЂРµРЅРґРё РЅРµ Р·РЅР°Р№РґРµРЅРѕ Р°РІС‚Рѕ.");
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
                RecordedByEmployeeId = request.ClosedByEmployeeId,
                Amount = refundAmount,
                MethodId = ResolveRefundMethod(rental.Payments),
                DirectionId = PaymentDirection.Refund,
                Notes = "РђРІС‚РѕРјР°С‚РёС‡РЅРµ РїРѕРІРµСЂРЅРµРЅРЅСЏ РїРµСЂРµРїР»Р°С‚Рё РїСЂРё Р·Р°РєСЂРёС‚С‚С– РѕСЂРµРЅРґРё.",
                CreatedAtUtc = DateTime.UtcNow
            };
            dbContext.Payments.Add(refundPayment);
        }

        rental.EndDate = normalizedActualEndDate;
        rental.EndMileage = request.EndMileage;
        rental.OverageFee = 0m;
        rental.TotalAmount = totalAmount;
        rental.StatusId = RentalStatus.Closed;
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
            "РћСЂРµРЅРґСѓ Р·Р°РєСЂРёС‚Рѕ. РџСЂРѕР±С–Рі Р°РІС‚Рѕ РѕРЅРѕРІР»РµРЅРѕ.",
            totalAmount);
    }

    // РЎРєР°СЃСѓРІР°РЅРЅСЏ booked-РѕСЂРµРЅРґРё РјРѕР¶Рµ Р°РІС‚РѕРјР°С‚РёС‡РЅРѕ РїРѕРІРµСЂРЅСѓС‚Рё РїРµСЂРµРґРѕРїР»Р°С‚Сѓ,
    // С‚РѕРјСѓ С‚СѓС‚ Р¶РёРІРµ С– СЃС‚Р°С‚СѓСЃРЅР° Р»РѕРіС–РєР°, С– С„С–РЅР°РЅСЃРѕРІРёР№ side effect.
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
            return new CancelRentalResult(false, "РћСЂРµРЅРґСѓ РЅРµ Р·РЅР°Р№РґРµРЅРѕ.");
        }

        if (rental.StatusId == RentalStatus.Closed)
        {
            return new CancelRentalResult(false, "Р—Р°РєСЂРёС‚Сѓ РѕСЂРµРЅРґСѓ РЅРµ РјРѕР¶РЅР° СЃРєР°СЃСѓРІР°С‚Рё.");
        }

        if (rental.StatusId == RentalStatus.Active)
        {
            return new CancelRentalResult(false, "РђРєС‚РёРІРЅСѓ РѕСЂРµРЅРґСѓ РЅРµ РјРѕР¶РЅР° СЃРєР°СЃСѓРІР°С‚Рё.");
        }

        if (rental.StatusId == RentalStatus.Canceled)
        {
            return new CancelRentalResult(false, "РћСЂРµРЅРґСѓ РІР¶Рµ СЃРєР°СЃРѕРІР°РЅРѕ.");
        }

        var normalizedReason = request.Reason.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason))
        {
            return new CancelRentalResult(false, "Р’РєР°Р¶С–С‚СЊ РїСЂРёС‡РёРЅСѓ СЃРєР°СЃСѓРІР°РЅРЅСЏ.");
        }

        var originalStatus = rental.StatusId;
        if (originalStatus == RentalStatus.Booked)
        {
            var netPaid = CalculateNetPaid(rental.Payments);
            if (netPaid > 0m)
            {
                dbContext.Payments.Add(new Payment
                {
                    RentalId = rental.Id,
                    RecordedByEmployeeId = request.CanceledByEmployeeId,
                    Amount = netPaid,
                    MethodId = ResolveRefundMethod(rental.Payments),
                    DirectionId = PaymentDirection.Refund,
                    Notes = BuildSystemNote(AutoCancellationRefundNote, normalizedReason),
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            rental.TotalAmount = 0m;
            rental.OverageFee = 0m;
        }

        rental.StatusId = RentalStatus.Canceled;
        rental.ClosedByEmployeeId = null;
        rental.CanceledByEmployeeId = request.CanceledByEmployeeId;
        rental.ClosedAtUtc = null;
        rental.CanceledAtUtc = DateTime.UtcNow;
        rental.CancellationReason = normalizedReason;

        await dbContext.SaveChangesAsync(cancellationToken);
        return new CancelRentalResult(true, "РћСЂРµРЅРґСѓ СЃРєР°СЃРѕРІР°РЅРѕ.");
    }

    // РџРµСЂРµРЅРµСЃРµРЅРЅСЏ РґРѕР·РІРѕР»РµРЅРµ Р»РёС€Рµ РґР»СЏ booked-РѕСЂРµРЅРґРё С– РІРѕРґРЅРѕС‡Р°СЃ РїРµСЂРµСЂР°С…РѕРІСѓС”
    // СЂС–Р·РЅРёС†СЋ РІ РѕРїР»Р°С‚С–, С‰РѕР± РїС–СЃР»СЏ Р·СЃСѓРІСѓ РґР°С‚ Р±Р°Р»Р°РЅСЃ РЅРµ Р·Р°Р»РёС€РёРІСЃСЏ "СЃС‚Р°СЂРёРј".
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
            return new RescheduleRentalResult(false, "РћСЂРµРЅРґСѓ РЅРµ Р·РЅР°Р№РґРµРЅРѕ.");
        }

        if (rental.StatusId != RentalStatus.Booked)
        {
            return new RescheduleRentalResult(false, "РџРµСЂРµРЅРµСЃРµРЅРЅСЏ РґРѕСЃС‚СѓРїРЅРµ Р»РёС€Рµ РґР»СЏ Р·Р°Р±СЂРѕРЅСЊРѕРІР°РЅРёС… РѕСЂРµРЅРґ.");
        }

        if (request.UpdatedByEmployeeId.HasValue &&
            !await dbContext.Employees.AnyAsync(item => item.Id == request.UpdatedByEmployeeId.Value, cancellationToken))
        {
            return new RescheduleRentalResult(false, "РџСЂР°С†С–РІРЅРёРєР° РЅРµ Р·РЅР°Р№РґРµРЅРѕ.");
        }

        if (request.StartDate >= request.EndDate)
        {
            return new RescheduleRentalResult(false, "Р”Р°С‚Р° РїРѕРІРµСЂРЅРµРЅРЅСЏ РјР°С” Р±СѓС‚Рё РїС–Р·РЅС–С€РѕСЋ Р·Р° РґР°С‚Сѓ РѕС‚СЂРёРјР°РЅРЅСЏ.");
        }

        if (normalizedStart < now)
        {
            return new RescheduleRentalResult(false, "РџРѕС‡Р°С‚РѕРє РѕСЂРµРЅРґРё РЅРµ РјРѕР¶Рµ Р±СѓС‚Рё РІ РјРёРЅСѓР»РѕРјСѓ.");
        }

        if (normalizedEnd <= now)
        {
            return new RescheduleRentalResult(false, "РќРѕРІРёР№ РїРµСЂС–РѕРґ РѕСЂРµРЅРґРё РІР¶Рµ Р·Р°РІРµСЂС€РёРІСЃСЏ.");
        }

        if (rental.Vehicle is null)
        {
            return new RescheduleRentalResult(false, "Р”Р»СЏ С†С–С”С— РѕСЂРµРЅРґРё РЅРµ Р·РЅР°Р№РґРµРЅРѕ Р°РІС‚Рѕ.");
        }

        var hasConflict = await HasDateConflictAsync(
            rental.VehicleId,
            normalizedStart,
            normalizedEnd,
            rental.Id,
            cancellationToken);
        if (hasConflict)
        {
            return new RescheduleRentalResult(false, "РћР±СЂР°РЅС– РґР°С‚Рё РїРµСЂРµС‚РёРЅР°СЋС‚СЊСЃСЏ Р· С–РЅС€РёРј Р±СЂРѕРЅСЋРІР°РЅРЅСЏРј.");
        }

        var totalAmount = CalculateRentalAmount(rental.Vehicle.DailyRate, normalizedStart, normalizedEnd);
        var netPaid = CalculateNetPaid(rental.Payments);
        var refundAmount = Math.Max(0m, netPaid - totalAmount);
        if (refundAmount > 0m)
        {
            dbContext.Payments.Add(new Payment
            {
                RentalId = rental.Id,
                RecordedByEmployeeId = request.UpdatedByEmployeeId,
                Amount = refundAmount,
                MethodId = ResolveRefundMethod(rental.Payments),
                DirectionId = PaymentDirection.Refund,
                Notes = AutoRescheduleRefundNote,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        rental.StartDate = normalizedStart;
        rental.EndDate = normalizedEnd;
        rental.TotalAmount = totalAmount;
        rental.StatusId = RentalStatus.Booked;
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
            "РџРµСЂС–РѕРґ РѕСЂРµРЅРґРё РѕРЅРѕРІР»РµРЅРѕ.",
            totalAmount,
            balance);
    }

    // РћРєСЂРµРјРёР№ СЃС†РµРЅР°СЂС–Р№ self-service РґРѕРїР»Р°С‚Рё: РєР»С–С”РЅС‚ РјРѕР¶Рµ Р»РёС€Рµ Р·Р°РЅСѓР»РёС‚Рё РїРѕС‚РѕС‡РЅРёР№ Р±РѕСЂРі,
    // Р°Р»Рµ РЅРµ Р·РјС–РЅРёС‚Рё РґРѕРІС–Р»СЊРЅРѕ СЃСѓРјСѓ С‡Рё РЅР°РїСЂСЏРј РїР»Р°С‚РµР¶Сѓ.
    public async Task<SettleRentalBalanceResult> SettleRentalBalanceAsync(
        SettleRentalBalanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var rental = await dbContext.Rentals
            .Include(item => item.Payments)
            .FirstOrDefaultAsync(item => item.Id == request.RentalId, cancellationToken);
        if (rental is null)
        {
            return new SettleRentalBalanceResult(false, "РћСЂРµРЅРґСѓ РЅРµ Р·РЅР°Р№РґРµРЅРѕ.");
        }

        if (request.RecordedByEmployeeId.HasValue &&
            !await dbContext.Employees.AnyAsync(item => item.Id == request.RecordedByEmployeeId.Value, cancellationToken))
        {
            return new SettleRentalBalanceResult(false, "РџСЂР°С†С–РІРЅРёРєР° РЅРµ Р·РЅР°Р№РґРµРЅРѕ.");
        }

        var balance = Math.Max(0m, rental.TotalAmount - CalculateNetPaid(rental.Payments));
        if (balance <= 0m)
        {
            return new SettleRentalBalanceResult(false, "Р”Р»СЏ С†С–С”С— РѕСЂРµРЅРґРё РЅРµРјР°С” РїРѕР·РёС‚РёРІРЅРѕРіРѕ Р±Р°Р»Р°РЅСЃСѓ.");
        }

        dbContext.Payments.Add(new Payment
        {
            RentalId = rental.Id,
            RecordedByEmployeeId = request.RecordedByEmployeeId,
            Amount = balance,
            MethodId = PaymentMethod.Card,
            DirectionId = PaymentDirection.Incoming,
            Notes = string.IsNullOrWhiteSpace(request.Notes)
                ? AutoBalanceSettlementNote
                : request.Notes.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new SettleRentalBalanceResult(true, "Р‘Р°Р»Р°РЅСЃ РѕСЂРµРЅРґРё СЃРїР»Р°С‡РµРЅРѕ.", balance);
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
            return new PickupInspectionResult(false, "РћСЂРµРЅРґСѓ РЅРµ Р·РЅР°Р№РґРµРЅРѕ.");
        }

        if (rental.StatusId == RentalStatus.Closed || rental.StatusId == RentalStatus.Canceled)
        {
            return new PickupInspectionResult(false, "Р”Р»СЏ С†С–С”С— РѕСЂРµРЅРґРё РѕРіР»СЏРґ РІРёРґР°С‡С– РЅРµРґРѕСЃС‚СѓРїРЅРёР№.");
        }

        if (rental.StatusId == RentalStatus.Booked && now > rental.StartDate)
        {
            return new PickupInspectionResult(false, "Р§Р°СЃ РІРёРґР°С‡С– Р·Р° С†РёРј Р±СЂРѕРЅСЋРІР°РЅРЅСЏРј СѓР¶Рµ РјРёРЅСѓРІ. РџРµСЂРµРЅРµСЃС–С‚СЊ Р°Р±Рѕ СЃРєР°СЃСѓР№С‚Рµ Р±СЂРѕРЅСЋРІР°РЅРЅСЏ.");
        }

        if (!await dbContext.Employees.AnyAsync(item => item.Id == request.PerformedByEmployeeId, cancellationToken))
        {
            return new PickupInspectionResult(false, "РџСЂР°С†С–РІРЅРёРєР° РЅРµ Р·РЅР°Р№РґРµРЅРѕ.");
        }

        if (rental.Vehicle is not null &&
            !string.Equals(rental.Vehicle.VehicleStatusCode, VehicleStatuses.Ready, StringComparison.OrdinalIgnoreCase))
        {
            return new PickupInspectionResult(false, "Vehicle is temporarily unavailable for pickup.");
        }

        UpsertInspection(rental, request.PerformedByEmployeeId, RentalInspectionType.Pickup, request.FuelPercent, request.Notes);

        if (rental.StatusId == RentalStatus.Booked)
        {
            rental.StatusId = RentalStatus.Active;
            rental.ClosedByEmployeeId = null;
            rental.CanceledByEmployeeId = null;
            rental.ClosedAtUtc = null;
            rental.CanceledAtUtc = null;
            rental.CancellationReason = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new PickupInspectionResult(true, "РћРіР»СЏРґ РІРёРґР°С‡С– Р·Р±РµСЂРµР¶РµРЅРѕ.");
    }

    public Task RefreshStatusesAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    // Р‘СЂРѕРЅСЋРІР°РЅРЅСЏ Р· payment С– Р±РµР· РЅСЊРѕРіРѕ СЃС…РѕРґСЏС‚СЊСЃСЏ РІ РѕРґРёРЅ internal flow,
    // С‰РѕР± РїСЂР°РІРёР»Р° РґР°С‚, РєРѕРЅС„Р»С–РєС‚С–РІ С– СЃС‚Р°СЂС‚РѕРІРёС… СЃСѓРј Р±СѓР»Рё С–РґРµРЅС‚РёС‡РЅРёРјРё.
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
            return new CreateRentalResult(false, "Р”Р°С‚Р° РїРѕС‡Р°С‚РєСѓ РЅРµ РјРѕР¶Рµ Р±СѓС‚Рё РїС–Р·РЅС–С€Рµ РґР°С‚Рё Р·Р°РІРµСЂС€РµРЅРЅСЏ.");
        }

        if (normalizedStart < now)
        {
            return new CreateRentalResult(false, "РџРѕС‡Р°С‚РѕРє РѕСЂРµРЅРґРё РЅРµ РјРѕР¶Рµ Р±СѓС‚Рё РІ РјРёРЅСѓР»РѕРјСѓ.");
        }

        var pickupLocation = NormalizeLocation(request.PickupLocation);
        if (string.IsNullOrWhiteSpace(pickupLocation))
        {
            return new CreateRentalResult(false, "Р’РєР°Р¶С–С‚СЊ Р»РѕРєР°С†С–СЋ РѕС‚СЂРёРјР°РЅРЅСЏ.");
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
                    return new CreateRentalResult(false, "РђРІС‚Рѕ РЅРµ Р·РЅР°Р№РґРµРЅРѕ.");
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
                    return new CreateRentalResult(false, "РљР»С–С”РЅС‚Р° РЅРµ Р·РЅР°Р№РґРµРЅРѕ.");
                }

                if (client.IsBlacklisted)
                {
                    return new CreateRentalResult(false, "РљР»С–С”РЅС‚ Сѓ С‡РѕСЂРЅРѕРјСѓ СЃРїРёСЃРєСѓ.");
                }

                if (!client.DriverLicenseExpirationDate.HasValue ||
                    client.DriverLicenseExpirationDate.Value.Date < normalizedStart.Date)
                {
                    return new CreateRentalResult(false, "Driver license is missing or expired for the rental period.");
                }

                if (request.CreatedByEmployeeId.HasValue &&
                    !await dbContext.Employees.AnyAsync(employee => employee.Id == request.CreatedByEmployeeId.Value, cancellationToken))
                {
                    return new CreateRentalResult(false, "РџСЂР°С†С–РІРЅРёРєР° РЅРµ Р·РЅР°Р№РґРµРЅРѕ.");
                }

                var hasConflict = await HasDateConflictAsync(
                    request.VehicleId,
                    normalizedStart,
                    normalizedEnd,
                    cancellationToken: cancellationToken);
                if (hasConflict)
                {
                    return new CreateRentalResult(false, "РћР±СЂР°РЅС– РґР°С‚Рё РїРµСЂРµС‚РёРЅР°СЋС‚СЊСЃСЏ Р· С–РЅС€РёРј Р±СЂРѕРЅСЋРІР°РЅРЅСЏРј.");
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
                    StatusId = RentalStatus.Booked,
                    CreatedAtUtc = DateTime.UtcNow
                };

                dbContext.Rentals.Add(rental);

                if (pendingPayment is not null)
                {
                    dbContext.Payments.Add(new Payment
                    {
                        Rental = rental,
                        RecordedByEmployeeId = request.CreatedByEmployeeId,
                        Amount = totalAmount,
                        MethodId = pendingPayment.MethodId,
                        DirectionId = pendingPayment.DirectionId,
                        Notes = pendingPayment.Notes?.Trim() ?? string.Empty,
                        CreatedAtUtc = DateTime.UtcNow
                    });
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new CreateRentalResult(
                    true,
                    "РћСЂРµРЅРґСѓ СЃС‚РІРѕСЂРµРЅРѕ.",
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
            payment.DirectionId == PaymentDirection.Incoming
                ? payment.Amount
                : payment.DirectionId == PaymentDirection.Refund
                    ? -payment.Amount
                    : 0m);
    }

    private static PaymentMethod ResolveRefundMethod(IEnumerable<Payment> payments)
    {
        return payments
            .Where(item => item.DirectionId == PaymentDirection.Incoming)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => item.MethodId)
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

    // РЈ СЃРёСЃС‚РµРјС– РІСЃС– "РѕРїРµСЂР°С†С–Р№РЅС–" DateTime Р·Р±РµСЂС–РіР°СЋС‚СЊСЃСЏ СЏРє UTC Р±РµР· timezone-РєРѕРјРїРѕРЅРµРЅС‚Р°,
    // С‚РѕРјСѓ С‚СѓС‚ РІС–РґС‚РёРЅР°С”РјРѕ Р·Р°Р№РІСѓ С‡Р°СЃРѕРІСѓ С–РЅС„РѕСЂРјР°С†С–СЋ РґРѕ СѓР·РіРѕРґР¶РµРЅРѕРіРѕ РІРёРґСѓ.
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

    // Inspection РґР»СЏ РІРёРґР°С‡С– С– РїРѕРІРµСЂРЅРµРЅРЅСЏ РѕРЅРѕРІР»СЋС”РјРѕ СЏРє upsert, С‰РѕР± РїРѕРІС‚РѕСЂРЅРµ
    // РїС–РґС‚РІРµСЂРґР¶РµРЅРЅСЏ РЅРµ РїР»РѕРґРёР»Рѕ РґСѓР±Р»С–РєР°С‚Рё, Р° РєРѕСЂРёРіСѓРІР°Р»Рѕ РІР¶Рµ С–СЃРЅСѓСЋС‡РёР№ Р·Р°РїРёСЃ.
    private static void UpsertInspection(
        Rental rental,
        int performedByEmployeeId,
        RentalInspectionType type,
        int? fuelPercent,
        string? notes)
    {
        var timestamp = DateTime.UtcNow;
        var inspection = rental.Inspections.FirstOrDefault(item => item.TypeId == type);
        if (inspection is null)
        {
            inspection = new RentalInspection
            {
                Rental = rental,
                TypeId = type,
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
        PaymentMethod MethodId,
        PaymentDirection DirectionId,
        string Notes);
}


