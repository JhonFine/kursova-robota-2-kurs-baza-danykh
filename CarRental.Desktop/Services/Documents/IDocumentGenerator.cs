namespace CarRental.Desktop.Services.Documents;

public interface IDocumentGenerator
{
    Task<GeneratedContractFiles> GenerateRentalContractAsync(
        ContractData data,
        CancellationToken cancellationToken = default);
}

public sealed record ContractData(
    int RentalId,
    string ContractNumber,
    string ClientName,
    string Vehicle,
    DateTime StartDate,
    DateTime EndDate,
    decimal TotalAmount);

public sealed record GeneratedContractFiles(string TextPath, string DocxPath, string PdfPath);

