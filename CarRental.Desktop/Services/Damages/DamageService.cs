using CarRental.Desktop.Data;
using CarRental.Desktop.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace CarRental.Desktop.Services.Damages;

public sealed class DamageService(RentalDbContext dbContext) : IDamageService
{
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

        var damage = new Damage
        {
            VehicleId = request.VehicleId,
            RentalId = request.RentalId,
            Description = request.Description.Trim(),
            DateReported = DateTime.UtcNow,
            RepairCost = request.RepairCost,
            PhotoPath = request.PhotoPath,
            ActNumber = GenerateActNumber(),
            ChargedAmount = 0m,
            Status = DamageStatus.Open
        };

        if (request.AutoChargeToRental && rental is not null)
        {
            rental.TotalAmount += request.RepairCost;
            damage.ChargedAmount = request.RepairCost;
            damage.Status = DamageStatus.Charged;
        }

        dbContext.Damages.Add(damage);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new DamageResult(true, $"Пошкодження зафіксовано, акт №{damage.ActNumber}.");
    }

    private static string GenerateActNumber()
    {
        var randomSuffix = RandomNumberGenerator.GetInt32(100000, 1000000);
        return $"ACT-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{randomSuffix}";
    }
}
