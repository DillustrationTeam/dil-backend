using ArtCommission.Application.ArtTrade.Common;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Dispute.TradeDispute.DTOs;
using ArtCommission.Application.Dispute.TradeDispute.Validators;
using INotificationPublisher = ArtCommission.Application.Notifications.Common.INotificationPublisher;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Dispute.TradeDispute.Commands;

public sealed record ResolveTradeDisputeCommand(
    Guid DisputeId,
    Guid AdminId,
    string Resolution,
    string? AdminNote = null
) : IRequest<(bool Success, string Message, string[] Errors)>;

public class ResolveTradeDisputeCommandHandler
    : IRequestHandler<ResolveTradeDisputeCommand, (bool Success, string Message, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly ITradeSlotService _slotService;
    private readonly INotificationPublisher _notifPublisher;

    public ResolveTradeDisputeCommandHandler(
        IApplicationDbContext db,
        ITradeSlotService slotService,
        INotificationPublisher notifPublisher)
    {
        _db = db;
        _slotService = slotService;
        _notifPublisher = notifPublisher;
    }

    public async Task<(bool Success, string Message, string[] Errors)> Handle(
        ResolveTradeDisputeCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new ResolveTradeDisputeCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, "Dữ liệu không hợp lệ.", validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var dispute = await _db.TradeDisputes
            .Include(d => d.ArtTrade)
            .FirstOrDefaultAsync(d => d.Id == request.DisputeId && !d.IsDeleted, cancellationToken);

        if (dispute == null)
        {
            return (false, "Không tìm thấy hồ sơ khiếu nại.", ["Khiếu nại không tồn tại."]);
        }

        if (dispute.Status == "Resolved" || dispute.Status == "Dismissed")
        {
            return (false, "Khiếu nại này đã được xử lý trước đó.", ["Không thể xử lý lại khiếu nại đã đóng."]);
        }

        var trade = dispute.ArtTrade;
        var now = DateTimeOffset.UtcNow;
        dispute.Resolution = request.Resolution;
        dispute.AdminNote = request.AdminNote?.Trim();
        dispute.ResolvedAt = now;

        switch (request.Resolution)
        {
            case TradeDisputeResolutions.FaultInitiator:
                // Lỗi thuộc về Initiator -> Huỷ trade, phạt đóng băng slot Initiator 30 ngày
                trade.Status = TradeStatus.Cancelled;
                trade.CancelledAt = now;
                trade.CancellationReason = $"Huỷ do vi phạm (Phán quyết Admin): {request.AdminNote}";
                trade.UpdatedAt = now;

                await _slotService.FreezeSlotAsync(
                    trade.InitiatorId,
                    trade.Id,
                    days: 30,
                    reason: $"Bị phạt do vi phạm khiếu nại trade #{trade.Id}: {request.AdminNote}",
                    cancellationToken);

                dispute.Status = "Resolved";
                break;

            case TradeDisputeResolutions.FaultReceiver:
                // Lỗi thuộc về Receiver -> Huỷ trade, phạt đóng băng slot Receiver 30 ngày
                trade.Status = TradeStatus.Cancelled;
                trade.CancelledAt = now;
                trade.CancellationReason = $"Huỷ do vi phạm (Phán quyết Admin): {request.AdminNote}";
                trade.UpdatedAt = now;

                await _slotService.FreezeSlotAsync(
                    trade.ReceiverId,
                    trade.Id,
                    days: 30,
                    reason: $"Bị phạt do vi phạm khiếu nại trade #{trade.Id}: {request.AdminNote}",
                    cancellationToken);

                dispute.Status = "Resolved";
                break;

            case TradeDisputeResolutions.MutualCancel:
                // Đồng thuận huỷ hoà -> Huỷ trade, giải phóng slot cho cả 2 bên (không ai bị phạt)
                trade.Status = TradeStatus.Cancelled;
                trade.CancelledAt = now;
                trade.CancellationReason = $"Huỷ hoà theo phán quyết Admin: {request.AdminNote}";
                trade.UpdatedAt = now;

                dispute.Status = "Resolved";
                break;

            case TradeDisputeResolutions.Dismissed:
                // Bác bỏ khiếu nại vô căn cứ -> Khôi phục lại trạng thái BothSubmitted để tiếp tục
                trade.Status = TradeStatus.BothSubmitted;
                trade.ConfirmationDeadlineAt = now.AddHours(72);
                trade.UpdatedAt = now;

                dispute.Status = "Dismissed";
                break;
        }

        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var isDismissed = request.Resolution == TradeDisputeResolutions.Dismissed;
            var resolutionLabel = request.Resolution switch
            {
                TradeDisputeResolutions.Dismissed => "Bác bỏ khiếu nại (khôi phục giao dịch)",
                TradeDisputeResolutions.MutualCancel => "Hủy giao dịch theo thỏa thuận / không lỗi bên nào",
                TradeDisputeResolutions.FaultInitiator => "Phán quyết lỗi thuộc về người khởi tạo (Side A)",
                TradeDisputeResolutions.FaultReceiver => "Phán quyết lỗi thuộc về đối tác nhận (Side B)",
                _ => request.Resolution
            };

            var body = isDismissed
                ? $"Quản trị viên đã bác bỏ khiếu nại phiên trao đổi \"{trade.Title}\" và khôi phục giao dịch.{(string.IsNullOrWhiteSpace(request.AdminNote) ? "" : $" Ghi chú: {request.AdminNote}")}"
                : $"Quản trị viên đã xử lý khiếu nại phiên trao đổi \"{trade.Title}\". Kết quả: {resolutionLabel}.{(string.IsNullOrWhiteSpace(request.AdminNote) ? "" : $" Ghi chú: {request.AdminNote}")}";

            var title = "Khiếu nại trao đổi tranh đã được xử lý";

            await _notifPublisher.PublishAsync(
                userId: trade.InitiatorId,
                notificationType: NotificationType.TradeDisputeResolved,
                title: title,
                body: body,
                refType: "ArtTrade",
                refId: trade.Id,
                dedupKey: $"TradeDisputeResolved:{dispute.Id}:{trade.InitiatorId}",
                cancellationToken: cancellationToken
            );

            await _notifPublisher.PublishAsync(
                userId: trade.ReceiverId,
                notificationType: NotificationType.TradeDisputeResolved,
                title: title,
                body: body,
                refType: "ArtTrade",
                refId: trade.Id,
                dedupKey: $"TradeDisputeResolved:{dispute.Id}:{trade.ReceiverId}",
                cancellationToken: cancellationToken
            );
        }
        catch
        {
            // Bỏ qua lỗi gửi thông báo để không ảnh hưởng phân xử
        }

        return (true, $"Đã giải quyết khiếu nại thành công theo phán quyết {request.Resolution}.", []);
    }
}

