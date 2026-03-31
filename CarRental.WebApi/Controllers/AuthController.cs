using CarRental.WebApi.Auth;
using CarRental.WebApi.Contracts;
using CarRental.WebApi.Data;
using CarRental.WebApi.Infrastructure;
using CarRental.WebApi.Models;
using CarRental.WebApi.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        if (!result.Success || result.Account is null)
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

        var token = tokenService.Create(result.Account, result.Employee, result.Client, result.Role);
        return Ok(new AuthTokenResponse(
            token.AccessToken,
            token.ExpiresAtUtc,
            result.Account.ToAccountContextDto(result.Employee, result.Client, result.Role),
            result.Employee?.ToDto()));
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

        if (!result.Success || result.Account is null)
        {
            return BadRequest(new { message = "Registration failed. Check input data or duplicate login." });
        }

        var token = tokenService.Create(result.Account, result.Employee, result.Client, result.Role);
        return CreatedAtAction(
            nameof(Me),
            new AuthTokenResponse(
                token.AccessToken,
                token.ExpiresAtUtc,
                result.Account.ToAccountContextDto(result.Employee, result.Client, result.Role),
                result.Employee?.ToDto()));
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<AccountContextDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(
        [FromServices] RentalDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var accountId = GetCurrentAccountId();
        if (!accountId.HasValue)
        {
            return Unauthorized();
        }

        var account = await dbContext.Accounts
            .Include(item => item.Employee)
                .ThenInclude(item => item!.Account)
            .Include(item => item.Client)
                .ThenInclude(item => item!.Documents)
            .FirstOrDefaultAsync(item => item.Id == accountId.Value, cancellationToken);
        if (account is null)
        {
            return Unauthorized();
        }

        var role = account.Employee?.Role ?? Models.UserRole.User;
        return Ok(account.ToAccountContextDto(account.Employee, account.Client, role));
    }

    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var accountId = GetCurrentAccountId();
        if (!accountId.HasValue)
        {
            return Unauthorized();
        }

        var result = await authService.ChangePasswordAsync(
            accountId.Value,
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

        var employee = await dbContext.Employees
            .Include(item => item.Account)
            .FirstOrDefaultAsync(item => item.Id == employeeId.Value, cancellationToken);
        if (employee is null)
        {
            return Unauthorized();
        }

        if (employee.Role != request.Role)
        {
            if (employee.Role == Models.UserRole.Admin && request.Role != Models.UserRole.Admin)
            {
                var adminCount = await dbContext.Employees.CountAsync(e => e.IsActive && e.Role == Models.UserRole.Admin, cancellationToken);
                if (adminCount <= 1)
                {
                    return BadRequest(new { message = "Неможливо змінити роль. Ви єдиний активний адміністратор у системі." });
                }
            }
            
            employee.Role = request.Role;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(employee.ToDto());
    }
}
