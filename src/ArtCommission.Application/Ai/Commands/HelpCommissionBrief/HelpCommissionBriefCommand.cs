using System.Text.RegularExpressions;
using ArtCommission.Application.Ai.Common;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Application.Ai.Commands.HelpCommissionBrief;

public sealed record AiBriefResultDto(string Content);

/// <summary>
/// AI-UC21 — Hỗ trợ khách hàng chuẩn hóa ý tưởng thô thành Commission Brief rõ ràng, tự nhiên, không văn mẫu AI.
/// </summary>
public sealed record HelpCommissionBriefCommand(
    string? Description,
    string? Title,
    string? Style,
    decimal? Budget,
    bool SurpriseMe = false
) : IRequest<(bool Success, AiBriefResultDto? Data, string[] Errors)>;

public class HelpCommissionBriefCommandValidator : AbstractValidator<HelpCommissionBriefCommand>
{
    public HelpCommissionBriefCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Description) || !string.IsNullOrWhiteSpace(x.Title))
            .WithMessage("Vui lòng nhập ít nhất tên dự án hoặc vài dòng mô tả ý tưởng thô.");
    }
}

public class HelpCommissionBriefCommandHandler
    : IRequestHandler<HelpCommissionBriefCommand, (bool Success, AiBriefResultDto? Data, string[] Errors)>
{
    private readonly IAiChatClient _aiChatClient;
    private readonly ILogger<HelpCommissionBriefCommandHandler> _logger;

    public HelpCommissionBriefCommandHandler(
        IAiChatClient aiChatClient,
        ILogger<HelpCommissionBriefCommandHandler> logger)
    {
        _aiChatClient = aiChatClient;
        _logger = logger;
    }

    public async Task<(bool Success, AiBriefResultDto? Data, string[] Errors)> Handle(
        HelpCommissionBriefCommand request,
        CancellationToken cancellationToken)
    {
        const string systemPrompt =
            "Bạn là công cụ chuẩn hóa Commission Brief cho khách đặt vẽ tranh sàn Dillustration.\n\n" +
            "QUY TẮC CỐT LÕI (HUMANIZER & CHỐNG ẢO GIÁC & RÀNH MẠCH HÓA):\n" +
            "1. KHÔNG PHẢI CHATBOT: Cấm tuyệt đối chào hỏi, xưng hô, hỏi han, kết bài (cấm 'Chào bạn', 'Hello', 'Với vai trò', 'Nhắn lại cho tôi...').\n" +
            "2. NHẬN DIỆN ĐÚNG GAME / ANIME / TỪ VIẾT TẮT, CẤM BỊA ĐẶT NGHĨA (ANTI-HALLUCINATION):\n" +
            "   - Khách hay dùng từ viết tắt của tựa game, manga hoặc anime phổ biến (ví dụ: 'klbq' = 卡拉彼丘 / CalabiYau / Strinova; 'wuwa' = Wuthering Waves; 'hsr' = Honkai Star Rail; 'zzz' = Zenless Zone Zero; 'ba' = Blue Archive; 'gi' = Genshin Impact; 'pgr' = Punishing Gray Raven; 'tanmoshi' = Tantei wa Mou, Shindeiru / The Detective Is Already Dead; 'oregairu' = My Teen Romantic Comedy SNAFU; 'danmachi' = Is It Wrong to Try to Pick Up Girls in a Dungeon?; 'konosuba' = KonoSuba; 'fgo' = Fate/Grand Order...). Bắt buộc nhận diện đúng tác phẩm gốc.\n" +
            "   - TUYỆT ĐỐI CẤM TỰ BỊA RA NGHĨA VIẾT TẮT TIẾNG VIỆT HOẶC ROMAJI TỰ CHẾ (ví dụ: cấm đoán 'klbq' thành 'Kim Lược Băng Tâm', cấm đoán 'tanmoshi' thành 'Tanomoshi Shinai').\n" +
            "   - Nếu nhận ra nhân vật (như Siesta, Kimizuka -> Tantei wa Mou, Shindeiru): Phải dùng tên tác phẩm chính thức. Nếu gặp từ viết tắt lạ không chắc chắn 100%: Giữ nguyên tên nhân vật và từ viết tắt của khách, không tự chế ra một tên phim/game tưởng tượng.\n" +
            "3. RÀNH MẠCH HÓA NHÂN VẬT ĐÃ CÓ THIẾT KẾ GỐC (CANONICAL CHARACTERS):\n" +
            "   - Khi nhân vật đã có thiết kế chuẩn nổi tiếng (như Jinhsi trong Wuthering Waves, Raiden Shogun, Acheron, Miku, 2B...): TUYỆT ĐỐI KHÔNG tả lại lan man từng chi tiết hiển nhiên của thiết kế gốc (màu mắt, từng sợi tóc, từng nếp áo cách tân mạ vàng...). Điều này làm brief bị loãng và mơ hồ vô ích.\n" +
            "   - CHỈ CẦN GHI NGẮN GỌN RÀNH MẠCH: Tên nhân vật, tên tác phẩm, và xác định rõ trang phục: 'Trang phục mặc định' hoặc tên trang phục biến thể (áo tắm, đồng phục học sinh, dạ hội...). Họa sĩ sẽ tra cứu official model sheet.\n" +
            "   - Chỉ tả chi tiết ngoại hình khi nhân vật là OC (nhân vật tự thiết kế) chưa có ảnh mẫu.\n" +
            "   - Nếu mô tả đầu vào của khách quá dài dòng, lặp từ, viết như văn xuôi: BẮT BUỘC chắt lọc, cô đọng lại thành từng dòng ngắn gọn, dứt khoát, loại bỏ toàn bộ từ ngữ thừa thãi.\n" +
            "4. HIỂU VÀ SỬ DỤNG CHUẨN THUẬT NGỮ GIỚI VẼ (ART TERMINOLOGY):\n" +
            "   - Khổ vẽ: Headshot (chân dung mặt); Bust-up (nửa thân trên từ ngực); Half-body/Waist-up (nửa người từ đầu đến eo); Thigh-up (ngang đùi); Full-body (toàn thân); Chibi (tỷ lệ 2-3 đầu dễ thương).\n" +
            "   - Kỹ thuật & Hoàn thiện: Sketch (phác thảo thô); Lineart (nét sạch); Flat color/Base color (màu mảng phẳng); Cel-shading (đổ bóng mảng cứng anime); Soft shade (đổ bóng chuyển màu mềm); Full render (lên khối, chất liệu, phản quang hoàn thiện cao nhất); Semi-realism (bán tả thực).\n" +
            "   - Màu sắc: Pastel (tông màu phấn nhạt dịu mắt); Monochrome/Grayscale (đơn sắc/đen trắng); Palette (bảng màu quy định).\n" +
            "   - Khác: OC (Original Character - nhân vật tự thiết kế); WIP (Work in progress - ảnh tiến độ); Ref sheet/Moodboard (bảng ảnh mẫu tư liệu).\n" +
            "5. ĐA NGÔN NGỮ (LANGUAGE ADAPTATION):\n" +
            "   - Nhận diện ngôn ngữ từ tiêu đề và mô tả của khách.\n" +
            "   - Nếu khách nhập bằng Tiếng Anh (English): Toàn bộ nhãn mục và nội dung brief PHẢI viết bằng Tiếng Anh chuẩn xác.\n" +
            "   - Nếu khách nhập bằng Tiếng Việt (hoặc ngôn ngữ khác/hỗn hợp): Xuất brief bằng Tiếng Việt chuẩn xác.\n" +
            "6. XỬ LÝ ĐẦU VÀO VÔ NGHĨA / IDK:\n" +
            "   - Nếu khách chỉ gõ tiếng thở dài, từ vô nghĩa, hoặc phân vân chưa biết vẽ gì (ví dụ: 'hmmm idk', 'chưa biết vẽ gì', 'tùy', 'không biết'): TUYỆT ĐỐI KHÔNG TỰ BỊA RA NHÂN VẬT ẢO. Hãy trả về KHUNG GỢI Ý ĐỊNH HƯỚNG để khách tự điền.\n" +
            "7. KHÔNG FORMAT RƯỜM RÀ: Không in đậm (**), không in nghiêng, không gạch chân, không tiêu đề Markdown (#, ##, ###), không kẻ ngang (---).\n" +
            "8. KHÔNG DÙNG NGOẶC ĐƠN CHÚ THÍCH PHÂN LOẠI: Cấm chèn (Overview & Mood), (Do's), (Don'ts), (slice-of-life)... Viết thành câu tự nhiên gãy gọn.\n" +
            "9. KHÔNG DÙNG TỪ MƠ HỒ: Cấm 'trong trẻo', 'hiện đại', 'thơ mộng', 'nghệ thuật'. Bắt buộc nêu tên màu sắc cụ thể (ví dụ: xanh navy, tím lavender, be kem, trắng sữa, vàng gold) hoặc tên studio/tác phẩm tham chiếu màu.\n\n" +
            "CẤU TRÚC ĐẦU RA TIẾNG VIỆT (KHI CÓ CHỦ ĐỀ RÕ RÀNG):\n" +
            "Yêu cầu vẽ: [Tên dự án hoặc nhân vật cụ thể kèm tên game/anime chuẩn]\n" +
            "Nhân vật: [Nếu là nhân vật có sẵn: ghi tên + trang phục mặc định hoặc trang phục biến thể. Nếu là OC: mô tả đặc điểm nhận diện chính]\n" +
            "Tư thế và biểu cảm: [Tư thế cụ thể, khổ vẽ như bust-up/half-body nếu có, cử chỉ tay, hướng nhìn, biểu cảm]\n" +
            "Phong cách và ánh sáng: [Kỹ thuật nét và màu như cel-shading/semi-realism, bảng màu chủ đạo cụ thể, hướng sáng]\n" +
            "Bối cảnh: [Không gian và đồ vật cụ thể xung quanh]\n" +
            "Lưu ý: [Yêu cầu kỹ thuật quan trọng nhất]\n" +
            "Từ khóa tra cứu mẫu: [Từ khóa chính xác để tìm ảnh tư liệu gốc]\n\n" +
            "CẤU TRÚC ĐẦU RA TIẾNG ANH (ENGLISH OUTPUT FORMAT):\n" +
            "Art Request: [Specific project or canonical character and title]\n" +
            "Character: [Canon character + Default outfit / Variant outfit, or OC key features]\n" +
            "Pose and Expression: [Framing like bust-up/half-body/full-body, gestures, gaze, expression]\n" +
            "Style and Lighting: [Art style like cel-shading/full render/semi-realism, exact color palette, lighting direction]\n" +
            "Background: [Specific environment and key props]\n" +
            "Notes: [Key technical notes or focal points]\n" +
            "Reference Keywords: [Exact search keywords for reference sheets]\n\n" +
            "CẤU TRÚC ĐẦU RA KHI KHÁCH CHƯA BIẾT VẼ GÌ / ĐẦU VÀO VÔ NGHĨA (IDK - VIETNAMESE):\n" +
            "Yêu cầu vẽ: Chưa xác định chủ đề\n" +
            "Nhân vật: Cần chọn đối tượng vẽ (người thật, nhân vật anime/game, hay linh vật)\n" +
            "Tư thế và biểu cảm: Cần chọn góc chụp (chân dung, nửa người hay toàn thân)\n" +
            "Phong cách và ánh sáng: Cần chọn nét vẽ (anime, chibi, bán tả thực) và tông màu ưa thích\n" +
            "Bối cảnh: Chọn phông nền trơn hoặc phong cảnh cụ thể\n" +
            "Lưu ý: Nêu rõ mục đích sử dụng (làm avatar, in ấn hay quà tặng)\n" +
            "Từ khóa tra cứu mẫu: Nhập từ khóa sau khi chọn được nhân vật mong muốn\n\n" +
            "CẤU TRÚC ĐẦU RA KHI KHÁCH CHƯA BIẾT VẼ GÌ / IDK (ENGLISH):\n" +
            "Art Request: Undefined Theme\n" +
            "Character: Choose subject (real person, anime/game character, or mascot)\n" +
            "Pose and Expression: Choose framing (portrait, half-body, or full-body)\n" +
            "Style and Lighting: Choose art style (anime, chibi, semi-realism) and preferred color palette\n" +
            "Background: Plain solid background or specific environment\n" +
            "Notes: State primary purpose (avatar, print, personal gift)\n" +
            "Reference Keywords: Fill after deciding the subject";

        var userMessage = $"Dữ liệu từ form khách hàng:\n" +
            $"- Tên dự án: {(!string.IsNullOrWhiteSpace(request.Title) ? request.Title : "(Chưa đặt)")}\n" +
            $"- Gói vẽ / Phong cách: {(!string.IsNullOrWhiteSpace(request.Style) ? request.Style : "(Tự do)")}\n" +
            (request.Budget.HasValue && request.Budget > 0 ? $"- Ngân sách: {request.Budget:N0} VND\n" : "") +
            $"- Mô tả ý tưởng: {(!string.IsNullOrWhiteSpace(request.Description) ? request.Description : "Chưa có ý tưởng.")}" +
            (request.SurpriseMe
                ? "\n- YÊU CẦU ĐẶC BIỆT (TẠO Ý TƯỞNG NGẪU NHIÊN / SURPRISE ME): Khách muốn bạn tự do sáng tạo một ý tưởng bất ngờ, độc đáo, giàu tính điện ảnh và nghệ thuật! Vẫn giữ đúng nhân vật/chủ thể mà khách đã cung cấp, nhưng hãy biến tấu tư thế, biểu cảm, góc quay, bối cảnh và ánh sáng thật mới lạ, ấn tượng!"
                : "");

        try
        {
            var result = await _aiChatClient.CompleteAsync(
                systemInstruction: systemPrompt,
                history: Array.Empty<AiChatTurn>(),
                userMessage: userMessage,
                cancellationToken: cancellationToken);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Content))
            {
                return (false, null, new[] { result.Error ?? "Không thể tạo gợi ý brief lúc này. Vui lòng thử lại sau." });
            }

            var clean = SanitizeHumanizedBrief(result.Content);

            return (true, new AiBriefResultDto(clean), Array.Empty<string>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi AI Brief Helper");
            return (false, null, new[] { "Lỗi hệ thống khi hỗ trợ tạo brief AI." });
        }
    }

    /// <summary>
    /// Xóa sạch mọi dấu vết văn mẫu AI: in đậm **, gạch đầu dòng Markdown, ngoặc chú thích, câu chào và câu hỏi kết bài.
    /// </summary>
    private static string SanitizeHumanizedBrief(string raw)
    {
        var lines = raw.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
            .Select(l => l.Trim())
            .ToList();

        var filtered = new List<string>();
        var started = false;

        foreach (var line in lines)
        {
            var lower = line.ToLowerInvariant();

            // Bỏ các câu chào mào đầu (Việt & Anh)
            if (!started)
            {
                if (lower.StartsWith("chào") ||
                    lower.StartsWith("hello") ||
                    lower.StartsWith("hi ") ||
                    lower.StartsWith("here is") ||
                    lower.StartsWith("here's") ||
                    lower.StartsWith("sure") ||
                    lower.StartsWith("certainly") ||
                    lower.StartsWith("as an art") ||
                    lower.StartsWith("với tư cách") ||
                    lower.StartsWith("với vai trò") ||
                    lower.StartsWith("với ý tưởng") ||
                    lower.StartsWith("để tôi") ||
                    lower.StartsWith("để giúp") ||
                    lower.StartsWith("dưới đây") ||
                    lower.StartsWith("bản brief này") ||
                    lower.StartsWith("###") ||
                    lower.StartsWith("####") ||
                    line.StartsWith("---") ||
                    string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                started = true;
            }

            // Bỏ các câu kết dông dài hoặc chatbot hỏi lại (Việt & Anh)
            if (lower.StartsWith("lời khuyên") ||
                lower.StartsWith("💡") ||
                lower.StartsWith("bước tiếp theo") ||
                lower.StartsWith("bạn thấy") ||
                lower.StartsWith("bạn chỉ cần") ||
                lower.StartsWith("nhắn lại cho") ||
                lower.StartsWith("nếu bạn muốn") ||
                lower.StartsWith("nếu bạn đồng ý") ||
                lower.StartsWith("sau khi chốt") ||
                lower.StartsWith("let me know") ||
                lower.StartsWith("hope this helps") ||
                lower.StartsWith("feel free to") ||
                lower.StartsWith("what do you think") ||
                lower.StartsWith("next steps") ||
                line.StartsWith("---"))
            {
                break;
            }

            // Xóa in đậm **, in nghiêng *, gạch chân __
            var cleanedLine = line.Replace("**", "").Replace("__", "");

            // Xóa markdown headings
            cleanedLine = Regex.Replace(cleanedLine, @"^#{1,6}\s*", "");

            // Xóa các chú thích trong ngoặc đơn dạng meta-labels (Overview & Mood), (Do's), (như...)
            cleanedLine = Regex.Replace(cleanedLine, @"\s*\((?:overview|mood|subject|pose|features|art style|lighting|background|atmosphere|do'?s|don'?ts|slice-of-life|như[^)]*)\)", "", RegexOptions.IgnoreCase);

            if (!string.IsNullOrWhiteSpace(cleanedLine))
            {
                filtered.Add(cleanedLine.Trim());
            }
        }

        return filtered.Count > 0 ? string.Join("\n\n", filtered) : raw.Trim();
    }
}
