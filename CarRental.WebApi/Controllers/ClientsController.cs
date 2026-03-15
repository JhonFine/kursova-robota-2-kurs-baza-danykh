using CarRental.WebApi.Contracts;
using CarRental.WebApi.Data;
using CarRental.WebApi.Infrastructure;
using CarRental.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRental.WebApi.Controllers;

[Authorize(Policy = ApiAuthorization.ManageClients)]
[Route("api/clients")]
public sealed class ClientsController(RentalDbContext dbContext) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ClientDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClientDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] bool? blacklisted,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var pagination = PaginationExtensions.Normalize(page, pageSize);
        var query = dbContext.Clients
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(item =>
                EF.Functions.ILike(item.FullName, pattern) ||
                EF.Functions.ILike(item.Phone, pattern) ||
                EF.Functions.ILike(item.DriverLicense, pattern));
        }

        if (blacklisted.HasValue)
        {
            query = query.Where(item => item.Blacklisted == blacklisted.Value);
        }

        query = query.OrderBy(item => item.FullName);

        if (pagination.HasValue)
        {
            var totalCount = await query.CountAsync(cancellationToken);
            Response.ApplyPaginationHeaders(pagination.Value, totalCount);
        }

        var clients = await query
            .ApplyPagination(pagination)
            .Select(item => new ClientDto(
                item.Id,
                item.FullName,
                item.PassportData,
                item.DriverLicense,
                item.Phone,
                item.Blacklisted))
            .ToListAsync(cancellationToken);

        return Ok(clients);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<ClientDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var client = await dbContext.Clients
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new ClientDto(
                item.Id,
                item.FullName,
                item.PassportData,
                item.DriverLicense,
                item.Phone,
                item.Blacklisted))
            .FirstOrDefaultAsync(cancellationToken);

        return client is null ? NotFound() : Ok(client);
    }

    [HttpPost]
    [ProducesResponseType<ClientDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] ClientUpsertRequest request, CancellationToken cancellationToken)
    {
        var normalizedLicense = request.DriverLicense.Trim();
        var licenseExists = await dbContext.Clients
            .AnyAsync(item => item.DriverLicense == normalizedLicense, cancellationToken);
        if (licenseExists)
        {
            return Conflict(new { message = "Driver license already exists." });
        }

        var entity = new Client
        {
            FullName = request.FullName.Trim(),
            PassportData = request.PassportData.Trim(),
            DriverLicense = normalizedLicense,
            Phone = request.Phone.Trim(),
            Blacklisted = request.Blacklisted
        };

        dbContext.Clients.Add(entity);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return Conflict(new { message = "Driver license already exists." });
        }

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToDto(entity));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<ClientDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, [FromBody] ClientUpsertRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Clients.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var normalizedLicense = request.DriverLicense.Trim();
        var licenseExists = await dbContext.Clients
            .AnyAsync(item => item.Id != id && item.DriverLicense == normalizedLicense, cancellationToken);
        if (licenseExists)
        {
            return Conflict(new { message = "Driver license already exists." });
        }

        entity.FullName = request.FullName.Trim();
        entity.PassportData = request.PassportData.Trim();
        entity.DriverLicense = normalizedLicense;
        entity.Phone = request.Phone.Trim();
        entity.Blacklisted = request.Blacklisted;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return Conflict(new { message = "Driver license already exists." });
        }

        return Ok(ToDto(entity));
    }

    [HttpPatch("{id:int}/blacklist")]
    [ProducesResponseType<ClientDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetBlacklist(int id, [FromBody] SetBlacklistRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Clients.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        entity.Blacklisted = request.Blacklisted;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(entity));
    }

    [Authorize(Policy = ApiAuthorization.DeleteRecords)]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Clients.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var hasRentals = await dbContext.Rentals.AnyAsync(item => item.ClientId == id, cancellationToken);
        if (hasRentals)
        {
            return Conflict(new { message = "Cannot delete client with rentals history." });
        }

        var linkedToEmployee = await dbContext.Employees.AnyAsync(item => item.ClientId == id, cancellationToken);
        if (linkedToEmployee)
        {
            return Conflict(new { message = "Cannot delete client linked to an employee account." });
        }

        dbContext.Clients.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static ClientDto ToDto(Client entity)
        => new(
            entity.Id,
            entity.FullName,
            entity.PassportData,
            entity.DriverLicense,
            entity.Phone,
            entity.Blacklisted);
}
