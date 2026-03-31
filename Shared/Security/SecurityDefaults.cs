namespace CarRental.Shared.Security;

internal static class SecurityDefaults
{
    public const int MaxFailedAttempts = 5;
    public const int LockoutMinutes = 10;
}
