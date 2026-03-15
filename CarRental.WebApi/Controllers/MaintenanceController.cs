using CarRental.WebApi.Contracts;
using CarRental.WebApi.Data;
using CarRental.WebApi.Infrastructure;
using CarRental.WebApi.Services.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRental.WebApi.Controllers;

[Authorize(Policy = ApiAuthorization.ManageMaintenance)]
[Route("api/maintenance")]
public sealed class MaintenanceController(
    RentalDbContext dbContext,
    IMaintenanceService maintenanceService) : ApiControllerBase
{
    [HttpGet("records")]
    [ProducesResponseType<IReadOnlyList<MaintenanceRecordDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MaintenanceRecordDto>>> GetRecords(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var pagination = PaginationExtensions.Normalize(page, pageSize);
        var query = dbContext.MaintenanceRecords
            .AsNoTracking()
            .OrderByDescending(item => item.ServiceDate);

        if (pagination.HasValue)
        {
            var totalCount = await query.CountAsync(cancellationToken);
            Response.ApplyPaginationHeaders(pagination.Value, totalCount);
        }

        var records = await query
            .ApplyPagination(pagination)
            .Select(item => new MaintenanceRecordDto(
                item.Id,
                item.VehicleId,
                item.Vehicle != null ? $"{item.Vehicle.Make} {item.Vehicle.Model} [{item.Vehicle.LicensePlate}]" : string.Empty,
                item.ServiceDate,
                item.MileageAtService,
                item.Description,
                item.Cost,
                item.NextServiceMileage))
            .ToListAsync(cancellationToken);

        return Ok(records);
    }

    [HttpGet("due")]
    [ProducesResponseType<IReadOnlyList<MaintenanceDueDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MaintenanceDueDto>>> GetDue(CancellationToken cancellationToken)
    {
        var due = await maintenanceService.GetDueItemsAsync(cancellationToken);
        return Ok(due.Select(item => new MaintenanceDueDto(
            item.VehicleId,
            item.Vehicle,
            item.CurrentMileage,
            item.NextServiceMileage,
            item.OverdueByKm)).ToList());
    }

    [HttpPost("records")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddRecord([FromBody] AddMaintenanceRecordRequest request, CancellationToken cancellationToken)
    {
        var result = await maintenanceService.AddRecordAsync(
            new MaintenanceRequest(
                request.VehicleId,
                request.ServiceDate,
                request.MileageAtService,
                request.Description,
                request.Cost,
                request.NextServiceMileage),
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { message = "Maintenance record creation failed. Check vehicle and mileage values." });
        }

        return StatusCode(StatusCodes.Status201Created, new { message = "Maintenance record created." });
    }
}
