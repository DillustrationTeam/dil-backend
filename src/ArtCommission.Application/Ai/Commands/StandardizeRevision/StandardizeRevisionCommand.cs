using System.Text.RegularExpressions;
using ArtCommission.Application.Ai.Common;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Application.Ai.Commands.StandardizeRevision;

public sealed record StandardizeRevisionResultDto(string Content);

/// <summary>
/// AI-UC26 — Chuẩn hóa phản hồi yêu cầu chỉnh sửa (Revision) của khách hàng thành checklist chi tiết, cụ thể cho họa sĩ.
/// </summary>
public sealed record StandardizeRevisionCommand(
    string Feedback,
    string? MilestoneTitle
) : IRequest<(bool Success, StandardizeRevisionResultDto? Data, string[] Errors)>;

public class StandardizeRevisionCommandValidator : AbstractValidator<StandardizeRevisionCommand>
{
    public StandardizeRevisionCommandValidator()
    {
        RuleFor(x => x.Feedback)
            .NotEmpty().WithMessage("Vui lòng nhập nội dung phản hồi chỉnh sửa cần chuẩn hóa.")
            .MaximumLength(4000).WithMessage("Nội dung phản hồi không được vượt quá 4000 ký tự.");
    }
}

public class StandardizeRevisionCommandHandler
    : IRequestHandler<StandardizeRevisionCommand, (bool Success, StandardizeRevisionResultDto? Data, string[] Errors)>
{
    private readonly IAiChatClient _aiChatClient;
    private readonly ILogger<StandardizeRevisionCommandHandler> _logger;

    public StandardizeRevisionCommandHandler(
        IAiChatClient aiChatClient,
        ILogger<StandardizeRevisionCommandHandler> logger)
    {
        _aiChatClient = aiChatClient;
        _logger = logger;
    }

    public async Task<(bool Success, StandardizeRevisionResultDto? Data, string[] Errors)> Handle(
        StandardizeRevisionCommand request,
        CancellationToken cancellationToken)
    {
        const string systemPrompt =
            "Bạn là công cụ chuẩn hóa yêu cầu chỉnh sửa tranh (Revision Checklist) từ khách hàng gửi cho họa sĩ.\n\n" +
            "QUY TẮC CỐT LÕI (CHỐNG HUỀ VỐN - CHỐNG MƠ HỒ - ĐÚNG TRỌNG TÂM):\n" +
            "1. KHÔNG HUỀ VỐN, KHÔNG BỊA THÊM Ý (ANTI-BLOAT):\n" +
            "   - Khách yêu cầu bao nhiêu điểm thì chuẩn hóa đúng bấy nhiêu điểm (tỷ lệ 1:1). Tuyệt đối không tự suy diễn thêm gạch đầu dòng.\n" +
            "   - CẤM TỰ DẠY ĐỜI HỌA SĨ những điều hiển nhiên, huề vốn (như 'đồng bộ phong cách với chi tiết cũ', 'không che khuất điểm nhấn', 'đảm bảo hài hòa thẩm mỹ'). Họa sĩ chuyên nghiệp tự biết điều đó!\n" +
            "   - Ví dụ: Khách bảo 'thêm lá' hoặc 'more leaf' -> CHỈ XUẤT ĐÚNG 1 DÒNG: 'Bổ sung thêm chi tiết lá vào tranh.' (hoặc 'Add more leaves to the illustration.'). CẤM đẻ thêm dòng 2, dòng 3!\n\n" +
            "2. XỬ LÝ CHÊ CẢM TÍNH MƠ HỒ (NHƯ 'UGLY', 'XẤU', 'GHÊ', 'GỚM', 'KỲ QUÁ', 'CHƯA ĐẸP'):\n" +
            "   - Vì bạn KHÔNG nhìn thấy tranh, TUYỆT ĐỐI KHÔNG tự đoán mò các lời khuyên chung chung vô nghĩa như 'điều chỉnh tỷ lệ', 'xem lại bảng màu' (họa sĩ đọc xong vẫn không biết phải sửa cái gì).\n" +
            "   - Hãy chuyển hóa thành yêu cầu làm rõ vị trí cụ thể (Clarification Checklist):\n" +
            "     + Tiếng Việt:\n" +
            "       1. Cần xác định vị trí: Vui lòng chỉ rõ bộ phận hoặc chi tiết cụ thể chưa ưng ý (ví dụ: gương mặt, tỷ lệ cơ thể, nếp tóc, hay tông màu).\n" +
            "       2. Đề xuất: Khoanh vùng trực tiếp lên tranh (redline) hoặc gửi ảnh mẫu tham chiếu để họa sĩ chỉnh sửa chính xác.\n" +
            "     + Tiếng Anh:\n" +
            "       1. Clarification needed: Please specify the exact area that feels off (e.g., face proportions, anatomy, hair shading, or colors).\n" +
            "       2. Recommendation: Provide a redline markup or reference image to help the artist adjust accurately.\n\n" +
            "3. ĐA NGÔN NGỮ (LANGUAGE ADAPTATION):\n" +
            "   - Khách nhập tiếng Anh -> Xuất checklist 100% bằng tiếng Anh.\n" +
            "   - Khách nhập tiếng Việt -> Xuất checklist bằng tiếng Việt.\n\n" +
            "4. ĐỊNH DẠNG ĐẦU RA:\n" +
            "   - Danh sách đánh số 1., 2., 3. Mỗi ý đúng 1 câu ngắn gọn, bắt đầu bằng động từ hành động kỹ thuật (Đổi, Thêm, Giảm, Chỉnh, Bỏ, Dời...).\n" +
            "   - CẤM Markdown in đậm (**), in nghiêng (*), tiêu đề (#, ##), kẻ ngang (---).\n" +
            "   - CẤM tuyệt đối mọi câu chào hỏi, xưng hô hoặc câu kết xã giao.";

        var userMessage = $"Mốc thực hiện: {(!string.IsNullOrWhiteSpace(request.MilestoneTitle) ? request.MilestoneTitle : "Mốc hiện tại")}\n" +
            $"Góp ý gốc của khách:\n\"{request.Feedback}\"";

        try
        {
            var result = await _aiChatClient.CompleteAsync(
                systemInstruction: systemPrompt,
                history: Array.Empty<AiChatTurn>(),
                userMessage: userMessage,
                cancellationToken: cancellationToken);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Content))
            {
                return (false, null, new[] { result.Error ?? "Không thể chuẩn hóa phản hồi lúc này. Vui lòng thử lại sau." });
            }

            var clean = SanitizeRevisionFeedback(result.Content);

            return (true, new StandardizeRevisionResultDto(clean), Array.Empty<string>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi AI Standardize Revision");
            return (false, null, new[] { "Lỗi hệ thống khi chuẩn hóa phản hồi chỉnh sửa." });
        }
    }

    private static string SanitizeRevisionFeedback(string raw)
    {
        var lines = raw.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
            .Select(l => l.Trim())
            .ToList();

        var filtered = new List<string>();
        var started = false;

        foreach (var line in lines)
        {
            var lower = line.ToLowerInvariant();

            if (!started)
            {
                if (lower.StartsWith("chào") ||
                    lower.StartsWith("hello") ||
                    lower.StartsWith("hi ") ||
                    lower.StartsWith("dưới đây") ||
                    lower.StartsWith("here is") ||
                    lower.StartsWith("here's") ||
                    lower.StartsWith("###") ||
                    line.StartsWith("---") ||
                    string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                started = true;
            }

            if (lower.StartsWith("hy vọng") ||
                lower.StartsWith("hope this") ||
                lower.StartsWith("bạn thấy") ||
                lower.StartsWith("let me know") ||
                line.StartsWith("---"))
            {
                break;
            }

            var cleanedLine = line.Replace("**", "").Replace("__", "");
            cleanedLine = Regex.Replace(cleanedLine, @"^#{1,6}\s*", "");

            if (!string.IsNullOrWhiteSpace(cleanedLine))
            {
                filtered.Add(cleanedLine.Trim());
            }
        }

        return filtered.Count > 0 ? string.Join("\n", filtered) : raw.Trim();
    }
}
