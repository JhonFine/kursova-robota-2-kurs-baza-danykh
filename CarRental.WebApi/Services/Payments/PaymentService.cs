using CarRental.WebApi.Data;
using CarRental.WebApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRental.WebApi.Services.Payments;

public sealed class PaymentService(RentalDbContext dbContext) : IPaymentService
{
    public async Task<PaymentResult> AddPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
        {
            return new PaymentResult(false, "Сума має бути більшою за нуль.");
        }

        var rental = await dbContext.Rentals.FirstOrDefaultAsync(item => item.Id == request.RentalId, cancellationToken);
        if (rental is null)
        {
            return new PaymentResult(false, "Оренду не знайдено.");
        }

        if (request.RecordedByEmployeeId.HasValue &&
            !await dbContext.Employees.AnyAsync(item => item.Id == request.RecordedByEmployeeId.Value, cancellationToken))
        {
            return new PaymentResult(false, "Працівника не знайдено.");
        }

        var payment = new Payment
        {
            RentalId = request.RentalId,
            RecordedByEmployeeId = request.RecordedByEmployeeId,
            Amount = request.Amount,
            MethodId = request.MethodId,
            DirectionId = request.DirectionId,
            StatusId = request.StatusId,
            ExternalTransactionId = string.IsNullOrWhiteSpace(request.ExternalTransactionId) ? null : request.ExternalTransactionId.Trim(),
            Notes = request.Notes.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new PaymentResult(true, "Платіж збережено.", payment.Id);
    }

    public async Task<IReadOnlyList<Payment>> GetRentalPaymentsAsync(int rentalId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Payments
            .AsNoTracking()
            .Include(item => item.RecordedByEmployee)
            .ThenInclude(item => item!.Account)
            .Where(item => item.RentalId == rentalId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<decimal> GetRentalBalanceAsync(int rentalId, CancellationToken cancellationToken = default)
    {
        var rentalAmount = await dbContext.Rentals
            .AsNoTracking()
            .Where(item => item.Id == rentalId)
            .Select(item => (decimal?)item.TotalAmount)
            .FirstOrDefaultAsync(cancellationToken) ?? 0m;

        var paid = await dbContext.Payments
            .AsNoTracking()
            .Where(item => item.RentalId == rentalId)
            .Where(item => item.StatusId == PaymentStatus.Completed)
            .SumAsync(
                item => (decimal?)(item.DirectionId == PaymentDirection.Incoming ? item.Amount : -item.Amount),
                cancellationToken) ?? 0m;

        return rentalAmount - paid;
    }
}



