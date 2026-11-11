using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace ArtCommission.API.Helpers;

internal static class ArtworkSourceStreamHelper
{
    public static async Task<Stream?> LoadSourceImageStreamAsync(
        string sourceUrl,
        IHttpClientFactory httpClientFactory,
        IWebHostEnvironment env,
        IConfiguration config,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl)) return null;

        if (sourceUrl.StartsWith("/", StringComparison.Ordinal))
        {
            var trimmed = sourceUrl.TrimStart('/');
            if (trimmed.StartsWith("demo-uploads/public/", StringComparison.OrdinalIgnoreCase))
            {
                var relKey = trimmed["demo-uploads/public/".Length..];
                var localFile = ResolveDemoUploadPublicFile(relKey, config);
                if (localFile is not null) return File.OpenRead(localFile);
            }

            var wwwrootPath = Path.Combine(env.ContentRootPath, "wwwroot", trimmed.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(wwwrootPath)) return File.OpenRead(wwwrootPath);

            var frontendPublic = Path.GetFullPath(Path.Combine(
                env.ContentRootPath, "..", "..", "..", "dil-frontend", "public", trimmed.Replace('/', Path.DirectorySeparatorChar)));
            if (File.Exists(frontendPublic)) return File.OpenRead(frontendPublic);
        }

        if (Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri))
        {
            if (uri.AbsolutePath.StartsWith("/demo-uploads/public/", StringComparison.OrdinalIgnoreCase))
            {
                var relKey = Uri.UnescapeDataString(uri.AbsolutePath["/demo-uploads/public/".Length..]);
                var localFile = ResolveDemoUploadPublicFile(relKey, config);
                if (localFile is not null) return File.OpenRead(localFile);
            }

            try
            {
                var client = httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(15);
                using var resp = await client.GetAsync(uri, ct);
                if (!resp.IsSuccessStatusCode) return null;
                var ms = new MemoryStream();
                await resp.Content.CopyToAsync(ms, ct);
                ms.Position = 0;
                return ms;
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static string? ResolveDemoUploadPublicFile(string relativeKey, IConfiguration config)
    {
        var root = config["LocalStorage:RootPath"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "demo-uploads");
        var publicRoot = Path.GetFullPath(Path.Combine(root, "public")) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(publicRoot, relativeKey.Replace('/', Path.DirectorySeparatorChar)));
        if (candidate.StartsWith(publicRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate))
        {
            return candidate;
        }
        return null;
    }
}
