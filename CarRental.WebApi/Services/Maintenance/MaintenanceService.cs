using CarRental.WebApi.Data;
using CarRental.WebApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRental.WebApi.Services.Maintenance;

public sealed class MaintenanceService(RentalDbContext dbContext) : IMaintenanceService
{
    // Запис ТО валідований не лише по авто, а й по довіднику типів,
    // щоб staff не міг зберегти "довільний" maintenance code поза reference data.
    public async Task<MaintenanceResult> AddRecordAsync(MaintenanceRequest request, CancellationToken cancellationToken = default)
    {
        var vehicle = await dbContext.Vehicles.FirstOrDefaultAsync(item => item.Id == request.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            return new MaintenanceResult(false, "Авто не знайдено.");
        }

        if (request.PerformedByEmployeeId.HasValue &&
            !await dbContext.Employees.AnyAsync(item => item.Id == request.PerformedByEmployeeId.Value, cancellationToken))
        {
            return new MaintenanceResult(false, "Працівника не знайдено.");
        }

        if (!await dbContext.MaintenanceTypes.AnyAsync(item => item.Code == request.MaintenanceTypeCode.Trim().ToUpperInvariant(), cancellationToken))
        {
            return new MaintenanceResult(false, "Тип обслуговування не знайдено.");
        }

        var record = new MaintenanceRecord
        {
            VehicleId = request.VehicleId,
            PerformedByEmployeeId = request.PerformedByEmployeeId,
            ServiceDate = request.ServiceDate,
            MileageAtService = request.MileageAtService,
            Description = request.Description.Trim(),
            Cost = request.Cost,
            NextServiceMileage = request.NextServiceMileage,
            MaintenanceTypeCode = request.MaintenanceTypeCode.Trim().ToUpperInvariant(),
            ServiceProviderName = string.IsNullOrWhiteSpace(request.ServiceProviderName) ? null : request.ServiceProviderName.Trim()
        };

        dbContext.MaintenanceRecords.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new MaintenanceResult(true, "Запис ТО додано.");
    }

    // Прострочення визначаємо від останнього зафіксованого порога NextServiceMileage,
    // а якщо записів ще не було — від штатного інтервалу самого авто.
    public async Task<IReadOnlyList<MaintenanceDueItem>> GetDueItemsAsync(CancellationToken cancellationToken = default)
    {
        var vehicles = await dbContext.Vehicles
            .AsNoTracking()
            .OrderBy(item => item.Make)
            .ThenBy(item => item.Model)
            .Select(item => new
            {
                item.Id,
                item.Make,
                item.Model,
                item.LicensePlate,
                item.Mileage,
                item.ServiceIntervalKm,
                LastNextServiceMileage = item.MaintenanceRecords
                    .OrderByDescending(record => record.ServiceDate)
                    .Select(record => (int?)record.NextServiceMileage)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var due = new List<MaintenanceDueItem>();
        foreach (var vehicle in vehicles)
        {
            var nextServiceMileage = vehicle.LastNextServiceMileage ?? (vehicle.Mileage + vehicle.ServiceIntervalKm);

            if (vehicle.Mileage >= nextServiceMileage)
            {
                due.Add(new MaintenanceDueItem(
                    vehicle.Id,
                    $"{vehicle.Make} {vehicle.Model} [{vehicle.LicensePlate}]",
                    vehicle.Mileage,
                    nextServiceMileage,
                    vehicle.Mileage - nextServiceMileage));
            }
        }

        return due;
    }
}
