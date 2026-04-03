using CarRental.Shared.ReferenceData;

namespace CarRental.Desktop.Models;

public sealed class ClientDocument : IAuditableEntity, ISoftDeletableEntity
{
    public int Id { get; set; }

    public int ClientId { get; set; }

    public string DocumentTypeCode { get; set; } = ClientDocumentTypes.Passport;

    public string DocumentNumber { get; set; } = string.Empty;

    public DateTime? ExpirationDate { get; set; }

    public string? StoredPath { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public Client? Client { get; set; }

    public ClientDocumentTypeLookup? DocumentType { get; set; }
}

