using CarRental.WebApi.Contracts;
using CarRental.WebApi.Data;
using CarRental.WebApi.Infrastructure;
using CarRental.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalOperations = CarRental.WebApi.Services.Rentals;

namespace CarRental.WebApi.Controllers;

[Authorize(Policy = ApiAuthorization.ManageRentals)]
[Route("api/rentals")]
public sealed class RentalsController(
    RentalDbContext dbContext,
    RentalOperations.IRentalService rentalService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RentalDto>>(StatusCodes.Status200OK)]
    // Один endpoint обслуговує і staff-таблицю, і self-service історію клієнта:
    // user-role автоматично звужується до власного ClientId, staff бачить повну вибірку.
    public async Task<ActionResult<IReadOnlyList<RentalDto>>> GetAll(
        [FromQuery] RentalStatus? status,
        [FromQuery] int? vehicleId,
        [FromQuery] int? clientId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? search,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var currentClientId = await GetAccessibleClientIdAsync(cancellationToken);
        if (IsCurrentUserInRole(UserRole.User) && !currentClientId.HasValue)
        {
            return Ok(Array.Empty<RentalDto>());
        }

        var query = dbContext.Rentals
            .AsNoTracking()
            .AsQueryable();
        var clients = dbContext.Clients.IgnoreQueryFilters();
        var vehicles = dbContext.Vehicles.IgnoreQueryFilters();

        if (currentClientId.HasValue && IsCurrentUserInRole(UserRole.User))
        {
            query = query.Where(item => item.ClientId == currentClientId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status.Value);
        }

        if (vehicleId.HasValue)
        {
            query = query.Where(item => item.VehicleId == vehicleId.Value);
        }

        if (clientId.HasValue)
        {
            query = query.Where(item => item.ClientId == clientId.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(item => item.StartDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(item => item.EndDate <= toDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(item =>
                EF.Functions.ILike(item.ContractNumber, pattern) ||
                clients.Any(clientRecord =>
                    clientRecord.Id == item.ClientId &&
                    EF.Functions.ILike(clientRecord.FullName, pattern)) ||
                vehicles.Any(vehicleRecord =>
                    vehicleRecord.Id == item.VehicleId &&
                    (EF.Functions.ILike(vehicleRecord.Make, pattern) ||
                     EF.Functions.ILike(vehicleRecord.Model, pattern) ||
                     EF.Functions.ILike(vehicleRecord.LicensePlate, pattern))));
        }

        var pagination = PaginationExtensions.Normalize(page, pageSize);
        if (pagination.HasValue)
        {
            var totalCount = await query.CountAsync(cancellationToken);
            Response.ApplyPaginationHeaders(pagination.Value, totalCount);
        }

        var rentals = await query
            .OrderByDescending(item => item.StartDate)
            .ProjectToRentalDto(dbContext)
            .ApplyPagination(pagination)
            .ToListAsync(cancellationToken);

        return Ok(rentals);
    }

    [HttpGet("availability")]
    [ProducesResponseType<IReadOnlyList<RentalAvailabilitySlotDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RentalAvailabilitySlotDto>>> GetAvailability(CancellationToken cancellationToken)
    {
        var rentals = await dbContext.Rentals
            .AsNoTracking()
            .Where(item => item.Status == RentalStatus.Booked || item.Status == RentalStatus.Active)
            .OrderBy(item => item.StartDate)
            .Select(item => new RentalAvailabilitySlotDto(
                item.VehicleId,
                item.StartDate,
                item.EndDate,
                item.Status))
            .ToListAsync(cancellationToken);

        return Ok(rentals);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<RentalDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var rental = await GetRentalByIdAsync(id, cancellationToken);
        return rental is null ? NotFound() : Ok(rental);
    }

    [HttpPost]
    [ProducesResponseType<RentalDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    // Створення оренди додатково перевіряє, що self-service користувач оформлює
    // бронювання лише на себе і тільки з повністю заповненим клієнтським профілем.
    public async Task<IActionResult> Create([FromBody] CreateRentalRequest request, CancellationToken cancellationToken)
    {
        var actingEmployeeId = await ResolveActingEmployeeIdAsync(cancellationToken);
        if (!actingEmployeeId.HasValue)
        {
            return Unauthorized();
        }

        if (IsCurrentUserInRole(UserRole.User))
        {
            var ownClient = await GetAccessibleClientAsync(cancellationToken);
            if (ownClient is null || ownClient.Id != request.ClientId)
            {
                return Forbid();
            }

            if (!ClientProfileRules.IsProfileComplete(ownClient))
            {
                return BadRequest(new { message = "Перед оформленням оренди завершіть профіль клієнта." });
            }
        }

        RentalOperations.CreateRentalResult result;
        if (request.CreateInitialPayment)
        {
            result = await rentalService.CreateRentalWithPaymentAsync(
                new RentalOperations.CreateRentalWithPaymentRequest(
                    request.ClientId,
                    request.VehicleId,
                    actingEmployeeId.Value,
                    request.StartDate,
                    request.EndDate,
                    request.PickupLocation,
                    request.ReturnLocation,
                    request.PaymentMethod,
                    request.PaymentDirection,
                    request.Notes),
                cancellationToken);
        }
        else
        {
            result = await rentalService.CreateRentalAsync(
                new RentalOperations.CreateRentalRequest(
                    request.ClientId,
                    request.VehicleId,
                    actingEmployeeId.Value,
                    request.StartDate,
                    request.EndDate,
                    request.PickupLocation,
                    request.ReturnLocation),
                cancellationToken);
        }

        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        var created = await GetRentalByIdAsync(result.RentalId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.RentalId }, created);
    }

    [HttpPost("{id:int}/close")]
    [ProducesResponseType<RentalDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Close(int id, [FromBody] CloseRentalRequest request, CancellationToken cancellationToken)
    {
        var employeeId = GetCurrentEmployeeId();
        if (!employeeId.HasValue)
        {
            return Unauthorized();
        }

        var accessStatus = await EnsureRentalMutationAccessAsync(id, RentalMutationType.Close, cancellationToken);
        if (accessStatus == RentalMutationAccessStatus.NotFound)
        {
            return NotFound();
        }

        if (accessStatus == RentalMutationAccessStatus.Forbidden)
        {
            return Forbid();
        }

        var result = await rentalService.CloseRentalAsync(
            new RentalOperations.CloseRentalRequest(
                RentalId: id,
                ActualEndDate: request.ActualEndDate,
                EndMileage: request.EndMileage,
                ClosedByEmployeeId: employeeId.Value,
                ReturnFuelPercent: request.ReturnFuelPercent,
                ReturnInspectionNotes: request.ReturnInspectionNotes),
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        var rental = await GetRentalByIdAsync(id, cancellationToken);
        return rental is null ? NotFound() : Ok(rental);
    }

    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType<RentalDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelRentalRequest request, CancellationToken cancellationToken)
    {
        var accessStatus = await EnsureRentalMutationAccessAsync(id, RentalMutationType.Cancel, cancellationToken);
        if (accessStatus == RentalMutationAccessStatus.NotFound)
        {
            return NotFound();
        }

        if (accessStatus == RentalMutationAccessStatus.Forbidden)
        {
            return Forbid();
        }

        var canceledByEmployeeId = IsCurrentUserInRole(UserRole.User)
            ? await ResolveActingEmployeeIdAsync(cancellationToken)
            : GetCurrentEmployeeId();
        if (!canceledByEmployeeId.HasValue)
        {
            return Unauthorized();
        }

        var result = await rentalService.CancelRentalAsync(
            new RentalOperations.CancelRentalRequest(
                RentalId: id,
                Reason: request.Reason,
                CanceledByEmployeeId: canceledByEmployeeId.Value),
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        var rental = await GetRentalByIdAsync(id, cancellationToken);
        return rental is null ? NotFound() : Ok(rental);
    }

    [HttpPost("{id:int}/reschedule")]
    [ProducesResponseType<RentalDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reschedule(
        int id,
        [FromBody] RescheduleRentalRequest request,
        CancellationToken cancellationToken)
    {
        var employeeId = await ResolveActingEmployeeIdAsync(cancellationToken);
        if (!employeeId.HasValue)
        {
            return Unauthorized();
        }

        var accessStatus = await EnsureRentalMutationAccessAsync(id, RentalMutationType.Reschedule, cancellationToken);
        if (accessStatus == RentalMutationAccessStatus.NotFound)
        {
            return NotFound();
        }

        if (accessStatus == RentalMutationAccessStatus.Forbidden)
        {
            return Forbid();
        }

        var result = await rentalService.RescheduleRentalAsync(
            new RentalOperations.RescheduleRentalRequest(
                id,
                request.StartDate,
                request.EndDate,
                employeeId.Value),
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        var rental = await GetRentalByIdAsync(id, cancellationToken);
        return rental is null ? NotFound() : Ok(rental);
    }

    [HttpPost("{id:int}/settle-balance")]
    [ProducesResponseType<RentalDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SettleBalance(
        int id,
        [FromBody] SettleRentalBalanceRequest request,
        CancellationToken cancellationToken)
    {
        var employeeId = await ResolveActingEmployeeIdAsync(cancellationToken);
        if (!employeeId.HasValue)
        {
            return Unauthorized();
        }

        var accessStatus = await EnsureRentalMutationAccessAsync(id, RentalMutationType.SettleBalance, cancellationToken);
        if (accessStatus == RentalMutationAccessStatus.NotFound)
        {
            return NotFound();
        }

        if (accessStatus == RentalMutationAccessStatus.Forbidden)
        {
            return Forbid();
        }

        var result = await rentalService.SettleRentalBalanceAsync(
            new RentalOperations.SettleRentalBalanceRequest(id, employeeId.Value, request.Notes),
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        var rental = await GetRentalByIdAsync(id, cancellationToken);
        return rental is null ? NotFound() : Ok(rental);
    }

    [HttpPost("{id:int}/pickup-inspection")]
    [ProducesResponseType<RentalDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompletePickupInspection(
        int id,
        [FromBody] PickupInspectionRequest request,
        CancellationToken cancellationToken)
    {
        var employeeId = GetCurrentEmployeeId();
        if (!employeeId.HasValue)
        {
            return Unauthorized();
        }

        var accessStatus = await EnsureRentalMutationAccessAsync(id, RentalMutationType.PickupInspection, cancellationToken);
        if (accessStatus == RentalMutationAccessStatus.NotFound)
        {
            return NotFound();
        }

        if (accessStatus == RentalMutationAccessStatus.Forbidden)
        {
            return Forbid();
        }

        var result = await rentalService.CompletePickupInspectionAsync(
            new RentalOperations.PickupInspectionRequest(
                RentalId: id,
                FuelPercent: request.FuelPercent,
                Notes: request.Notes,
                PerformedByEmployeeId: employeeId.Value),
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        var rental = await GetRentalByIdAsync(id, cancellationToken);
        return rental is null ? NotFound() : Ok(rental);
    }

    [Authorize(Policy = ApiAuthorization.ManageRentals)]
    [HttpPost("refresh-statuses")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RefreshStatuses(CancellationToken cancellationToken)
    {
        await rentalService.RefreshStatusesAsync(cancellationToken);
        return Ok(new { message = "Rental statuses refreshed." });
    }

    private async Task<int?> ResolveActingEmployeeIdAsync(CancellationToken cancellationToken)
    {
        var employeeId = GetCurrentEmployeeId();
        if (employeeId.HasValue)
        {
            return employeeId;
        }

        return await dbContext.Employees
            .AsNoTracking()
            .OrderBy(item => item.Role == UserRole.Admin ? 0 : 1)
            .ThenBy(item => item.Id)
            .Select(item => (int?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<int?> GetAccessibleClientIdAsync(CancellationToken cancellationToken)
    {
        if (!IsCurrentUserInRole(UserRole.User))
        {
            return null;
        }

        var clientId = GetCurrentClientId();
        if (clientId.HasValue)
        {
            return clientId;
        }

        var accountId = GetCurrentAccountId();
        if (!accountId.HasValue)
        {
            return null;
        }

        return await dbContext.Clients
            .AsNoTracking()
            .Where(item => item.AccountId == accountId.Value)
            .Select(item => (int?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Client?> GetAccessibleClientAsync(CancellationToken cancellationToken)
    {
        var clientId = await GetAccessibleClientIdAsync(cancellationToken);
        if (!clientId.HasValue)
        {
            return null;
        }

        return await dbContext.Clients
            .Include(item => item.Documents)
            .FirstOrDefaultAsync(item => item.Id == clientId.Value, cancellationToken);
    }

    private async Task<RentalMutationAccessStatus> EnsureRentalMutationAccessAsync(
        int rentalId,
        RentalMutationType mutationType,
        CancellationToken cancellationToken)
    {
        var rental = await dbContext.Rentals
            .AsNoTracking()
            .Where(item => item.Id == rentalId)
            .Select(item => new { item.ClientId })
            .FirstOrDefaultAsync(cancellationToken);

        if (rental is null)
        {
            return RentalMutationAccessStatus.NotFound;
        }

        if (!IsCurrentUserInRole(UserRole.User))
        {
            return RentalMutationAccessStatus.Allowed;
        }

        var ownClientId = await GetAccessibleClientIdAsync(cancellationToken);
        if (!ownClientId.HasValue || ownClientId.Value != rental.ClientId)
        {
            return RentalMutationAccessStatus.Forbidden;
        }

        if (mutationType is RentalMutationType.Close or RentalMutationType.PickupInspection)
        {
            return RentalMutationAccessStatus.Forbidden;
        }

        return RentalMutationAccessStatus.Allowed;
    }

    private async Task<RentalDto?> GetRentalByIdAsync(int id, CancellationToken cancellationToken)
    {
        var query = dbContext.Rentals
            .AsNoTracking()
            .Where(item => item.Id == id);

        if (IsCurrentUserInRole(UserRole.User))
        {
            var ownClientId = await GetAccessibleClientIdAsync(cancellationToken);
            query = ownClientId.HasValue
                ? query.Where(item => item.ClientId == ownClientId.Value)
                : query.Where(_ => false);
        }

        return await query
            .ProjectToRentalDto(dbContext)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private enum RentalMutationAccessStatus
    {
        Allowed = 0,
        Forbidden = 1,
        NotFound = 2
    }

    private enum RentalMutationType
    {
        Close = 0,
        Cancel = 1,
        Reschedule = 2,
        SettleBalance = 3,
        PickupInspection = 4
    }

    public sealed record RentalAvailabilitySlotDto(
        int VehicleId,
        DateTime StartDate,
        DateTime EndDate,
        RentalStatus Status);
}
