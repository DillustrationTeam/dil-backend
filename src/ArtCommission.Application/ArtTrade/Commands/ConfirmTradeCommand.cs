using ArtCommission.Application.ArtTrade.Common;
using ArtCommission.Application.ArtTrade.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Enums;
using ArtTradeEntity = ArtCommission.Domain.Entities.Trade.ArtTrade;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtTrade.Commands;

public sealed record ConfirmTradeCommand(
    Guid TradeId,
    Guid UserId
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

    public ConfirmTradeCommandHandler(IApplicationDbContext db)
    {
        _db = db;
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

        if (trade.Status != TradeStatus.BothSubmitted)
        {
            return (false, null, ["Chỉ có thể xác nhận hài lòng khi cả 2 bên đã nộp tranh (BothSubmitted)."]);
        }

        var now = DateTimeOffset.UtcNow;

        if (isInitiator)
        {
            trade.IsInitiatorConfirmed = true;
            trade.InitiatorConfirmedAt = now;
        }
        else
        {
            trade.IsReceiverConfirmed = true;
            trade.ReceiverConfirmedAt = now;
        }

        trade.UpdatedAt = now;

        // Nếu cả 2 bên cùng xác nhận hài lòng -> Hoàn tất giao dịch
        if (trade.IsInitiatorConfirmed && trade.IsReceiverConfirmed)
        {
            trade.Status = TradeStatus.Completed;
            trade.CompletedAt = now;

            // Chuyển quyền sở hữu trên sổ cái ArtworkOwnership (nếu có artwork tương ứng)
            await TransferOwnershipIfApplicableAsync(trade, now, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (true, TradeMapper.ToDto(trade), []);
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

