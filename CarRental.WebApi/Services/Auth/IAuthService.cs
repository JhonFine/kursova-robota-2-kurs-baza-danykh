using CarRental.WebApi.Models;

namespace CarRental.WebApi.Services.Auth;

public interface IAuthService
{
    Task<AuthResult> AuthenticateAsync(string login, string password, CancellationToken cancellationToken = default);

    Task<RegistrationResult> RegisterAsync(
        string fullName,
        string login,
        string phone,
        string password,
        CancellationToken cancellationToken = default);

    Task<PasswordChangeResult> ChangePasswordAsync(
        int employeeId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);
}

public sealed record AuthResult(
    bool Success,
    string Message,
    Employee? Employee = null,
    bool IsLockedOut = false,
    DateTime? LockedUntilUtc = null);

public sealed record RegistrationResult(bool Success, string Message, Employee? Employee = null);

public sealed record PasswordChangeResult(bool Success, string Message);

