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

    /// <summary>Số vòng gọi công cụ tối đa trong một lượt hỏi để tránh vòng lặp vô hạn.</summary>
    private const int MaxToolRounds = 4;

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
        CancellationToken cancellationToken = default,
        int? maxOutputTokens = null,
        bool jsonResponse = false)
    {
        var contents = BuildContents(history, userMessage);
        return SendAsync(contents, systemInstruction, cancellationToken, maxOutputTokens, jsonResponse);
    }

    public async Task<AiCompletionResult> CompleteWithToolsAsync(
        string? systemInstruction,
        IReadOnlyList<AiChatTurn> history,
        string userMessage,
        IReadOnlyList<IAiTool> tools,
        CancellationToken cancellationToken = default)
    {
        if (tools.Count == 0)
        {
            return await CompleteAsync(systemInstruction, history, userMessage, cancellationToken);
        }

        var contents = BuildContents(history, userMessage);
        var toolsByName = tools.ToDictionary(t => t.Name, StringComparer.Ordinal);
        var toolDeclaration = new GeminiTool
        {
            FunctionDeclarations = tools.Select(t => new GeminiFunctionDeclaration
            {
                Name = t.Name,
                Description = t.Description,
                Parameters = t.ParametersSchema
            }).ToList()
        };

        int? totalTokens = null;

        for (var round = 0; round < MaxToolRounds; round++)
        {
            var post = await PostAsync(
                contents,
                systemInstruction,
                [toolDeclaration],
                cancellationToken);

            if (!post.Success || post.Response is null)
            {
                return AiCompletionResult.Fail(post.Error ?? "Gọi Gemini thất bại.");
            }

            if (post.Response.UsageMetadata?.TotalTokenCount is int roundTokens)
            {
                totalTokens = (totalTokens ?? 0) + roundTokens;
            }

            var modelContent = post.Response.Candidates?.FirstOrDefault()?.Content;
            if (modelContent is null || modelContent.Parts.Count == 0)
            {
                return AiCompletionResult.Fail(FormatEmptyContentReason(post.Response));
            }

            var functionCalls = modelContent.Parts
                .Where(p => p.FunctionCall is not null)
                .ToList();

            if (functionCalls.Count == 0)
            {
                var text = string.Concat(
                    modelContent.Parts
                        .Where(p => !string.IsNullOrWhiteSpace(p.Text))
                        .Select(p => p.Text))
                    .Trim();

                return string.IsNullOrEmpty(text)
                    ? AiCompletionResult.Fail(FormatEmptyContentReason(post.Response))
                    : AiCompletionResult.Ok(text, totalTokens, post.Model!);
            }

            // Giữ nguyên turn của model (kể cả thoughtSignature nếu có) rồi nối kết quả tra cứu.
            modelContent.Role = "model";
            contents.Add(modelContent);

            var responseParts = new List<GeminiPart>(functionCalls.Count);
            foreach (var part in functionCalls)
            {
                var call = part.FunctionCall!;
                object result;
                try
                {
                    result = toolsByName.TryGetValue(call.Name, out var tool)
                        ? await tool.ExecuteAsync(call.Args, cancellationToken)
                        : new { error = $"Unknown function '{call.Name}'." };
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Công cụ AI {ToolName} thực thi thất bại.", call.Name);
                    result = new { error = "Lookup failed." };
                }

                responseParts.Add(new GeminiPart
                {
                    FunctionResponse = new GeminiFunctionResponse
                    {
                        Name = call.Name,
                        Response = new { result }
                    }
                });
            }

            contents.Add(new GeminiContent
            {
                Role = "user",
                Parts = responseParts
            });
        }

        return AiCompletionResult.Fail("Quá nhiều vòng gọi công cụ tra cứu.");
    }

    private static List<GeminiContent> BuildContents(
        IReadOnlyList<AiChatTurn> history,
        string userMessage)
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

        return contents;
    }

    public Task<AiCompletionResult> TranslateAsync(
        string text,
        string targetLang,
        CancellationToken cancellationToken = default)
    {
        // Prompt dịch siết chặt: chỉ trả bản dịch, không giải thích, giữ nguyên
        // xuống dòng và các đoạn code/tên riêng. Nếu không siết, model sẽ thêm
        // "Here is the translation:" và bản dịch lưu vào DB sẽ bẩn.
        //
        // ⚠️ LỖI ĐÃ GẶP THẬT: ghi "Dịch đoạn sau sang vi" thì model hiểu nhầm và trả về
        // TIẾNG ANH cho một tin tiếng Việt (targetLang = "vi"). Vì vậy phải ghi rõ TÊN
        // ngôn ngữ đích và nói thẳng "không đổi sang ngôn ngữ khác".
        var target = DescribeLanguage(targetLang);

        var system = "Bạn là công cụ dịch thuật. Chỉ trả về bản dịch, không thêm lời dẫn, " +
                     "không giải thích, không đặt trong dấu ngoặc kép. Giữ nguyên xuống dòng, " +
                     "tên riêng, con số và thuật ngữ kỹ thuật. " +
                     $"Luôn dịch SANG {target}; tuyệt đối không dịch sang ngôn ngữ khác. " +
                     $"Nếu văn bản đã ở {target} thì trả lại nguyên văn, không đổi ngôn ngữ.";

        var contents = new List<GeminiContent>
        {
            new()
            {
                Role = "user",
                Parts = [new GeminiPart { Text = $"Dịch đoạn sau sang {target}:\n\n{text}" }]
            }
        };

        return SendAsync(contents, system, cancellationToken);
    }

    /// <summary>
    /// Mã ngôn ngữ → tên đọc được, để model không đoán sai ngôn ngữ đích.
    /// Mã lạ thì trả lại nguyên mã (hành vi cũ).
    /// </summary>
    private static string DescribeLanguage(string code) => code.Trim().ToLowerInvariant() switch
    {
        "vi" or "vi-vn" => "tiếng Việt (Vietnamese)",
        "en" or "en-us" or "en-gb" => "tiếng Anh (English)",
        "ja" or "ja-jp" => "tiếng Nhật (Japanese)",
        "ko" or "ko-kr" => "tiếng Hàn (Korean)",
        "zh" or "zh-cn" or "zh-tw" => "tiếng Trung (Chinese)",
        "fr" or "fr-fr" => "tiếng Pháp (French)",
        "es" or "es-es" => "tiếng Tây Ban Nha (Spanish)",
        _ => code
    };

    // ------------------------------------------------------------------
    // Gọi API
    // ------------------------------------------------------------------

    private async Task<AiCompletionResult> SendAsync(
        List<GeminiContent> contents,
        string? systemInstruction,
        CancellationToken cancellationToken,
        int? maxOutputTokens = null,
        bool jsonResponse = false)
    {
        var post = await PostAsync(
            contents,
            systemInstruction,
            tools: null,
            cancellationToken,
            maxOutputTokens,
            jsonResponse);

        if (!post.Success || post.Response is null)
        {
            return AiCompletionResult.Fail(post.Error ?? "Gọi Gemini thất bại sau nhiều lần thử.");
        }

        var text = post.Response.Candidates?
            .FirstOrDefault()?
            .Content?
            .Parts?
            .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.Text))?
            .Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            return AiCompletionResult.Fail(FormatEmptyContentReason(post.Response));
        }

        return AiCompletionResult.Ok(
            text.Trim(),
            post.Response.UsageMetadata?.TotalTokenCount,
            post.Model!);
    }

    private async Task<(bool Success, GeminiResponse? Response, string? Model, string? Error)> PostAsync(
        List<GeminiContent> contents,
        string? systemInstruction,
        List<GeminiTool>? tools,
        CancellationToken cancellationToken,
        int? maxOutputTokens = null,
        bool jsonResponse = false)
    {
        if (!_options.IsConfigured)
        {
            return (false, null, null,
                "Chưa cấu hình Gemini API key (Gemini:ApiKey / biến môi trường Gemini__ApiKey).");
        }

        var payload = new GeminiRequest
        {
            Contents = contents,
            SystemInstruction = string.IsNullOrWhiteSpace(systemInstruction)
                ? null
                : new GeminiContent { Parts = [new GeminiPart { Text = systemInstruction }] },
            Tools = tools is { Count: > 0 } ? tools : null,
            GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = _options.Temperature,
                MaxOutputTokens = maxOutputTokens ?? _options.MaxOutputTokens,
                ResponseMimeType = jsonResponse ? "application/json" : null
            }
        };

        var models = _options.CandidateModels;

        if (models.Count == 0)
        {
            return (false, null, null, "Chưa cấu hình model Gemini nào (Gemini:Model).");
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
                        var (parsed, parseError) = await DeserializeSuccessAsync(response, cancellationToken);
                        if (parsed is not null)
                        {
                            var parts = parsed.Candidates?.FirstOrDefault()?.Content?.Parts;
                            var hasUsablePart = parts is not null && parts.Any(p =>
                                !string.IsNullOrWhiteSpace(p.Text) || p.FunctionCall is not null);

                            if (hasUsablePart)
                            {
                                return (true, parsed, model, null);
                            }

                            lastError = FormatEmptyContentReason(parsed);
                            _logger.LogWarning("Gemini không có nội dung: {Reason}", lastError);
                        }
                        else
                        {
                            lastError = parseError ?? $"Model {model} không trả nội dung.";
                        }

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
                        return (false, null, model, AppendDetail(lastError, body));
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
        return (false, null, null, lastError ?? "Gọi Gemini thất bại sau nhiều lần thử.");
    }

    private async Task<(GeminiResponse? Response, string? Error)> DeserializeSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            return (JsonSerializer.Deserialize<GeminiResponse>(json), null);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Không đọc được phản hồi Gemini.");
            return (null, "Phản hồi từ Gemini không đúng định dạng JSON.");
        }
    }

    private static string FormatEmptyContentReason(GeminiResponse? parsed)
    {
        var finishReason = parsed?.Candidates?.FirstOrDefault()?.FinishReason;
        var blockReason = parsed?.PromptFeedback?.BlockReason;

        return blockReason is not null
            ? $"Nội dung bị chặn (blockReason={blockReason})."
            : finishReason is not null
                ? $"Gemini không trả nội dung (finishReason={finishReason})."
                : "Gemini trả về nội dung rỗng.";
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

        [JsonPropertyName("tools")]
        public List<GeminiTool>? Tools { get; set; }

        [JsonPropertyName("generationConfig")]
        public GeminiGenerationConfig? GenerationConfig { get; set; }
    }

    private sealed class GeminiTool
    {
        [JsonPropertyName("functionDeclarations")]
        public List<GeminiFunctionDeclaration> FunctionDeclarations { get; set; } = [];
    }

    private sealed class GeminiFunctionDeclaration
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("parameters")]
        public object? Parameters { get; set; }
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
        public string? Text { get; set; }

        [JsonPropertyName("functionCall")]
        public GeminiFunctionCall? FunctionCall { get; set; }

        [JsonPropertyName("functionResponse")]
        public GeminiFunctionResponse? FunctionResponse { get; set; }

        [JsonPropertyName("thoughtSignature")]
        public string? ThoughtSignature { get; set; }
    }

    private sealed class GeminiFunctionCall
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("args")]
        public JsonElement Args { get; set; }
    }

    private sealed class GeminiFunctionResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("response")]
        public object? Response { get; set; }
    }

    private sealed class GeminiGenerationConfig
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("maxOutputTokens")]
        public int MaxOutputTokens { get; set; }

        // Null is omitted (SerializerOptions ignores nulls), so plain-text callers are unaffected.
        [JsonPropertyName("responseMimeType")]
        public string? ResponseMimeType { get; set; }
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
