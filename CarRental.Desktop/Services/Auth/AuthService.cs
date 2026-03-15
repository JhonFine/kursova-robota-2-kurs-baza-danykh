using CarRental.Desktop.Data;
using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Security;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Desktop.Services.Auth;

public sealed class AuthService(RentalDbContext dbContext) : IAuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(10);

    public async Task<AuthResult> AuthenticateAsync(string login, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        {
            return new AuthResult(false, "Потрібні логін і пароль.");
        }

        var normalizedLogin = login.Trim().ToLowerInvariant();
        var employee = await dbContext.Employees
            .FirstOrDefaultAsync(item => item.Login == normalizedLogin, cancellationToken);
        if (employee is null || !employee.IsActive)
        {
            return new AuthResult(false, "Невірні облікові дані.");
        }

        if (employee.LockoutUntilUtc.HasValue && employee.LockoutUntilUtc.Value > DateTime.UtcNow)
        {
            return new AuthResult(
                false,
                $"Обліковий запис заблоковано до {employee.LockoutUntilUtc.Value:HH:mm:ss} UTC.",
                IsLockedOut: true,
                LockedUntilUtc: employee.LockoutUntilUtc);
        }

        var isValid = PasswordHasher.VerifyPassword(password, employee.PasswordHash);
        if (!isValid)
        {
            employee.FailedLoginAttempts += 1;
            if (employee.FailedLoginAttempts >= MaxFailedAttempts)
            {
                employee.LockoutUntilUtc = DateTime.UtcNow.Add(LockoutDuration);
                employee.FailedLoginAttempts = 0;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return new AuthResult(false, "Невірні облікові дані.");
        }

        if (PasswordHasher.NeedsRehash(employee.PasswordHash))
        {
            employee.PasswordHash = PasswordHasher.HashPassword(password);
        }

        employee.FailedLoginAttempts = 0;
        employee.LockoutUntilUtc = null;
        employee.LastLoginUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthResult(true, "Успішний вхід.", employee);
    }

    public async Task<RegistrationResult> RegisterAsync(
        string fullName,
        string login,
        string phone,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return new RegistrationResult(false, "Вкажіть ПІБ.");
        }

        if (string.IsNullOrWhiteSpace(login))
        {
            return new RegistrationResult(false, "Вкажіть логін.");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            return new RegistrationResult(false, "Пароль має містити щонайменше 8 символів.");
        }

        var normalizedPhone = TryNormalizePhone(phone);
        if (normalizedPhone is null)
        {
            return new RegistrationResult(false, "Вкажіть коректний телефон (10-15 цифр).");
        }

        var normalizedLogin = login.Trim().ToLowerInvariant();
        var exists = await dbContext.Employees
            .AnyAsync(item => item.Login == normalizedLogin, cancellationToken);
        if (exists)
        {
            return new RegistrationResult(false, "Користувач з таким логіном вже існує.");
        }

        var employee = new Employee
        {
            FullName = fullName.Trim(),
            Login = normalizedLogin,
            PasswordHash = PasswordHasher.HashPassword(password),
            Role = UserRole.User,
            IsActive = true,
            PasswordChangedAtUtc = DateTime.UtcNow
        };

        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync(cancellationToken);

        var passportData = $"EMP-{employee.Id:D6}";
        var driverLicense = $"USR-{employee.Id:D6}";
        var client = await dbContext.Clients.FirstOrDefaultAsync(
            item => item.Id == employee.ClientId,
            cancellationToken);

        if (client is null)
        {
            client = new Client
            {
                FullName = employee.FullName,
                PassportData = passportData,
                DriverLicense = driverLicense,
                Phone = normalizedPhone
            };
            dbContext.Clients.Add(client);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var hasChanges = false;
            if (!string.Equals(client.FullName, employee.FullName, StringComparison.Ordinal))
            {
                client.FullName = employee.FullName;
                hasChanges = true;
            }

            if (!string.Equals(client.Phone, normalizedPhone, StringComparison.Ordinal))
            {
                client.Phone = normalizedPhone;
                hasChanges = true;
            }

            if (!string.Equals(client.PassportData, passportData, StringComparison.Ordinal))
            {
                client.PassportData = passportData;
                hasChanges = true;
            }

            if (!string.Equals(client.DriverLicense, driverLicense, StringComparison.Ordinal))
            {
                client.DriverLicense = driverLicense;
                hasChanges = true;
            }

            if (hasChanges)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        if (employee.ClientId != client.Id)
        {
            employee.ClientId = client.Id;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new RegistrationResult(true, "Реєстрація успішна.", employee);
    }

    public async Task<PasswordChangeResult> ChangePasswordAsync(
        int employeeId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            return new PasswordChangeResult(false, "Новий пароль має містити щонайменше 8 символів.");
        }

        var employee = await dbContext.Employees.FirstOrDefaultAsync(item => item.Id == employeeId, cancellationToken);
        if (employee is null)
        {
            return new PasswordChangeResult(false, "Працівника не знайдено.");
        }

        if (!PasswordHasher.VerifyPassword(currentPassword, employee.PasswordHash))
        {
            return new PasswordChangeResult(false, "Поточний пароль невірний.");
        }

        employee.PasswordHash = PasswordHasher.HashPassword(newPassword);
        employee.PasswordChangedAtUtc = DateTime.UtcNow;
        employee.FailedLoginAttempts = 0;
        employee.LockoutUntilUtc = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new PasswordChangeResult(true, "Пароль оновлено.");
    }

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
