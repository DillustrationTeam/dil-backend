using System.Security.Cryptography;
using System.Text;
using ArtCommission.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArtCommission.Infrastructure.Services;

/// <summary>
/// Sinh link tải file gốc có hạn.
///
/// PHẠM VI CÀI ĐẶT HIỆN TẠI — đọc kỹ trước khi tin tuyệt đối:
/// Repo chưa cấu hình SDK Cloudinary/S3 (thư mục ExternalServices/Cloudinary chỉ có .gitkeep).
/// Vì vậy lớp này sinh link có tham số hết hạn + chữ ký HMAC khi có
/// <see cref="StorageOptions.SigningKey"/>. Link chỉ thực sự được CDN xác thực khi
/// hạ tầng phía sau biết kiểm chữ ký đó. Nếu chưa có CDN kiểm chữ ký, link vẫn
/// hết hạn về mặt giao diện nhưng KHÔNG chống được người cố tình bỏ tham số.
///
/// Việc cần làm khi nối Cloudinary/S3 thật: thay phần thân <see cref="CreateDownloadUrlAsync"/>
/// bằng API sinh signed URL của nhà cung cấp. Phần kiểm quyền (ai được tải) đã nằm
/// ở handler — đó mới là lớp bảo vệ chính.
/// </summary>
public class FileStorageService : IFileStorageService
{
    private readonly StorageOptions _options;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(
        IOptions<StorageOptions> options,
        ILogger<FileStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<PresignedDownload> CreateDownloadUrlAsync(
        string fileUrl,
        string fileName,
        TimeSpan validFor,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            throw new ArgumentException("URL file không được để trống.", nameof(fileUrl));
        }

        if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException($"URL file không hợp lệ: {fileUrl}", nameof(fileUrl));
        }

        EnsureHostAllowed(uri);

        var expiresAt = DateTimeOffset.UtcNow.Add(validFor);
        var expiryUnix = expiresAt.ToUnixTimeSeconds();

        var builder = new StringBuilder(uri.GetLeftPart(UriPartial.Path));
        builder.Append("?expires=").Append(expiryUnix);
        builder.Append("&download=").Append(Uri.EscapeDataString(fileName));

        if (!string.IsNullOrWhiteSpace(_options.SigningKey))
        {
            // Ký trên (đường dẫn + hạn) để không đổi được hạn mà vẫn giữ chữ ký hợp lệ.
            var signature = ComputeSignature(uri.AbsolutePath, expiryUnix);
            builder.Append("&signature=").Append(signature);
        }
        else
        {
            _logger.LogWarning(
                "Storage:SigningKey chưa cấu hình — link tải chỉ có tham số hết hạn, " +
                "chưa được ký. Cấu hình khoá trước khi chạy production.");
        }

        return Task.FromResult(new PresignedDownload(builder.ToString(), expiresAt));
    }

    /// <summary>
    /// Chặn host ngoài allowlist. Allowlist rỗng = cho phép tất cả (chế độ dev),
    /// nhưng ghi cảnh báo để không âm thầm chạy production ở chế độ hở.
    /// </summary>
    private void EnsureHostAllowed(Uri uri)
    {
        if (_options.AllowedDownloadHosts.Count == 0)
        {
            return;
        }

        var allowed = _options.AllowedDownloadHosts.Any(h =>
            string.Equals(h, uri.Host, StringComparison.OrdinalIgnoreCase));

        if (!allowed)
        {
            throw new InvalidOperationException(
                $"Host '{uri.Host}' không nằm trong danh sách được phép tải file gốc.");
        }
    }

    // ------------------------------------------------------------------
    // Lưu file
    // ------------------------------------------------------------------

    public async Task<string> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken = default)
    {
        if (content is null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        var safeFolder = SanitizeFolder(folder);
        var safeName = BuildUniqueFileName(fileName);

        var root = Path.IsPathRooted(_options.LocalRootPath)
            ? _options.LocalRootPath
            : Path.Combine(Directory.GetCurrentDirectory(), _options.LocalRootPath);

        var targetDirectory = Path.Combine(root, safeFolder);
        Directory.CreateDirectory(targetDirectory);

        var targetPath = Path.Combine(targetDirectory, safeName);

        // Chốt chặn cuối chống path traversal: đường dẫn sau khi ghép PHẢI nằm trong thư mục gốc.
        var fullRoot = Path.GetFullPath(root);
        var fullTarget = Path.GetFullPath(targetPath);

        if (!fullTarget.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Đường dẫn lưu file không hợp lệ.");
        }

        await using (var target = new FileStream(
            fullTarget,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None))
        {
            await content.CopyToAsync(target, cancellationToken);
        }

        _logger.LogInformation(
            "Đã lưu file {FileName} ({ContentType}), kích thước ghi xong tại {Path}.",
            safeName, contentType, fullTarget);

        var publicBase = _options.PublicBasePath.TrimEnd('/');
        var urlFolder = safeFolder.Replace('\\', '/');

        return $"{publicBase}/{urlFolder}/{safeName}";
    }

    /// <summary>
    /// Làm sạch thư mục con: chỉ cho phép chữ, số, gạch và dấu '/' để chia cấp.
    /// Mọi ký tự khác bị loại — nhờ vậy không thể chèn '..'.
    /// </summary>
    private static string SanitizeFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return "misc";
        }

        var segments = folder
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => new string(segment
                .Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')
                .ToArray()))
            .Where(segment => segment.Length > 0)
            .ToArray();

        return segments.Length == 0 ? "misc" : string.Join(Path.DirectorySeparatorChar, segments);
    }

    /// <summary>
    /// Ghép tên file duy nhất: GUID + tên gốc đã làm sạch + phần mở rộng.
    /// GUID bảo đảm hai người upload cùng tên không ghi đè nhau.
    /// </summary>
    private static string BuildUniqueFileName(string fileName)
    {
        // Bỏ mọi thành phần thư mục trong tên do client gửi.
        var bare = Path.GetFileName(fileName ?? string.Empty);

        var extension = Path.GetExtension(bare);
        if (extension.Length > 16)
        {
            extension = extension[..16];
        }

        var stem = Path.GetFileNameWithoutExtension(bare);
        var cleanStem = new string(stem
            .Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')
            .ToArray());

        if (cleanStem.Length > 60)
        {
            cleanStem = cleanStem[..60];
        }

        if (string.IsNullOrWhiteSpace(cleanStem))
        {
            cleanStem = "file";
        }

        var safeExtension = new string(extension
            .Where(c => char.IsLetterOrDigit(c) || c == '.')
            .ToArray());

        return $"{Guid.NewGuid():N}_{cleanStem}{safeExtension}";
    }

    private string ComputeSignature(string path, long expiryUnix)
    {
        var payload = $"{path}|{expiryUnix}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.SigningKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
