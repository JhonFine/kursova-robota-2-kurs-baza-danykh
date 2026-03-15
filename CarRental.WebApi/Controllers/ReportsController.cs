using CarRental.WebApi.Contracts;
using CarRental.WebApi.Data;
using CarRental.WebApi.Infrastructure;
using CarRental.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRental.WebApi.Controllers;

[Authorize(Policy = ApiAuthorization.ExportReports)]
[Route("api/reports")]
public sealed class ReportsController(RentalDbContext dbContext) : ApiControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType<ReportSummaryDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReportSummaryDto>> Summary(CancellationToken cancellationToken)
    {
        var today = DateTime.Today;

        var totalRentals = await dbContext.Rentals.AsNoTracking().CountAsync(cancellationToken);
        var activeRentals = await dbContext.Rentals
            .AsNoTracking()
            .CountAsync(
                rental => rental.Status == RentalStatus.Active && rental.StartDate <= today && today <= rental.EndDate,
                cancellationToken);

        var totalRevenue = await dbContext.Rentals
            .AsNoTracking()
            .Where(rental => rental.Status == RentalStatus.Closed)
            .SumAsync(rental => (decimal?)rental.TotalAmount, cancellationToken) ?? 0m;

        var totalDamageCost = await dbContext.Damages
            .AsNoTracking()
            .SumAsync(damage => (decimal?)damage.RepairCost, cancellationToken) ?? 0m;

        return Ok(new ReportSummaryDto(totalRentals, activeRentals, totalRevenue, totalDamageCost));
    }

    [HttpGet("rentals")]
    [ProducesResponseType<IReadOnlyList<RentalDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RentalDto>>> Rentals(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        [FromQuery] int? vehicleId,
        [FromQuery] int? employeeId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Rentals
            .AsNoTracking()
            .Where(item => item.CreatedAtUtc >= fromDate && item.CreatedAtUtc <= toDate);

        if (vehicleId.HasValue)
        {
            query = query.Where(item => item.VehicleId == vehicleId.Value);
        }

        if (employeeId.HasValue)
        {
            query = query.Where(item => item.EmployeeId == employeeId.Value);
        }

        var pagination = PaginationExtensions.Normalize(page, pageSize);
        if (pagination.HasValue)
        {
            var totalCount = await query.CountAsync(cancellationToken);
            Response.ApplyPaginationHeaders(pagination.Value, totalCount);
        }

        var rentals = await query
            .OrderByDescending(item => item.CreatedAtUtc)
            .ApplyPagination(pagination)
            .Select(item => new
            {
                item.Id,
                item.ContractNumber,
                item.ClientId,
                ClientName = item.Client != null ? item.Client.FullName : string.Empty,
                item.VehicleId,
                VehicleName = item.Vehicle != null ? $"{item.Vehicle.Make} {item.Vehicle.Model} [{item.Vehicle.LicensePlate}]" : string.Empty,
                item.EmployeeId,
                EmployeeName = item.Employee != null ? item.Employee.FullName : string.Empty,
                item.StartDate,
                item.EndDate,
                item.PickupLocation,
                item.ReturnLocation,
                item.StartMileage,
                item.EndMileage,
                item.Status,
                item.TotalAmount,
                item.OverageFee,
                PaidAmount = item.Payments.Sum(payment => (decimal?)(
                    payment.Direction == PaymentDirection.Incoming
                        ? payment.Amount
                        : payment.Direction == PaymentDirection.Refund
                            ? -payment.Amount
                            : 0m)) ?? 0m,
                item.CreatedAtUtc,
                item.ClosedAtUtc,
                item.CanceledAtUtc,
                item.CancellationReason,
                item.PickupInspectionCompletedAtUtc,
                item.PickupFuelPercent,
                item.PickupInspectionNotes,
                item.ReturnInspectionCompletedAtUtc,
                item.ReturnFuelPercent,
                item.ReturnInspectionNotes
            })
            .Select(item => new RentalDto(
                item.Id,
                item.ContractNumber,
                item.ClientId,
                item.ClientName,
                item.VehicleId,
                item.VehicleName,
                item.EmployeeId,
                item.EmployeeName,
                item.StartDate,
                item.EndDate,
                item.PickupLocation,
                item.ReturnLocation,
                item.StartMileage,
                item.EndMileage,
                item.Status,
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
                item.ReturnInspectionCompletedAtUtc,
                item.ReturnFuelPercent,
                item.ReturnInspectionNotes))
            .ToListAsync(cancellationToken);

        return Ok(rentals);
    }
}
