using CafePOS.Core.Contracts;

namespace CafePOS.Api.Services;

/// <summary>
/// Stores uploaded catalog and branding images outside the database. Files are served
/// read-only from /uploads so desktop clients can cache them like any other web image.
/// </summary>
public sealed class ImageStorage
{
    public const int MaxBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "product", "category", "branding"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
    };

    public ImageStorage(string rootPath) => RootPath = Path.GetFullPath(rootPath);

    public string RootPath { get; }

    public async Task<ImageUploadResult> SaveAsync(IFormFile file, string? kind, CancellationToken ct)
    {
        var normalizedKind = (kind ?? string.Empty).Trim().ToLowerInvariant();
        if (!AllowedKinds.Contains(normalizedKind))
            throw new InvalidOperationException("Image type must be product, category or branding.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException("Choose a JPG, PNG, GIF, WEBP or BMP image.");
        if (file.Length <= 0)
            throw new InvalidOperationException("The selected image is empty.");
        if (file.Length > MaxBytes)
            throw new InvalidOperationException("Images must be 5 MB or smaller.");

        byte[] bytes;
        await using (var stream = file.OpenReadStream())
        {
            using var memory = new MemoryStream((int)file.Length);
            await stream.CopyToAsync(memory, ct);
            bytes = memory.ToArray();
        }

        if (!LooksLikeImage(bytes))
            throw new InvalidOperationException("The selected file is not a valid image.");

        var targetDirectory = Path.Combine(RootPath, normalizedKind);
        Directory.CreateDirectory(targetDirectory);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var targetPath = Path.Combine(targetDirectory, fileName);
        await File.WriteAllBytesAsync(targetPath, bytes, ct);

        return new ImageUploadResult($"/uploads/{normalizedKind}/{fileName}");
    }

    /// <summary>Deletes an image only when it is one of our managed /uploads paths.</summary>
    public bool TryDelete(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return false;
        var path = imageUrl.Trim();
        if (!path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)) return false;

        var relative = path["/uploads/".Length..].Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(RootPath, relative));
        var rootWithSeparator = RootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)) return false;

        try
        {
            if (!File.Exists(fullPath)) return false;
            File.Delete(fullPath);
            return true;
        }
        catch (IOException)
        {
            // A stale image must never prevent a catalog change from being saved.
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool LooksLikeImage(byte[] bytes)
    {
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
            return true;

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return true;

        if (bytes.Length >= 6 &&
            bytes[0] == (byte)'G' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' &&
            bytes[3] == (byte)'8' && (bytes[4] == (byte)'7' || bytes[4] == (byte)'9') &&
            bytes[5] == (byte)'a')
            return true;

        if (bytes.Length >= 12 &&
            bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F' &&
            bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
            return true;

        return bytes.Length >= 2 && bytes[0] == (byte)'B' && bytes[1] == (byte)'M';
    }
}
