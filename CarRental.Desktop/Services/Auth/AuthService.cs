using CarRental.Desktop.Data;
using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Security;
using CarRental.Shared.Security;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Desktop.Services.Auth;

// Desktop auth працює навколо shared account-моделі:
// тут перевіряються lockout-правила, а за потреби staff-principal ліниво добудовується з client/account запису.
public sealed class AuthService(RentalDbContext dbContext) : IAuthService
{
    private const int MaxFailedAttempts = SecurityDefaults.MaxFailedAttempts;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(SecurityDefaults.LockoutMinutes);

    public async Task<AuthResult> AuthenticateAsync(string login, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        {
            return new AuthResult(false, "Потрібні логін і пароль.");
        }

        // Логін нормалізується до одного canonical виду, щоб web/desktop не створювали дублікати по регістру.
        var normalizedLogin = login.Trim().ToLowerInvariant();
        var account = await dbContext.Accounts
            .Include(item => item.Employee)
            .Include(item => item.Client)
                .ThenInclude(item => item!.Documents)
            .FirstOrDefaultAsync(item => item.Login == normalizedLogin, cancellationToken);
        if (account is null || !account.IsActive)
        {
            return new AuthResult(false, "Невірні облікові дані.");
        }

        if (account.LockoutUntilUtc.HasValue && account.LockoutUntilUtc.Value > DateTime.UtcNow)
        {
            return new AuthResult(
                false,
                $"Обліковий запис заблоковано до {account.LockoutUntilUtc.Value:HH:mm:ss} UTC.",
                IsLockedOut: true,
                LockedUntilUtc: account.LockoutUntilUtc);
        }

        var isValid = PasswordHasher.VerifyPassword(password, account.PasswordHash);
        if (!isValid)
        {
            // Лічильник невдалих входів оновлюємо до будь-якого раннього виходу, інакше lockout можна обійти повторними спробами.
            account.FailedLoginAttempts += 1;
            if (account.FailedLoginAttempts >= MaxFailedAttempts)
            {
                account.LockoutUntilUtc = DateTime.UtcNow.Add(LockoutDuration);
                account.FailedLoginAttempts = 0;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return new AuthResult(false, "Невірні облікові дані.");
        }

        if (PasswordHasher.NeedsRehash(account.PasswordHash))
        {
            account.PasswordHash = PasswordHasher.HashPassword(password);
        }

        account.FailedLoginAttempts = 0;
        account.LockoutUntilUtc = null;
        account.LastLoginUtc = DateTime.UtcNow;

        var employee = await EnsureDesktopEmployeeAsync(account, cancellationToken);
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
        if (await dbContext.Accounts.AnyAsync(item => item.Login == normalizedLogin, cancellationToken))
        {
            return new RegistrationResult(false, "Користувач з таким логіном вже існує.");
        }

        if (await dbContext.Clients.AnyAsync(item => item.Phone == normalizedPhone, cancellationToken))
        {
            return new RegistrationResult(false, "Клієнт з таким телефоном вже існує.");
        }

        var account = new Account
        {
            Login = normalizedLogin,
            PasswordHash = PasswordHasher.HashPassword(password),
            IsActive = true,
            PasswordChangedAtUtc = DateTime.UtcNow
        };

        var client = new Client
        {
            FullName = fullName.Trim(),
            Phone = normalizedPhone,
            Account = account
        };

        var employee = new Employee
        {
            FullName = client.FullName,
            Role = UserRole.User,
            Account = account
        };

        dbContext.Accounts.Add(account);
        dbContext.Clients.Add(client);
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync(cancellationToken);

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

        var employee = await dbContext.Employees
            .Include(item => item.Account)
            .FirstOrDefaultAsync(item => item.Id == employeeId, cancellationToken);
        if (employee?.Account is null)
        {
            return new PasswordChangeResult(false, "Працівника не знайдено.");
        }

        if (!PasswordHasher.VerifyPassword(currentPassword, employee.Account.PasswordHash))
        {
            return new PasswordChangeResult(false, "Поточний пароль невірний.");
        }

        employee.Account.PasswordHash = PasswordHasher.HashPassword(newPassword);
        employee.Account.PasswordChangedAtUtc = DateTime.UtcNow;
        employee.Account.FailedLoginAttempts = 0;
        employee.Account.LockoutUntilUtc = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new PasswordChangeResult(true, "Пароль оновлено.");
    }

    private async Task<Employee> EnsureDesktopEmployeeAsync(Account account, CancellationToken cancellationToken)
    {
        // Desktop shell працює через Employee principal навіть для self-service користувача,
        // тому акаунт клієнта при вході отримує або знаходить відповідний Employee-запис.
        if (account.Employee is not null)
        {
            if (account.Client is not null &&
                !string.IsNullOrWhiteSpace(account.Client.FullName) &&
                !string.Equals(account.Employee.FullName, account.Client.FullName, StringComparison.Ordinal))
            {
                account.Employee.FullName = account.Client.FullName;
            }

            return account.Employee;
        }

        var employee = await dbContext.Employees
            .Include(item => item.Account)
            .FirstOrDefaultAsync(item => item.AccountId == account.Id, cancellationToken);
        if (employee is not null)
        {
            account.Employee = employee;
            return employee;
        }

        employee = new Employee
        {
            Account = account,
            AccountId = account.Id,
            FullName = !string.IsNullOrWhiteSpace(account.Client?.FullName)
                ? account.Client.FullName
                : account.Login,
            Role = UserRole.User
        };

        dbContext.Employees.Add(employee);
        account.Employee = employee;
        return employee;
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
