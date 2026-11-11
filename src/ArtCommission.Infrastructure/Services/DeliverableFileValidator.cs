using System.IO.Compression;
using ArtCommission.Application.Commission.Interfaces;
using SixLabors.ImageSharp;

namespace ArtCommission.Infrastructure.Services;

/// <summary>
/// Magic-byte, raster-header, and ZIP central-directory validator for final deliverables.
/// Prevents extension spoofing, Zip Slip, Zip Bombs, and embedded executables without buffering whole files in RAM.
/// </summary>
internal static class DeliverableFileValidator
{
    public const long MaxTotalBytes = 1024L * 1024 * 1024; // 1 GB
    private const int MaxZipEntries = 500;
    private const long MaxZipUncompressedBytes = 4L * 1024 * 1024 * 1024; // 4 GB
    private const long MaxRasterPixels = 250_000_000; // ~15800x15800

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".psd", ".psb", ".pdf", ".ai", ".clip", ".zip"
    };

    private static readonly HashSet<string> AllowedZipInnerExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".psd", ".psb", ".pdf", ".ai", ".clip", ".txt"
    };

    private static readonly HashSet<string> RasterExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp"
    };

    public static async Task<string> ValidateAndSanitizeAsync(DeliverableUploadItem item, CancellationToken ct)
    {
        if (item.FileStream is null)
            throw new ArgumentException("Final deliverable file stream is required.");
        if (!item.FileStream.CanSeek)
            throw new ArgumentException($"Deliverable stream for '{item.FileName}' must be seekable.");

        var safeName = SanitizeFileName(item.FileName);
        var ext = Path.GetExtension(safeName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            throw new ArgumentException($"Unsupported final deliverable extension '{ext}'. Allowed: {string.Join(", ", AllowedExtensions)}.");

        if (item.FileStream.Length <= 0)
            throw new ArgumentException($"File '{safeName}' is empty.");
        if (item.FileStream.Length > MaxTotalBytes)
            throw new ArgumentException($"File '{safeName}' exceeds the 1 GB limit.");

        item.FileStream.Position = 0;
        var header = new byte[12];
        var read = await ReadPrefixAsync(item.FileStream, header, ct);
        if (!MatchesMagicBytes(ext, header.AsSpan(0, read)))
            throw new ArgumentException($"File '{safeName}' content does not match its '{ext}' signature.");

        if (RasterExtensions.Contains(ext))
        {
            await ValidateRasterHeaderAsync(item.FileStream, safeName, ext, ct);
        }
        else if (ext == ".zip")
        {
            ValidateZipArchive(item.FileStream, safeName);
        }

        item.FileStream.Position = 0;
        return safeName;
    }

    public static string SanitizeFileName(string? rawFileName)
    {
        var baseName = Path.GetFileName(rawFileName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(baseName))
            return "deliverable";

        var ext = Path.GetExtension(baseName).ToLowerInvariant();
        var stem = Path.GetFileNameWithoutExtension(baseName);
        var cleaned = new string(stem.Select(ch =>
            char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '_').ToArray()).Trim('_');
        if (string.IsNullOrEmpty(cleaned))
            cleaned = "deliverable";

        if (cleaned.Length > 80)
            cleaned = cleaned[..80];

        return $"{cleaned}{ext}";
    }

    private static async Task<int> ReadPrefixAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), ct);
            if (n == 0) break;
            total += n;
        }
        return total;
    }

    private static bool MatchesMagicBytes(string ext, ReadOnlySpan<byte> h) => ext switch
    {
        ".png" => h.Length >= 8
            && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47
            && h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A,
        ".jpg" or ".jpeg" => h.Length >= 3
            && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF,
        ".webp" => h.Length >= 12
            && h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46
            && h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50,
        ".psd" or ".psb" => h.Length >= 6
            && h[0] == 0x38 && h[1] == 0x42 && h[2] == 0x50 && h[3] == 0x53 // 8BPS
            && h[4] == 0x00 && (h[5] == 0x01 || h[5] == 0x02),
        ".pdf" or ".ai" => h.Length >= 4
            && h[0] == 0x25 && h[1] == 0x50 && h[2] == 0x44 && h[3] == 0x46, // %PDF
        ".clip" => h.Length >= 8
            && h[0] == 0x43 && h[1] == 0x53 && h[2] == 0x46 && h[3] == 0x43
            && h[4] == 0x48 && h[5] == 0x55 && h[6] == 0x4E && h[7] == 0x4B, // CSFCHUNK
        ".zip" => h.Length >= 4 && h[0] == 0x50 && h[1] == 0x4B
            && ((h[2] == 0x03 && h[3] == 0x04) || (h[2] == 0x05 && h[3] == 0x06) || (h[2] == 0x07 && h[3] == 0x08)),
        _ => false
    };

    private static async Task ValidateRasterHeaderAsync(Stream stream, string safeName, string ext, CancellationToken ct)
    {
        try
        {
            stream.Position = 0;
            var info = await Image.IdentifyAsync(stream, ct);
            if (info is null || info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > MaxRasterPixels)
                throw new ArgumentException($"Image '{safeName}' dimensions are invalid or exceed the maximum allowed resolution.");
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            throw new ArgumentException($"File '{safeName}' is damaged or not a valid {ext} image.");
        }
    }

    private static void ValidateZipArchive(Stream stream, string safeName)
    {
        try
        {
            stream.Position = 0;
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count > MaxZipEntries)
                throw new ArgumentException($"ZIP archive '{safeName}' exceeds the maximum of {MaxZipEntries} entries.");

            long totalUncompressed = 0;
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(entry.FullName))
                    throw new ArgumentException($"ZIP archive '{safeName}' contains an unsafe path '{entry.FullName}'.");

                var isDirectory = entry.Length == 0
                    && (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'));
                if (isDirectory)
                    continue;

                totalUncompressed += entry.Length;
                if (totalUncompressed > MaxZipUncompressedBytes)
                    throw new ArgumentException($"ZIP archive '{safeName}' exceeds the maximum uncompressed size limit.");

                var innerExt = Path.GetExtension(entry.Name).ToLowerInvariant();
                if (string.IsNullOrEmpty(innerExt) || !AllowedZipInnerExtensions.Contains(innerExt))
                    throw new ArgumentException($"ZIP archive '{safeName}' contains disallowed file '{entry.Name}'.");
            }
        }
        catch (InvalidDataException)
        {
            throw new ArgumentException($"File '{safeName}' is corrupted or not a valid ZIP archive.");
        }
    }
}
