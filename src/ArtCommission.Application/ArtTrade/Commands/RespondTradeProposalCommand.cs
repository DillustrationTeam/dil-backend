using ArtCommission.Application.ArtTrade.Common;
using ArtCommission.Application.ArtTrade.DTOs;
using ArtCommission.Application.Common.Interfaces;
using INotificationPublisher = ArtCommission.Application.Notifications.Common.INotificationPublisher;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtTrade.Commands;

public sealed record RespondTradeProposalCommand(
    Guid TradeId,
    Guid UserId,
    bool Accept,
    string? RequestNote = null,
    string? RejectionReason = null
) : IRequest<(bool Success, ArtTradeDto? Data, string[] Errors)>;

public class RespondTradeProposalCommandValidator : AbstractValidator<RespondTradeProposalCommand>
{
    public RespondTradeProposalCommandValidator()
    {
        RuleFor(x => x.TradeId).NotEmpty().WithMessage("Mã giao dịch không hợp lệ.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Mã người dùng không hợp lệ.");

        RuleFor(x => x.RejectionReason)
            .NotEmpty().When(x => !x.Accept)
            .WithMessage("Vui lòng nhập lý do từ chối.")
            .MaximumLength(500).WithMessage("Lý do từ chối không được vượt quá 500 ký tự.");

        RuleFor(x => x.RequestNote)
            .MaximumLength(2000).WithMessage("Yêu cầu vẽ không được vượt quá 2000 ký tự.");
    }
}

public class RespondTradeProposalCommandHandler : IRequestHandler<RespondTradeProposalCommand, (bool Success, ArtTradeDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly ITradeSlotService _slotService;
    private readonly INotificationPublisher _notifPublisher;

    public RespondTradeProposalCommandHandler(
        IApplicationDbContext db,
        ITradeSlotService slotService,
        INotificationPublisher notifPublisher)
    {
        _db = db;
        _slotService = slotService;
        _notifPublisher = notifPublisher;
    }

    public async Task<(bool Success, ArtTradeDto? Data, string[] Errors)> Handle(RespondTradeProposalCommand request, CancellationToken cancellationToken)
    {
        var validator = new RespondTradeProposalCommandValidator();
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

        if (trade.ReceiverId != request.UserId)
        {
            return (false, null, ["Bạn không có quyền phản hồi đề nghị này."]);
        }

        if (trade.Status != TradeStatus.Proposed)
        {
            return (false, null, [$"Giao dịch đang ở trạng thái {trade.Status}, không thể phản hồi."]);
        }

        if (request.Accept && trade.ReceiverAcceptedAt.HasValue)
        {
            return (false, null, ["Bạn đã chấp nhận đề nghị này, đang chờ đối tác xác nhận."]);
        }

        var now = DateTimeOffset.UtcNow;

        if (!request.Accept)
        {
            trade.Status = TradeStatus.Rejected;
            trade.RejectionReason = request.RejectionReason?.Trim();
            trade.UpdatedAt = now;
        }
        else
        {
            // Kiểm tra slot của Receiver trước khi nhận
            var (canTradeReceiver, _, receiverReason) = await _slotService.CheckTradeSlotAvailableAsync(request.UserId, cancellationToken);
            if (!canTradeReceiver)
            {
                return (false, null, [receiverReason ?? "Bạn không còn Slot rảnh để chấp nhận giao dịch này."]);
            }

            // Kiểm tra lại slot của Initiator tại thời điểm chấp nhận (phòng khi A đã kín slot trong lúc chờ B)
            var (canTradeInitiator, _, initiatorReason) = await _slotService.CheckTradeSlotAvailableAsync(trade.InitiatorId, cancellationToken);
            if (!canTradeInitiator)
            {
                return (false, null, ["Người gửi lời mời hiện đã kín Slot hoặc bị đóng băng slot, không thể bắt đầu giao dịch."]);
            }

            // Side B chấp nhận nhưng giao dịch chưa bắt đầu: cần Side A xác nhận lại (cả 2 bên cùng accept)
            trade.ReceiverAcceptedAt = now;
            if (!string.IsNullOrWhiteSpace(request.RequestNote))
            {
                trade.ReceiverResponseNote = request.RequestNote.Trim();
            }
            trade.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var partnerName = trade.Receiver?.FullName ?? "Đối tác";
            if (!request.Accept)
            {
                await _notifPublisher.PublishAsync(
                    userId: trade.InitiatorId,
                    notificationType: NotificationType.ArtTradeDeclined,
                    title: "Đề nghị trao đổi bị từ chối",
                    body: $"Họa sĩ {partnerName} đã từ chối lời mời trao đổi \"{trade.Title}\".",
                    refType: "ArtTrade",
                    refId: trade.Id,
                    dedupKey: $"ArtTradeDeclined:{trade.Id}:{trade.InitiatorId}",
                    cancellationToken: cancellationToken
                );
            }
            else
            {
                await _notifPublisher.PublishAsync(
                    userId: trade.InitiatorId,
                    notificationType: NotificationType.ArtTradeAccepted,
                    title: "Đối tác đã chấp nhận đề nghị trao đổi",
                    body: $"Họa sĩ {partnerName} đã chấp nhận \"{trade.Title}\". Hãy xem lại yêu cầu của họ và xác nhận để bắt đầu vẽ.",
                    refType: "ArtTrade",
                    refId: trade.Id,
                    dedupKey: $"ArtTradeReceiverAccepted:{trade.Id}:{trade.InitiatorId}",
                    cancellationToken: cancellationToken
                );
            }
        }
        catch
        {
            // Bỏ qua lỗi gửi thông báo để không ảnh hưởng giao dịch chính
        }

        return (true, ArtTradeDto.FromEntity(trade), []);
    }
}

