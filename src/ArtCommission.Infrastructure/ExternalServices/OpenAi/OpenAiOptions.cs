namespace ArtCommission.Infrastructure.ExternalServices.OpenAi;

/// <summary>
/// Cấu hình OpenAI Images API. Bind từ section "OpenAi" trong configuration.
///
/// Key KHÔNG để trong appsettings.json — đọc từ biến môi trường <c>OpenAi__ApiKey</c>
/// (Render) hoặc <c>dotnet user-secrets</c> (local). Không có key ⇒ <see cref="FakeAiImageClient"/>
/// được đăng ký thay thế, không gọi OpenAI thật.
/// </summary>
public class OpenAiOptions
{
    public const string SectionName = "OpenAi";

    /// <summary>Khoá API OpenAI (dạng sk-...).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Model sinh ảnh. Mặc định theo quyết định thiết kế: gpt-image-1-mini.</summary>
    public string ImageModel { get; set; } = "gpt-image-1-mini";

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public int TimeoutSeconds { get; set; } = 90;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
