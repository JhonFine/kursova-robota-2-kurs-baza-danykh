using CarRental.WebApi.Contracts;
using CarRental.WebApi.Data;
using CarRental.WebApi.Infrastructure;
using CarRental.WebApi.Services.Damages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRental.WebApi.Controllers;

[Authorize(Policy = ApiAuthorization.ManageDamages)]
[Route("api/damages")]
public sealed class DamagesController(
    RentalDbContext dbContext,
    IDamageService damageService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DamageDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DamageDto>>> GetAll(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var pagination = PaginationExtensions.Normalize(page, pageSize);
        var query = dbContext.Damages
            .AsNoTracking()
            .OrderByDescending(item => item.DateReported);

        if (pagination.HasValue)
        {
            var totalCount = await query.CountAsync(cancellationToken);
            Response.ApplyPaginationHeaders(pagination.Value, totalCount);
        }

        var damages = await query
            .ApplyPagination(pagination)
            .Select(item => new DamageDto(
                item.Id,
                item.VehicleId,
                item.Vehicle != null ? $"{item.Vehicle.Make} {item.Vehicle.Model} [{item.Vehicle.LicensePlate}]" : string.Empty,
                item.RentalId,
                item.Rental != null ? item.Rental.ContractNumber : null,
                item.Description,
                item.DateReported,
                item.RepairCost,
                item.PhotoPath,
                item.ActNumber,
                item.ChargedAmount,
                item.IsChargedToClient,
                item.Status))
            .ToListAsync(cancellationToken);

        return Ok(damages);
    }

    [HttpPost]
    [ProducesResponseType<DamageDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Add([FromBody] AddDamageRequest request, CancellationToken cancellationToken)
    {
        var result = await damageService.AddDamageAsync(
            new DamageRequest(
                request.VehicleId,
                request.RentalId,
                request.Description,
                request.RepairCost,
                request.PhotoPath,
                request.AutoChargeToRental),
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { message = "Damage registration failed. Check vehicle, rental, and amount." });
        }

        var damage = await dbContext.Damages
            .AsNoTracking()
            .Where(item => item.Id == result.DamageId)
            .Select(item => new DamageDto(
                item.Id,
                item.VehicleId,
                item.Vehicle != null ? $"{item.Vehicle.Make} {item.Vehicle.Model} [{item.Vehicle.LicensePlate}]" : string.Empty,
                item.RentalId,
                item.Rental != null ? item.Rental.ContractNumber : null,
                item.Description,
                item.DateReported,
                item.RepairCost,
                item.PhotoPath,
                item.ActNumber,
                item.ChargedAmount,
                item.IsChargedToClient,
                item.Status))
            .FirstOrDefaultAsync(cancellationToken);

        if (damage is null)
        {
            return BadRequest(new { message = "Damage created but could not be loaded." });
        }

        return CreatedAtAction(nameof(GetAll), damage);
    }
}
