using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArtCommission.Application.ArtistStudio.Moderation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArtCommission.Infrastructure.ExternalServices.Gemini;

/// <summary>
/// Cài đặt quét tác phẩm nghệ thuật bằng Gemini Multimodal Vision API (SCR-20 / UC28).
/// Phân tích độ an toàn (Safe/Adult/Violence) và nhận diện tranh AI (AiDetectionScore).
/// </summary>
public class GeminiArtworkScanService : IArtworkScanService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiArtworkScanService> _logger;

    private const int MaxAttempts = 3;
    private const int MaxImageDownloadSizeBytes = 25 * 1024 * 1024; // 25 MB

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public GeminiArtworkScanService(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<GeminiArtworkScanService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ArtworkScanResult> ScanArtworkAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return ArtworkScanResult.Fail("URL ảnh tác phẩm không được để trống.");
        }

        // Nếu chưa cấu hình API Key (môi trường dev cục bộ), trả kết quả an toàn mặc định thay vì crash pipeline
        if (!_options.IsConfigured)
        {
            _logger.LogWarning("Gemini API Key chưa được cấu hình. Sử dụng kết quả quét an toàn mặc định cho tranh {ImageUrl}.", imageUrl);
            return ArtworkScanResult.Ok(
                safeScore: 0.99m,
                adultScore: 0.01m,
                violenceScore: 0.01m,
                isAiGenerated: false,
                aiDetectionScore: 0.05m,
                suggestedTags: Array.Empty<string>(),
                flagReason: null);
        }

        // 1. Tải dữ liệu ảnh sang Base64 (hỗ trợ cả file cục bộ trong wwwroot lẫn URL HTTP)
        byte[] imageBytes;
        string mimeType;

        var (localBytes, localMime) = TryLoadFromLocalFile(imageUrl);
        if (localBytes != null && localMime != null)
        {
            imageBytes = localBytes;
            mimeType = localMime;
        }
        else
        {
            try
            {
                using var imgRequest = new HttpRequestMessage(HttpMethod.Get, imageUrl);
                using var imgResponse = await _httpClient.SendAsync(imgRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (!imgResponse.IsSuccessStatusCode)
                {
                    return ArtworkScanResult.Fail($"Không thể tải ảnh tác phẩm từ URL ({imgResponse.StatusCode}).");
                }

                if (imgResponse.Content.Headers.ContentLength > MaxImageDownloadSizeBytes)
                {
                    return ArtworkScanResult.Fail("Dung lượng ảnh vượt quá giới hạn 25MB cho phép phân tích AI.");
                }

                mimeType = imgResponse.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                imageBytes = await imgResponse.Content.ReadAsByteArrayAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi tải ảnh tác phẩm từ URL {ImageUrl}.", imageUrl);
                return ArtworkScanResult.Fail($"Lỗi khi tải ảnh: {ex.Message}");
            }
        }

        var base64Data = Convert.ToBase64String(imageBytes);

        // 2. Xây dựng prompt phân tích và cấu hình JSON schema
        var systemPrompt = 
            "You are an expert AI content moderator and digital artwork analysis specialist.\n" +
            "Analyze the provided artwork image and evaluate the following metrics accurately:\n" +
            "1. Safety & Content Moderation:\n" +
            "   - safeScore: decimal from 0.0 to 1.0 (1.0 = completely safe, appropriate for general audience).\n" +
            "   - adultScore: decimal from 0.0 to 1.0 (1.0 = explicit adult, sexual, nudity, or NSFW content).\n" +
            "   - violenceScore: decimal from 0.0 to 1.0 (1.0 = graphic violence, gore, blood, or disturbing imagery).\n" +
            "2. AI Generation Detection:\n" +
            "   - isAiGenerated: boolean indicating whether the image exhibits hallmarks of AI image generation (e.g. Midjourney, Stable Diffusion, DALL-E artifacts, prompt-rendered gloss, impossible anatomy).\n" +
            "   - aiDetectionScore: decimal from 0.0 to 1.0 representing confidence that this image was generated by AI.\n" +
            "3. Moderation Reasoning & Tags:\n" +
            "   - flagReason: if adultScore >= 0.70 output 'AI NSFW Threshold Exceeded'; if violenceScore >= 0.70 output 'Violence/Gore Detected'; if aiDetectionScore >= 0.75 output 'AI Generated Elements Detected'; otherwise null.\n" +
            "   - suggestedTags: array of up to 5 concise visual style/subject tags (e.g. ['anime', 'cyberpunk', 'concept art']).\n\n" +
            "Output MUST be valid JSON matching this schema:\n" +
            "{\n" +
            "  \"safeScore\": 0.95,\n" +
            "  \"adultScore\": 0.02,\n" +
            "  \"violenceScore\": 0.01,\n" +
            "  \"isAiGenerated\": false,\n" +
            "  \"aiDetectionScore\": 0.05,\n" +
            "  \"flagReason\": null,\n" +
            "  \"suggestedTags\": [\"style1\", \"style2\"]\n" +
            "}";

        var payload = new GeminiRequest
        {
            Contents =
            [
                new GeminiContent
                {
                    Role = "user",
                    Parts =
                    [
                        new GeminiPart { Text = "Analyze this artwork image according to the safety and AI detection guidelines." },
                        new GeminiPart
                        {
                            InlineData = new GeminiInlineData
                            {
                                MimeType = mimeType,
                                Data = base64Data
                            }
                        }
                    ]
                }
            ],
            SystemInstruction = new GeminiContent
            {
                Parts = [new GeminiPart { Text = systemPrompt }]
            },
            GenerationConfig = new GeminiGenerationConfig
            {
                ResponseMimeType = "application/json",
                Temperature = 0.2,
                MaxOutputTokens = 1024
            }
        };

        var models = _options.CandidateModels;
        if (models.Count == 0)
        {
            return ArtworkScanResult.Fail("Chưa cấu hình model Gemini nào.");
        }

        string? lastError = null;

        // 3. Thử gọi API qua các model và cơ chế retry
        for (var modelIndex = 0; modelIndex < models.Count; modelIndex++)
        {
            var model = models[modelIndex];

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    var url = $"{_options.BaseUrl.TrimEnd('/')}/models/{model}:generateContent";

                    using var request = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = JsonContent.Create(payload, options: SerializerOptions)
                    };
                    request.Headers.Add("x-goog-api-key", _options.ApiKey);

                    using var response = await _httpClient.SendAsync(request, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var scanResult = await ParseScanResponseAsync(response, cancellationToken);
                        if (scanResult.Success)
                        {
                            return scanResult;
                        }

                        lastError = scanResult.Error ?? $"Model {model} không trả nội dung phân tích.";
                        break;
                    }

                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    var status = (int)response.StatusCode;
                    lastError = $"Gemini Vision trả HTTP {status} (model {model}): {body}";

                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        break; // Model không tồn tại hoặc bị gỡ, thử model kế tiếp
                    }

                    var retryable = response.StatusCode == HttpStatusCode.TooManyRequests || status >= 500;
                    if (!retryable)
                    {
                        return ArtworkScanResult.Fail(lastError);
                    }

                    if (attempt < MaxAttempts)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
                {
                    lastError = $"Lỗi mạng khi phân tích tranh bằng Gemini (model {model}): {ex.Message}";
                    _logger.LogWarning(ex, "Lỗi kết nối Gemini (lần thử {Attempt}/{Max}).", attempt, MaxAttempts);

                    if (attempt < MaxAttempts)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), cancellationToken);
                    }
                }
            }
        }

        return ArtworkScanResult.Fail(lastError ?? "Quá trình quét AI thất bại.");
    }

    private async Task<ArtworkScanResult> ParseScanResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var parsed = JsonSerializer.Deserialize<GeminiResponseRoot>(rawJson, SerializerOptions);

        var text = parsed?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            var finishReason = parsed?.Candidates?.FirstOrDefault()?.FinishReason;
            var blockReason = parsed?.PromptFeedback?.BlockReason;
            return ArtworkScanResult.Fail($"Gemini không trả nội dung (blockReason={blockReason}, finishReason={finishReason}).");
        }

        try
        {
            var dto = JsonSerializer.Deserialize<GeminiScanResultDto>(text, SerializerOptions);
            if (dto == null)
            {
                return ArtworkScanResult.Fail("Không thể giải mã dữ liệu JSON từ kết quả phân tích Gemini.");
            }

            var safeScore = Math.Clamp(dto.SafeScore ?? 1.0m, 0.0m, 1.0m);
            var adultScore = Math.Clamp(dto.AdultScore ?? 0.0m, 0.0m, 1.0m);
            var violenceScore = Math.Clamp(dto.ViolenceScore ?? 0.0m, 0.0m, 1.0m);
            var aiScore = Math.Clamp(dto.AiDetectionScore ?? 0.0m, 0.0m, 1.0m);
            var isAi = dto.IsAiGenerated ?? (aiScore >= 0.75m);

            string? flagReason = dto.FlagReason;
            if (string.IsNullOrWhiteSpace(flagReason))
            {
                if (adultScore >= 0.70m) flagReason = "AI NSFW Threshold Exceeded";
                else if (violenceScore >= 0.70m) flagReason = "Violence/Gore Detected";
                else if (aiScore >= 0.75m) flagReason = "AI Generated Elements Detected";
            }

            return ArtworkScanResult.Ok(
                safeScore: safeScore,
                adultScore: adultScore,
                violenceScore: violenceScore,
                isAiGenerated: isAi,
                aiDetectionScore: aiScore,
                suggestedTags: (IReadOnlyList<string>?)dto.SuggestedTags ?? Array.Empty<string>(),
                flagReason: flagReason);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Lỗi phân tích JSON kết quả quét AI: {Text}", text);
            return ArtworkScanResult.Fail($"JSON từ Gemini không đúng định dạng: {ex.Message}");
        }
    }

    // ------------------------------------------------------------------
    // DTOs & Models cho Gemini Multimodal API
    // ------------------------------------------------------------------

    private sealed class GeminiRequest
    {
        [JsonPropertyName("contents")]
        public List<GeminiContent> Contents { get; set; } = [];

        [JsonPropertyName("systemInstruction")]
        public GeminiContent? SystemInstruction { get; set; }

        [JsonPropertyName("generationConfig")]
        public GeminiGenerationConfig? GenerationConfig { get; set; }
    }

    private sealed class GeminiContent
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; } = [];
    }

    private sealed class GeminiPart
    {
        [JsonPropertyName("text")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Text { get; set; }

        [JsonPropertyName("inline_data")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GeminiInlineData? InlineData { get; set; }
    }

    private sealed class GeminiInlineData
    {
        [JsonPropertyName("mime_type")]
        public string MimeType { get; set; } = "image/jpeg";

        [JsonPropertyName("data")]
        public string Data { get; set; } = string.Empty;
    }

    private sealed class GeminiGenerationConfig
    {
        [JsonPropertyName("response_mime_type")]
        public string ResponseMimeType { get; set; } = "application/json";

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.2;

        [JsonPropertyName("maxOutputTokens")]
        public int MaxOutputTokens { get; set; } = 1024;
    }

    private sealed class GeminiResponseRoot
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }

        [JsonPropertyName("promptFeedback")]
        public GeminiPromptFeedback? PromptFeedback { get; set; }
    }

    private sealed class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }

        [JsonPropertyName("finishReason")]
        public string? FinishReason { get; set; }
    }

    private sealed class GeminiPromptFeedback
    {
        [JsonPropertyName("blockReason")]
        public string? BlockReason { get; set; }
    }

    private sealed class GeminiScanResultDto
    {
        [JsonPropertyName("safeScore")]
        public decimal? SafeScore { get; set; }

        [JsonPropertyName("adultScore")]
        public decimal? AdultScore { get; set; }

        [JsonPropertyName("violenceScore")]
        public decimal? ViolenceScore { get; set; }

        [JsonPropertyName("isAiGenerated")]
        public bool? IsAiGenerated { get; set; }

        [JsonPropertyName("aiDetectionScore")]
        public decimal? AiDetectionScore { get; set; }

        [JsonPropertyName("flagReason")]
        public string? FlagReason { get; set; }

        [JsonPropertyName("suggestedTags")]
        public List<string>? SuggestedTags { get; set; }
    }

    private static (byte[]? bytes, string? mimeType) TryLoadFromLocalFile(string imageUrl)
    {
        try
        {
            string? relativePath = null;
            if (imageUrl.StartsWith("/") || imageUrl.StartsWith("\\"))
            {
                relativePath = imageUrl.TrimStart('/', '\\');
            }
            else if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) &&
                     (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("127.0.0.1")))
            {
                relativePath = uri.AbsolutePath.TrimStart('/', '\\');
            }

            if (!string.IsNullOrWhiteSpace(relativePath))
            {
                var candidates = new[]
                {
                    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath),
                    Path.Combine(Directory.GetCurrentDirectory(), relativePath),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", relativePath),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath)
                };

                foreach (var candidate in candidates)
                {
                    if (File.Exists(candidate))
                    {
                        var ext = Path.GetExtension(candidate).ToLowerInvariant();
                        var mime = ext switch
                        {
                            ".png" => "image/png",
                            ".webp" => "image/webp",
                            ".gif" => "image/gif",
                            _ => "image/jpeg"
                        };
                        return (File.ReadAllBytes(candidate), mime);
                    }
                }
            }
        }
        catch
        {
            // Bỏ qua lỗi đọc file cục bộ, tiếp tục với HTTP client
        }

        return (null, null);
    }
}

