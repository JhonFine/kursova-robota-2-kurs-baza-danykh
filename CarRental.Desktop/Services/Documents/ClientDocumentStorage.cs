using CarRental.Shared.ReferenceData;
using System.IO;

namespace CarRental.Desktop.Services.Documents;

public sealed class ClientDocumentStorage : IClientDocumentStorage
{
    private const string AppDataRootDirectoryName = "CarRentalSystem";
    private const string ClientDocumentsDirectoryName = "ClientDocuments";
    private const string StoragePrefix = "client-documents";

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    public string SaveDocumentCopy(string sourcePath, int clientId, string documentTypeCode)
    {
        var normalizedSourcePath = string.IsNullOrWhiteSpace(sourcePath)
            ? throw new ArgumentException("Не вказано шлях до файла документа.", nameof(sourcePath))
            : sourcePath.Trim();

        if (!File.Exists(normalizedSourcePath))
        {
            throw new FileNotFoundException("Вибраний файл документа не знайдено.", normalizedSourcePath);
        }

        var extension = Path.GetExtension(normalizedSourcePath);
        if (!IsSupportedExtension(extension))
        {
            throw new NotSupportedException("Дозволені лише JPG, PNG або WEBP файли.");
        }

        var documentFolder = documentTypeCode switch
        {
            ClientDocumentTypes.Passport => "passport",
            ClientDocumentTypes.DriverLicense => "driver-license",
            _ => throw new ArgumentOutOfRangeException(nameof(documentTypeCode), documentTypeCode, "Непідтримуваний тип документа.")
        };

        var storageRoot = ResolveStorageRoot();
        var relativePath = $"{StoragePrefix}/{clientId}/{documentFolder}/{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var destinationPath = Path.Combine(
            storageRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Copy(normalizedSourcePath, destinationPath, overwrite: false);

        return relativePath;
    }

    public bool TryResolvePath(string? storedPath, out string? fullPath)
    {
        fullPath = null;
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return false;
        }

        var trimmedPath = storedPath.Trim();
        if (TryResolveManagedPath(trimmedPath, requireFileExists: true, out fullPath))
        {
            return true;
        }

        if (Uri.TryCreate(trimmedPath, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            trimmedPath = uri.LocalPath;
        }

        if (!Path.IsPathRooted(trimmedPath) || !IsSupportedExtension(Path.GetExtension(trimmedPath)) || !File.Exists(trimmedPath))
        {
            return false;
        }

        fullPath = Path.GetFullPath(trimmedPath);
        return true;
    }

    public bool TryDeleteManagedDocument(string? storedPath)
    {
        if (!TryResolveManagedPath(storedPath, requireFileExists: true, out var fullPath) ||
            string.IsNullOrWhiteSpace(fullPath))
        {
            return false;
        }

        try
        {
            File.Delete(fullPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool IsManagedStoredPath(string? storedPath)
        => TryNormalizeManagedRelativePath(storedPath) is not null;

    private static bool TryResolveManagedPath(string? storedPath, bool requireFileExists, out string? fullPath)
    {
        fullPath = null;
        var relativePath = TryNormalizeManagedRelativePath(storedPath);
        if (relativePath is null)
        {
            return false;
        }

        var storageRoot = ResolveStorageRoot();
        var candidatePath = Path.GetFullPath(Path.Combine(
            storageRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsPathInsideRoot(candidatePath, storageRoot))
        {
            return false;
        }

        if (!IsSupportedExtension(Path.GetExtension(candidatePath)))
        {
            return false;
        }

        if (requireFileExists && !File.Exists(candidatePath))
        {
            return false;
        }

        fullPath = candidatePath;
        return true;
    }

    private static string? TryNormalizeManagedRelativePath(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return null;
        }

        var normalized = storedPath
            .Replace('\\', '/')
            .Trim();

        if (normalized.StartsWith($"{StoragePrefix}/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Trim('/');
        }
        else
        {
            return null;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 4 || !string.Equals(segments[0], StoragePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (segments.Any(segment => segment is "." or ".."))
        {
            return null;
        }

        return string.Join('/', segments);
    }

    private static bool IsSupportedExtension(string? extension)
        => !string.IsNullOrWhiteSpace(extension) && SupportedExtensions.Contains(extension.Trim());

    private static string ResolveStorageRoot()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = AppContext.BaseDirectory;
        }

        return Path.Combine(localAppData, AppDataRootDirectoryName, ClientDocumentsDirectoryName);
    }

    private static bool IsPathInsideRoot(string fullPath, string rootPath)
    {
        var normalizedRoot = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (fullPath.Length == normalizedRoot.Length)
        {
            return true;
        }

        var nextCharacter = fullPath[normalizedRoot.Length];
        return nextCharacter == Path.DirectorySeparatorChar || nextCharacter == Path.AltDirectorySeparatorChar;
    }
}
