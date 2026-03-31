using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using CarRental.WebApi.Models;
using Npgsql;

namespace CarRental.WebApi.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected int? GetCurrentAccountId()
    {
        var value = User.FindFirstValue("account_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : null;
    }

    protected int? GetCurrentEmployeeId()
    {
        var value = User.FindFirstValue("employee_id");
        return int.TryParse(value, out var id) ? id : null;
    }

    protected int? GetCurrentClientId()
    {
        var value = User.FindFirstValue("client_id");
        return int.TryParse(value, out var id) ? id : null;
    }

    protected UserRole? GetCurrentUserRole()
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        return Enum.TryParse<UserRole>(role, true, out var parsed) ? parsed : null;
    }

    protected bool IsCurrentUserInRole(UserRole role)
        => GetCurrentUserRole() == role;

    protected static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        if (exception.InnerException is PostgresException postgresException)
        {
            return postgresException.SqlState == PostgresErrorCodes.UniqueViolation;
        }

        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);
    }
}
