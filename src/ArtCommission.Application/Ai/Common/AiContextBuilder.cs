using System.Text;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Common;

/// <summary>
/// Dựng chỉ dẫn hệ thống (system instruction) cho chatbot dựa trên ngữ cảnh.
///
/// BA LUẬT KHI VIẾT PROMPT Ở ĐÂY:
///   1. Ngữ cảnh được NẠP LẠI mỗi lượt hỏi, không đóng băng lúc tạo phiên — người dùng
///      hỏi "đơn tôi tới đâu rồi" thì câu trả lời phải phản ánh trạng thái hiện tại.
///   2. Chỉ đưa vào prompt dữ liệu mà CHÍNH người hỏi được phép xem. Nếu không,
///      chatbot trở thành lỗ hổng rò rỉ dữ liệu của người khác.
///   3. Luôn chốt rằng bot KHÔNG được bịa số liệu ngoài ngữ cảnh — đây là lý do
///      phổ biến nhất khiến câu trả lời sai trong các hệ thống kiểu này.
/// </summary>
public interface IAiContextBuilder
{
    /// <summary>
    /// Trả về chỉ dẫn hệ thống; chuỗi rỗng nghĩa là không có ngữ cảnh đặc biệt.
    /// </summary>
    Task<string> BuildSystemInstructionAsync(
        Guid userId,
        AiContextType contextType,
        Guid? contextId,
        CancellationToken cancellationToken = default);
}

public class AiContextBuilder : IAiContextBuilder
{
    /// <summary>Phần mô tả nền tảng — dùng chung cho mọi lượt hỏi.</summary>
    private const string PlatformBrief = """
        Bạn là trợ lý ảo của Dillustration — nền tảng đặt vẽ tranh và đấu giá tranh trực tuyến.

        Cách trả lời:
        - Trả lời bằng tiếng Việt, ngắn gọn, thân thiện, xưng "mình" và gọi người dùng là "bạn".
        - ĐƯỢC PHÉP dùng hai nguồn:
            (a) KIẾN THỨC VỀ QUY TRÌNH bên dưới — để giải thích cách nền tảng hoạt động,
                cách đặt giá, nạp/rút tiền, dùng mã giảm giá... Đây là câu hỏi phổ biến nhất.
            (b) NGỮ CẢNH của lượt hỏi — dữ liệu thật của đơn hàng/tranh cụ thể.
        - TUYỆT ĐỐI không tự nghĩ ra SỐ LIỆU CỤ THỂ: mã đơn, số tiền thật trong ví, ngày
          tháng, trạng thái đơn, tên người dùng. Những thứ đó chỉ được lấy từ NGỮ CẢNH.
        - Nếu câu hỏi cần số liệu mà NGỮ CẢNH không có, hãy nói rõ là chưa có dữ liệu cho
          trường hợp này và hướng dẫn người dùng xem ở đâu trong ứng dụng.
        - Không hứa hẹn thay đổi trạng thái đơn hàng, không cam kết hoàn tiền.
        - Khi người dùng hỏi về tranh chấp hoặc khiếu nại, hướng dẫn họ dùng chức năng
          "Khiếu nại" trong đơn thay vì tự xử lý.
        """;

    private readonly IApplicationDbContext _db;

    public AiContextBuilder(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<string> BuildSystemInstructionAsync(
        Guid userId,
        AiContextType contextType,
        Guid? contextId,
        CancellationToken cancellationToken = default)
    {
        // Luôn có phần mô tả nền tảng + KIẾN THỨC QUY TRÌNH, kể cả khi không gắn ngữ cảnh.
        // Trước đây chỉ có mô tả nền tảng nên bot từ chối mọi câu hỏi dạng "làm sao để
        // đấu giá / nạp tiền thế nào" — đó là nhóm câu hỏi phổ biến nhất của UC44.
        var builder = new StringBuilder(PlatformBrief);

        builder.AppendLine();
        builder.AppendLine();
        builder.Append(PlatformKnowledge.Workflows);

        if (contextId is null || contextType == AiContextType.General)
        {
            return builder.ToString();
        }

        switch (contextType)
        {
            case AiContextType.Commission:
                await AppendCommissionContextAsync(builder, userId, contextId.Value, cancellationToken);
                break;

            case AiContextType.Artwork:
                await AppendArtworkContextAsync(builder, contextId.Value, cancellationToken);
                break;

            default:
                break;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Ngữ cảnh đơn đặt vẽ. CHỈ dựng khi người hỏi là client hoặc creator của đơn —
    /// nếu không, không đưa dữ liệu đơn vào prompt.
    /// </summary>
    private async Task AppendCommissionContextAsync(
        StringBuilder builder,
        Guid userId,
        Guid commissionId,
        CancellationToken cancellationToken)
    {
        var commission = await _db.Commissions
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == commissionId && !c.IsDeleted, cancellationToken);

        if (commission is null)
        {
            builder.AppendLine();
            builder.AppendLine("NGỮ CẢNH: Người dùng có nhắc một đơn đặt vẽ nhưng không tìm thấy đơn này.");
            return;
        }

        var isParticipant = commission.ClientId == userId || commission.CreatorId == userId;

        if (!isParticipant)
        {
            builder.AppendLine();
            builder.AppendLine(
                "NGỮ CẢNH: Người dùng hỏi về một đơn không thuộc quyền của họ. " +
                "Hãy từ chối cung cấp chi tiết đơn và đề nghị họ kiểm tra lại.");
            return;
        }

        var milestones = await _db.Milestones
            .AsNoTracking()
            .Where(m => m.CommissionId == commissionId && !m.IsDeleted)
            .OrderBy(m => m.Sequence)
            .Select(m => new { m.Sequence, m.Title, m.Status, m.RevisionCount, m.Price })
            .ToListAsync(cancellationToken);

        builder.AppendLine();
        builder.AppendLine("NGỮ CẢNH — ĐƠN ĐẶT VẼ:");
        builder.AppendLine($"- Mã đơn: {commission.Id}");
        builder.AppendLine($"- Tiêu đề: {commission.Title}");
        builder.AppendLine($"- Trạng thái đơn: {commission.Status}");
        builder.AppendLine($"- Trạng thái escrow: {commission.EscrowStatus}");
        builder.AppendLine($"- Tổng giá: {commission.TotalPrice:N0} VND");
        builder.AppendLine($"- Giá sau giảm: {commission.FinalPrice:N0} VND");
        builder.AppendLine($"- Tiền đang giữ: {commission.EscrowHeldAmount:N0} VND");
        builder.AppendLine($"- Đã giải ngân: {commission.DisbursedAmount:N0} VND");
        builder.AppendLine($"- Giai đoạn hiện tại: {commission.CurrentStage}");
        builder.AppendLine($"- Hạn hoàn thành: {(commission.DeadlineAt.HasValue ? commission.DeadlineAt.Value.ToString("yyyy-MM-dd HH:mm") : "chưa đặt")}");
        builder.AppendLine($"- Vai trò người hỏi: {(commission.CreatorId == userId ? "Creator" : "Client")}");

        if (milestones.Count == 0)
        {
            builder.AppendLine("- Chưa có mốc công việc nào.");
            return;
        }

        builder.AppendLine("- Các mốc công việc:");
        foreach (var milestone in milestones)
        {
            builder.AppendLine(
                $"  + Mốc {milestone.Sequence}: {milestone.Title} | trạng thái {milestone.Status} | " +
                $"giá {milestone.Price:N0} VND | số lần sửa {milestone.RevisionCount}");
        }
    }

    /// <summary>Ngữ cảnh tranh — chỉ thông tin công khai, không có dữ liệu riêng tư.</summary>
    private async Task AppendArtworkContextAsync(
        StringBuilder builder,
        Guid artworkId,
        CancellationToken cancellationToken)
    {
        var artwork = await _db.Artworks
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == artworkId && !a.IsDeleted, cancellationToken);

        if (artwork is null)
        {
            builder.AppendLine();
            builder.AppendLine("NGỮ CẢNH: Người dùng có nhắc một tranh nhưng không tìm thấy tranh này.");
            return;
        }

        builder.AppendLine();
        builder.AppendLine("NGỮ CẢNH — TRANH:");
        builder.AppendLine($"- Mã tranh: {artwork.Id}");
        builder.AppendLine($"- Tiêu đề: {artwork.Title}");
        builder.AppendLine($"- Phong cách: {artwork.Style ?? "chưa ghi"}");
        builder.AppendLine($"- Trạng thái kiểm duyệt: {artwork.ModerationStatus}");
        builder.AppendLine($"- Lượt xem: {artwork.ViewCount} | Lượt thích: {artwork.LikeCount}");
        builder.AppendLine($"- Do AI tạo: {(artwork.IsAiGenerated ? "có" : "không")}");
        builder.AppendLine($"- Có huy hiệu xác thực vẽ tay: {(artwork.IsAiGenerated ? "chưa" : "đang xét")}");
    }
}
