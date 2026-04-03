using CarRental.WebApi.Contracts;
using CarRental.WebApi.Data;
using CarRental.WebApi.Infrastructure;
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
            .Include(item => item.Account)
            .ToListAsync(cancellationToken);

        return Ok(employees.Select(item => item.ToDto()).ToList());
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

        var employee = await dbContext.Employees
            .Include(item => item.Account)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (employee is null)
        {
            return NotFound();
        }

        if (employee.Id == currentEmployeeId.Value && employee.IsActive)
        {
            return BadRequest(new { message = "You cannot disable your own account." });
        }

        if (employee.Account is null)
        {
            return BadRequest(new { message = "Employee account is not configured." });
        }

        employee.Account.IsActive = !employee.Account.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(employee.ToDto());
    }

    [HttpPatch("employees/{id:int}/toggle-manager-role")]
    [ProducesResponseType<EmployeeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleManagerRole(int id, CancellationToken cancellationToken)
    {
        var employee = await dbContext.Employees
            .Include(item => item.Account)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (employee is null)
        {
            return NotFound();
        }

        if (employee.RoleId == Models.UserRole.Admin)
        {
            return BadRequest(new { message = "Admin role cannot be changed by this operation." });
        }

        employee.RoleId = employee.RoleId == Models.UserRole.Manager ? Models.UserRole.User : Models.UserRole.Manager;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(employee.ToDto());
    }

    [HttpPatch("employees/{id:int}/unlock")]
    [ProducesResponseType<EmployeeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unlock(int id, CancellationToken cancellationToken)
    {
        var employee = await dbContext.Employees
            .Include(item => item.Account)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (employee is null)
        {
            return NotFound();
        }

        if (employee.Account is null)
        {
            return BadRequest(new { message = "Employee account is not configured." });
        }

        employee.Account.LockoutUntilUtc = null;
        employee.Account.FailedLoginAttempts = 0;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(employee.ToDto());
    }

    [HttpPost("employees/{id:int}/reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(
        int id,
        [FromBody] ResetEmployeePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var employee = await dbContext.Employees
            .Include(item => item.Account)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (employee is null)
        {
            return NotFound();
        }

        if (employee.Account is null)
        {
            return BadRequest(new { message = "Employee account is not configured." });
        }

        employee.Account.PasswordHash = PasswordHasher.HashPassword(request.NewPassword);
        employee.Account.PasswordChangedAtUtc = DateTime.UtcNow;
        employee.Account.FailedLoginAttempts = 0;
        employee.Account.LockoutUntilUtc = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Password has been reset." });
    }
}
