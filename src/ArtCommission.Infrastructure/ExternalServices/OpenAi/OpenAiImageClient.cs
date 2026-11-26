using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArtCommission.Application.Ai.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArtCommission.Infrastructure.ExternalServices.OpenAi;

/// <summary>
/// Cài đặt <see cref="IAiImageClient"/> bằng OpenAI Images API (model gpt-image-1-mini)
/// cho tính năng AI phác thảo bố cục reference.
///
/// NGUYÊN TẮC THIẾT KẾ (giống <c>GeminiAiClient</c>):
/// 1. KHÔNG ném exception ra ngoài cho lỗi phía nhà cung cấp — mọi lỗi gói vào
///    <see cref="AiImageResult.Fail"/> để handler quyết định có trừ lượt hay không.
/// 2. Nội dung bị chặn kiểm duyệt (<c>moderation_blocked</c>) phải tách riêng khỏi lỗi
///    mạng/quota qua <see cref="AiImageResult.ContentBlocked"/>, vì luồng sinh ảnh
///    KHÔNG trừ lượt khi bị chặn nội dung.
/// 3. KHÔNG log API key, không log toàn bộ prompt (có thể chứa dữ liệu người dùng).
/// </summary>
public class OpenAiImageClient : IAiImageClient
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiImageClient> _logger;

    public OpenAiImageClient(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        ILogger<OpenAiImageClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AiImageResult> GenerateAsync(
        string prompt,
        string size,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            return AiImageResult.Fail(
                "Chưa cấu hình OpenAI API key (OpenAi:ApiKey / biến môi trường OpenAi__ApiKey).");
        }

        var payload = new OpenAiImageRequest
        {
            Model = _options.ImageModel,
            Prompt = prompt,
            Size = size,
            Quality = "low",
            N = 1
        };

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/images/generations")
            {
                Content = JsonContent.Create(payload)
            };
            // Key đi qua header Authorization, KHÔNG nhét vào query-string.
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return await ParseSuccessAsync(response, cancellationToken);
            }

            return await ParseErrorAsync(response, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Người dùng/timeout huỷ request — không phải lỗi của AI.
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            _logger.LogWarning(ex, "Lỗi mạng khi gọi OpenAI Images API.");
            return AiImageResult.Fail($"Không gọi được OpenAI: {ex.Message}");
        }
    }

    private async Task<AiImageResult> ParseSuccessAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        OpenAiImageResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<OpenAiImageResponse>(json);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Không đọc được phản hồi OpenAI Images API.");
            return AiImageResult.Fail("Phản hồi từ OpenAI không đúng định dạng JSON.");
        }

        var b64 = parsed?.Data?.FirstOrDefault()?.B64Json;

        if (string.IsNullOrWhiteSpace(b64))
        {
            return AiImageResult.Fail("OpenAI trả về nhưng không có ảnh.");
        }

        byte[] png;
        try
        {
            png = Convert.FromBase64String(b64);
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Ảnh OpenAI trả về không phải base64 hợp lệ.");
            return AiImageResult.Fail("Ảnh trả về từ OpenAI không đúng định dạng.");
        }

        return AiImageResult.Ok(png, _options.ImageModel);
    }

    private async Task<AiImageResult> ParseErrorAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var status = (int)response.StatusCode;

        OpenAiErrorResponse? parsed = null;
        try
        {
            parsed = JsonSerializer.Deserialize<OpenAiErrorResponse>(body);
        }
        catch (JsonException)
        {
            // Body không phải JSON lỗi chuẩn — bỏ qua, vẫn trả lỗi HTTP chung bên dưới.
        }

        var code = parsed?.Error?.Code;
        var message = parsed?.Error?.Message;

        // moderation_blocked = OpenAI chặn vì nội dung prompt vi phạm chính sách của họ.
        // Đây KHÔNG phải lỗi hệ thống — luồng gọi không được trừ lượt của khách.
        if (string.Equals(code, "moderation_blocked", StringComparison.OrdinalIgnoreCase)
            || response.StatusCode == HttpStatusCode.BadRequest
               && string.Equals(parsed?.Error?.Type, "image_generation_user_error", StringComparison.OrdinalIgnoreCase)
               && message is not null
               && message.Contains("safety", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("OpenAI chặn nội dung ảnh: {Code}", code);
            return AiImageResult.Fail(message ?? "Nội dung bị chặn bởi hệ thống kiểm duyệt của OpenAI.", contentBlocked: true);
        }

        _logger.LogWarning("Gọi OpenAI Images API thất bại: {Status} {Message}", status, message);

        return AiImageResult.Fail(
            message is not null ? $"OpenAI trả {status}: {message}" : $"OpenAI trả {status} {response.ReasonPhrase}.");
    }

    private sealed class OpenAiImageRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public string Size { get; set; } = string.Empty;

        [JsonPropertyName("quality")]
        public string Quality { get; set; } = "low";

        [JsonPropertyName("n")]
        public int N { get; set; } = 1;
    }

    private sealed class OpenAiImageResponse
    {
        [JsonPropertyName("data")]
        public List<OpenAiImageData>? Data { get; set; }
    }

    private sealed class OpenAiImageData
    {
        [JsonPropertyName("b64_json")]
        public string? B64Json { get; set; }
    }

    private sealed class OpenAiErrorResponse
    {
        [JsonPropertyName("error")]
        public OpenAiErrorDetail? Error { get; set; }
    }

    private sealed class OpenAiErrorDetail
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }
    }
}
