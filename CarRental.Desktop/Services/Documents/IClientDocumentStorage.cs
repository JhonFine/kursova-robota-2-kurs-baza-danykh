namespace CarRental.Desktop.Services.Documents;

public interface IClientDocumentStorage
{
    string SaveDocumentCopy(string sourcePath, int clientId, string documentTypeCode);

    bool TryResolvePath(string? storedPath, out string? fullPath);

    bool TryDeleteManagedDocument(string? storedPath);

    bool IsManagedStoredPath(string? storedPath);
}
