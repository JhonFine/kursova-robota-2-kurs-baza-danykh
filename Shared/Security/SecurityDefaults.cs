namespace CarRental.Shared.Security;

// Спільні security-константи для всіх клієнтів системи.
// Вони мають лишатися синхронними між desktop і web, щоб lockout поводився однаково.
internal static class SecurityDefaults
{
    public const int MaxFailedAttempts = 5;
    public const int LockoutMinutes = 10;
}
