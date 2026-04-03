using CarRental.Desktop.Models;

namespace CarRental.Desktop.Services.Auth;

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
        int accountId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);
}

public sealed record AuthResult(
    bool Success,
    string Message,
    Account? Account = null,
    Employee? Employee = null,
    Client? Client = null,
    UserRole Role = UserRole.User,
    bool IsLockedOut = false,
    DateTime? LockedUntilUtc = null);

public sealed record RegistrationResult(
    bool Success,
    string Message,
    Account? Account = null,
    Employee? Employee = null,
    Client? Client = null,
    UserRole Role = UserRole.User);

public sealed record PasswordChangeResult(bool Success, string Message);


