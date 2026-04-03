using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Auth;
using CarRental.Desktop.Services.Security;
using FluentAssertions;

namespace CarRental.Desktop.Tests;

public sealed class AuthServiceIntegrationTests
{
    [Fact]
    public async Task AuthenticateAsync_ShouldLockUserAfterFiveFailedAttempts()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();

        var account = new Account
        {
            Login = "manager",
            PasswordHash = PasswordHasher.HashPassword("manager123"),
            IsActive = true
        };

        dbContext.Accounts.Add(account);
        dbContext.Employees.Add(new Employee
        {
            FullName = "Manager",
            RoleId = UserRole.Manager,
            Account = account
        });
        await dbContext.SaveChangesAsync();

        var authService = new AuthService(dbContext);
        for (var i = 0; i < 5; i++)
        {
            var failed = await authService.AuthenticateAsync("manager", "wrong");
            failed.Success.Should().BeFalse();
        }

        var locked = await authService.AuthenticateAsync("manager", "manager123");
        locked.Success.Should().BeFalse();
        locked.IsLockedOut.Should().BeTrue();
    }
}
