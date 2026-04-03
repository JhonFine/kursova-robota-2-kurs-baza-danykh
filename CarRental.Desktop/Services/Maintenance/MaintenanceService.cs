using CarRental.Desktop.Data;
using CarRental.Desktop.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Desktop.Services.Maintenance;

public sealed class MaintenanceService(RentalDbContext dbContext) : IMaintenanceService
{
    private const int SoonMileageThresholdKm = 1000;
    private const int SoonDaysThreshold = 14;
    private const int MileageForecastLookbackDays = 90;

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
        var today = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Unspecified);
        var lookbackDate = today.AddDays(-MileageForecastLookbackDays);

        var recentClosedRentals = await dbContext.Rentals
            .AsNoTracking()
            .Where(item =>
                item.StatusId == RentalStatus.Closed &&
                item.EndMileage.HasValue &&
                item.EndMileage.Value > item.StartMileage &&
                item.EndDate.Date >= lookbackDate)
            .Select(item => new
            {
                item.VehicleId,
                item.StartDate,
                item.EndDate,
                item.StartMileage,
                EndMileage = item.EndMileage!.Value
            })
            .ToListAsync(cancellationToken);

        var averageDailyMileageByVehicleId = recentClosedRentals
            .GroupBy(item => item.VehicleId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var totalDistance = 0m;
                    var totalDays = 0m;

                    foreach (var rental in group)
                    {
                        var distance = rental.EndMileage - rental.StartMileage;
                        if (distance <= 0)
                        {
                            continue;
                        }

                        var days = Math.Max(1, (rental.EndDate.Date - rental.StartDate.Date).Days);
                        totalDistance += distance;
                        totalDays += days;
                    }

                    return totalDistance > 0m && totalDays > 0m
                        ? totalDistance / totalDays
                        : 0m;
                });

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
                    .FirstOrDefault(),
                HasRecordedNextServiceTarget = item.MaintenanceRecords
                    .OrderByDescending(record => record.ServiceDate)
                    .Select(record => record.NextServiceMileage.HasValue || record.NextServiceDate.HasValue)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var due = new List<MaintenanceDueItem>();
        foreach (var vehicle in vehicles)
        {
            var nextServiceMileage = vehicle.LastNextServiceMileage ?? (vehicle.Mileage + vehicle.ServiceIntervalKm);
            var nextServiceDate = vehicle.LastNextServiceDate?.Date;
            var distanceToNextServiceKm = nextServiceMileage - vehicle.Mileage;

            averageDailyMileageByVehicleId.TryGetValue(vehicle.Id, out var averageDailyMileage);
            var daysToNextService = CalculateDaysToNextService(today, nextServiceDate, distanceToNextServiceKm, averageDailyMileage);
            var forecastStatus = ResolveForecastStatus(today, nextServiceDate, distanceToNextServiceKm, daysToNextService);
            if (forecastStatus == MaintenanceForecastStatus.OnTrack)
            {
                continue;
            }

            var forecastNotes = vehicle.HasRecordedNextServiceTarget
                ? "Заплановано за останнім сервісним записом"
                : $"Планове ТО за інтервалом {vehicle.ServiceIntervalKm:N0} км";

            due.Add(new MaintenanceDueItem(
                vehicle.Id,
                $"{vehicle.MakeName} {vehicle.ModelName} [{vehicle.LicensePlate}]",
                vehicle.Mileage,
                nextServiceMileage,
                nextServiceDate,
                distanceToNextServiceKm,
                daysToNextService,
                forecastStatus,
                forecastNotes));
        }

        return due
            .OrderBy(item => item.ForecastStatus)
            .ThenBy(item => item.DaysToNextService ?? int.MaxValue)
            .ThenBy(item => item.DistanceToNextServiceKm ?? int.MaxValue)
            .ToList();
    }

    private static int? CalculateDaysToNextService(
        DateTime today,
        DateTime? nextServiceDate,
        int? distanceToNextServiceKm,
        decimal averageDailyMileage)
    {
        if (nextServiceDate.HasValue)
        {
            return (nextServiceDate.Value.Date - today).Days;
        }

        if (!distanceToNextServiceKm.HasValue || averageDailyMileage <= 0m)
        {
            return null;
        }

        var estimatedDays = distanceToNextServiceKm.Value / averageDailyMileage;
        return estimatedDays >= 0m
            ? (int)Math.Ceiling(estimatedDays)
            : (int)Math.Floor(estimatedDays);
    }

    private static MaintenanceForecastStatus ResolveForecastStatus(
        DateTime today,
        DateTime? nextServiceDate,
        int? distanceToNextServiceKm,
        int? daysToNextService)
    {
        var isOverdue = (distanceToNextServiceKm.HasValue && distanceToNextServiceKm.Value <= 0) ||
                        (nextServiceDate.HasValue && today > nextServiceDate.Value.Date);
        if (isOverdue)
        {
            return MaintenanceForecastStatus.Overdue;
        }

        var isSoon = (distanceToNextServiceKm.HasValue && distanceToNextServiceKm.Value <= SoonMileageThresholdKm) ||
                     (daysToNextService.HasValue && daysToNextService.Value <= SoonDaysThreshold);

        return isSoon
            ? MaintenanceForecastStatus.Soon
            : MaintenanceForecastStatus.OnTrack;
    }
}
