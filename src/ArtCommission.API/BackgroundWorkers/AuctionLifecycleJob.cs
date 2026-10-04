using ArtCommission.Application.Auction.Commands.SettleAuction;
using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.API.BackgroundWorkers;

/// <summary>Idempotent auction lifecycle transitions, scheduled once per minute by Hangfire.</summary>
public sealed class AuctionLifecycleJob
{
    private readonly IApplicationDbContext _db;
    private readonly IMediator _mediator;
    private readonly IAuctionLifecycleNotifier _lifecycleNotifier;
    private readonly IAuctionRealtimePublisher _realtime;
    private readonly ILogger<AuctionLifecycleJob> _logger;

    public AuctionLifecycleJob(
        IApplicationDbContext db,
        IMediator mediator,
        IAuctionLifecycleNotifier lifecycleNotifier,
        IAuctionRealtimePublisher realtime,
        ILogger<AuctionLifecycleJob> logger)
    {
        _db = db;
        _mediator = mediator;
        _lifecycleNotifier = lifecycleNotifier;
        _realtime = realtime;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var scheduledIds = await _db.Auctions.AsNoTracking()
            .Where(a => !a.IsDeleted && a.Status == AuctionStatus.Scheduled && a.StartAt <= now)
            .Select(a => a.Id).ToListAsync(cancellationToken);

        foreach (var auctionId in scheduledIds)
        {
            var changed = await TransitionAsync(
                auctionId, AuctionStatus.Scheduled, AuctionStatus.Active,
                a => a.StartAt <= now, now, cancellationToken);
            if (changed)
            {
                await BroadcastStatusAsync(auctionId, AuctionStatus.Active, cancellationToken);
            }
        }

        var endingIds = await _db.Auctions.AsNoTracking()
            .Where(a => !a.IsDeleted && a.Status == AuctionStatus.Active && a.EndAt <= now)
            .Select(a => a.Id).ToListAsync(cancellationToken);

        foreach (var auctionId in endingIds)
        {
            var changed = await TransitionAsync(
                auctionId, AuctionStatus.Active, AuctionStatus.Ended,
                a => a.EndAt <= now, now, cancellationToken);
            if (!changed)
            {
                continue;
            }

            await BroadcastStatusAsync(auctionId, AuctionStatus.Ended, cancellationToken);
        }

        // Quét mọi phiên Ended quá hạn ở mỗi lượt chạy, kể cả phiên đã chuyển trạng
        // thái ở lượt trước nhưng process dừng trước settlement. Settle handler là
        // idempotent nên Hangfire có thể retry an toàn.
        var dueSettlementIds = await _db.Auctions.AsNoTracking()
            .Where(a => !a.IsDeleted && a.Status == AuctionStatus.Ended && a.EndAt <= now)
            .Select(a => a.Id).ToListAsync(cancellationToken);

        foreach (var auctionId in dueSettlementIds)
        {
            await _mediator.Send(
                new SettleAuctionCommand(Guid.Empty, auctionId, IsAdministrator: true, Force: false),
                cancellationToken);

            // SettleAuctionCommand xử lý bid, ví, ownership và ledger trong transaction
            // riêng; chạy lại sẽ trả kết quả cũ mà không chuyển tiền/bàn giao lần nữa.
            await BroadcastCurrentStatusAsync(auctionId, cancellationToken);
        }

        // Bù trường hợp process dừng sau commit nhưng trước khi ghi thông báo. Dedup key
        // ổn định làm cho lần quét sau chỉ tạo mỗi thông báo một lần.
        await PublishMissingLifecycleNotificationsAsync(now, cancellationToken);
    }

    private async Task<bool> TransitionAsync(
        Guid auctionId,
        AuctionStatus from,
        AuctionStatus to,
        System.Linq.Expressions.Expression<Func<Auction, bool>> duePredicate,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var affected = await _db.Auctions
            .Where(a => a.Id == auctionId && !a.IsDeleted && a.Status == from)
            .Where(duePredicate)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(a => a.Status, to)
                .SetProperty(a => a.UpdatedAt, now), cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return affected == 1;
    }

    private async Task BroadcastStatusAsync(Guid auctionId, AuctionStatus status, CancellationToken cancellationToken)
    {
        var auction = await _db.Auctions.AsNoTracking()
            .Where(a => a.Id == auctionId && !a.IsDeleted)
            .Select(a => new { a.CurrentPrice, a.EndAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (auction is null)
        {
            return;
        }

        try
        {
            await _realtime.PublishAuctionStatusChangedAsync(
                auctionId, status.ToString(), auction.CurrentPrice, auction.EndAt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không broadcast được trạng thái auctionId={AuctionId}.", auctionId);
        }
    }

    private async Task BroadcastCurrentStatusAsync(Guid auctionId, CancellationToken cancellationToken)
    {
        var auction = await _db.Auctions.AsNoTracking()
            .Where(a => a.Id == auctionId && !a.IsDeleted)
            .Select(a => new { a.Status, a.CurrentPrice, a.EndAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (auction is null)
        {
            return;
        }

        try
        {
            await _realtime.PublishAuctionStatusChangedAsync(
                auctionId, auction.Status.ToString(), auction.CurrentPrice, auction.EndAt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không broadcast được trạng thái sau settlement auctionId={AuctionId}.", auctionId);
        }
    }

    private async Task PublishMissingLifecycleNotificationsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var current = await _db.Auctions.AsNoTracking()
            .Where(a => a.Status == AuctionStatus.Cancelled
                || (!a.IsDeleted && (
                    (a.Status == AuctionStatus.Active && a.StartAt <= now)
                    || ((a.Status == AuctionStatus.Ended || a.Status == AuctionStatus.Settled) && a.EndAt <= now))))
            .Select(a => new { a.Id, a.SellerId, a.Status, a.StartAt, a.EndAt })
            .ToListAsync(cancellationToken);

        foreach (var auction in current)
        {
            var lifecycleEvent = auction.Status == AuctionStatus.Active
                ? AuctionLifecycleEvent.Started
                : AuctionLifecycleEvent.Ended;
            await _lifecycleNotifier.NotifyParticipantsAsync(auction.Id, lifecycleEvent, cancellationToken: cancellationToken);
        }
    }
}
