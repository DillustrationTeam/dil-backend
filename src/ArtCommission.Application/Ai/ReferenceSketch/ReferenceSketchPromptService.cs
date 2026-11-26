using System.Text.Json;
using System.Text.RegularExpressions;
using ArtCommission.Application.Ai.Common;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Application.Ai.ReferenceSketch;

public sealed record ReferenceSketchPolicyResult(bool Success, bool Allowed, string? ReasonVi, string? ReasonEn, string? Error)
{
    public static ReferenceSketchPolicyResult Ok(bool allowed, string? reasonVi, string? reasonEn) =>
        new(true, allowed, reasonVi, reasonEn, null);

    public static ReferenceSketchPolicyResult Fail(string error) =>
        new(false, false, null, null, error);
}

public sealed record ReferenceSketchVariant(
    string Pose,
    string Camera,
    string Composition,
    string PromptEn,
    string LabelVi,
    string LabelEn);

public sealed record ReferenceSketchVariantsResult(bool Success, ReferenceSketchVariant[]? Variants, string? Error)
{
    public static ReferenceSketchVariantsResult Ok(ReferenceSketchVariant[] variants) =>
        new(true, variants, null);

    public static ReferenceSketchVariantsResult Fail(string error) =>
        new(false, null, error);
}

public sealed record ReferenceSketchLayoutHint(string Technique, string TextVi);

public sealed record ReferenceSketchLayoutHintsResult(bool Success, ReferenceSketchLayoutHint[]? Hints, string? Error)
{
    public static ReferenceSketchLayoutHintsResult Ok(ReferenceSketchLayoutHint[] hints) =>
        new(true, hints, null);

    public static ReferenceSketchLayoutHintsResult Fail(string error) =>
        new(false, null, error);
}

/// <summary>
/// AI-UC — Kiểm tra quy định creator và viết 2 phương án bố cục (pose/camera/composition)
/// cho tính năng AI phác thảo bố cục tham khảo khi khách đặt commission.
/// </summary>
public class ReferenceSketchPromptService
{
    /// <summary>
    /// Trần token riêng cho 2 lượt gọi Gemini của tính năng này — cao hơn mặc định chung
    /// (<c>GeminiOptions.MaxOutputTokens</c> = 1024) vì JSON trả về (đặc biệt là 2 phương án +
    /// promptEn) từng bị Gemini cắt giữa chuỗi khi "thinking" ngầm ăn gần hết ngân sách token
    /// trước khi sinh chữ hiển thị. Đặt riêng ở đây để không ảnh hưởng mặc định chung của các
    /// tính năng AI khác (chatbot, dịch tin nhắn, brief-helper).
    /// </summary>
    private const int PromptMaxOutputTokens = 8192; // hidden "thinking" tokens count against this cap; 4096 could still cut the JSON

    /// <summary>
    /// Phong cách vẽ cố định cho ảnh phác thảo bố cục — khách không đổi được.
    /// Ghép vào đầu mọi promptEn gửi cho AI sinh ảnh.
    /// </summary>
    private const string SketchStyleBase =
        "Art style: simple storyboard thumbnail. Bold even dark teal-navy outlines around the big shapes only, " +
        "drawn with few confident strokes. Flat tones only: shadows are one flat mid-gray shape, background " +
        "buildings, walls and terrain are big flat light-gray blocks with a few perspective lines, sky and open " +
        "areas plain white. Always drawn on bright white paper: never draw night, darkness, rain, fog or " +
        "lighting effects, even if the scene mentions them. Foreground objects are flat gray silhouettes with a clean outline, never blurred. " +
        "No blur, no depth of field, no gradients, no hatching, no texture, no color, no small props, no decorative " +
        "details, no fine details.";

    /// <summary>Chỉ ghép khi cảnh có nhân vật — cảnh thuần bối cảnh/vật thể không cần câu này.</summary>
    public const string MannequinFigureClause =
        " Every character is drawn ONLY as a figure construction sketch (block-in): a curved gesture line for " +
        "the spine, simple ovals and boxes for ribcage and pelvis, cylinders for arms and legs, small circles " +
        "for joints, simple wedge shapes for hands and feet, and the head as a plain oval with a cross " +
        "guideline. Bare wooden-mannequin construction only: no clothing, no hair, no accessories, no " +
        "held objects, no facial features, no muscles. The construction must clearly show the pose.";

    private const string SketchStyleClosing =
        " This is a rough layout sketch only, not a finished illustration — it must only suggest pose, " +
        "framing, and point of view.";

    private readonly IAiChatClient _aiChatClient;
    private readonly ILogger<ReferenceSketchPromptService> _logger;

    public ReferenceSketchPromptService(IAiChatClient aiChatClient, ILogger<ReferenceSketchPromptService> logger)
    {
        _aiChatClient = aiChatClient;
        _logger = logger;
    }

    public async Task<ReferenceSketchPolicyResult> CheckPolicyAsync(
        string brief,
        string? layoutHint,
        string? willDraw,
        string? wontDraw,
        CancellationToken cancellationToken = default)
    {
        var first = await CheckPolicyOnceAsync(brief, layoutHint, willDraw, wontDraw, cancellationToken);
        if (first.Success)
        {
            return first;
        }

        // Gọi lại ĐÚNG 1 lần: lỗi thường gặp là Gemini cắt JSON giữa chuỗi (xem PromptMaxOutputTokens),
        // không phải brief/quy định sai — thử lại trước khi báo lỗi oan cho khách.
        return await CheckPolicyOnceAsync(brief, layoutHint, willDraw, wontDraw, cancellationToken);
    }

    private async Task<ReferenceSketchPolicyResult> CheckPolicyOnceAsync(
        string brief,
        string? layoutHint,
        string? willDraw,
        string? wontDraw,
        CancellationToken cancellationToken)
    {
        const string systemPrompt =
            "Bạn là bộ kiểm duyệt nội dung cho tính năng AI phác thảo bố cục tham khảo trong form đặt " +
            "commission vẽ tranh trên sàn Dillustration.\n\n" +
            "Nhiệm vụ: đọc mô tả yêu cầu (brief) và gợi ý bố cục của khách, so với quy định của họa sĩ " +
            "(những gì họa sĩ SẴN SÀNG vẽ và những gì họa sĩ TỪ CHỐI vẽ), rồi quyết định có cho phép sinh " +
            "ảnh phác thảo hay không.\n\n" +
            "QUY TẮC:\n" +
            "1. Nếu nội dung khách yêu cầu (dù chỉ một phần) rơi vào danh sách TỪ CHỐI của họa sĩ, hoặc là " +
            "nội dung 18+/khiêu dâm/bạo lực cực đoan/vi phạm pháp luật: allowed = false.\n" +
            "2. Nếu không vi phạm gì: allowed = true, reasonVi và reasonEn để chuỗi rỗng \"\".\n" +
            "3. Khi allowed = false, nêu lý do cụ thể (chỉ rõ phần nào vi phạm) nhưng viết NGẮN GỌN, " +
            "1 câu, không nói chung chung.\n" +
            "4. CHỈ trả JSON thuần, không thêm chữ nào khác, không dùng markdown code fence. Đúng format:\n" +
            "{\"allowed\": true hoặc false, \"reasonVi\": \"...\", \"reasonEn\": \"...\"}";

        var userMessage =
            $"Mô tả yêu cầu (brief): {brief}\n" +
            $"Gợi ý bố cục: {(!string.IsNullOrWhiteSpace(layoutHint) ? layoutHint : "(không có)")}\n" +
            $"Họa sĩ SẴN SÀNG vẽ: {(!string.IsNullOrWhiteSpace(willDraw) ? willDraw : "(không ghi rõ)")}\n" +
            $"Họa sĩ TỪ CHỐI vẽ: {(!string.IsNullOrWhiteSpace(wontDraw) ? wontDraw : "(không ghi rõ)")}";

        try
        {
            var result = await _aiChatClient.CompleteAsync(
                systemInstruction: systemPrompt,
                history: Array.Empty<AiChatTurn>(),
                userMessage: userMessage,
                cancellationToken: cancellationToken,
                maxOutputTokens: PromptMaxOutputTokens,
                jsonResponse: true);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Content))
            {
                return ReferenceSketchPolicyResult.Fail(result.Error ?? "Không thể kiểm tra quy định lúc này.");
            }

            var json = StripJsonFence(result.Content);
            var parsed = JsonSerializer.Deserialize<PolicyCheckJson>(json, JsonOptions);

            if (parsed is null)
            {
                return ReferenceSketchPolicyResult.Fail("Không đọc được kết quả kiểm tra quy định.");
            }

            return ReferenceSketchPolicyResult.Ok(parsed.Allowed, parsed.ReasonVi, parsed.ReasonEn);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Lỗi parse JSON khi kiểm tra quy định AI Reference Sketch");
            return ReferenceSketchPolicyResult.Fail("Không đọc được kết quả kiểm tra quy định.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi hệ thống khi kiểm tra quy định AI Reference Sketch");
            return ReferenceSketchPolicyResult.Fail("Lỗi hệ thống khi kiểm tra quy định.");
        }
    }

    public async Task<ReferenceSketchVariantsResult> BuildVariantsAsync(
        string brief,
        string? layoutHint,
        string? wontDraw,
        CancellationToken cancellationToken = default)
    {
        var first = await GenerateVariantsOnceAsync(brief, layoutHint, wontDraw, cancellationToken);
        if (first.Success && AreVariantsDistinct(first.Variants!))
        {
            return ReferenceSketchVariantsResult.Ok(AppendStyle(first.Variants!, first.HasCharacter, first.WontDrawEn));
        }

        // Gọi lại ĐÚNG 1 lần — dùng chung cho cả 2 lý do: JSON lỗi/bị cắt giữa chuỗi (xem
        // PromptMaxOutputTokens) và 2 phương án chưa đủ khác biệt ở lần gọi đầu.
        var retry = await GenerateVariantsOnceAsync(brief, layoutHint, wontDraw, cancellationToken);
        if (!retry.Success)
        {
            return ReferenceSketchVariantsResult.Fail(
                retry.Error ?? first.Error ?? "Không thể tạo phương án bố cục lúc này.");
        }

        if (!AreVariantsDistinct(retry.Variants!))
        {
            return ReferenceSketchVariantsResult.Fail(
                "AI không tạo được 2 phương án bố cục đủ khác biệt. Vui lòng thử lại.");
        }

        return ReferenceSketchVariantsResult.Ok(AppendStyle(retry.Variants!, retry.HasCharacter, retry.WontDrawEn));
    }

    private async Task<VariantsAttempt> GenerateVariantsOnceAsync(
        string brief,
        string? layoutHint,
        string? wontDraw,
        CancellationToken cancellationToken)
    {
        const string systemPrompt =
            "Bạn là trợ lý viết prompt cho AI tạo ảnh phác thảo bố cục (không phải tranh hoàn thiện) dùng " +
            "làm tham khảo khi khách đặt vẽ tranh.\n\n" +
            "Nhiệm vụ: đọc mô tả yêu cầu và gợi ý bố cục (tiếng Việt) của khách, viết ra ĐÚNG 2 PHƯƠNG ÁN " +
            "bố cục khác nhau rõ rệt để khách chọn.\n\n" +
            "QUY TẮC:\n" +
            "1. Mỗi phương án gồm 3 trường ngắn, LUÔN VIẾT BẰNG TIẾNG ANH: pose (tư thế/dáng), " +
            "camera (góc máy/khung hình), composition (bố cục/sắp xếp khung hình). 2 phương án PHẢI " +
            "khác nhau ở CẢ 3 trường này — không lặp lại giá trị nào. Cảnh KHÔNG có nhân vật (phong cảnh, " +
            "kiến trúc, vật thể): pose mô tả trạng thái/hướng của chủ thể chính (ví dụ 'lighthouse standing upright " +
            "on the cliff edge' và 'lighthouse half hidden behind rocks, leaning into the frame') — " +
            "KHÔNG BAO GIỜ để 'none', 'N/A' hay 'no character', và 2 pose vẫn phải khác nhau.\n" +
            "2. promptEn: LUÔN VIẾT BẰNG TIẾNG ANH, mô tả chủ thể theo mô tả của khách (dịch đúng nghĩa, không " +
            "thêm chủ thể khách không yêu cầu) VÀ PHẢI nêu rõ ĐÚNG pose, camera, composition của CHÍNH " +
            "phương án đó — 2 promptEn phải đọc ra 2 bức tranh khác nhau. Viết ngắn gọn, tối đa khoảng 55 từ. " +
            "KHÔNG nêu phong cách vẽ (anime, line art, realistic, chibi…) và KHÔNG tả chi tiết ngoại hình " +
            "(màu/kiểu tóc, quần áo, phụ kiện, vật cầm trên tay, nét mặt, màu sắc) — nhân vật chỉ là 'a figure' " +
            "kèm tư thế/hành động (vd 'a figure standing with one arm raised'), cộng bối cảnh, vì nhân vật chỉ " +
            "được vẽ dạng construction sketch để thể hiện dáng và bố cục. Vật cầm/đeo chỉ được diễn tả qua TƯ " +
            "THẾ, không gọi tên vật (vd khách viết 'cô gái cầm ô' → 'a figure with one arm raised above the " +
            "head', KHÔNG viết 'umbrella'; 'cầm kiếm' → 'a figure with both arms extended forward'). KHÔNG nêu thời gian, thời tiết, ánh sáng " +
            "(night, rain, sunset, glowing…) — chỉ nêu vật thể của bối cảnh.\n" +
            "2b. KỸ THUẬT BỐ CỤC BẮT BUỘC: mỗi phương án được giao 1 kỹ thuật bố cục ở cuối tin nhắn. Trường " +
            "composition PHẢI áp dụng đúng kỹ thuật đó một cách CỤ THỂ vào cảnh này (nêu vật thể nào làm " +
            "khung, đường dẫn, tiền cảnh…, chủ thể nằm ở đâu trong khung), không viết chung chung kiểu " +
            "'centered' hay 'rule of thirds', và KHÔNG chép nguyên văn phần định nghĩa của kỹ thuật. Được dùng vật thể có sẵn trong bối cảnh khách mô tả để tạo kỹ " +
            "thuật (cửa sổ, lan can, mặt nước, cột…), nhưng không thêm nhân vật mới. Nếu gợi ý bố cục của " +
            "khách đã chỉ định góc nhìn hoặc vị trí, vẫn giữ ý đó và kết hợp với kỹ thuật được giao.\n" +
            "3. labelVi: mô tả ngắn dạng 'dáng · góc máy · bố cục' BẰNG TIẾNG VIỆT hiển thị cho khách chọn, " +
            "ví dụ 'Đứng nghiêng · Góc thấp · Cận cảnh nửa người'. labelEn: cùng nội dung nhưng BẰNG TIẾNG " +
            "ANH. Cả 2 viết ngắn, tối đa khoảng 8 từ.\n" +
            "4. TUYỆT ĐỐI KHÔNG đưa vào phương án bất kỳ nội dung nằm trong danh sách TỪ CHỐI vẽ của họa " +
            "sĩ dưới đây.\n" +
            "5. hasCharacter: true nếu cảnh có nhân vật/con vật có mặt cần vẽ, false nếu cảnh chỉ có bối " +
            "cảnh/vật thể, không có khuôn mặt nào.\n" +
            "6. wontDrawEn: dịch NGẮN GỌN danh sách TỪ CHỐI vẽ của họa sĩ (nếu có) sang tiếng Anh, viết " +
            "liệt kê ngắn phù hợp để ghép thẳng vào câu \"Strictly avoid depicting any of the following: " +
            "...\" — không thêm dấu câu kết thúc. Nếu họa sĩ không ghi rõ TỪ CHỐI gì thì để chuỗi rỗng \"\".\n" +
            "7. CHỈ trả JSON thuần là 1 OBJECT, không thêm chữ nào khác, không dùng markdown code fence. " +
            "Đúng format:\n" +
            "{\"hasCharacter\": true hoặc false, \"wontDrawEn\": \"...\", \"variants\": " +
            "[{\"pose\":\"...\",\"camera\":\"...\",\"composition\":\"...\",\"promptEn\":\"...\"," +
            "\"labelVi\":\"...\",\"labelEn\":\"...\"}, {...}]}";

        // A fresh pair on every call (including the retry), so repeated generations explore new layouts.
        var techniques = CompositionTechniques.PickRandom(2);

        var userMessage =
            $"Mô tả yêu cầu (brief): {brief}\n" +
            $"Gợi ý bố cục: {(!string.IsNullOrWhiteSpace(layoutHint) ? layoutHint : "(không có, tự đề xuất hợp lý)")}\n" +
            $"Họa sĩ TỪ CHỐI vẽ (không được phạm): {(!string.IsNullOrWhiteSpace(wontDraw) ? wontDraw : "(không ghi rõ)")}\n" +
            $"Kỹ thuật bố cục BẮT BUỘC cho phương án 1: {techniques[0]}\n" +
            $"Kỹ thuật bố cục BẮT BUỘC cho phương án 2: {techniques[1]}";

        try
        {
            var result = await _aiChatClient.CompleteAsync(
                systemInstruction: systemPrompt,
                history: Array.Empty<AiChatTurn>(),
                userMessage: userMessage,
                cancellationToken: cancellationToken,
                maxOutputTokens: PromptMaxOutputTokens,
                jsonResponse: true);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Content))
            {
                return VariantsAttempt.Fail(result.Error ?? "Không thể tạo phương án bố cục lúc này.");
            }

            var json = StripJsonFence(result.Content);
            var parsed = JsonSerializer.Deserialize<VariantsResponseJson>(json, JsonOptions);

            if (parsed?.Variants is null || parsed.Variants.Length != 2)
            {
                return VariantsAttempt.Fail("AI không trả đúng 2 phương án bố cục.");
            }

            var variants = parsed.Variants
                .Select(v => new ReferenceSketchVariant(
                    v.Pose?.Trim() ?? "",
                    v.Camera?.Trim() ?? "",
                    v.Composition?.Trim() ?? "",
                    v.PromptEn?.Trim() ?? "",
                    v.LabelVi?.Trim() ?? "",
                    v.LabelEn?.Trim() ?? ""))
                .ToArray();

            if (variants.Any(v => v.Pose.Length == 0 || v.Camera.Length == 0 || v.Composition.Length == 0 ||
                                   v.PromptEn.Length == 0 || v.LabelVi.Length == 0 || v.LabelEn.Length == 0))
            {
                return VariantsAttempt.Fail("AI trả thiếu dữ liệu phương án bố cục.");
            }

            return VariantsAttempt.Ok(variants, parsed.HasCharacter, parsed.WontDrawEn);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Lỗi parse JSON khi tạo phương án bố cục AI Reference Sketch");
            return VariantsAttempt.Fail("Không đọc được kết quả tạo phương án bố cục.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi hệ thống khi tạo phương án bố cục AI Reference Sketch");
            return VariantsAttempt.Fail("Lỗi hệ thống khi tạo phương án bố cục.");
        }
    }

    /// <summary>
    /// Nút "🎲 Gợi ý bố cục độc đáo" — viết 3 gợi ý bố cục ngắn tiếng Việt, mỗi gợi ý dựa trên
    /// đúng 1 kỹ thuật trong <paramref name="techniques"/> (xem <see cref="LayoutHintTechniques"/>).
    /// Không trừ lượt sinh ảnh — chỉ gắn rate limiter riêng ở endpoint.
    /// </summary>
    public async Task<ReferenceSketchLayoutHintsResult> SuggestLayoutHintsAsync(
        string brief,
        string? currentHint,
        string? wontDraw,
        string[] techniques,
        CancellationToken cancellationToken = default)
    {
        var first = await SuggestLayoutHintsOnceAsync(brief, currentHint, wontDraw, techniques, cancellationToken);
        if (first.Success)
        {
            return first;
        }

        // Gọi lại ĐÚNG 1 lần — cùng lý do với CheckPolicyAsync/BuildVariantsAsync: JSON đôi khi bị
        // cắt giữa chuỗi (xem PromptMaxOutputTokens).
        return await SuggestLayoutHintsOnceAsync(brief, currentHint, wontDraw, techniques, cancellationToken);
    }

    private async Task<ReferenceSketchLayoutHintsResult> SuggestLayoutHintsOnceAsync(
        string brief,
        string? currentHint,
        string? wontDraw,
        string[] techniques,
        CancellationToken cancellationToken)
    {
        const string systemPrompt =
            "Bạn là trợ lý viết gợi ý bố cục cho khách đặt vẽ tranh trên sàn Dillustration.\n\n" +
            "Nhiệm vụ: đọc mô tả yêu cầu (brief) của khách, viết ra ĐÚNG 3 GỢI Ý BỐ CỤC NGẮN bằng " +
            "TIẾNG VIỆT, mỗi gợi ý áp dụng đúng 1 kỹ thuật bố cục được giao (theo đúng thứ tự đã " +
            "cho) vào nội dung brief.\n\n" +
            "QUY TẮC:\n" +
            "1. Mỗi gợi ý (textVi) tối đa khoảng 300 ký tự, viết ngắn gọn, cụ thể, dễ hiểu, không " +
            "dùng thuật ngữ tiếng Anh của kỹ thuật trong câu (ví dụ không viết 'Dutch angle' mà tả " +
            "bằng tiếng Việt 'nghiêng khung hình').\n" +
            "2. technique: ghi lại ĐÚNG tên kỹ thuật tiếng Anh đã được giao cho gợi ý đó (không đổi).\n" +
            "3. TUYỆT ĐỐI KHÔNG đưa vào gợi ý bất kỳ nội dung nằm trong danh sách TỪ CHỐI vẽ của họa " +
            "sĩ dưới đây, và KHÔNG thêm chủ thể/nhân vật mà khách không yêu cầu.\n" +
            "4. Nếu brief là cảnh phong cảnh/vật thể, không có nhân vật: KHÔNG gợi ý dáng người, chỉ " +
            "gợi ý góc máy/bố cục khung hình cho cảnh đó.\n" +
            "5. 3 gợi ý phải khác nhau rõ rệt (vì dựa trên 3 kỹ thuật khác nhau), không lặp ý.\n" +
            "6. CHỈ trả JSON thuần là 1 OBJECT, không thêm chữ nào khác, không dùng markdown code " +
            "fence. Đúng format:\n" +
            "{\"hints\": [{\"technique\":\"...\",\"textVi\":\"...\"}, {...}, {...}]}";

        var userMessage =
            $"Mô tả yêu cầu (brief): {brief}\n" +
            $"Gợi ý bố cục hiện tại của khách: {(!string.IsNullOrWhiteSpace(currentHint) ? currentHint : "(chưa có)")}\n" +
            $"Họa sĩ TỪ CHỐI vẽ (không được phạm): {(!string.IsNullOrWhiteSpace(wontDraw) ? wontDraw : "(không ghi rõ)")}\n" +
            $"3 kỹ thuật bố cục cần áp dụng, theo đúng thứ tự: {string.Join(", ", techniques)}";

        try
        {
            var result = await _aiChatClient.CompleteAsync(
                systemInstruction: systemPrompt,
                history: Array.Empty<AiChatTurn>(),
                userMessage: userMessage,
                cancellationToken: cancellationToken,
                maxOutputTokens: PromptMaxOutputTokens,
                jsonResponse: true);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Content))
            {
                return ReferenceSketchLayoutHintsResult.Fail(result.Error ?? "Không thể tạo gợi ý bố cục lúc này.");
            }

            var json = StripJsonFence(result.Content);
            var parsed = JsonSerializer.Deserialize<LayoutHintsResponseJson>(json, JsonOptions);

            if (parsed?.Hints is null || parsed.Hints.Length != 3)
            {
                return ReferenceSketchLayoutHintsResult.Fail("AI không trả đúng 3 gợi ý bố cục.");
            }

            var hints = parsed.Hints
                .Select(h => new ReferenceSketchLayoutHint(
                    (h.Technique ?? "").Trim(),
                    TruncateHint((h.TextVi ?? "").Trim())))
                .ToArray();

            if (hints.Any(h => h.Technique.Length == 0 || h.TextVi.Length == 0))
            {
                return ReferenceSketchLayoutHintsResult.Fail("AI trả thiếu nội dung gợi ý bố cục.");
            }

            return ReferenceSketchLayoutHintsResult.Ok(hints);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Lỗi parse JSON khi tạo gợi ý bố cục AI Reference Sketch");
            return ReferenceSketchLayoutHintsResult.Fail("Không đọc được kết quả tạo gợi ý bố cục.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi hệ thống khi tạo gợi ý bố cục AI Reference Sketch");
            return ReferenceSketchLayoutHintsResult.Fail("Lỗi hệ thống khi tạo gợi ý bố cục.");
        }
    }

    private static string TruncateHint(string text) =>
        text.Length <= ReferenceSketchLimits.MaxLayoutHintLength ? text : text[..ReferenceSketchLimits.MaxLayoutHintLength];

    private static bool AreVariantsDistinct(ReferenceSketchVariant[] variants)
    {
        var a = variants[0];
        var b = variants[1];

        return !string.Equals(a.Pose, b.Pose, StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(a.Camera, b.Camera, StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(a.Composition, b.Composition, StringComparison.OrdinalIgnoreCase);
    }

    private static ReferenceSketchVariant[] AppendStyle(ReferenceSketchVariant[] variants, bool hasCharacter, string? wontDrawEn)
    {
        var banClause = BuildBanClause(wontDrawEn);

        return variants
            // Style goes first: image models weight the start of the prompt most, so the storyboard
            // look wins over any style words that slip into the scene text.
            .Select(v => v with { PromptEn = $"{SketchStyleBase}{(hasCharacter ? MannequinFigureClause : "")} Scene: {v.PromptEn.Trim().TrimEnd('.')}.{SketchStyleClosing}{banClause}" })
            .ToArray();
    }

    /// <summary>
    /// Ghép câu cấm từ bản dịch tiếng Anh (Gemini trả trong <c>wontDrawEn</c>) — bỏ dấu câu kết
    /// thúc thừa ở cuối chuỗi nguồn trước khi tự thêm dấu "." để không bị lặp dấu (".." ).
    /// </summary>
    private static string BuildBanClause(string? wontDrawEn)
    {
        var trimmed = (wontDrawEn ?? "").Trim().TrimEnd('.', ' ');
        return trimmed.Length == 0
            ? ""
            : $" Strictly avoid depicting any of the following: {trimmed}.";
    }

    private static string StripJsonFence(string raw)
    {
        var trimmed = raw.Trim();
        trimmed = Regex.Replace(trimmed, @"^```(?:json)?\s*", "", RegexOptions.IgnoreCase);
        trimmed = Regex.Replace(trimmed, @"\s*```$", "");
        return trimmed.Trim();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed record PolicyCheckJson(bool Allowed, string? ReasonVi, string? ReasonEn);

    private sealed record VariantJson(
        string? Pose,
        string? Camera,
        string? Composition,
        string? PromptEn,
        string? LabelVi,
        string? LabelEn);

    private sealed record VariantsResponseJson(bool HasCharacter, string? WontDrawEn, VariantJson[]? Variants);

    private sealed record LayoutHintJson(string? Technique, string? TextVi);

    private sealed record LayoutHintsResponseJson(LayoutHintJson[]? Hints);

    /// <summary>Kết quả 1 lượt gọi Gemini viết phương án — mang thêm hasCharacter/wontDrawEn để
    /// <see cref="AppendStyle"/> dùng, khác với <see cref="ReferenceSketchVariantsResult"/> (hợp đồng
    /// trả ra ngoài service, không cần 2 trường phụ này).</summary>
    private sealed record VariantsAttempt(bool Success, ReferenceSketchVariant[]? Variants, bool HasCharacter, string? WontDrawEn, string? Error)
    {
        public static VariantsAttempt Ok(ReferenceSketchVariant[] variants, bool hasCharacter, string? wontDrawEn) =>
            new(true, variants, hasCharacter, wontDrawEn, null);

        public static VariantsAttempt Fail(string error) =>
            new(false, null, false, null, error);
    }
}
