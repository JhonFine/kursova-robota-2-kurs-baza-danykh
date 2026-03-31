using System.Security.Claims;
using CarRental.WebApi.Auth;
using CarRental.WebApi.Contracts;
using CarRental.WebApi.Controllers;
using CarRental.WebApi.Data;
using CarRental.WebApi.Models;
using CarRental.WebApi.Services.Auth;
using CarRental.WebApi.Services.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CarRental.WebApi.Tests;

public sealed class AuthControllerContractTests
{
    [Fact]
    public async Task Login_ShouldReturnClientContext_WhenAccountHasNoEmployee()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();

        var account = new Account
        {
            Login = "client.user",
            PasswordHash = PasswordHasher.HashPassword("client12345"),
            IsActive = true,
            PasswordChangedAtUtc = DateTime.UtcNow
        };
        var client = new Client
        {
            FullName = "Client User",
            Phone = "+380501112233",
            Account = account
        };

        dbContext.Accounts.Add(account);
        dbContext.Clients.Add(client);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(new AuthService(dbContext));

        var result = await controller.Login(
            new LoginRequest
            {
                Login = "client.user",
                Password = "client12345"
            },
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<AuthTokenResponse>().Subject;
        response.Employee.Should().BeNull();
        response.User.Role.Should().Be(UserRole.User);
        response.User.Employee.Should().BeNull();
        response.User.Client.Should().NotBeNull();
        response.User.Client!.Id.Should().Be(client.Id);
    }

    [Fact]
    public async Task Me_ShouldReturnEmployeeContext_ForStaffAccount()
    {
        await using var testDatabase = await WebApiPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();

        var account = new Account
        {
            Login = "manager.user",
            PasswordHash = PasswordHasher.HashPassword("manager12345"),
            IsActive = true,
            PasswordChangedAtUtc = DateTime.UtcNow
        };
        var employee = new Employee
        {
            FullName = "Manager User",
            Role = UserRole.Manager,
            Account = account
        };

        dbContext.Accounts.Add(account);
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(
            new AuthService(dbContext),
            CreatePrincipal(account.Id, employee.Id, UserRole.Manager));

        var result = await controller.Me(dbContext, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<AccountContextDto>().Subject;
        response.Role.Should().Be(UserRole.Manager);
        response.Account.Id.Should().Be(account.Id);
        response.Employee.Should().NotBeNull();
        response.Employee!.Id.Should().Be(employee.Id);
        response.Employee.Login.Should().Be(account.Login);
        response.Client.Should().BeNull();
    }

    private static AuthController CreateController(IAuthService authService, ClaimsPrincipal? user = null)
    {
        var controller = new AuthController(authService, CreateTokenService());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = user ?? new ClaimsPrincipal(new ClaimsIdentity())
            }
        };

        return controller;
    }

    private static ITokenService CreateTokenService()
    {
        return new JwtTokenService(Options.Create(new JwtOptions
        {
            Issuer = "test",
            Audience = "test",
            SigningKey = "test-signing-key-with-sufficient-length-123456",
            AccessTokenMinutes = 60
        }));
    }

    private static ClaimsPrincipal CreatePrincipal(int accountId, int employeeId, UserRole role)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, accountId.ToString()),
            new Claim("account_id", accountId.ToString()),
            new Claim("employee_id", employeeId.ToString()),
            new Claim(ClaimTypes.Role, role.ToString())
        ],
        "TestAuth"));
    }
}
