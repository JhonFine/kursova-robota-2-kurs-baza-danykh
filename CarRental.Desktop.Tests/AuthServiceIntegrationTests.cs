using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Auth;
using CarRental.Desktop.Services.Security;
using FluentAssertions;

namespace CarRental.Desktop.Tests;

// Auth integration test фіксує shared lockout-поведінку, яка має бути однаковою для desktop і web.
public sealed class AuthServiceIntegrationTests
{
    [Fact]
    public async Task AuthenticateAsync_ShouldLockUserAfterFiveFailedAttempts()
    {
        await using var testDatabase = await DesktopPostgresTestDatabase.CreateAsync();
        await using var dbContext = testDatabase.CreateDbContext();

        dbContext.Employees.Add(new Employee
        {
            FullName = "Manager",
            Login = "manager",
            PasswordHash = PasswordHasher.HashPassword("manager123"),
            Role = UserRole.Manager,
            IsActive = true
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
