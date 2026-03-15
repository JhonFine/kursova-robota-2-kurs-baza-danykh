using CarRental.WebApi.Auth;
using CarRental.WebApi.Contracts;
using CarRental.WebApi.Data;
using CarRental.WebApi.Models;
using CarRental.WebApi.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.WebApi.Controllers;

[Route("api/auth")]
public sealed class AuthController(
    IAuthService authService,
    ITokenService tokenService) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<AuthTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status423Locked)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.AuthenticateAsync(request.Login, request.Password, cancellationToken);
        if (!result.Success || result.Employee is null)
        {
            if (result.IsLockedOut)
            {
                return StatusCode(StatusCodes.Status423Locked, new
                {
                    message = "Account is temporarily locked.",
                    result.LockedUntilUtc
                });
            }

            return Unauthorized(new { message = "Invalid login or password." });
        }

        var token = tokenService.Create(result.Employee);
        return Ok(new AuthTokenResponse(token.AccessToken, token.ExpiresAtUtc, ToEmployeeDto(result.Employee)));
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType<AuthTokenResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(
            request.FullName,
            request.Login,
            request.Phone,
            request.Password,
            cancellationToken);

        if (!result.Success || result.Employee is null)
        {
            return BadRequest(new { message = "Registration failed. Check input data or duplicate login." });
        }

        var token = tokenService.Create(result.Employee);
        return CreatedAtAction(
            nameof(Me),
            new AuthTokenResponse(token.AccessToken, token.ExpiresAtUtc, ToEmployeeDto(result.Employee)));
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<EmployeeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(
        [FromServices] CarRental.WebApi.Data.RentalDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var employeeId = GetCurrentEmployeeId();
        if (!employeeId.HasValue)
        {
            return Unauthorized();
        }

        var employee = await dbContext.Employees.FindAsync([employeeId.Value], cancellationToken);
        if (employee is null)
        {
            return Unauthorized();
        }

        return Ok(ToEmployeeDto(employee));
    }

    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var employeeId = GetCurrentEmployeeId();
        if (!employeeId.HasValue)
        {
            return Unauthorized();
        }

        var result = await authService.ChangePasswordAsync(
            employeeId.Value,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { message = "Password change failed." });
        }

        return Ok(new { message = "Password changed successfully." });
    }

    [Authorize(Policy = ApiAuthorization.ManageEmployees)]
    [HttpPatch("me/role")]
    [ProducesResponseType<EmployeeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ChangeOwnRole(
        [FromBody] ChangeOwnRoleRequest request,
        [FromServices] RentalDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var employeeId = GetCurrentEmployeeId();
        if (!employeeId.HasValue)
        {
            return Unauthorized();
        }

        var employee = await dbContext.Employees.FindAsync([employeeId.Value], cancellationToken);
        if (employee is null)
        {
            return Unauthorized();
        }

        if (employee.Role != request.Role)
        {
            employee.Role = request.Role;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(ToEmployeeDto(employee));
    }

    private static EmployeeDto ToEmployeeDto(Employee employee)
        => new(
            employee.Id,
            employee.FullName,
            employee.Login,
            employee.Role,
            employee.IsActive,
            employee.LastLoginUtc,
            employee.LockoutUntilUtc);
}
