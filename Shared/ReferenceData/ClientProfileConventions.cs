namespace CarRental.Shared.ReferenceData;

public static class ClientProfileConventions
{
    public static string? TryNormalizePhone(string? phone)
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

    public static bool IsProfileComplete(
        string? fullName,
        string? phone,
        string? passportNumber,
        string? driverLicenseNumber,
        DateTime? driverLicenseExpirationDate)
    {
        return !string.IsNullOrWhiteSpace(fullName) &&
               TryNormalizePhone(phone) is not null &&
               !string.IsNullOrWhiteSpace(passportNumber) &&
               !string.IsNullOrWhiteSpace(driverLicenseNumber) &&
               driverLicenseExpirationDate.HasValue &&
               driverLicenseExpirationDate.Value.Date >= DateTime.UtcNow.Date;
    }
}
