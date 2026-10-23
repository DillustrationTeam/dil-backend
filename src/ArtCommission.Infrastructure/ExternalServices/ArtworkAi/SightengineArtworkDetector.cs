using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Text.Json;
using ArtCommission.Application.ArtistStudio.Ai;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
namespace ArtCommission.Infrastructure.ExternalServices.ArtworkAi;
public class SightengineArtworkDetector(HttpClient http, IOptions<ArtworkAiOptions> options, IConfiguration configuration) : IArtworkAiDetector
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.SightengineApiUser) && !string.IsNullOrWhiteSpace(options.Value.SightengineApiSecret);
    public async Task<decimal> DetectAsync(string imageUrl, CancellationToken ct)
    {
        if (!IsConfigured) throw new InvalidOperationException("Chưa cấu hình Sightengine.");
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)) throw new ArgumentException("Đường dẫn ảnh không hợp lệ.");
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("genai"), "models");
        content.Add(new StringContent(options.Value.SightengineApiUser), "api_user");
        content.Add(new StringContent(options.Value.SightengineApiSecret), "api_secret");
        var localBase = new Uri(configuration["LocalStorage:BaseUrl"] ?? "http://localhost:5075");
        if (uri.GetLeftPart(UriPartial.Authority) == localBase.GetLeftPart(UriPartial.Authority) && uri.AbsolutePath.StartsWith("/demo-uploads/public/artworks/", StringComparison.Ordinal))
        {
            var storageRoot = Path.GetFullPath(Path.Combine(configuration["LocalStorage:RootPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "demo-uploads"), "public", "artworks"));
            var relative = Uri.UnescapeDataString(uri.AbsolutePath["/demo-uploads/public/artworks/".Length..]);
            var filePath = Path.GetFullPath(Path.Combine(storageRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!filePath.StartsWith(storageRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || relative.Contains('\\') || !File.Exists(filePath)) throw new ArgumentException("Không tìm thấy ảnh đã upload.");
            var file = new FileInfo(filePath);
            if (file.Length is <= 0 or > 25 * 1024 * 1024) throw new ArgumentException("Ảnh tối đa 25 MB.");
            var media = new StreamContent(File.OpenRead(filePath));
            media.Headers.ContentType = new MediaTypeHeaderValue(Path.GetExtension(filePath).ToLowerInvariant() switch { ".png" => "image/png", ".webp" => "image/webp", ".gif" => "image/gif", _ => "image/jpeg" });
            if (file.Length > 10 * 1024 * 1024)
            {
                // Only transform the provider copy; preserve the uploaded artwork.
                using (media)
                using (var image = await Image.LoadAsync(new SixLabors.ImageSharp.Formats.DecoderOptions { MaxFrames = 1 }, filePath, ct))
                {
                    image.Mutate(x => x.AutoOrient().Resize(new ResizeOptions { Size = new Size(Math.Min(2048, image.Width), Math.Min(2048, image.Height)), Mode = ResizeMode.Max }));
                    using var resized = new MemoryStream();
                    await image.SaveAsJpegAsync(resized, ct);
                    var prepared = new ByteArrayContent(resized.ToArray());
                    prepared.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                    content.Add(prepared, "media", Path.GetFileNameWithoutExtension(file.Name) + ".jpg");
                }
            }
            else content.Add(media, "media", file.Name);
        }
        else
        {
            var hosts = options.Value.AllowedImageHosts.ToList();
            if (Uri.TryCreate(configuration["CloudflareR2:PublicDomain"], UriKind.Absolute, out var storage)) hosts.Add(storage.IdnHost);
            if (uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort || !hosts.Contains(uri.IdnHost, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException("Ảnh không thuộc kho lưu trữ được phép phân tích.");
            content.Add(new StringContent(imageUrl), "url");
        }
        using var response = await http.PostAsync("https://api.sightengine.com/1.0/check.json", content, ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Dịch vụ phát hiện ảnh AI tạm thời không khả dụng.");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = json.RootElement;
        if (!root.TryGetProperty("status", out var status) || status.GetString() != "success" || !root.TryGetProperty("type", out var type) || !type.TryGetProperty("ai_generated", out var score) || !score.TryGetDecimal(out var number) || number is < 0 or > 1)
            throw new InvalidOperationException("Dịch vụ phát hiện trả kết quả không hợp lệ.");
        return number;
    }
}
