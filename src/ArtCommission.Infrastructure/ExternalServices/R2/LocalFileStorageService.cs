using ArtCommission.Application.Commission.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ArtCommission.Infrastructure.ExternalServices.R2;

/// <summary>
/// Local-only object storage used when Cloudflare R2 credentials are absent.
/// It keeps the development workroom delivery flow usable without external storage.
/// </summary>
public sealed class LocalFileStorageService : IStorageService
{
    private readonly string _rootPath;
    private readonly string _baseUrl;

    public LocalFileStorageService(IConfiguration configuration)
    {
        _rootPath = configuration["LocalStorage:RootPath"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "demo-uploads");
        _baseUrl = (configuration["LocalStorage:BaseUrl"] ?? "http://localhost:5075").TrimEnd('/');
    }

    public Task<string> UploadPublicAsync(Stream stream, string key, string contentType, CancellationToken ct = default) =>
        UploadAsync(stream, "public", key, ct).ContinueWith(
            _ => $"{_baseUrl}/demo-uploads/public/{ToUrlPath(key)}", ct,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.Default);

    public async Task<string> UploadPrivateAsync(Stream stream, string key, string contentType, CancellationToken ct = default)
    {
        await UploadAsync(stream, "private", key, ct);
        return key;
    }

    public string GeneratePresignedDownloadUrl(string privateKey, TimeSpan expiry) =>
        $"{_baseUrl}/demo-uploads/private/{ToUrlPath(privateKey)}";

    private async Task UploadAsync(Stream stream, string visibility, string key, CancellationToken ct)
    {
        var path = Path.Combine(_rootPath, visibility, key.Replace('/', Path.DirectorySeparatorChar));
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetFullPath(Path.Combine(_rootPath, visibility)) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Invalid storage key.", nameof(key));

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var output = File.Create(fullPath);
        await stream.CopyToAsync(output, ct);
    }

    private static string ToUrlPath(string key) => string.Join('/', key.Split('/', StringSplitOptions.RemoveEmptyEntries)
        .Select(Uri.EscapeDataString));
}
