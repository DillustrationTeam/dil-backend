using ArtCommission.Application.ArtTrade.Common;
using ArtCommission.Application.ArtTrade.DTOs;
using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Application.Common.Interfaces;
using INotificationPublisher = ArtCommission.Application.Notifications.Common.INotificationPublisher;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtTrade.Commands;

public sealed record RemoveTradeArtworkCommand(
    Guid TradeId,
    Guid UserId
) : IRequest<(bool Success, ArtTradeDto? Data, string[] Errors)>;

public class RemoveTradeArtworkCommandValidator : AbstractValidator<RemoveTradeArtworkCommand>
{
    public RemoveTradeArtworkCommandValidator()
    {
        RuleFor(x => x.TradeId).NotEmpty().WithMessage("Mã giao dịch không hợp lệ.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Mã người dùng không hợp lệ.");
    }
}

public class RemoveTradeArtworkCommandHandler : IRequestHandler<RemoveTradeArtworkCommand, (bool Success, ArtTradeDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly IStorageService _storageService;
    private readonly INotificationPublisher _notifPublisher;

    public RemoveTradeArtworkCommandHandler(
        IApplicationDbContext db,
        IStorageService storageService,
        INotificationPublisher notifPublisher)
    {
        _db = db;
        _storageService = storageService;
        _notifPublisher = notifPublisher;
    }

    public async Task<(bool Success, ArtTradeDto? Data, string[] Errors)> Handle(
        RemoveTradeArtworkCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new RemoveTradeArtworkCommandValidator();
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

        var isInitiator = trade.InitiatorId == request.UserId;
        var isReceiver = trade.ReceiverId == request.UserId;
        if (!isInitiator && !isReceiver)
        {
            return (false, null, ["Bạn không tham gia vào giao dịch này."]);
        }

        if (trade.Status != TradeStatus.Accepted && trade.Status != TradeStatus.BothSubmitted)
        {
            return (false, null, ["Chỉ có thể gỡ tranh khi giao dịch đang vẽ tranh hoặc đang chờ xác nhận."]);
        }

        var now = DateTimeOffset.UtcNow;

        if (isInitiator)
        {
            if (string.IsNullOrEmpty(trade.InitiatorOriginalFileUrl))
            {
                return (false, null, ["Bạn chưa nộp tác phẩm nào để gỡ bỏ."]);
            }

            trade.InitiatorOriginalFileUrl = null;
            trade.InitiatorPreviewUrl = null;
            trade.InitiatorSubmittedAt = null;
            trade.InitiatorArtworkId = null;

            // Reset sự xác nhận của đối tác vì tranh đã bị gỡ
            trade.IsReceiverConfirmed = false;
            trade.ReceiverConfirmedAt = null;
        }
        else
        {
            if (string.IsNullOrEmpty(trade.ReceiverOriginalFileUrl))
            {
                return (false, null, ["Bạn chưa nộp tác phẩm nào để gỡ bỏ."]);
            }

            trade.ReceiverOriginalFileUrl = null;
            trade.ReceiverPreviewUrl = null;
            trade.ReceiverSubmittedAt = null;
            trade.ReceiverArtworkId = null;

            // Reset sự xác nhận của đối tác vì tranh đã bị gỡ
            trade.IsInitiatorConfirmed = false;
            trade.InitiatorConfirmedAt = null;
        }

        // Khi gỡ tranh, giao dịch quay về trạng thái Accepted (Đang vẽ/chờ nộp)
        trade.Status = TradeStatus.Accepted;
        trade.ConfirmationDeadlineAt = null;
        trade.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var recipientId = isInitiator ? trade.ReceiverId : trade.InitiatorId;
            var removerName = (isInitiator ? trade.Initiator?.FullName : trade.Receiver?.FullName) ?? "Đối tác";
            await _notifPublisher.PublishAsync(
                userId: recipientId,
                notificationType: NotificationType.ArtTradeArtworkSubmitted,
                title: "Đối tác đã gỡ tranh để thay thế",
                body: $"Họa sĩ {removerName} đã gỡ tác phẩm để nộp bản vẽ mới cho phiên \"{trade.Title}\".",
                refType: "ArtTrade",
                refId: trade.Id,
                dedupKey: $"ArtTradeArtworkRemoved:{trade.Id}:{request.UserId}",
                cancellationToken: cancellationToken
            );
        }
        catch
        {
            // Bỏ qua lỗi gửi thông báo để không gián đoạn thao tác
        }

        return (true, ArtTradeDto.FromEntity(trade, _storageService), []);
    }
}

