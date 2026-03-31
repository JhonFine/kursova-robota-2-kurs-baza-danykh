namespace CarRental.WebApi.Models;

public sealed class ClientDocumentTypeLookup
{
    public string Code { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public ICollection<ClientDocument> Documents { get; set; } = new List<ClientDocument>();
}
