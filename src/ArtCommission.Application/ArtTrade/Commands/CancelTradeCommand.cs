using ArtCommission.Application.ArtTrade.Common;
using ArtCommission.Application.ArtTrade.DTOs;
using ArtCommission.Application.Common.Interfaces;
using INotificationPublisher = ArtCommission.Application.Notifications.Common.INotificationPublisher;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtTrade.Commands;

public sealed record CancelTradeCommand(
    Guid TradeId,
    Guid UserId,
    string Reason,
    bool IsViolation = false,
    Guid? ViolatorUserId = null
) : IRequest<(bool Success, ArtTradeDto? Data, string[] Errors)>;

public class CancelTradeCommandValidator : AbstractValidator<CancelTradeCommand>
{
    public CancelTradeCommandValidator()
    {
        RuleFor(x => x.TradeId).NotEmpty().WithMessage("Mã giao dịch không hợp lệ.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Mã người dùng không hợp lệ.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do huỷ giao dịch không được để trống.")
            .MaximumLength(500).WithMessage("Lý do không được vượt quá 500 ký tự.");
    }
}

public class CancelTradeCommandHandler : IRequestHandler<CancelTradeCommand, (bool Success, ArtTradeDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly ITradeSlotService _slotService;
    private readonly INotificationPublisher _notifPublisher;

    public CancelTradeCommandHandler(
        IApplicationDbContext db,
        ITradeSlotService slotService,
        INotificationPublisher notifPublisher)
    {
        _db = db;
        _slotService = slotService;
        _notifPublisher = notifPublisher;
    }

    public async Task<(bool Success, ArtTradeDto? Data, string[] Errors)> Handle(CancelTradeCommand request, CancellationToken cancellationToken)
    {
        var validator = new CancelTradeCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, null, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var trade = await _db.ArtTrades
            .Include(t => t.Initiator)
            .Include(t => t.Receiver)
            .FirstOrDefaultAsync(t => t.Id == request.TradeId && !t.IsDeleted, cancellationToken);

        if (trade == null)
        {
            return (false, null, ["Không tìm thấy giao dịch trao đổi tranh."]);
        }

        if (trade.Status == TradeStatus.Completed)
        {
            return (false, null, ["Giao dịch đã hoàn tất thành công, không thể huỷ bỏ."]);
        }

        if (trade.Status == TradeStatus.Cancelled)
        {
            return (false, null, ["Giao dịch này đã bị huỷ trước đó."]);
        }

        var isParticipant = trade.InitiatorId == request.UserId || trade.ReceiverId == request.UserId;
        if (!isParticipant)
        {
            return (false, null, ["Bạn không có quyền huỷ giao dịch này."]);
        }

        var now = DateTimeOffset.UtcNow;
        trade.Status = TradeStatus.Cancelled;
        trade.CancelledAt = now;
        trade.CancelledById = request.UserId;
        trade.CancellationReason = request.Reason.Trim();
        trade.UpdatedAt = now;

        // Nếu có vi phạm (sủi kèo, quá hạn) -> Phạt đóng băng slot 30 ngày đối với người vi phạm
        if (request.IsViolation && request.ViolatorUserId.HasValue)
        {
            await _slotService.FreezeSlotAsync(
                request.ViolatorUserId.Value,
                trade.Id,
                days: 30,
                reason: $"Đóng băng do huỷ giao dịch vi phạm: {request.Reason}",
                cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var recipientId = request.UserId == trade.InitiatorId ? trade.ReceiverId : trade.InitiatorId;
            var cancellerName = request.UserId == trade.InitiatorId ? (trade.Initiator?.FullName ?? "Người khởi tạo") : (trade.Receiver?.FullName ?? "Đối tác");
            await _notifPublisher.PublishAsync(
                userId: recipientId,
                notificationType: NotificationType.ArtTradeCancelled,
                title: "Giao dịch trao đổi tranh đã bị hủy",
                body: $"{cancellerName} đã hủy phiên trao đổi \"{trade.Title}\". Lý do: {trade.CancellationReason}",
                refType: "ArtTrade",
                refId: trade.Id,
                dedupKey: $"ArtTradeCancelled:{trade.Id}:{recipientId}",
                cancellationToken: cancellationToken
            );
        }
        catch
        {
            // Bỏ qua lỗi gửi thông báo để không ảnh hưởng giao dịch chính
        }

        return (true, ArtTradeDto.FromEntity(trade), []);
    }
}

