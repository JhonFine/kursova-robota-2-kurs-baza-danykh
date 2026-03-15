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
        var employeeId = GetCurrentEmployeeId();
        if (!employeeId.HasValue)
        {
            return Unauthorized();
        }

        var query = dbContext.Rentals
            .AsNoTracking()
            .AsQueryable();

        if (IsCurrentUserInRole(UserRole.User))
        {
            var ownClientId = await GetCurrentUserClientIdAsync(employeeId.Value, cancellationToken);
            query = ownClientId.HasValue
                ? query.Where(item => item.ClientId == ownClientId.Value)
                : query.Where(_ => false);
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
                (item.Client != null && EF.Functions.ILike(item.Client.FullName, pattern)) ||
                (item.Vehicle != null && (
                    EF.Functions.ILike(item.Vehicle.Make, pattern) ||
                    EF.Functions.ILike(item.Vehicle.Model, pattern) ||
                    EF.Functions.ILike(item.Vehicle.LicensePlate, pattern))));
        }

        var pagination = PaginationExtensions.Normalize(page, pageSize);
        if (pagination.HasValue)
        {
            var totalCount = await query.CountAsync(cancellationToken);
            Response.ApplyPaginationHeaders(pagination.Value, totalCount);
        }

        var rentals = await ProjectRentals(
                query.OrderByDescending(item => item.StartDate))
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
        var employeeId = GetCurrentEmployeeId();
        if (!employeeId.HasValue)
        {
            return Unauthorized();
        }

        var rental = await GetRentalByIdAsync(id, employeeId.Value, cancellationToken);
        return rental is null ? NotFound() : Ok(rental);
    }

    [HttpPost]
    [ProducesResponseType<RentalDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateRentalRequest request, CancellationToken cancellationToken)
    {
        var currentEmployeeId = GetCurrentEmployeeId();
        if (!currentEmployeeId.HasValue)
        {
            return Unauthorized();
        }

        if (IsCurrentUserInRole(UserRole.User))
        {
            if (request.StartDate < DateTime.Now)
            {
                return BadRequest(new { message = "Клієнт не може створити бронювання з початком у минулому." });
            }

            var ownClient = await GetCurrentUserClientAsync(currentEmployeeId.Value, cancellationToken);
            if (ownClient is null || ownClient.Id != request.ClientId)
            {
                return Forbid();
            }

            if (!IsProfileComplete(ownClient))
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
                    currentEmployeeId.Value,
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
                    currentEmployeeId.Value,
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

        var created = await GetRentalByIdAsync(result.RentalId, currentEmployeeId.Value, cancellationToken);
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

        var accessStatus = await EnsureRentalMutationAccessAsync(
            id,
            employeeId.Value,
            RentalMutationType.Close,
            cancellationToken);
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
                id,
                request.ActualEndDate,
                request.EndMileage,
                request.ReturnFuelPercent,
                request.ReturnInspectionNotes),
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        var rental = await GetRentalByIdAsync(id, employeeId.Value, cancellationToken);
        if (rental is null)
        {
            return NotFound();
        }

        return Ok(rental);
    }

    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType<RentalDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelRentalRequest request, CancellationToken cancellationToken)
    {
        var employeeId = GetCurrentEmployeeId();
        if (!employeeId.HasValue)
        {
            return Unauthorized();
        }

        var accessStatus = await EnsureRentalMutationAccessAsync(
            id,
            employeeId.Value,
            RentalMutationType.Cancel,
            cancellationToken);
        if (accessStatus == RentalMutationAccessStatus.NotFound)
        {
            return NotFound();
        }

        if (accessStatus == RentalMutationAccessStatus.InvalidState)
        {
            return BadRequest(new { message = "Only booked rentals can be canceled in self-service." });
        }

        if (accessStatus == RentalMutationAccessStatus.Forbidden)
        {
            return Forbid();
        }

        var result = await rentalService.CancelRentalAsync(
            new RentalOperations.CancelRentalRequest(id, request.Reason),
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        var rental = await GetRentalByIdAsync(id, employeeId.Value, cancellationToken);
        if (rental is null)
        {
            return NotFound();
        }

        return Ok(rental);
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
        var employeeId = GetCurrentEmployeeId();
        if (!employeeId.HasValue)
        {
            return Unauthorized();
        }

        var accessStatus = await EnsureRentalMutationAccessAsync(
            id,
            employeeId.Value,
            RentalMutationType.Reschedule,
            cancellationToken);
        if (accessStatus == RentalMutationAccessStatus.NotFound)
        {
            return NotFound();
        }

        if (accessStatus == RentalMutationAccessStatus.InvalidState)
        {
            return BadRequest(new { message = "Only booked rentals can be rescheduled in self-service." });
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

        var rental = await GetRentalByIdAsync(id, employeeId.Value, cancellationToken);
        if (rental is null)
        {
            return NotFound();
        }

        return Ok(rental);
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
        var employeeId = GetCurrentEmployeeId();
        if (!employeeId.HasValue)
        {
            return Unauthorized();
        }

        var accessStatus = await EnsureRentalMutationAccessAsync(
            id,
            employeeId.Value,
            RentalMutationType.SettleBalance,
            cancellationToken);
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

        var rental = await GetRentalByIdAsync(id, employeeId.Value, cancellationToken);
        if (rental is null)
        {
            return NotFound();
        }

        return Ok(rental);
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

        var accessStatus = await EnsureRentalMutationAccessAsync(
            id,
            employeeId.Value,
            RentalMutationType.PickupInspection,
            cancellationToken);
        if (accessStatus == RentalMutationAccessStatus.NotFound)
        {
            return NotFound();
        }

        if (accessStatus == RentalMutationAccessStatus.Forbidden)
        {
            return Forbid();
        }

        var result = await rentalService.CompletePickupInspectionAsync(
            new RentalOperations.PickupInspectionRequest(id, request.FuelPercent, request.Notes),
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        var rental = await GetRentalByIdAsync(id, employeeId.Value, cancellationToken);
        if (rental is null)
        {
            return NotFound();
        }

        return Ok(rental);
    }

    [Authorize(Policy = ApiAuthorization.ManageRentals)]
    [HttpPost("refresh-statuses")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RefreshStatuses(CancellationToken cancellationToken)
    {
        await rentalService.RefreshStatusesAsync(cancellationToken);
        return Ok(new { message = "Rental statuses refreshed." });
    }

    private async Task<int?> GetCurrentUserClientIdAsync(int employeeId, CancellationToken cancellationToken)
    {
        var client = await GetCurrentUserClientAsync(employeeId, cancellationToken);
        return client?.Id;
    }

    private async Task<Client?> GetCurrentUserClientAsync(int employeeId, CancellationToken cancellationToken)
    {
        var employee = await dbContext.Employees
            .FirstOrDefaultAsync(item => item.Id == employeeId, cancellationToken);
        if (employee is null)
        {
            return null;
        }

        Client? client = null;
        if (employee.ClientId.HasValue)
        {
            client = await dbContext.Clients
                .FirstOrDefaultAsync(item => item.Id == employee.ClientId.Value, cancellationToken);
        }

        if (client is null)
        {
            var passportData = $"EMP-{employeeId:D6}";
            var driverLicense = $"USR-{employeeId:D6}";

            client = await dbContext.Clients
                .FirstOrDefaultAsync(
                    item => item.PassportData == passportData || item.DriverLicense == driverLicense,
                    cancellationToken);

            if (client is not null && employee.ClientId != client.Id)
            {
                employee.ClientId = client.Id;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return client;
    }

    private async Task<RentalMutationAccessStatus> EnsureRentalMutationAccessAsync(
        int rentalId,
        int currentEmployeeId,
        RentalMutationType mutationType,
        CancellationToken cancellationToken)
    {
        var rental = await dbContext.Rentals
            .AsNoTracking()
            .Where(item => item.Id == rentalId)
            .Select(item => new
            {
                item.ClientId,
                item.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (rental is null)
        {
            return RentalMutationAccessStatus.NotFound;
        }

        if (!IsCurrentUserInRole(UserRole.User))
        {
            return RentalMutationAccessStatus.Allowed;
        }

        var ownClientId = await GetCurrentUserClientIdAsync(currentEmployeeId, cancellationToken);
        if (!ownClientId.HasValue || ownClientId.Value != rental.ClientId)
        {
            return RentalMutationAccessStatus.Forbidden;
        }

        if (mutationType is RentalMutationType.Close or RentalMutationType.PickupInspection)
        {
            return RentalMutationAccessStatus.Forbidden;
        }

        if ((mutationType is RentalMutationType.Cancel or RentalMutationType.Reschedule) &&
            rental.Status != RentalStatus.Booked)
        {
            return RentalMutationAccessStatus.InvalidState;
        }

        return RentalMutationAccessStatus.Allowed;
    }

    private async Task<RentalDto?> GetRentalByIdAsync(int id, int currentEmployeeId, CancellationToken cancellationToken)
    {
        var query = dbContext.Rentals
            .AsNoTracking()
            .Where(item => item.Id == id);

        if (IsCurrentUserInRole(UserRole.User))
        {
            var ownClientId = await GetCurrentUserClientIdAsync(currentEmployeeId, cancellationToken);
            query = ownClientId.HasValue
                ? query.Where(item => item.ClientId == ownClientId.Value)
                : query.Where(_ => false);
        }

        return await ProjectRentals(query)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static IQueryable<RentalDto> ProjectRentals(IQueryable<Rental> query)
    {
        return query
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
                item.ReturnInspectionNotes));
    }

    private static bool IsProfileComplete(Client client)
    {
        return !string.IsNullOrWhiteSpace(client.FullName) &&
               !string.IsNullOrWhiteSpace(client.Phone) &&
               !string.IsNullOrWhiteSpace(client.PassportData) &&
               !string.IsNullOrWhiteSpace(client.DriverLicense) &&
               !client.PassportData.Trim().StartsWith("EMP-", StringComparison.OrdinalIgnoreCase) &&
               !client.DriverLicense.Trim().StartsWith("USR-", StringComparison.OrdinalIgnoreCase) &&
               TryNormalizePhone(client.Phone) is not null;
    }

    private static string? TryNormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length is < 10 or > 15)
        {
            return null;
        }

        return "+" + digits;
    }

    private enum RentalMutationAccessStatus
    {
        Allowed = 0,
        Forbidden = 1,
        NotFound = 2,
        InvalidState = 3
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
