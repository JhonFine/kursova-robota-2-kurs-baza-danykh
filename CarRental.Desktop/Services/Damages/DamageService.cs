using CarRental.Desktop.Data;
using CarRental.Desktop.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace CarRental.Desktop.Services.Damages;

// Damage service С„С–РєСЃСѓС” СЃР°Рј Р°РєС‚ РїРѕС€РєРѕРґР¶РµРЅРЅСЏ С–, Р·Р° Р±Р°Р¶Р°РЅРЅСЏРј РѕРїРµСЂР°С‚РѕСЂР°,
// РѕРґСЂР°Р·Сѓ РїСЂРёРІ'СЏР·СѓС” Р№РѕРіРѕ РґРѕ РѕСЂРµРЅРґРё СЏРє charge, РЅРµ Р·РјС–С€СѓСЋС‡Рё С†Рµ Р· payment flow.
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
            DamageActNumber = GenerateActNumber(),
            ChargedAmount = 0m,
            StatusId = DamageStatus.Open
        };

        if (request.AutoChargeToRental && rental is not null)
        {
            // РђРІС‚РѕРґРѕРЅР°СЂР°С…СѓРІР°РЅРЅСЏ РїС–РґРЅС–РјР°С” total РґРѕРіРѕРІРѕСЂСѓ РІ РјРѕРјРµРЅС‚ СЂРµС”СЃС‚СЂР°С†С–С— РїРѕС€РєРѕРґР¶РµРЅРЅСЏ, С‰РѕР± Р±Р°Р»Р°РЅСЃ РѕРЅРѕРІРёРІСЃСЏ Р±РµР· РґРѕРґР°С‚РєРѕРІРёС… РєСЂРѕРєС–РІ.
            rental.TotalAmount += request.RepairCost;
            damage.ChargedAmount = request.RepairCost;
            damage.StatusId = DamageStatus.Charged;
        }

        dbContext.Damages.Add(damage);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new DamageResult(true, $"Пошкодження зафіксовано, акт №{damage.DamageActNumber}.");
    }

    private static string GenerateActNumber()
    {
        // Timestamp РїР»СЋСЃ РІРёРїР°РґРєРѕРІРёР№ suffix РґР°СЋС‚СЊ СЃС‚Р°Р±С–Р»СЊРЅРѕ СѓРЅС–РєР°Р»СЊРЅРёР№ РЅРѕРјРµСЂ РЅР°РІС–С‚СЊ РїСЂРё СЃРµСЂС–Р№РЅРѕРјСѓ СЃС‚РІРѕСЂРµРЅРЅС– Р°РєС‚С–РІ.
        var randomSuffix = RandomNumberGenerator.GetInt32(100000, 1000000);
        return $"ACT-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{randomSuffix}";
    }
}

