using ArtCommission.Application.ArtTrade.Common;
using ArtCommission.Application.ArtTrade.DTOs;
using ArtCommission.Application.Common.Interfaces;
using INotificationPublisher = ArtCommission.Application.Notifications.Common.INotificationPublisher;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtTrade.Commands;

/// <summary>
/// Side A (người khởi tạo) xác nhận đề nghị sau khi Side B đã chấp nhận.
/// Chỉ khi cả 2 bên cùng accept, trade mới chuyển sang Accepted và bắt đầu giai đoạn vẽ.
/// </summary>
public sealed record ConfirmTradeProposalCommand(
    Guid TradeId,
    Guid UserId
) : IRequest<(bool Success, ArtTradeDto? Data, string[] Errors)>;

public class ConfirmTradeProposalCommandHandler : IRequestHandler<ConfirmTradeProposalCommand, (bool Success, ArtTradeDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly ITradeSlotService _slotService;
    private readonly INotificationPublisher _notifPublisher;

    public ConfirmTradeProposalCommandHandler(
        IApplicationDbContext db,
        ITradeSlotService slotService,
        INotificationPublisher notifPublisher)
    {
        _db = db;
        _slotService = slotService;
        _notifPublisher = notifPublisher;
    }

    public async Task<(bool Success, ArtTradeDto? Data, string[] Errors)> Handle(ConfirmTradeProposalCommand request, CancellationToken cancellationToken)
    {
        var trade = await _db.ArtTrades
            .Include(t => t.Initiator)
            .Include(t => t.Receiver)
            .FirstOrDefaultAsync(t => t.Id == request.TradeId && !t.IsDeleted, cancellationToken);

        if (trade == null)
            return (false, null, ["Không tìm thấy giao dịch trao đổi tranh."]);

        if (trade.InitiatorId != request.UserId)
            return (false, null, ["Chỉ người gửi đề nghị mới có thể xác nhận bước này."]);

        if (trade.Status != TradeStatus.Proposed)
            return (false, null, [$"Giao dịch đang ở trạng thái {trade.Status}, không thể xác nhận."]);

        if (!trade.ReceiverAcceptedAt.HasValue)
            return (false, null, ["Đối tác chưa chấp nhận đề nghị này."]);

        var (canTradeInitiator, _, initiatorReason) = await _slotService.CheckTradeSlotAvailableAsync(trade.InitiatorId, cancellationToken);
        if (!canTradeInitiator)
            return (false, null, [initiatorReason ?? "Bạn không còn Slot rảnh để bắt đầu giao dịch này."]);

        var (canTradeReceiver, _, receiverReason) = await _slotService.CheckTradeSlotAvailableAsync(trade.ReceiverId, cancellationToken);
        if (!canTradeReceiver)
            return (false, null, [receiverReason ?? "Đối tác hiện không còn Slot rảnh để bắt đầu giao dịch."]);

        var now = DateTimeOffset.UtcNow;
        trade.Status = TradeStatus.Accepted;
        trade.SubmissionDeadlineAt = now.AddDays(14);
        trade.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var initiatorName = trade.Initiator?.FullName ?? "Đối tác";
            await _notifPublisher.PublishAsync(
                userId: trade.ReceiverId,
                notificationType: NotificationType.ArtTradeAccepted,
                title: "Trao đổi tranh đã bắt đầu!",
                body: $"Họa sĩ {initiatorName} đã xác nhận \"{trade.Title}\". Phòng vẽ tranh đã chính thức mở!",
                refType: "ArtTrade",
                refId: trade.Id,
                dedupKey: $"ArtTradeAccepted:{trade.Id}:{trade.ReceiverId}",
                cancellationToken: cancellationToken
            );
        }
        catch
        {
            // Bỏ qua lỗi gửi thông báo
        }

        return (true, ArtTradeDto.FromEntity(trade), []);
    }
}

