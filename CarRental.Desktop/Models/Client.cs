using System.ComponentModel.DataAnnotations.Schema;
using CarRental.Shared.ReferenceData;

namespace CarRental.Desktop.Models;

public sealed class Client : IAuditableEntity, ISoftDeletableEntity
{
    public int Id { get; set; }

    public int? AccountId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public bool IsBlacklisted { get; set; }

    public string? BlacklistReason { get; set; }

    public DateTime? BlacklistedAtUtc { get; set; }

    public int? BlacklistedByEmployeeId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public string PassportData
    {
        get => GetDocument(ClientDocumentTypes.Passport)?.DocumentNumber ?? string.Empty;
        set => UpsertDocument(ClientDocumentTypes.Passport).DocumentNumber = value;
    }

    [NotMapped]
    public DateTime? PassportExpirationDate
    {
        get => GetDocument(ClientDocumentTypes.Passport)?.ExpirationDate;
        set => UpsertDocument(ClientDocumentTypes.Passport).ExpirationDate = value?.Date;
    }

    [NotMapped]
    public string? PassportPhotoPath
    {
        get => GetDocument(ClientDocumentTypes.Passport)?.StoredPath;
        set => UpsertDocument(ClientDocumentTypes.Passport).StoredPath = value;
    }

    [NotMapped]
    public string DriverLicense
    {
        get => GetDocument(ClientDocumentTypes.DriverLicense)?.DocumentNumber ?? string.Empty;
        set => UpsertDocument(ClientDocumentTypes.DriverLicense).DocumentNumber = value;
    }

    [NotMapped]
    public DateTime? DriverLicenseExpirationDate
    {
        get => GetDocument(ClientDocumentTypes.DriverLicense)?.ExpirationDate;
        set => UpsertDocument(ClientDocumentTypes.DriverLicense).ExpirationDate = value?.Date;
    }

    [NotMapped]
    public string? DriverLicensePhotoPath
    {
        get => GetDocument(ClientDocumentTypes.DriverLicense)?.StoredPath;
        set => UpsertDocument(ClientDocumentTypes.DriverLicense).StoredPath = value;
    }

    public Account? Account { get; set; }

    public Employee? BlacklistedByEmployee { get; set; }

    public ICollection<ClientDocument> Documents { get; set; } = new List<ClientDocument>();

    public ICollection<Rental> Rentals { get; set; } = new List<Rental>();

    private ClientDocument? GetDocument(string code)
        => Documents.FirstOrDefault(item => string.Equals(item.DocumentTypeCode, code, StringComparison.OrdinalIgnoreCase));

    private ClientDocument UpsertDocument(string code)
    {
        var document = GetDocument(code);
        if (document is not null)
        {
            return document;
        }

        document = new ClientDocument
        {
            ClientId = Id,
            DocumentTypeCode = code
        };
        Documents.Add(document);
        return document;
    }
}

