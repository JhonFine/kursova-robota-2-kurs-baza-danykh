using System.IO;

namespace CarRental.Desktop.Infrastructure;

internal static class VehiclePhotoCatalog
{
    public const string StaticImagesRoot = "/images/vehicles";

    public static string? TryBuildCatalogPhotoPath(string make, string model)
    {
        var staticDirectory = ResolveStaticImagesDirectory();
        if (string.IsNullOrWhiteSpace(staticDirectory))
        {
            return null;
        }

        var fileName = BuildImageFileName(make, model);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var fullPath = Path.Combine(staticDirectory, fileName);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        return $"{StaticImagesRoot}/{fileName}";
    }

    public static bool TryResolveStoredPhotoPath(string? storedPath, out string? fullPath)
    {
        fullPath = null;
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return false;
        }

        var trimmedPath = storedPath.Trim();
        if (TryResolveStaticPhotoPath(trimmedPath, out fullPath))
        {
            return true;
        }

        if (Uri.TryCreate(trimmedPath, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            trimmedPath = uri.LocalPath;
        }

        if (!Path.IsPathRooted(trimmedPath))
        {
            return false;
        }

        if (!IsAllowedImageExtension(Path.GetExtension(trimmedPath)) || !File.Exists(trimmedPath))
        {
            return false;
        }

        fullPath = Path.GetFullPath(trimmedPath);
        return true;
    }

    private static bool TryResolveStaticPhotoPath(string sourcePath, out string? fullPath)
    {
        fullPath = null;

        var staticDirectory = ResolveStaticImagesDirectory();
        if (string.IsNullOrWhiteSpace(staticDirectory))
        {
            return false;
        }

        var relativePath = NormalizeStaticRelativePath(sourcePath);
        if (relativePath is null)
        {
            if (sourcePath.StartsWith("/", StringComparison.Ordinal) || sourcePath.StartsWith("\\", StringComparison.Ordinal))
            {
                return false;
            }

            relativePath = sourcePath;
        }

        var cleanedRelativePath = relativePath
            .Replace('\\', '/')
            .Trim();

        if (cleanedRelativePath.StartsWith("images/vehicles/", StringComparison.OrdinalIgnoreCase))
        {
            cleanedRelativePath = cleanedRelativePath["images/vehicles/".Length..];
        }
        else if (cleanedRelativePath.StartsWith("/images/vehicles/", StringComparison.OrdinalIgnoreCase))
        {
            cleanedRelativePath = cleanedRelativePath["/images/vehicles/".Length..];
        }

        if (string.IsNullOrWhiteSpace(cleanedRelativePath))
        {
            return false;
        }

        var candidatePath = Path.GetFullPath(Path.Combine(staticDirectory, cleanedRelativePath));
        if (!IsPathInsideRoot(candidatePath, staticDirectory))
        {
            return false;
        }

        if (!IsAllowedImageExtension(Path.GetExtension(candidatePath)) || !File.Exists(candidatePath))
        {
            return false;
        }

        fullPath = candidatePath;
        return true;
    }

    private static string? NormalizeStaticRelativePath(string sourcePath)
    {
        if (sourcePath.StartsWith($"{StaticImagesRoot}/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sourcePath, StaticImagesRoot, StringComparison.OrdinalIgnoreCase))
        {
            return sourcePath.TrimStart('/');
        }

        if (sourcePath.StartsWith("images/vehicles/", StringComparison.OrdinalIgnoreCase))
        {
            return sourcePath;
        }

        return null;
    }

    private static string? ResolveStaticImagesDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "CarRental.WebApi", "wwwroot", "images", "vehicles"),
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "vehicles"),
            Path.Combine(AppContext.BaseDirectory, "wwwroot", "images", "vehicles"),
            Path.Combine(AppContext.BaseDirectory, "CarRental.WebApi", "wwwroot", "images", "vehicles")
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        var probe = new DirectoryInfo(AppContext.BaseDirectory);
        while (probe is not null)
        {
            var candidate = Path.Combine(probe.FullName, "CarRental.WebApi", "wwwroot", "images", "vehicles");
            if (Directory.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }

            probe = probe.Parent;
        }

        return null;
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

    private static bool IsAllowedImageExtension(string extension)
        => extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
           extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
           extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
           extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
           extension.Equals(".webp", StringComparison.OrdinalIgnoreCase) ||
           extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);

    private static string BuildImageFileName(string make, string model)
    {
        var makeSlug = ToSlug(make);
        if (makeSlug == "volkswagen")
        {
            makeSlug = "vw";
        }

        var modelSlug = ToSlug(model);
        modelSlug = modelSlug
            .Replace("lc-prado", "prado", StringComparison.Ordinal)
            .Replace("x-trail", "xtrail", StringComparison.Ordinal)
            .Replace("mx-5", "mx5", StringComparison.Ordinal)
            .Replace("d-max", "dmax", StringComparison.Ordinal)
            .Replace("g55-amg", "g55", StringComparison.Ordinal)
            .Replace("ioniq-5", "ioniq5", StringComparison.Ordinal);

        if (modelSlug == "camry")
        {
            modelSlug = "camry-xv70";
        }

        if (modelSlug == "octavia")
        {
            modelSlug = "octavia-a8";
        }

        if (makeSlug == "renault" && modelSlug == "duster")
        {
            makeSlug = "dacia";
        }

        if (string.IsNullOrWhiteSpace(makeSlug) || string.IsNullOrWhiteSpace(modelSlug))
        {
            return string.Empty;
        }

        return $"{makeSlug}-{modelSlug}.jpg";
    }

    private static string ToSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value
            .ToLowerInvariant()
            .Replace("+", "plus", StringComparison.Ordinal);

        var buffer = new List<char>(normalized.Length);
        var previousDash = false;
        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer.Add(ch);
                previousDash = false;
                continue;
            }

            if (!previousDash)
            {
                buffer.Add('-');
                previousDash = true;
            }
        }

        return new string(buffer.ToArray()).Trim('-');
    }
}

