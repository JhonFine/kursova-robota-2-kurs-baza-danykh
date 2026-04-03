using CarRental.Shared.ReferenceData;
using CarRental.WebApi.Contracts;
using CarRental.WebApi.Models;

namespace CarRental.WebApi.Infrastructure;

public static class ClientProfileRules
{
    public static string? TryNormalizePhone(string? phone)
        => ClientProfileConventions.TryNormalizePhone(phone);

    public static bool IsProfileComplete(Client client)
    {
        var passport = GetActiveDocument(client, ClientDocumentTypes.Passport);
        var driverLicense = GetActiveDocument(client, ClientDocumentTypes.DriverLicense);

        return ClientProfileConventions.IsProfileComplete(
            client.FullName,
            client.Phone,
            passport?.DocumentNumber,
            driverLicense?.DocumentNumber,
            driverLicense?.ExpirationDate);
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
