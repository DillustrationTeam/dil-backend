using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArtCommission.Application.Ai.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArtCommission.Infrastructure.ExternalServices.Gemini;

/// <summary>
/// Cài đặt cổng AI bằng Google AI Studio (Gemini) cho UC44 (chatbot) và UC43 (dịch tin nhắn).
///
/// NGUYÊN TẮC THIẾT KẾ:
/// 1. KHÔNG ném exception ra ngoài cho lỗi phía nhà cung cấp. Mọi lỗi được gói vào
///    <see cref="AiCompletionResult.Fail"/> để handler lưu được lý do và cho phép retry.
///    Ném exception sẽ biến lỗi tạm thời của AI thành lỗi 400/500 của cả endpoint.
/// 2. Chỉ retry lỗi CÓ THỂ retry (429, 5xx, lỗi mạng). Lỗi 400/403 là lỗi cấu hình
///    hoặc nội dung bị chặn — retry chỉ tốn thời gian và tiền.
/// 3. KHÔNG log API key, không log toàn bộ prompt (có thể chứa dữ liệu người dùng).
/// </summary>
public class GeminiAiClient : IAiChatClient, ITranslationClient
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiAiClient> _logger;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Số lần thử lại tối đa cho lỗi tạm thời.</summary>
    private const int MaxAttempts = 3;

    public GeminiAiClient(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<GeminiAiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public Task<AiCompletionResult> CompleteAsync(
        string? systemInstruction,
        IReadOnlyList<AiChatTurn> history,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var contents = new List<GeminiContent>();

        foreach (var turn in history)
        {
            if (string.IsNullOrWhiteSpace(turn.Content))
            {
                continue;
            }

            contents.Add(new GeminiContent
            {
                // Gemini chỉ nhận 2 vai trò: "user" và "model". Vai trò "system"
                // của ta phải hạ xuống "user", nếu không API trả 400.
                Role = string.Equals(turn.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                    ? "model"
                    : "user",
                Parts = [new GeminiPart { Text = turn.Content }]
            });
        }

        contents.Add(new GeminiContent
        {
            Role = "user",
            Parts = [new GeminiPart { Text = userMessage }]
        });

        return SendAsync(contents, systemInstruction, cancellationToken);
    }

    public Task<AiCompletionResult> TranslateAsync(
        string text,
        string targetLang,
        CancellationToken cancellationToken = default)
    {
        // Prompt dịch siết chặt: chỉ trả bản dịch, không giải thích, giữ nguyên
        // xuống dòng và các đoạn code/tên riêng. Nếu không siết, model sẽ thêm
        // "Here is the translation:" và bản dịch lưu vào DB sẽ bẩn.
        var system = "Bạn là công cụ dịch thuật. Chỉ trả về bản dịch, không thêm lời dẫn, " +
                     "không giải thích, không đặt trong dấu ngoặc kép. Giữ nguyên xuống dòng, " +
                     "tên riêng, con số và thuật ngữ kỹ thuật.";

        var contents = new List<GeminiContent>
        {
            new()
            {
                Role = "user",
                Parts = [new GeminiPart { Text = $"Dịch đoạn sau sang {targetLang}:\n\n{text}" }]
            }
        };

        return SendAsync(contents, system, cancellationToken);
    }

    // ------------------------------------------------------------------
    // Gọi API
    // ------------------------------------------------------------------

    private async Task<AiCompletionResult> SendAsync(
        List<GeminiContent> contents,
        string? systemInstruction,
        CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            return AiCompletionResult.Fail(
                "Chưa cấu hình Gemini API key (Gemini:ApiKey / biến môi trường Gemini__ApiKey).");
        }

        var payload = new GeminiRequest
        {
            Contents = contents,
            SystemInstruction = string.IsNullOrWhiteSpace(systemInstruction)
                ? null
                : new GeminiContent { Parts = [new GeminiPart { Text = systemInstruction }] },
            GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = _options.Temperature,
                MaxOutputTokens = _options.MaxOutputTokens
            }
        };

        var models = _options.CandidateModels;

        if (models.Count == 0)
        {
            return AiCompletionResult.Fail("Chưa cấu hình model Gemini nào (Gemini:Model).");
        }

        string? lastError = null;

        // Thử LẦN LƯỢT từng model: model chính trước, rồi model dự phòng.
        for (var modelIndex = 0; modelIndex < models.Count; modelIndex++)
        {
            var model = models[modelIndex];
            var isFallback = modelIndex > 0;

            if (isFallback)
            {
                _logger.LogWarning(
                    "Chuyển sang model dự phòng {Model} sau khi {Primary} thất bại: {Error}",
                    model, models[0], lastError);
            }

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    var url = $"{_options.BaseUrl.TrimEnd('/')}/models/{model}:generateContent";

                    using var request = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        // Key đi qua header, KHÔNG nhét vào query-string: query-string
                        // lọt vào log truy cập của proxy/App Service.
                        Content = JsonContent.Create(payload, options: SerializerOptions)
                    };
                    request.Headers.Add("x-goog-api-key", _options.ApiKey);

                    using var response = await _httpClient.SendAsync(request, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var completion = await ParseSuccessAsync(response, model, cancellationToken);

                        if (completion.Success)
                        {
                            return completion;
                        }

                        // HTTP 200 nhưng không có nội dung dùng được: JSON hỏng, bị chặn
                        // vì an toàn nội dung, hoặc hết token trước khi sinh chữ.
                        // Chuyển sang model dự phòng vì có trường hợp đây là đặc thù của
                        // riêng model đang gọi (ví dụ cách chia ngân sách token khác nhau).
                        lastError = completion.Error ?? $"Model {model} không trả nội dung.";
                        _logger.LogWarning(
                            "Model {Model} trả về nhưng không có nội dung: {Error}", model, lastError);

                        break;
                    }

                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    var status = (int)response.StatusCode;
                    lastError = $"Gemini trả {status} {response.ReasonPhrase} (model {model}).";

                    _logger.LogWarning(
                        "Gọi Gemini thất bại (model {Model}, lần {Attempt}/{Max}, mã {Status}).",
                        model, attempt, MaxAttempts, status);

                    // 404 = model không tồn tại hoặc đã bị Google gỡ. Thử lại cùng model
                    // là vô ích ⇒ nhảy ngay sang model dự phòng.
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        break;
                    }

                    // 429 và 5xx là lỗi TẠM THỜI ⇒ thử lại cùng model, hết lượt thì
                    // mới chuyển sang model dự phòng (rate limit thường theo từng model).
                    var retryable = response.StatusCode == HttpStatusCode.TooManyRequests || status >= 500;

                    if (!retryable)
                    {
                        // 400/401/403 là lỗi cấu hình hoặc request sai — model dự phòng
                        // cũng sẽ hỏng y hệt, nên dừng luôn thay vì tốn thêm một lượt gọi.
                        return AiCompletionResult.Fail(AppendDetail(lastError, body));
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Người dùng huỷ request — không phải lỗi của AI, không retry.
                    throw;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
                {
                    lastError = $"Không gọi được Gemini (model {model}): {ex.Message}";
                    _logger.LogWarning(
                        ex, "Lỗi mạng khi gọi Gemini (model {Model}, lần {Attempt}/{Max}).",
                        model, attempt, MaxAttempts);
                }

                if (attempt < MaxAttempts)
                {
                    // Backoff luỹ tiến: 400ms, 800ms. Đủ để vượt rate-limit ngắn mà
                    // không giữ request của người dùng quá lâu.
                    var delay = TimeSpan.FromMilliseconds(400 * Math.Pow(2, attempt - 1));
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        // Hết cả model chính lẫn dự phòng.
        return AiCompletionResult.Fail(lastError ?? "Gọi Gemini thất bại sau nhiều lần thử.");
    }

    /// <param name="model">
    /// Model THỰC SỰ đã trả lời lượt này. Ghi lại vào <c>ModelVersion</c> để biết
    /// câu trả lời đến từ model chính hay model dự phòng — không có nó thì khi chất
    /// lượng câu trả lời giảm, không ai biết là do đã âm thầm chuyển model.
    /// </param>
    private async Task<AiCompletionResult> ParseSuccessAsync(
        HttpResponseMessage response,
        string model,
        CancellationToken cancellationToken)
    {
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        GeminiResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<GeminiResponse>(json);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Không đọc được phản hồi Gemini.");
            return AiCompletionResult.Fail("Phản hồi từ Gemini không đúng định dạng JSON.");
        }

        var text = parsed?.Candidates?
            .FirstOrDefault()?
            .Content?
            .Parts?
            .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.Text))?
            .Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            // Trường hợp điển hình: bị chặn bởi safety filter, hoặc hết token vì
            // thinking budget. Ghi lại finishReason để tra soát thay vì báo lỗi chung chung.
            var finishReason = parsed?.Candidates?.FirstOrDefault()?.FinishReason;
            var blockReason = parsed?.PromptFeedback?.BlockReason;

            var reason = blockReason is not null
                ? $"Nội dung bị chặn (blockReason={blockReason})."
                : finishReason is not null
                    ? $"Gemini không trả nội dung (finishReason={finishReason})."
                    : "Gemini trả về nội dung rỗng.";

            _logger.LogWarning("Gemini không có nội dung: {Reason}", reason);
            return AiCompletionResult.Fail(reason);
        }

        var tokenCount = parsed?.UsageMetadata?.TotalTokenCount;

        return AiCompletionResult.Ok(text.Trim(), tokenCount, model);
    }

    /// <summary>
    /// Gắn trích đoạn thân lỗi vào thông báo — cắt ngắn để không tràn cột DB
    /// và không lộ chi tiết nội bộ ra ngoài quá mức cần thiết.
    /// </summary>
    private static string AppendDetail(string message, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return message;
        }

        var trimmed = body.Length > 300 ? body[..300] : body;
        return $"{message} Chi tiết: {trimmed}";
    }

    // ------------------------------------------------------------------
    // Hợp đồng JSON của Gemini
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
        public string Text { get; set; } = string.Empty;
    }

    private sealed class GeminiGenerationConfig
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("maxOutputTokens")]
        public int MaxOutputTokens { get; set; }
    }

    private sealed class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }

        [JsonPropertyName("usageMetadata")]
        public GeminiUsageMetadata? UsageMetadata { get; set; }

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

    private sealed class GeminiUsageMetadata
    {
        [JsonPropertyName("totalTokenCount")]
        public int? TotalTokenCount { get; set; }
    }

    private sealed class GeminiPromptFeedback
    {
        [JsonPropertyName("blockReason")]
        public string? BlockReason { get; set; }
    }
}
