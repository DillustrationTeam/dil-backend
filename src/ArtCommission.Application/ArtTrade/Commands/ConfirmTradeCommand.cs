using ArtCommission.Application.ArtTrade.Common;
using ArtCommission.Application.ArtTrade.DTOs;
using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Application.Common.Interfaces;
using INotificationPublisher = ArtCommission.Application.Notifications.Common.INotificationPublisher;
using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Enums;
using ArtTradeEntity = ArtCommission.Domain.Entities.Trade.ArtTrade;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtTrade.Commands;

public sealed record ConfirmTradeCommand(
    Guid TradeId,
    Guid UserId,
    string? Side = null
) : IRequest<(bool Success, ArtTradeDto? Data, string[] Errors)>;

public class ConfirmTradeCommandValidator : AbstractValidator<ConfirmTradeCommand>
{
    public ConfirmTradeCommandValidator()
    {
        RuleFor(x => x.TradeId).NotEmpty().WithMessage("Mã giao dịch không hợp lệ.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Mã người dùng không hợp lệ.");
    }
}

public class ConfirmTradeCommandHandler : IRequestHandler<ConfirmTradeCommand, (bool Success, ArtTradeDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly INotificationPublisher _notifPublisher;
    private readonly IStorageService _storageService;

    public ConfirmTradeCommandHandler(
        IApplicationDbContext db,
        INotificationPublisher notifPublisher,
        IStorageService storageService)
    {
        _db = db;
        _notifPublisher = notifPublisher;
        _storageService = storageService;
    }

    public async Task<(bool Success, ArtTradeDto? Data, string[] Errors)> Handle(ConfirmTradeCommand request, CancellationToken cancellationToken)
    {
        var validator = new ConfirmTradeCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, null, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var trade = await _db.ArtTrades
            .Include(t => t.Initiator)
            .Include(t => t.Receiver)
            .Include(t => t.InitiatorArtwork)
            .Include(t => t.ReceiverArtwork)
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

        if (trade.Status != TradeStatus.BothSubmitted && trade.Status != TradeStatus.Accepted)
        {
            return (false, null, ["Chỉ có thể xác nhận hài lòng khi giao dịch đang vẽ tranh hoặc đã nộp tranh."]);
        }

        var now = DateTimeOffset.UtcNow;
        var sideNorm = request.Side?.Trim().ToLowerInvariant();

        if (sideNorm == "initiator")
        {
            trade.IsInitiatorConfirmed = true;
            trade.InitiatorConfirmedAt = now;
        }
        else if (sideNorm == "receiver")
        {
            trade.IsReceiverConfirmed = true;
            trade.ReceiverConfirmedAt = now;
        }
        else
        {
            if (isInitiator)
            {
                trade.IsInitiatorConfirmed = true;
                trade.InitiatorConfirmedAt = now;
            }
            if (isReceiver)
            {
                trade.IsReceiverConfirmed = true;
                trade.ReceiverConfirmedAt = now;
            }
        }

        trade.UpdatedAt = now;

        // Nếu cả 2 bên cùng xác nhận hài lòng VÀ cả 2 bên đã nộp file -> Hoàn tất giao dịch
        var bothSubmitted = !string.IsNullOrEmpty(trade.InitiatorOriginalFileUrl) && !string.IsNullOrEmpty(trade.ReceiverOriginalFileUrl);
        if (trade.IsInitiatorConfirmed && trade.IsReceiverConfirmed && bothSubmitted)
        {
            trade.Status = TradeStatus.Completed;
            trade.CompletedAt = now;

            // Chuyển quyền sở hữu trên sổ cái ArtworkOwnership (nếu có artwork tương ứng)
            await TransferOwnershipIfApplicableAsync(trade, now, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (trade.Status == TradeStatus.Completed)
        {
            try
            {
                var title = "Giao dịch trao đổi tranh hoàn tất! 🎉";
                var body = $"Cả hai bên đã xác nhận hài lòng cho phiên \"{trade.Title}\". Bạn có thể tải file tranh gốc và tác phẩm đã được thêm vào bộ sưu tập sở hữu của bạn.";

                await _notifPublisher.PublishAsync(
                    userId: trade.InitiatorId,
                    notificationType: NotificationType.ArtTradeCompleted,
                    title: title,
                    body: body,
                    refType: "ArtTrade",
                    refId: trade.Id,
                    dedupKey: $"ArtTradeCompleted:{trade.Id}:{trade.InitiatorId}",
                    cancellationToken: cancellationToken
                );

                await _notifPublisher.PublishAsync(
                    userId: trade.ReceiverId,
                    notificationType: NotificationType.ArtTradeCompleted,
                    title: title,
                    body: body,
                    refType: "ArtTrade",
                    refId: trade.Id,
                    dedupKey: $"ArtTradeCompleted:{trade.Id}:{trade.ReceiverId}",
                    cancellationToken: cancellationToken
                );
            }
            catch
            {
                // Bỏ qua lỗi gửi thông báo để không ảnh hưởng đến giao dịch hoàn tất
            }
        }

        return (true, ArtTradeDto.FromEntity(trade, _storageService), []);
    }

    private async Task TransferOwnershipIfApplicableAsync(
        ArtTradeEntity trade,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // 1. Chuyển quyền sở hữu tranh A vẽ (InitiatorArtwork) sang cho B (ReceiverId)
        if (trade.InitiatorArtworkId.HasValue)
        {
            await ReassignCurrentOwnerAsync(trade.InitiatorArtworkId.Value, trade.ReceiverId, trade.Id, now, cancellationToken);
        }

        // 2. Chuyển quyền sở hữu tranh B vẽ (ReceiverArtwork) sang cho A (InitiatorId)
        if (trade.ReceiverArtworkId.HasValue)
        {
            await ReassignCurrentOwnerAsync(trade.ReceiverArtworkId.Value, trade.InitiatorId, trade.Id, now, cancellationToken);
        }
    }

    private async Task ReassignCurrentOwnerAsync(
        Guid artworkId,
        Guid newOwnerId,
        Guid tradeId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var currentOwnerships = await _db.ArtworkOwnerships
            .Where(o => o.ArtworkId == artworkId && o.IsCurrent && !o.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var oldOwnership in currentOwnerships)
        {
            if (oldOwnership.OwnerId == newOwnerId)
            {
                return; // Đã là chủ sở hữu
            }

            oldOwnership.IsCurrent = false;
            oldOwnership.ReleasedAt = now;
            oldOwnership.UpdatedAt = now;
        }

        var newOwnership = new ArtworkOwnership
        {
            ArtworkId = artworkId,
            OwnerId = newOwnerId,
            AcquiredAt = now,
            TransferReason = OwnershipTransferReason.ArtTrade,
            ArtTradeId = tradeId,
            IsCurrent = true
        };

        _db.ArtworkOwnerships.Add(newOwnership);
    }
}

