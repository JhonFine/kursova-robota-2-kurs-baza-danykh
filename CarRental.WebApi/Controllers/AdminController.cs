using CarRental.WebApi.Contracts;
using CarRental.WebApi.Data;
using CarRental.WebApi.Infrastructure;
using CarRental.WebApi.Models;
using CarRental.WebApi.Services.Auth;
using CarRental.WebApi.Services.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRental.WebApi.Controllers;

[Authorize(Policy = ApiAuthorization.ManageEmployees)]
[Route("api/admin")]
public sealed class AdminController(
    RentalDbContext dbContext) : ApiControllerBase
{
    [HttpGet("employees")]
    [ProducesResponseType<IReadOnlyList<EmployeeDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EmployeeDto>>> GetEmployees(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var pagination = PaginationExtensions.Normalize(page, pageSize);
        var query = dbContext.Employees
            .AsNoTracking()
            .OrderBy(item => item.FullName);

        if (pagination.HasValue)
        {
            var totalCount = await query.CountAsync(cancellationToken);
            Response.ApplyPaginationHeaders(pagination.Value, totalCount);
        }

        var employees = await query
            .ApplyPagination(pagination)
            .Select(item => ToDto(item))
            .ToListAsync(cancellationToken);

        return Ok(employees);
    }

    [HttpPatch("employees/{id:int}/toggle-active")]
    [ProducesResponseType<EmployeeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleActive(int id, CancellationToken cancellationToken)
    {
        var currentEmployeeId = GetCurrentEmployeeId();
        if (!currentEmployeeId.HasValue)
        {
            return Unauthorized();
        }

        var employee = await dbContext.Employees.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (employee is null)
        {
            return NotFound();
        }

        if (employee.Id == currentEmployeeId.Value && employee.IsActive)
        {
            return BadRequest(new { message = "You cannot disable your own account." });
        }

        employee.IsActive = !employee.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(employee));
    }

    [HttpPatch("employees/{id:int}/toggle-manager-role")]
    [ProducesResponseType<EmployeeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleManagerRole(int id, CancellationToken cancellationToken)
    {
        var employee = await dbContext.Employees.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (employee is null)
        {
            return NotFound();
        }

        if (employee.Role == UserRole.Admin)
        {
            return BadRequest(new { message = "Admin role cannot be changed by this operation." });
        }

        employee.Role = employee.Role == UserRole.Manager ? UserRole.User : UserRole.Manager;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(employee));
    }

    [HttpPatch("employees/{id:int}/unlock")]
    [ProducesResponseType<EmployeeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unlock(int id, CancellationToken cancellationToken)
    {
        var employee = await dbContext.Employees.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (employee is null)
        {
            return NotFound();
        }

        employee.LockoutUntilUtc = null;
        employee.FailedLoginAttempts = 0;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(employee));
    }

    [HttpPost("employees/{id:int}/reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(
        int id,
        [FromBody] ResetEmployeePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var employee = await dbContext.Employees.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (employee is null)
        {
            return NotFound();
        }

        employee.PasswordHash = PasswordHasher.HashPassword(request.NewPassword);
        employee.PasswordChangedAtUtc = DateTime.UtcNow;
        employee.FailedLoginAttempts = 0;
        employee.LockoutUntilUtc = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Password has been reset." });
    }

    private static EmployeeDto ToDto(Employee employee)
        => new(
            employee.Id,
            employee.FullName,
            employee.Login,
            employee.Role,
            employee.IsActive,
            employee.LastLoginUtc,
            employee.LockoutUntilUtc);
}
