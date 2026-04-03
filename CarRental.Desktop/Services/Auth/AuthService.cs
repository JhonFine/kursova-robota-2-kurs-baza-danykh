using CarRental.Desktop.Data;
using CarRental.Desktop.Models;
using CarRental.Desktop.Services.Security;
using CarRental.Shared.ReferenceData;
using CarRental.Shared.Security;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Desktop.Services.Auth;

public sealed class AuthService(RentalDbContext dbContext) : IAuthService
{
    private const int MaxFailedAttempts = SecurityDefaults.MaxFailedAttempts;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(SecurityDefaults.LockoutMinutes);

    // РўСѓС‚ Р·С–Р±СЂР°РЅРѕ РІРµСЃСЊ login-flow: РІР°Р»С–РґР°С†С–СЏ РІРІРѕРґСѓ, lockout, РїРµСЂРµРІС–СЂРєР° РїР°СЂРѕР»СЏ,
    // РјС–РіСЂР°С†С–СЏ legacy-С…РµС€С–РІ С– РѕРЅРѕРІР»РµРЅРЅСЏ audit-РїРѕР»С–РІ СѓСЃРїС–С€РЅРѕРіРѕ РІС…РѕРґСѓ.
    public async Task<AuthResult> AuthenticateAsync(string login, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        {
            return new AuthResult(false, "Потрібні логін і пароль.");
        }

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
            var newLockoutTime = DateTime.UtcNow.Add(LockoutDuration);
            // Р›С–С‡РёР»СЊРЅРёРє РЅРµРІРґР°Р»РёС… СЃРїСЂРѕР± РѕРЅРѕРІР»СЋС”РјРѕ Р±РµР· Р·Р°РІР°РЅС‚Р°Р¶РµРЅРЅСЏ СЃСѓС‚РЅРѕСЃС‚С– РїРѕРІС‚РѕСЂРЅРѕ,
            // С‰РѕР± lockout РїСЂР°С†СЋРІР°РІ Р°С‚РѕРјР°СЂРЅРѕ РЅР°РІС–С‚СЊ РїС–Рґ РєРѕРЅРєСѓСЂРµРЅС‚РЅРёРјРё СЃРїСЂРѕР±Р°РјРё РІС…РѕРґСѓ.
            await dbContext.Accounts
                .Where(a => a.Id == account.Id)
                .ExecuteUpdateAsync(updates =>
                    updates.SetProperty(p => p.FailedLoginAttempts, p =>
                                p.FailedLoginAttempts + 1 >= MaxFailedAttempts ? 0 : p.FailedLoginAttempts + 1)
                           .SetProperty(p => p.LockoutUntilUtc, p =>
                                p.FailedLoginAttempts + 1 >= MaxFailedAttempts ? (DateTime?)newLockoutTime : p.LockoutUntilUtc),
                    cancellationToken: cancellationToken);

            return new AuthResult(false, "Невірні облікові дані.");
        }

        if (PasswordHasher.NeedsRehash(account.PasswordHash))
        {
            var newHash = PasswordHasher.HashPassword(password);
            await dbContext.Accounts
                .Where(a => a.Id == account.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(p => p.PasswordHash, newHash), cancellationToken);
            account.PasswordHash = newHash;
        }

        var lastLoginTime = DateTime.UtcNow;
        await dbContext.Accounts
            .Where(a => a.Id == account.Id)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(p => p.FailedLoginAttempts, 0)
                .SetProperty(p => p.LockoutUntilUtc, (DateTime?)null)
                .SetProperty(p => p.LastLoginUtc, lastLoginTime),
            cancellationToken);

        account.FailedLoginAttempts = 0;
        account.LockoutUntilUtc = null;
        account.LastLoginUtc = lastLoginTime;

        var role = account.Employee?.RoleId ?? UserRole.User;
        return new AuthResult(true, "Успішний вхід.", account, account.Employee, account.Client, role);
    }

    public async Task<RegistrationResult> RegisterAsync(
        string fullName,
        string login,
        string phone,
        string password,
        CancellationToken cancellationToken = default)
    {
        // Р РµС”СЃС‚СЂР°С†С–СЏ СЃС‚РІРѕСЂСЋС” РѕРєСЂРµРјС– Account С– Client, С‰РѕР± Р°РІС‚РѕСЂРёР·Р°С†С–СЏ С– Р±С–Р·РЅРµСЃ-РїСЂРѕС„С–Р»СЊ
        // Р·Р°Р»РёС€Р°Р»РёСЃСЏ СЂРѕР·РґС–Р»РµРЅРёРјРё С‚Р°Рє СЃР°РјРѕ, СЏРє РґР»СЏ staff-Р°РєР°СѓРЅС‚С–РІ.
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

        var normalizedPhone = ClientProfileConventions.TryNormalizePhone(phone);
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

        dbContext.Accounts.Add(account);
        dbContext.Clients.Add(client);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RegistrationResult(true, "Реєстрація успішна.", account, null, client, UserRole.User);
    }

    public async Task<PasswordChangeResult> ChangePasswordAsync(
        int accountId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            return new PasswordChangeResult(false, "Новий пароль має містити щонайменше 8 символів.");
        }

        var account = await dbContext.Accounts.FirstOrDefaultAsync(item => item.Id == accountId, cancellationToken);
        if (account is null)
        {
            return new PasswordChangeResult(false, "Обліковий запис не знайдено.");
        }

        if (!PasswordHasher.VerifyPassword(currentPassword, account.PasswordHash))
        {
            return new PasswordChangeResult(false, "Поточний пароль невірний.");
        }

        account.PasswordHash = PasswordHasher.HashPassword(newPassword);
        account.PasswordChangedAtUtc = DateTime.UtcNow;
        account.FailedLoginAttempts = 0;
        account.LockoutUntilUtc = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new PasswordChangeResult(true, "Пароль оновлено.");
    }

}
