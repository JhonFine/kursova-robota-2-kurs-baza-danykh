using System.Security.Cryptography;
using System.Text;

namespace CarRental.WebApi.Services.Security;

public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 120_000;
    private const string Prefix = "PBKDF2";

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        if (storedHash.StartsWith($"{Prefix}$", StringComparison.OrdinalIgnoreCase))
        {
            return VerifyPbkdf2(password, storedHash);
        }

        // Backward compatibility with legacy SHA256 hex strings.
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = SHA256.HashData(bytes);
        var legacyHash = Convert.ToHexString(hash);
        return string.Equals(legacyHash, storedHash, StringComparison.OrdinalIgnoreCase);
    }

    public static bool NeedsRehash(string storedHash)
        => !storedHash.StartsWith($"{Prefix}$", StringComparison.OrdinalIgnoreCase);

    private static bool VerifyPbkdf2(string password, string storedHash)
    {
        var parts = storedHash.Split('$');
        if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

