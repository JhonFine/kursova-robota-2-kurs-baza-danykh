using CarRental.WebApi.Contracts;
using CarRental.WebApi.Data;
using CarRental.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarRental.WebApi.Controllers;

[Authorize]
[Route("api/profile")]
public sealed class ProfileController(RentalDbContext dbContext) : ApiControllerBase
{
    [HttpGet("client")]
    [ProducesResponseType<ClientProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetClientProfile(CancellationToken cancellationToken)
    {
        var employeeId = GetCurrentEmployeeId();
        if (!employeeId.HasValue)
        {
            return Unauthorized();
        }

        var employee = await dbContext.Employees
            .FirstOrDefaultAsync(item => item.Id == employeeId.Value, cancellationToken);
        if (employee is null)
        {
            return Unauthorized();
        }

        var client = await EnsureLinkedClientAsync(employee, cancellationToken);
        return Ok(ToClientProfileDto(client));
    }

    [HttpPut("client")]
    [ProducesResponseType<ClientProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateClientProfile(
        [FromBody] UpdateClientProfileRequest request,
        CancellationToken cancellationToken)
    {
        var employeeId = GetCurrentEmployeeId();
        if (!employeeId.HasValue)
        {
            return Unauthorized();
        }

        var employee = await dbContext.Employees
            .FirstOrDefaultAsync(item => item.Id == employeeId.Value, cancellationToken);
        if (employee is null)
        {
            return Unauthorized();
        }

        var normalizedPhone = TryNormalizePhone(request.Phone);
        if (normalizedPhone is null)
        {
            return BadRequest(new { message = "Вкажіть коректний телефон (10-15 цифр)." });
        }

        var fullName = request.FullName.Trim();
        var passportData = request.PassportData.Trim();
        var driverLicense = request.DriverLicense.Trim();
        if (string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(passportData) ||
            string.IsNullOrWhiteSpace(driverLicense))
        {
            return BadRequest(new { message = "Усі поля профілю мають бути заповнені." });
        }

        var client = await EnsureLinkedClientAsync(employee, cancellationToken);

        var duplicateDriverLicense = await dbContext.Clients
            .AnyAsync(item => item.Id != client.Id && item.DriverLicense == driverLicense, cancellationToken);
        if (duplicateDriverLicense)
        {
            return Conflict(new { message = "Клієнт з таким номером посвідчення вже існує." });
        }

        client.FullName = fullName;
        client.Phone = normalizedPhone;
        client.PassportData = passportData;
        client.DriverLicense = driverLicense;
        if (!string.Equals(employee.FullName, fullName, StringComparison.Ordinal))
        {
            employee.FullName = fullName;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToClientProfileDto(client));
    }

    private async Task<Client> EnsureLinkedClientAsync(Employee employee, CancellationToken cancellationToken)
    {
        Client? client = null;
        if (employee.ClientId.HasValue)
        {
            client = await dbContext.Clients
                .FirstOrDefaultAsync(item => item.Id == employee.ClientId.Value, cancellationToken);
        }

        if (client is null)
        {
            var passportData = BuildLegacyPassport(employee.Id);
            var driverLicense = BuildLegacyDriverLicense(employee.Id);

            client = await dbContext.Clients
                .FirstOrDefaultAsync(
                    item => item.PassportData == passportData || item.DriverLicense == driverLicense,
                    cancellationToken);

            if (client is null)
            {
                client = new Client
                {
                    FullName = employee.FullName,
                    PassportData = passportData,
                    DriverLicense = driverLicense,
                    Phone = TryNormalizePhone(employee.Login) ?? "+380000000000",
                    Blacklisted = false
                };
                dbContext.Clients.Add(client);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        var hasChanges = false;
        if (employee.ClientId != client.Id)
        {
            employee.ClientId = client.Id;
            hasChanges = true;
        }

        if (!string.Equals(client.FullName, employee.FullName, StringComparison.Ordinal))
        {
            client.FullName = employee.FullName;
            hasChanges = true;
        }

        if (hasChanges)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return client;
    }

    private static ClientProfileDto ToClientProfileDto(Client client)
    {
        return new ClientProfileDto(
            client.Id,
            client.FullName,
            client.PassportData,
            client.DriverLicense,
            client.Phone,
            client.Blacklisted,
            IsProfileComplete(client));
    }

    private static bool IsProfileComplete(Client client)
    {
        return !string.IsNullOrWhiteSpace(client.FullName) &&
               !string.IsNullOrWhiteSpace(client.Phone) &&
               !string.IsNullOrWhiteSpace(client.PassportData) &&
               !string.IsNullOrWhiteSpace(client.DriverLicense) &&
               !IsLegacyIdentityValue(client.PassportData, "EMP-") &&
               !IsLegacyIdentityValue(client.DriverLicense, "USR-") &&
               TryNormalizePhone(client.Phone) is not null;
    }

    private static bool IsLegacyIdentityValue(string? value, string prefix)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Trim().StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildLegacyPassport(int employeeId)
        => $"EMP-{employeeId:D6}";

    private static string BuildLegacyDriverLicense(int employeeId)
        => $"USR-{employeeId:D6}";

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
}
