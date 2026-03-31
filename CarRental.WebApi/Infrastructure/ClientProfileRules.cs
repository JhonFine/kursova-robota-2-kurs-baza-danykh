using CarRental.Shared.ReferenceData;
using CarRental.WebApi.Contracts;
using CarRental.WebApi.Models;

namespace CarRental.WebApi.Infrastructure;

public static class ClientProfileRules
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

    public static bool IsProfileComplete(Client client)
    {
        var passport = GetActiveDocument(client, ClientDocumentTypes.Passport);
        var driverLicense = GetActiveDocument(client, ClientDocumentTypes.DriverLicense);

        return !string.IsNullOrWhiteSpace(client.FullName) &&
               !string.IsNullOrWhiteSpace(client.Phone) &&
               passport is not null &&
               !string.IsNullOrWhiteSpace(passport.DocumentNumber) &&
               driverLicense is not null &&
               !string.IsNullOrWhiteSpace(driverLicense.DocumentNumber) &&
               driverLicense.ExpirationDate.HasValue &&
               driverLicense.ExpirationDate.Value.Date >= DateTime.UtcNow.Date &&
               TryNormalizePhone(client.Phone) is not null;
    }

    public static ClientDocument? GetActiveDocument(Client client, string documentTypeCode)
        => client.Documents
            .Where(item => !item.IsDeleted)
            .FirstOrDefault(item => string.Equals(item.DocumentTypeCode, documentTypeCode, StringComparison.OrdinalIgnoreCase));

    public static void UpsertDocuments(
        Client client,
        IEnumerable<ClientDocumentUpsertRequest> requests)
    {
        var normalized = requests
            .Where(item => !string.IsNullOrWhiteSpace(item.DocumentTypeCode) || !string.IsNullOrWhiteSpace(item.DocumentNumber) || item.ExpirationDate.HasValue || !string.IsNullOrWhiteSpace(item.StoredPath))
            .Select(item => new ClientDocumentUpsertRequest
            {
                DocumentTypeCode = item.DocumentTypeCode.Trim().ToUpperInvariant(),
                DocumentNumber = item.DocumentNumber.Trim(),
                ExpirationDate = item.ExpirationDate?.Date,
                StoredPath = item.StoredPath?.Trim()
            })
            .ToList();

        foreach (var documentTypeCode in new[] { ClientDocumentTypes.Passport, ClientDocumentTypes.DriverLicense })
        {
            var incoming = normalized.FirstOrDefault(item => item.DocumentTypeCode == documentTypeCode);
            var existing = GetActiveDocument(client, documentTypeCode);

            if (incoming is null)
            {
                if (existing is not null)
                {
                    existing.IsDeleted = true;
                    existing.UpdatedAtUtc = DateTime.UtcNow;
                }

                continue;
            }

            if (existing is null)
            {
                existing = new ClientDocument
                {
                    ClientId = client.Id,
                    DocumentTypeCode = documentTypeCode
                };
                client.Documents.Add(existing);
            }

            existing.IsDeleted = false;
            existing.DocumentTypeCode = documentTypeCode;
            existing.DocumentNumber = incoming.DocumentNumber;
            existing.ExpirationDate = incoming.ExpirationDate?.Date;
            existing.StoredPath = incoming.StoredPath;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}
