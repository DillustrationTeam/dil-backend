using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ArtCommission.Application.Ai.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArtCommission.Infrastructure.ExternalServices.Google;

/// <summary>
/// Dịch tin nhắn bằng Google Translate (UC43 — AI Real-time Translation).
///
/// VÌ SAO tách khỏi <c>GeminiAiClient</c>: dịch KHÔNG cần mô hình sinh văn bản. Google
/// Translate trả bản dịch trực tiếp nên nhanh hơn, rẻ hơn và không bao giờ "tự đổi
/// ngôn ngữ đích" như lỗi đã gặp với prompt Gemini (tin tiếng Việt bị dịch sang tiếng Anh).
/// Chatbot UC44 vẫn dùng Gemini — hai việc khác nhau, hai cổng khác nhau.
///
/// NGUYÊN TẮC:
///   1. KHÔNG ném exception ra ngoài: lỗi nhà cung cấp được gói vào
///      <see cref="AiCompletionResult.Fail"/> để handler lưu lý do và cho phép retry.
///   2. Có ApiKey ⇒ gọi Cloud Translation v2 chính thức; không có ⇒ endpoint công khai.
///   3. Chia nhỏ văn bản dài: endpoint công khai truyền văn bản qua query-string nên
///      URL quá dài sẽ bị từ chối.
/// </summary>
public class GoogleTranslateClient : ITranslationClient
{
    private readonly HttpClient _httpClient;
    private readonly GoogleTranslateOptions _options;
    private readonly ILogger<GoogleTranslateClient> _logger;

    public GoogleTranslateClient(
        HttpClient httpClient,
        IOptions<GoogleTranslateOptions> options,
        ILogger<GoogleTranslateClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AiCompletionResult> TranslateAsync(
        string text,
        string targetLang,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return AiCompletionResult.Fail("Nội dung rỗng, không có gì để dịch.");
        }

        var target = NormalizeLanguage(targetLang);
        var chunks = SplitIntoChunks(text.Trim(), Math.Max(200, _options.MaxCharactersPerRequest));
        var translatedParts = new List<string>(chunks.Count);

        foreach (var chunk in chunks)
        {
            var (success, translated, error) = await TranslateChunkAsync(chunk, target, cancellationToken);
            if (!success)
            {
                return AiCompletionResult.Fail(error ?? "Google Translate không trả về bản dịch.");
            }

            translatedParts.Add(translated!);
        }

        var content = string.Join(" ", translatedParts).Trim();
        if (content.Length == 0)
        {
            return AiCompletionResult.Fail("Google Translate trả về bản dịch rỗng.");
        }

        return AiCompletionResult.Ok(
            content,
            tokenCount: null,
            modelVersion: _options.HasApiKey ? "google-translate-v2" : "google-translate-free");
    }

    /// <summary>Dịch một đoạn (đã đủ ngắn) và trả về (thành công, bản dịch, lỗi).</summary>
    private async Task<(bool Success, string? Content, string? Error)> TranslateChunkAsync(
        string chunk,
        string target,
        CancellationToken cancellationToken)
    {
        try
        {
            return _options.HasApiKey
                ? await CallOfficialApiAsync(chunk, target, cancellationToken)
                : await CallPublicEndpointAsync(chunk, target, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Google Translate quá thời gian chờ ({Seconds}s).", _options.TimeoutSeconds);
            return (false, null, "Google Translate phản hồi quá chậm, vui lòng thử lại.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Không gọi được Google Translate.");
            return (false, null, "Không kết nối được Google Translate.");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Không đọc được phản hồi của Google Translate.");
            return (false, null, "Google Translate trả về dữ liệu không đọc được.");
        }
    }

    /// <summary>
    /// Endpoint công khai (không cần key). Thử LẦN LƯỢT hai host vì một host có thể bị
    /// Google chặn theo IP/mạng (đã gặp HTTP 429 với translate.googleapis.com).
    /// </summary>
    private async Task<(bool, string?, string?)> CallPublicEndpointAsync(
        string chunk,
        string target,
        CancellationToken cancellationToken)
    {
        var encodedText = Uri.EscapeDataString(chunk);
        var encodedTarget = Uri.EscapeDataString(target);

        var endpoints = new[]
        {
            // Host chính: trả mảng lồng [[["bản dịch","nguồn",...]],...]
            $"{_options.FreeBaseUrl}?client=gtx&sl=auto&tl={encodedTarget}&dt=t&q={encodedText}",
            // Host dự phòng: trả mảng phẳng ["bản dịch"]
            $"{_options.FreeFallbackBaseUrl}?client=dict-chrome-ex&sl=auto&tl={encodedTarget}&q={encodedText}"
        };

        string? lastError = null;

        foreach (var url in endpoints)
        {
            var host = new Uri(url).Host;

            using var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                lastError = $"{host} trả về HTTP {(int)response.StatusCode}";
                _logger.LogWarning("Dịch công khai thất bại: {Reason}", lastError);
                continue;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var (text, detected) = ParsePublicResponse(json);

            if (!string.IsNullOrWhiteSpace(text))
            {
                _logger.LogInformation(
                    "Dịch bằng Google Translate ({Host}): {Chars} ký tự, {Source} -> {Target}.",
                    host, chunk.Length, detected ?? "auto", target);

                return (true, text, null);
            }

            lastError = $"{host} trả về dữ liệu không đọc được";
            _logger.LogWarning("Dịch công khai thất bại: {Reason}", lastError);
        }

        return (false, null, $"Google Translate công khai thất bại ({lastError}).");
    }

    /// <summary>
    /// Đọc phản hồi của hai endpoint công khai thành một chuỗi. Ba shape đã gặp thật:
    ///   • gtx      : <c>[[["dịch","nguồn",null,null,10],["phần 2","nguồn",...]],null,"en"]</c>
    ///   • clients5 (sl=auto) : <c>[["dịch","en"]]</c>  ← phần tử 1 là mã ngôn ngữ phát hiện
    ///   • clients5 (sl=en)   : <c>["dịch"]</c>
    /// Vì vậy KHÔNG được giả định cứng một shape — đã từng làm parser trả "không đọc được"
    /// dù endpoint trả HTTP 200.
    /// </summary>
    private static (string? Text, string? Detected) ParsePublicResponse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
        {
            return (null, null);
        }

        var builder = new StringBuilder();
        string? detected = null;

        if (root[0].ValueKind == JsonValueKind.String)
        {
            // clients5 với sl cố định: ["dịch", "phần 2", ...]
            foreach (var item in root.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    builder.Append(item.GetString());
                }
            }
        }
        else if (root[0].ValueKind == JsonValueKind.Array)
        {
            var inner = root[0];

            if (inner.GetArrayLength() > 0 && inner[0].ValueKind == JsonValueKind.Array)
            {
                // gtx: mỗi phần tử là ["bản dịch", "nguồn", ...]
                foreach (var segment in inner.EnumerateArray())
                {
                    if (segment.ValueKind == JsonValueKind.Array
                        && segment.GetArrayLength() > 0
                        && segment[0].ValueKind == JsonValueKind.String)
                    {
                        builder.Append(segment[0].GetString());
                    }
                }
            }
            else
            {
                // clients5 với sl=auto: ["bản dịch", "en"] — phần tử cuối là mã ngôn ngữ.
                var values = inner.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? string.Empty)
                    .ToList();

                if (values.Count >= 2 && IsLanguageCode(values[^1]))
                {
                    detected = values[^1];
                    values.RemoveAt(values.Count - 1);
                }

                builder.Append(string.Join(" ", values));
            }
        }
        else
        {
            return (null, null);
        }

        // gtx đặt ngôn ngữ phát hiện ở phần tử thứ 3 của mảng gốc.
        if (detected is null && root.GetArrayLength() > 2 && root[2].ValueKind == JsonValueKind.String)
        {
            detected = root[2].GetString();
        }

        var text = builder.ToString();
        return (text.Length > 0 ? text : null, detected);
    }

    /// <summary>Mã ngôn ngữ ISO 639-1 (có thể kèm vùng): "en", "vi", "zh-CN".</summary>
    private static bool IsLanguageCode(string value) =>
        value.Length is >= 2 and <= 5
        && (value.Length == 2 || value[2] == '-')
        && value[..2].All(char.IsAsciiLetterLower);

    /// <summary>
    /// Cloud Translation API v2 (cần ApiKey). Phản hồi:
    /// <c>{ "data": { "translations": [ { "translatedText": "..." } ] } }</c>.
    /// </summary>
    private async Task<(bool, string?, string?)> CallOfficialApiAsync(
        string chunk,
        string target,
        CancellationToken cancellationToken)
    {
        var url = $"{_options.OfficialBaseUrl}?key={Uri.EscapeDataString(_options.ApiKey)}";

        using var response = await _httpClient.PostAsJsonAsync(
            url,
            new { q = chunk, target, format = "text" },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var reason = $"Google Cloud Translation trả về HTTP {(int)response.StatusCode}.";
            _logger.LogWarning("Dịch thất bại: {Reason}", reason);
            return (false, null, reason);
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("data", out var data)
            || !data.TryGetProperty("translations", out var translations)
            || translations.ValueKind != JsonValueKind.Array
            || translations.GetArrayLength() == 0)
        {
            return (false, null, "Google Cloud Translation trả về dữ liệu không đúng định dạng.");
        }

        var first = translations[0];
        var text = first.TryGetProperty("translatedText", out var value) ? value.GetString() : null;
        var detected = first.TryGetProperty("detectedSourceLanguage", out var lang) ? lang.GetString() : null;

        _logger.LogInformation(
            "Dịch bằng Google Cloud Translation v2: {Chars} ký tự, {Source} -> {Target}.",
            chunk.Length, detected ?? "auto", target);

        // v2 trả HTML entity (&#39;...) nên phải giải mã trước khi lưu.
        var content = WebUtility.HtmlDecode(text ?? string.Empty);
        return content.Length > 0
            ? (true, content, null)
            : (false, null, "Google Cloud Translation trả về bản dịch rỗng.");
    }

    /// <summary>Chuẩn hoá mã ngôn ngữ về dạng Google nhận ("vi-VN" → "vi").</summary>
    private static string NormalizeLanguage(string code)
    {
        var value = (code ?? string.Empty).Trim().ToLowerInvariant();
        var dash = value.IndexOf('-');
        return dash > 0 ? value[..dash] : value;
    }

    /// <summary>
    /// Chia văn bản thành các đoạn ≤ <paramref name="maxCharacters"/>, ưu tiên cắt ở
    /// khoảng trắng/xuống dòng để không chặt giữa từ.
    /// </summary>
    private static List<string> SplitIntoChunks(string text, int maxCharacters)
    {
        var chunks = new List<string>();

        if (text.Length <= maxCharacters)
        {
            chunks.Add(text);
            return chunks;
        }

        var start = 0;
        while (start < text.Length)
        {
            var length = Math.Min(maxCharacters, text.Length - start);
            var end = start + length;

            if (end < text.Length)
            {
                var boundary = text.LastIndexOfAny([' ', '\n', '\t', '.', ',', '!', '?'], end - 1, length);
                if (boundary > start)
                {
                    end = boundary + 1;
                }
            }

            chunks.Add(text[start..end].Trim());
            start = end;
        }

        return chunks.Where(c => c.Length > 0).ToList();
    }
}
