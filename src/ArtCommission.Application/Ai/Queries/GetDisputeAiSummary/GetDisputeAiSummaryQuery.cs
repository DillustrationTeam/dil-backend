using System.Text;
using System.Text.RegularExpressions;
using ArtCommission.Application.Ai.Common;
using ArtCommission.Application.Dispute.CommissionDispute.Queries;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Application.Ai.Queries.GetDisputeAiSummary;

public sealed record DisputeAiSummaryResultDto(string Summary);

/// <summary>
/// AI-UC56 — Phân tích hồ sơ tranh chấp và tóm tắt vụ việc cho Admin/Moderator trọng tài phân xử.
/// </summary>
public sealed record GetDisputeAiSummaryQuery(Guid DisputeId)
    : IRequest<(bool Success, DisputeAiSummaryResultDto? Data, string[] Errors)>;

public class GetDisputeAiSummaryQueryHandler
    : IRequestHandler<GetDisputeAiSummaryQuery, (bool Success, DisputeAiSummaryResultDto? Data, string[] Errors)>
{
    private readonly IMediator _mediator;
    private readonly IAiChatClient _aiChatClient;
    private readonly ILogger<GetDisputeAiSummaryQueryHandler> _logger;

    public GetDisputeAiSummaryQueryHandler(
        IMediator mediator,
        IAiChatClient aiChatClient,
        ILogger<GetDisputeAiSummaryQueryHandler> logger)
    {
        _mediator = mediator;
        _aiChatClient = aiChatClient;
        _logger = logger;
    }

    public async Task<(bool Success, DisputeAiSummaryResultDto? Data, string[] Errors)> Handle(
        GetDisputeAiSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var detail = await _mediator.Send(new GetDisputeArbitrationDetailQuery(request.DisputeId), cancellationToken);
        if (detail == null)
        {
            return (false, null, new[] { "Không tìm thấy hồ sơ tranh chấp tương ứng." });
        }

        const string systemPrompt =
            "Bạn là Trợ lý Pháp lý & Trọng tài độc lập của sàn đặt vẽ tranh Dillustration.\n" +
            "Nhiệm vụ của bạn là đọc hồ sơ tranh chấp, tiến độ mốc, và lịch sử trao đổi giữa Khách hàng (Client) và Họa sĩ (Creator) để lập Bản Tóm Tắt Trọng Tài khách quan giúp Ban Quản Trị (Admin) ra quyết định xử lý nhanh chóng trong 10 giây.\n\n" +
            "QUY TẮC CỐT LÕI (KẾT LUẬN TRƯỚC - CĂN CỨ VÀ TRÍCH DẪN BẰNG CHỨNG - KHÔNG VĂN MẪU):\n" +
            "1. TUYỆT ĐỐI KHÔNG chào hỏi, không mở đầu hay kết luận xã giao. Không dùng in đậm (**), không dùng Markdown headings (#, ##), không dùng kẻ ngang (---).\n" +
            "2. PHẢI CÓ TRÍCH DẪN NGUYÊN VĂN BẰNG CHỨNG: Khi chỉ ra bên có lỗi hoặc bất đồng, BẮT BUỘC trích dẫn ít nhất 1 câu nguyên văn trong đoạn chat (hoặc hành động nộp file/hủy hẹn) làm chứng cứ trực tiếp.\n" +
            "3. SỐ TIỀN CHÍNH XÁC: Đề xuất số tiền hoàn cho Khách và thanh toán cho Họa sĩ bằng số tiền VND cụ thể dựa trên số tiền ký quỹ Escrow và các mốc thực tế.\n\n" +
            "CẤU TRÚC ĐẦU RA BẮT BUỘC (VIẾT ĐÚNG THEO KHUNG SAU):\n" +
            "KẾT LUẬN & ĐỀ XUẤT PHÂN XỬ:\n" +
            "- Bên có lỗi chính: [Họa sĩ / Khách hàng / Cả hai bên / Chưa đủ chứng cứ]\n" +
            "- Đề xuất chia tiền quỹ: [Nêu rõ số tiền VND: Hoàn lại X VND cho Khách, Thanh toán Y VND cho Họa sĩ]\n\n" +
            "CĂN CỨ VÀ TRÍCH DẪN BẰNG CHỨNG:\n" +
            "- Trọng tâm khiếu nại: [1 câu tóm tắt cốt lõi tranh chấp]\n" +
            "- Tiến độ mốc: [Tình trạng các mốc và số tiền tương ứng]\n" +
            "- Bằng chứng trích dẫn:\n" +
            "  + [Tên người nói / Vai trò]: \"[Trích dẫn nguyên văn 1 câu quan trọng từ chat chứng minh cho sự việc hoặc vi phạm]\"\n" +
            "  + Nhận định mấu chốt: [1 câu ngắn giải thích vì sao câu nói/hành động trên dẫn đến kết luận bên có lỗi]";

        var sb = new StringBuilder();
        sb.AppendLine($"HỒ SƠ TRANH CHẤP #{detail.DisputeId.ToString().Substring(0, 8).ToUpper()}");
        sb.AppendLine($"- Đơn hàng: {detail.CommissionTitle} (Mã: {detail.CommissionId.ToString().Substring(0, 8)})");
        sb.AppendLine($"- Tiền ký quỹ Escrow: {(detail.EscrowHeldAmount > 0 ? detail.EscrowHeldAmount : detail.FinalPrice):N0} VND");
        sb.AppendLine($"- Khách hàng: {detail.Client?.FullName ?? "N/A"} (@{detail.Client?.UserName ?? "client"})");
        sb.AppendLine($"- Họa sĩ: {detail.Creator?.FullName ?? "N/A"} (@{detail.Creator?.UserName ?? "creator"})");
        sb.AppendLine($"- Người gửi đơn: {detail.RaisedBy?.FullName ?? "N/A"} ({detail.RaisedBy?.Role ?? "User"})");
        sb.AppendLine($"- Lý do gửi đơn: {detail.Reason}");
        sb.AppendLine($"- Mô tả ban đầu: {detail.CommissionDescription ?? "Không có mô tả thêm"}");

        if (detail.ClaimantRefundPercent.HasValue || detail.RespondentRefundPercent.HasValue)
        {
            sb.AppendLine("\n[ĐỀ XUẤT HÒA GIẢI CỦA HAI BÊN]:");
            sb.AppendLine($"- Bên mở đơn ({detail.RaisedBy?.Role}): Yêu cầu hoàn {detail.ClaimantRefundPercent ?? 100:G29}%");
            if (detail.RespondentRefundPercent.HasValue)
            {
                var otherRole = detail.RaisedBy?.Role == "Client" ? "Creator" : "Client";
                sb.AppendLine($"- Bên phản hồi ({otherRole}): Đề xuất hoàn {detail.RespondentRefundPercent.Value:G29}%");
                if (!string.IsNullOrWhiteSpace(detail.RespondentResponse))
                {
                    sb.AppendLine($"- Lời giải trình của bên phản hồi: \"{detail.RespondentResponse}\"");
                }
            }
            else
            {
                sb.AppendLine($"- Bên phản hồi: Chưa gửi đề xuất đối ứng (đơn được chuyển thẳng hoặc hết hạn).");
            }
        }

        if (detail.EvidenceUrls != null && detail.EvidenceUrls.Count > 0)
        {
            sb.AppendLine("\n[FILE/LINK BẰNG CHỨNG DO BÊN KHIẾU NẠI ĐÍNH KÈM]:");
            foreach (var url in detail.EvidenceUrls)
            {
                sb.AppendLine($"- {url}");
            }
        }

        if (detail.Milestones != null && detail.Milestones.Count > 0)
        {
            sb.AppendLine("\n[CÁC MỐC THỰC HIỆN]:");
            foreach (var m in detail.Milestones)
            {
                sb.AppendLine($"- Mốc #{m.Sequence}: {m.Title} | Giá: {m.Price:N0} VND | Trạng thái: {m.Status} | Đã sửa: {m.RevisionCount} lần");
            }
        }

        if (detail.ChatSnapshot != null && detail.ChatSnapshot.Count > 0)
        {
            sb.AppendLine("\n[TRÍCH ĐOẠN TRAO ĐỔI GẦN NHẤT]:");
            var recentMessages = detail.ChatSnapshot.TakeLast(25);
            foreach (var msg in recentMessages)
            {
                sb.AppendLine($"- [{msg.SenderRole}] {msg.SenderName}: {msg.Body}");
            }
        }

        try
        {
            var result = await _aiChatClient.CompleteAsync(
                systemInstruction: systemPrompt,
                history: Array.Empty<AiChatTurn>(),
                userMessage: sb.ToString(),
                cancellationToken: cancellationToken);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Content))
            {
                return (false, null, new[] { result.Error ?? "Không thể tổng hợp tóm tắt tranh chấp lúc này. Vui lòng thử lại sau." });
            }

            var clean = SanitizeDisputeSummary(result.Content);

            return (true, new DisputeAiSummaryResultDto(clean), Array.Empty<string>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi AI Dispute Summarizer cho tranh chấp {DisputeId}", request.DisputeId);
            return (false, null, new[] { "Lỗi hệ thống khi tạo bản tóm tắt tranh chấp." });
        }
    }

    private static string SanitizeDisputeSummary(string raw)
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
                    lower.StartsWith("dưới đây") ||
                    lower.StartsWith("###") ||
                    line.StartsWith("---") ||
                    string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                started = true;
            }

            if (lower.StartsWith("hy vọng") ||
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
