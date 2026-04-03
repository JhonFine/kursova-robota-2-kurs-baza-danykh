using CarRental.WebApi.Data;
using CarRental.WebApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRental.WebApi.Services.Maintenance;

public sealed class MaintenanceService(RentalDbContext dbContext) : IMaintenanceService
{
    public async Task<MaintenanceResult> AddRecordAsync(MaintenanceRequest request, CancellationToken cancellationToken = default)
    {
        var vehicle = await dbContext.Vehicles.FirstOrDefaultAsync(item => item.Id == request.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            return new MaintenanceResult(false, "Авто не знайдено.");
        }

        var hasEmployee = request.PerformedByEmployeeId.HasValue;
        var hasProvider = !string.IsNullOrWhiteSpace(request.ServiceProviderName);
        if (hasEmployee == hasProvider)
        {
            return new MaintenanceResult(false, "Вкажіть або внутрішнього виконавця, або зовнішню СТО.");
        }

        if (!request.NextServiceMileage.HasValue && !request.NextServiceDate.HasValue)
        {
            return new MaintenanceResult(false, "Вкажіть наступний сервіс за пробігом або датою.");
        }

        if (request.PerformedByEmployeeId.HasValue &&
            !await dbContext.Employees.AnyAsync(item => item.Id == request.PerformedByEmployeeId.Value, cancellationToken))
        {
            return new MaintenanceResult(false, "Працівника не знайдено.");
        }

        var maintenanceTypeCode = request.MaintenanceTypeCode.Trim().ToUpperInvariant();
        if (!await dbContext.MaintenanceTypes.AnyAsync(item => item.Code == maintenanceTypeCode, cancellationToken))
        {
            return new MaintenanceResult(false, "Тип обслуговування не знайдено.");
        }

        var record = new MaintenanceRecord
        {
            VehicleId = request.VehicleId,
            PerformedByEmployeeId = request.PerformedByEmployeeId,
            ServiceDate = request.ServiceDate.Date,
            MileageAtService = request.MileageAtService,
            Description = request.Description.Trim(),
            Cost = request.Cost,
            NextServiceMileage = request.NextServiceMileage,
            NextServiceDate = request.NextServiceDate?.Date,
            MaintenanceTypeCode = maintenanceTypeCode,
            ServiceProviderName = string.IsNullOrWhiteSpace(request.ServiceProviderName) ? null : request.ServiceProviderName.Trim()
        };

        dbContext.MaintenanceRecords.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new MaintenanceResult(true, "Запис ТО додано.");
    }

    public async Task<IReadOnlyList<MaintenanceDueItem>> GetDueItemsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var vehicles = await dbContext.Vehicles
            .AsNoTracking()
            .OrderBy(item => item.MakeLookup!.Name)
            .ThenBy(item => item.ModelLookup!.Name)
            .Select(item => new
            {
                item.Id,
                MakeName = item.MakeLookup!.Name,
                ModelName = item.ModelLookup!.Name,
                item.LicensePlate,
                item.Mileage,
                item.ServiceIntervalKm,
                LastNextServiceMileage = item.MaintenanceRecords
                    .OrderByDescending(record => record.ServiceDate)
                    .Select(record => record.NextServiceMileage)
                    .FirstOrDefault(),
                LastNextServiceDate = item.MaintenanceRecords
                    .OrderByDescending(record => record.ServiceDate)
                    .Select(record => record.NextServiceDate)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var due = new List<MaintenanceDueItem>();
        foreach (var vehicle in vehicles)
        {
            var nextServiceMileage = vehicle.LastNextServiceMileage ?? (vehicle.Mileage + vehicle.ServiceIntervalKm);
            var nextServiceDate = vehicle.LastNextServiceDate?.Date;
            var overdueByKm = vehicle.Mileage >= nextServiceMileage ? vehicle.Mileage - nextServiceMileage : 0;
            var overdueByDays = nextServiceDate.HasValue && today >= nextServiceDate.Value
                ? (today - nextServiceDate.Value).Days
                : 0;

            if (overdueByKm > 0 || overdueByDays > 0)
            {
                due.Add(new MaintenanceDueItem(
                    vehicle.Id,
                    $"{vehicle.MakeName} {vehicle.ModelName} [{vehicle.LicensePlate}]",
                    vehicle.Mileage,
                    nextServiceMileage,
                    nextServiceDate,
                    overdueByKm,
                    overdueByDays));
            }
        }

        return due;
    }
}
