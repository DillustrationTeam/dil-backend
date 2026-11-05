using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Dispute.TradeDispute.Validators;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ArtCommission.Application.Dispute.TradeDispute.Commands;

public sealed record CreateTradeDisputeCommand(
    Guid ArtTradeId,
    Guid RaisedById,
    string Reason,
    List<string>? EvidenceUrls = null
) : IRequest<(bool Success, string Message, Guid? DisputeId, string[] Errors)>;

public class CreateTradeDisputeCommandHandler
    : IRequestHandler<CreateTradeDisputeCommand, (bool Success, string Message, Guid? DisputeId, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public CreateTradeDisputeCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, string Message, Guid? DisputeId, string[] Errors)> Handle(
        CreateTradeDisputeCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new CreateTradeDisputeCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, "Dữ liệu không hợp lệ.", null, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var trade = await _db.ArtTrades
            .FirstOrDefaultAsync(t => t.Id == request.ArtTradeId && !t.IsDeleted, cancellationToken);

        if (trade == null)
        {
            return (false, "Không tìm thấy giao dịch trao đổi tranh.", null, ["Giao dịch không tồn tại."]);
        }

        var isParticipant = trade.InitiatorId == request.RaisedById || trade.ReceiverId == request.RaisedById;
        if (!isParticipant)
        {
            return (false, "Bạn không có quyền khiếu nại giao dịch này.", null, ["Chỉ hai bên tham gia trade mới có quyền gửi khiếu nại."]);
        }

        if (trade.Status == TradeStatus.Completed)
        {
            return (false, "Giao dịch đã hoàn tất và kết thúc.", null, ["Không thể khiếu nại giao dịch đã hoàn tất."]);
        }

        if (trade.Status == TradeStatus.Cancelled)
        {
            return (false, "Giao dịch đã bị huỷ trước đó.", null, ["Không thể khiếu nại giao dịch đã huỷ."]);
        }

        // Kiểm tra xem đã có khiếu nại đang chờ xử lý chưa
        var hasPendingDispute = await _db.TradeDisputes.AnyAsync(d =>
            d.ArtTradeId == request.ArtTradeId &&
            !d.IsDeleted &&
            (d.Status == "Pending" || d.Status == "UnderReview"), cancellationToken);

        if (hasPendingDispute)
        {
            return (false, "Giao dịch này đang có khiếu nại chờ Admin giải quyết.", null, ["Đã có một đơn khiếu nại đang được xử lý."]);
        }

        var evidenceJson = request.EvidenceUrls != null && request.EvidenceUrls.Count > 0
            ? JsonSerializer.Serialize(request.EvidenceUrls)
            : null;

        var dispute = new Domain.Entities.Dispute.TradeDispute
        {
            ArtTradeId = request.ArtTradeId,
            RaisedById = request.RaisedById,
            Reason = request.Reason.Trim(),
            EvidenceUrls = evidenceJson,
            Status = "Pending"
        };

        // Đóng băng trạng thái trade sang Disputed để tạm dừng mọi thao tác confirm/download
        trade.Status = TradeStatus.Disputed;
        trade.UpdatedAt = DateTimeOffset.UtcNow;

        _db.TradeDisputes.Add(dispute);
        await _db.SaveChangesAsync(cancellationToken);

        return (true, "Đã gửi khiếu nại thành công. Quản trị viên sẽ xem xét và phản hồi.", dispute.Id, []);
    }
}

