using CarRental.WebApi.Contracts;
using CarRental.WebApi.Data;
using CarRental.WebApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRental.WebApi.Infrastructure;

public static class RentalQueryProjections
{
    // DTO будуємо проєкцією в SQL замість каскаду Include, щоб таблиці оренд,
    // звітів і self-service списки забирали лише потрібні колонки.
    public static IQueryable<RentalDto> ProjectToRentalDto(this IQueryable<Rental> query, RentalDbContext dbContext)
    {
        var clients = dbContext.Clients.IgnoreQueryFilters();
        var vehicles = dbContext.Vehicles.IgnoreQueryFilters();
        var employees = dbContext.Employees.AsNoTracking();

        return query
            .Select(item => new
            {
                item.Id,
                item.ContractNumber,
                item.ClientId,
                ClientName = clients
                    .Where(client => client.Id == item.ClientId)
                    .Select(client => client.FullName)
                    .FirstOrDefault() ?? string.Empty,
                item.VehicleId,
                VehicleName = vehicles
                    .Where(vehicle => vehicle.Id == item.VehicleId)
                    .Select(vehicle => vehicle.MakeLookup!.Name + " " + vehicle.ModelLookup!.Name + " [" + vehicle.LicensePlate + "]")
                    .FirstOrDefault() ?? string.Empty,
                item.CreatedByEmployeeId,
                CreatedByEmployeeName = employees
                    .Where(employee => employee.Id == item.CreatedByEmployeeId)
                    .Select(employee => employee.FullName)
                    .FirstOrDefault(),
                item.ClosedByEmployeeId,
                ClosedByEmployeeName = employees
                    .Where(employee => employee.Id == item.ClosedByEmployeeId)
                    .Select(employee => employee.FullName)
                    .FirstOrDefault(),
                item.CanceledByEmployeeId,
                CanceledByEmployeeName = employees
                    .Where(employee => employee.Id == item.CanceledByEmployeeId)
                    .Select(employee => employee.FullName)
                    .FirstOrDefault(),
                item.StartDate,
                item.EndDate,
                item.PickupLocation,
                item.ReturnLocation,
                item.StartMileage,
                item.EndMileage,
                item.StatusId,
                item.TotalAmount,
                item.OverageFee,
                PaidAmount = item.Payments
                    .Where(payment => payment.StatusId == PaymentStatus.Completed)
                    .Sum(payment => (decimal?)(
                        payment.DirectionId == PaymentDirection.Incoming
                            ? payment.Amount
                            : payment.DirectionId == PaymentDirection.Refund
                                ? -payment.Amount
                                : 0m)) ?? 0m,
                item.CreatedAtUtc,
                item.ClosedAtUtc,
                item.CanceledAtUtc,
                item.CancellationReason,
                PickupInspectionCompletedAtUtc = item.Inspections
                    .Where(inspection => inspection.TypeId == RentalInspectionType.Pickup)
                    .Select(inspection => (DateTime?)inspection.CompletedAtUtc)
                    .FirstOrDefault(),
                PickupFuelPercent = item.Inspections
                    .Where(inspection => inspection.TypeId == RentalInspectionType.Pickup)
                    .Select(inspection => inspection.FuelPercent)
                    .FirstOrDefault(),
                PickupInspectionNotes = item.Inspections
                    .Where(inspection => inspection.TypeId == RentalInspectionType.Pickup)
                    .Select(inspection => inspection.Notes)
                    .FirstOrDefault(),
                PickupInspectionPerformedByEmployeeId = item.Inspections
                    .Where(inspection => inspection.TypeId == RentalInspectionType.Pickup)
                    .Select(inspection => (int?)inspection.PerformedByEmployeeId)
                    .FirstOrDefault(),
                PickupInspectionPerformedByEmployeeName = item.Inspections
                    .Where(inspection => inspection.TypeId == RentalInspectionType.Pickup)
                    .Select(inspection => inspection.PerformedByEmployee!.FullName)
                    .FirstOrDefault(),
                ReturnInspectionCompletedAtUtc = item.Inspections
                    .Where(inspection => inspection.TypeId == RentalInspectionType.Return)
                    .Select(inspection => (DateTime?)inspection.CompletedAtUtc)
                    .FirstOrDefault(),
                ReturnFuelPercent = item.Inspections
                    .Where(inspection => inspection.TypeId == RentalInspectionType.Return)
                    .Select(inspection => inspection.FuelPercent)
                    .FirstOrDefault(),
                ReturnInspectionNotes = item.Inspections
                    .Where(inspection => inspection.TypeId == RentalInspectionType.Return)
                    .Select(inspection => inspection.Notes)
                    .FirstOrDefault(),
                ReturnInspectionPerformedByEmployeeId = item.Inspections
                    .Where(inspection => inspection.TypeId == RentalInspectionType.Return)
                    .Select(inspection => (int?)inspection.PerformedByEmployeeId)
                    .FirstOrDefault(),
                ReturnInspectionPerformedByEmployeeName = item.Inspections
                    .Where(inspection => inspection.TypeId == RentalInspectionType.Return)
                    .Select(inspection => inspection.PerformedByEmployee!.FullName)
                    .FirstOrDefault()
            })
            .Select(item => new RentalDto(
                item.Id,
                item.ContractNumber,
                item.ClientId,
                item.ClientName,
                item.VehicleId,
                item.VehicleName,
                item.CreatedByEmployeeId,
                item.CreatedByEmployeeName,
                item.ClosedByEmployeeId,
                item.ClosedByEmployeeName,
                item.CanceledByEmployeeId,
                item.CanceledByEmployeeName,
                item.StartDate,
                item.EndDate,
                item.PickupLocation,
                item.ReturnLocation,
                item.StartMileage,
                item.EndMileage,
                item.StatusId,
                item.TotalAmount,
                item.OverageFee,
                item.PaidAmount,
                item.TotalAmount - item.PaidAmount,
                item.CreatedAtUtc,
                item.ClosedAtUtc,
                item.CanceledAtUtc,
                item.CancellationReason,
                item.PickupInspectionCompletedAtUtc,
                item.PickupFuelPercent,
                item.PickupInspectionNotes,
                item.PickupInspectionPerformedByEmployeeId,
                item.PickupInspectionPerformedByEmployeeName,
                item.ReturnInspectionCompletedAtUtc,
                item.ReturnFuelPercent,
                item.ReturnInspectionNotes,
                item.ReturnInspectionPerformedByEmployeeId,
                item.ReturnInspectionPerformedByEmployeeName));
    }
}
