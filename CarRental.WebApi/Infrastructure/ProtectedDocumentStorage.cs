using Microsoft.AspNetCore.Http;
using System.IO;

namespace CarRental.WebApi.Infrastructure;

internal enum ClientDocumentPhotoType
{
    Passport = 1,
    DriverLicense = 2
}

internal static class ProtectedDocumentStorage
{
    public const string StoragePrefix = "/protected/documents";
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;

    public static bool TryParseDocumentType(string? value, out ClientDocumentPhotoType documentType)
    {
        documentType = default;
        switch ((value ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "passport":
            case "passport-photo":
                documentType = ClientDocumentPhotoType.Passport;
                return true;

            case "driver-license":
            case "driver-license-photo":
            case "license":
                documentType = ClientDocumentPhotoType.DriverLicense;
                return true;

            default:
                return false;
        }
    }

    public static bool TryNormalizeStoredPhotoPath(string? sourcePath, out string? normalizedPath)
    {
        normalizedPath = null;
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return true;
        }

        if (!TryResolveStoredPhotoPath(sourcePath, requireFileExists: false, out _, out normalizedPath))
        {
            return false;
        }

        return true;
    }

    public static bool TryResolveStoredPhotoPath(
        string? storedPath,
        bool requireFileExists,
        out string? fullPath,
        out string? normalizedPath)
    {
        // Розв'язуємо лише наш внутрішній /protected/documents path і додатково
        // перевіряємо, що результат не виводить нас за межі storage root.
        fullPath = null;
        normalizedPath = null;
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return false;
        }

        if (Uri.TryCreate(storedPath.Trim(), UriKind.Absolute, out _))
        {
            return false;
        }

        var relativePath = NormalizeRelativePath(storedPath.Trim());
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

        if (!IsAllowedImageExtension(Path.GetExtension(candidatePath)))
        {
            return false;
        }

        if (requireFileExists && !File.Exists(candidatePath))
        {
            return false;
        }

        normalizedPath = $"{StoragePrefix}/{relativePath}";
        fullPath = candidatePath;
        return true;
    }

    public static async Task<StoreDocumentPhotoResult> StoreDocumentPhotoAsync(
        IFormFile? file,
        int clientId,
        ClientDocumentPhotoType documentType,
        CancellationToken cancellationToken)
    {
        // Завантаження документа проходить через жорстку перевірку розміру і типу,
        // бо ці файли потім роздаються як захищені персональні дані клієнта.
        if (file is null || file.Length <= 0)
        {
            return StoreDocumentPhotoResult.FromFailure("Виберіть файл документа.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return StoreDocumentPhotoResult.FromFailure("Розмір файла перевищує 5 МБ.");
        }

        if (!TryResolveExtension(file.FileName, file.ContentType, out var extension))
        {
            return StoreDocumentPhotoResult.FromFailure("Дозволені лише JPG, PNG або WEBP файли.");
        }

        var storageRoot = ResolveStorageRoot();
        var folderName = documentType == ClientDocumentPhotoType.Passport
            ? "passport"
            : "driver-license";
        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{extension}";
        var relativePath = $"clients/{clientId}/{folderName}/{fileName}";
        var fullPath = Path.Combine(
            storageRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var targetStream = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        await file.CopyToAsync(targetStream, cancellationToken);

        return StoreDocumentPhotoResult.FromSuccess($"{StoragePrefix}/{relativePath}");
    }

    public static bool TryDeleteStoredPhoto(string? storedPath)
    {
        if (!TryResolveStoredPhotoPath(storedPath, requireFileExists: true, out var fullPath, out _)
            || string.IsNullOrWhiteSpace(fullPath))
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

    public static string ResolveContentType(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }

    private static string ResolveStorageRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("CAR_RENTAL_DOCUMENTS_ROOT");
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            return Path.GetFullPath(configuredRoot.Trim());
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "App_Data", "documents"));
    }

    private static string? NormalizeRelativePath(string sourcePath)
    {
        var normalized = sourcePath
            .Replace('\\', '/')
            .Trim();

        if (normalized.StartsWith($"{StoragePrefix}/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[StoragePrefix.Length..];
        }
        else if (string.Equals(normalized, StoragePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        else if (normalized.StartsWith("protected/documents/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["protected/documents".Length..];
        }
        else
        {
            return null;
        }

        normalized = normalized.Trim('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 4 || !string.Equals(segments[0], "clients", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (segments.Any(segment => segment == "." || segment == ".."))
        {
            return null;
        }

        return string.Join('/', segments);
    }

    private static bool TryResolveExtension(string? fileName, string? contentType, out string extension)
    {
        extension = string.Empty;

        var normalizedContentType = (contentType ?? string.Empty)
            .Split(';', 2)[0]
            .Trim()
            .ToLowerInvariant();

        var extensionFromContentType = normalizedContentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => null
        };

        var extensionFromFileName = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(extensionFromFileName) && !IsAllowedImageExtension(extensionFromFileName))
        {
            return false;
        }

        if (extensionFromContentType is null && string.IsNullOrWhiteSpace(extensionFromFileName))
        {
            return false;
        }

        if (extensionFromContentType is not null &&
            !string.IsNullOrWhiteSpace(extensionFromFileName) &&
            !AreEquivalentExtensions(extensionFromContentType, extensionFromFileName))
        {
            return false;
        }

        extension = extensionFromContentType ?? extensionFromFileName;
        return IsAllowedImageExtension(extension);
    }

    private static bool AreEquivalentExtensions(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return (string.Equals(left, ".jpg", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(right, ".jpeg", StringComparison.OrdinalIgnoreCase)) ||
               (string.Equals(left, ".jpeg", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(right, ".jpg", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAllowedImageExtension(string extension)
    {
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
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

    internal sealed record StoreDocumentPhotoResult(bool Success, string? StoredPath, string? ErrorMessage)
    {
        public static StoreDocumentPhotoResult FromFailure(string message)
            => new(false, null, message);

        public static StoreDocumentPhotoResult FromSuccess(string storedPath)
            => new(true, storedPath, null);
    }
}
