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
        var today = DateTime.UtcNow.Date;

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
            query = query.Where(item => item.CreatedByEmployeeId == employeeId.Value);
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
            .ProjectToRentalDto(dbContext)
            .ToListAsync(cancellationToken);

        return Ok(rentals);
    }
}
