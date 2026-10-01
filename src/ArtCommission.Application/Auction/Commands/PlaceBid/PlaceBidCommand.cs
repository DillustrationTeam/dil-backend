using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Notifications.Common;
using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using INotificationPublisher = ArtCommission.Application.Notifications.Common.INotificationPublisher;

namespace ArtCommission.Application.Auction.Commands.PlaceBid;

/// <summary>UC32 — đặt giá thường hoặc đăng ký/thay trần proxy-bid trong một transaction.</summary>
public record PlaceBidCommand(
    Guid UserId,
    Guid AuctionId,
    decimal Amount,
    bool IsAuto = false,
    decimal? MaxAutoBid = null
) : IRequest<(bool Success, PlaceBidResultDto? Data, string[] Errors, AuctionBidConflictDto? Conflict)>;

public class PlaceBidCommandValidator : AbstractValidator<PlaceBidCommand>
{
    public PlaceBidCommandValidator()
    {
        RuleFor(x => x.AuctionId).NotEmpty().WithMessage("Thiếu mã phiên đấu giá.");
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .When(x => !x.IsAuto)
            .WithMessage("Số tiền đặt giá phải lớn hơn 0.");
        RuleFor(x => x.MaxAutoBid)
            .NotNull()
            .GreaterThan(0)
            .When(x => x.IsAuto)
            .WithMessage("Đặt giá tự động phải kèm trần giá lớn hơn 0.");
    }
}

public sealed class PlaceBidCommandHandler
    : IRequestHandler<PlaceBidCommand, (bool Success, PlaceBidResultDto? Data, string[] Errors, AuctionBidConflictDto? Conflict)>
{
    private static readonly TimeSpan AntiSnipeWindow = TimeSpan.FromMinutes(3);
    private const int MaxLimit = 100;

    private readonly IApplicationDbContext _db;
    private readonly IAuctionMoneyService _moneyService;
    private readonly INotificationPublisher _notifications;
    private readonly IAuctionRealtimePublisher _realtime;
    private readonly IAuctionLifecycleNotifier _lifecycleNotifier;
    private readonly ILogger<PlaceBidCommandHandler> _logger;

    public PlaceBidCommandHandler(
        IApplicationDbContext db,
        IAuctionMoneyService moneyService,
        INotificationPublisher notifications,
        IAuctionRealtimePublisher realtime,
        IAuctionLifecycleNotifier lifecycleNotifier,
        ILogger<PlaceBidCommandHandler> logger)
    {
        _db = db;
        _moneyService = moneyService;
        _notifications = notifications;
        _realtime = realtime;
        _lifecycleNotifier = lifecycleNotifier;
        _logger = logger;
    }

    public async Task<(bool Success, PlaceBidResultDto? Data, string[] Errors, AuctionBidConflictDto? Conflict)> Handle(
        PlaceBidCommand request,
        CancellationToken cancellationToken)
    {
        var validation = new PlaceBidCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, validation.Errors.Select(e => e.ErrorMessage).ToArray(), null);
        }

        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            var auction = await _db.Auctions.FirstOrDefaultAsync(
                a => a.Id == request.AuctionId && !a.IsDeleted,
                cancellationToken);

            if (auction is null)
            {
                return (false, null, ["Không tìm thấy phiên đấu giá."], null);
            }

            var now = DateTimeOffset.UtcNow;
            if (auction.Status == AuctionStatus.Active && auction.EndAt <= now)
            {
                auction.Status = AuctionStatus.Ended;
                auction.UpdatedAt = now;
                await _db.SaveChangesAsync(cancellationToken);
            }

            if (auction.Status != AuctionStatus.Active || auction.StartAt > now)
            {
                return (false, null,
                    [$"Phiên không nhận đặt giá (trạng thái: {AuctionMapper.DescribeStatus(auction.Status)})."], null);
            }

            if (auction.SellerId == request.UserId)
            {
                return (false, null, ["Người bán không thể tự đặt giá cho phiên của mình."], null);
            }

            var oldLeading = await _db.Bids.FirstOrDefaultAsync(
                b => b.AuctionId == auction.Id && b.Status == BidStatus.Leading && !b.IsDeleted,
                cancellationToken);
            var minimumNextBid = auction.BidCount == 0
                ? auction.StartPrice
                : auction.CurrentPrice + auction.BidStep;
            var requestedCap = request.IsAuto ? request.MaxAutoBid!.Value : request.Amount;

            if (requestedCap < minimumNextBid)
            {
                // Request dùng mức giá vừa thắng nhưng đến sau commit khác: coi đây là
                // xung đột stale-price và trả snapshot mới, không gộp thành lỗi validation.
                if (!request.IsAuto && oldLeading is not null && requestedCap <= auction.CurrentPrice)
                {
                    return (false, null,
                        ["Phiên đấu giá vừa được cập nhật bởi một thao tác khác. Hãy kiểm tra giá mới."],
                        new AuctionBidConflictDto(
                            auction.CurrentPrice,
                            minimumNextBid,
                            auction.EndAt,
                            auction.Status.ToString()));
                }

                return (false, null,
                    [$"Giá đặt tối thiểu là {minimumNextBid:N0} VND."], null);
            }

            // Không tiết lộ reserve price. Giữ nguyên quy tắc hiện tại: mức trần phải đủ
            // điều kiện bán, nhưng lỗi không kèm con số giá sàn bí mật.
            if (auction.ReservePrice.HasValue && requestedCap < auction.ReservePrice.Value)
            {
                return (false, null,
                    ["Giá đặt chưa đạt mức tối thiểu mà người bán yêu cầu cho phiên này."], null);
            }

            var autoBids = await _db.AuctionAutoBids
                .Where(x => x.AuctionId == auction.Id && !x.IsDeleted)
                .ToListAsync(cancellationToken);

            var ownAutoBid = autoBids.FirstOrDefault(x => x.BidderId == request.UserId);
            if (request.IsAuto)
            {
                if (ownAutoBid is null)
                {
                    ownAutoBid = new AuctionAutoBid
                    {
                        AuctionId = auction.Id,
                        BidderId = request.UserId,
                        MaxAmount = requestedCap,
                        RegisteredAt = now
                    };
                    autoBids.Add(ownAutoBid);
                    _db.AuctionAutoBids.Add(ownAutoBid);
                }
                else
                {
                    ownAutoBid.MaxAmount = requestedCap;
                    ownAutoBid.UpdatedAt = now;
                }
            }

            var candidates = new Dictionary<Guid, ProxyCandidate>();
            foreach (var autoBid in autoBids)
            {
                candidates[autoBid.BidderId] = new ProxyCandidate(
                    autoBid.BidderId,
                    autoBid.MaxAmount,
                    autoBid.RegisteredAt,
                    true);
            }

            if (oldLeading is not null)
            {
                var leaderCap = candidates.TryGetValue(oldLeading.BidderId, out var savedCap)
                    ? Math.Max(oldLeading.Amount, savedCap.MaxAmount)
                    : oldLeading.Amount;
                candidates[oldLeading.BidderId] = new ProxyCandidate(
                    oldLeading.BidderId,
                    leaderCap,
                    savedCap?.RegisteredAt ?? oldLeading.PlacedAt,
                    savedCap?.IsAuto ?? oldLeading.IsAuto);
            }

            var ownRegistration = ownAutoBid?.RegisteredAt ?? now;
            if (candidates.TryGetValue(request.UserId, out var ownCandidate))
            {
                candidates[request.UserId] = ownCandidate with
                {
                    MaxAmount = Math.Max(ownCandidate.MaxAmount, requestedCap),
                    RegisteredAt = ownCandidate.RegisteredAt
                };
            }
            else
            {
                candidates[request.UserId] = new ProxyCandidate(
                    request.UserId,
                    requestedCap,
                    ownRegistration,
                    request.IsAuto);
            }

            var ranked = candidates.Values
                .OrderByDescending(x => x.MaxAmount)
                .ThenBy(x => x.RegisteredAt)
                .ThenBy(x => x.BidderId)
                .ToList();
            var winner = ranked[0];
            var runnerUp = ranked.Count > 1 ? ranked[1] : null;

            decimal clearingPrice;
            if (oldLeading is null)
            {
                clearingPrice = !request.IsAuto && winner.BidderId == request.UserId
                    ? request.Amount
                    : auction.StartPrice;
            }
            else if (winner.BidderId == request.UserId && !request.IsAuto)
            {
                // Bid thường là giá người dùng chọn trả; proxy-bid mới dùng giá vừa đủ thắng.
                clearingPrice = request.Amount;
            }
            else
            {
                var proxyPrice = runnerUp is null
                    ? oldLeading.Amount
                    : runnerUp.MaxAmount + auction.BidStep;
                clearingPrice = Math.Min(winner.MaxAmount, Math.Max(minimumNextBid, proxyPrice));
            }

            if (auction.ReservePrice.HasValue && winner.MaxAmount >= auction.ReservePrice.Value)
            {
                clearingPrice = Math.Max(clearingPrice, auction.ReservePrice.Value);
            }

            clearingPrice = decimal.Round(clearingPrice, 2, MidpointRounding.AwayFromZero);
            if (clearingPrice > winner.MaxAmount)
            {
                return (false, null, ["Trần giá không đủ để vượt lượt đặt giá hiện tại."], null);
            }

            var leaderChanged = oldLeading is null
                || winner.BidderId != oldLeading.BidderId
                || clearingPrice > oldLeading.Amount
                || oldLeading.BidderId == request.UserId;

            var oldEndAt = auction.EndAt;
            Bid requestBid;
            if (leaderChanged)
            {
                if (oldLeading is not null)
                {
                    oldLeading.Status = BidStatus.Outbid;
                    oldLeading.UpdatedAt = now;
                    await _moneyService.RefundDepositAsync(
                        oldLeading, "Bị đè giá bởi lượt đặt giá cao hơn.", cancellationToken);
                    // Rời filtered unique index trước khi chèn bid dẫn đầu kế tiếp.
                    await _db.SaveChangesAsync(cancellationToken);
                }

                var winnerBid = CreateBid(auction.Id, winner, clearingPrice, now, request);
                var (held, _) = await _moneyService.HoldDepositAsync(
                    winner.BidderId, winnerBid.Id, clearingPrice, cancellationToken);
                if (!held.Success)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return (false, null, held.Errors, null);
                }

                winnerBid.HoldAmount = clearingPrice;
                winnerBid.HoldStatus = HoldStatus.Held;
                _db.Bids.Add(winnerBid);
                requestBid = winner.BidderId == request.UserId
                    ? winnerBid
                    : CreateBid(auction.Id, CandidateForRequest(request, requestedCap, ownRegistration), RequestBidAmount(request, requestedCap, minimumNextBid), now, request, BidStatus.Outbid);

                if (winner.BidderId != request.UserId)
                {
                    _db.Bids.Add(requestBid);
                }

                auction.CurrentPrice = clearingPrice;
            }
            else
            {
                requestBid = CreateBid(
                    auction.Id,
                    CandidateForRequest(request, requestedCap, ownRegistration),
                    RequestBidAmount(request, requestedCap, minimumNextBid),
                    now,
                    request,
                    BidStatus.Outbid);
                _db.Bids.Add(requestBid);
            }

            auction.BidCount += leaderChanged && winner.BidderId != request.UserId ? 2 : 1;
            auction.UpdatedAt = now;

            var extended = auction.EndAt > now && auction.EndAt - now <= AntiSnipeWindow;
            if (extended)
            {
                auction.EndAt = auction.EndAt.Add(AntiSnipeWindow);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            // Snapshot luôn thuộc người gọi. Proxy-bid có thể tự nâng một bidder khác;
            // tuyệt đối không trả số dư của người đang/được dẫn đầu cho requestor.
            var requestWallet = await _db.Wallets.AsNoTracking()
                .FirstOrDefaultAsync(w => w.UserId == request.UserId && !w.IsDeleted, cancellationToken);
            var bidDto = AuctionMapper.ToDto(requestBid);
            var result = new PlaceBidResultDto(
                bidDto,
                new WalletSnapshotDto(requestWallet?.Balance ?? 0m, requestWallet?.LockedBalance ?? 0m));

            _logger.LogInformation(
                "Đặt giá thành công: auctionId={AuctionId}, bidId={BidId}, bidderId={BidderId}, amount={Amount}, leaderId={LeaderId}, currentPrice={CurrentPrice}.",
                auction.Id, requestBid.Id, request.UserId, requestBid.Amount, winner.BidderId, auction.CurrentPrice);

            if (extended)
            {
                _logger.LogInformation(
                    "Auction anti-snipe extension: auctionId={AuctionId}, bidId={BidId}, oldEndAt={OldEndAt}, newEndAt={NewEndAt}.",
                    auction.Id, requestBid.Id, oldEndAt, auction.EndAt);
                await _lifecycleNotifier.NotifyParticipantsAsync(
                    auction.Id, AuctionLifecycleEvent.Extended, requestBid.Id, cancellationToken);
            }

            try
            {
                await _realtime.PublishBidPlacedAsync(
                    auction.Id, result, auction.CurrentPrice,
                    auction.BidCount == 0 ? auction.StartPrice : auction.CurrentPrice + auction.BidStep,
                    auction.EndAt, cancellationToken);
                if (extended)
                {
                    await _realtime.PublishAuctionExtendedAsync(
                        auction.Id, requestBid.Id, oldEndAt, auction.EndAt, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không broadcast được bid đã commit cho auctionId={AuctionId}.", auction.Id);
            }

            if (oldLeading is not null && oldLeading.BidderId != winner.BidderId)
            {
                await PublishOutbidNotificationAsync(oldLeading, winnerBidderName: null, auction, requestBid.Id, cancellationToken);
            }

            return (true, result, [], null);
        }
        catch (Exception ex) when (AuctionConcurrency.IsExpectedConflict(ex))
        {
            _logger.LogInformation(ex, "Bid cạnh tranh bị từ chối: auctionId={AuctionId}, bidderId={BidderId}.", request.AuctionId, request.UserId);
            var latest = await _db.Auctions.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == request.AuctionId && !a.IsDeleted, cancellationToken);
            if (latest is null)
            {
                return (false, null, ["Phiên đấu giá đã thay đổi hoặc không còn tồn tại."], null);
            }

            return (false, null, ["Phiên đấu giá vừa được cập nhật bởi một thao tác khác. Hãy kiểm tra giá mới."],
                new AuctionBidConflictDto(
                    latest.CurrentPrice,
                    latest.BidCount == 0 ? latest.StartPrice : latest.CurrentPrice + latest.BidStep,
                    latest.EndAt,
                    latest.Status.ToString()));
        }
    }

    private async Task PublishOutbidNotificationAsync(
        Bid oldLeading,
        string? winnerBidderName,
        Domain.Entities.Auction.Auction auction,
        Guid bidId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.PublishAsync(
                oldLeading.BidderId,
                NotificationType.OutbidAlert,
                "Bạn vừa bị đè giá",
                "Có người đặt giá cao hơn cho phiên bạn đang dẫn đầu. Tiền cọc đã được hoàn về ví.",
                nameof(Domain.Entities.Auction.Auction),
                auction.Id,
                NotificationChannel.InApp,
                $"Outbid:{auction.Id}:{oldLeading.BidderId}:{bidId}",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không gửi được thông báo đè giá auctionId={AuctionId}, userId={UserId}.", auction.Id, oldLeading.BidderId);
        }
    }

    private static ProxyCandidate CandidateForRequest(PlaceBidCommand request, decimal cap, DateTimeOffset registeredAt) =>
        new(request.UserId, cap, registeredAt, request.IsAuto);

    private static decimal RequestBidAmount(PlaceBidCommand request, decimal cap, decimal minimumNextBid) =>
        request.IsAuto ? Math.Min(cap, minimumNextBid) : request.Amount;

    private static Bid CreateBid(
        Guid auctionId,
        ProxyCandidate candidate,
        decimal amount,
        DateTimeOffset now,
        PlaceBidCommand request,
        BidStatus status = BidStatus.Leading) => new()
    {
        AuctionId = auctionId,
        BidderId = candidate.BidderId,
        Amount = amount,
        HoldAmount = status == BidStatus.Leading ? amount : 0m,
        Status = status,
        HoldStatus = HoldStatus.None,
        IsAuto = candidate.IsAuto,
        MaxAutoBid = candidate.IsAuto ? candidate.MaxAmount : request.IsAuto && candidate.BidderId == request.UserId ? request.MaxAutoBid : null,
        PlacedAt = now
    };

    private sealed record ProxyCandidate(Guid BidderId, decimal MaxAmount, DateTimeOffset RegisteredAt, bool IsAuto);
}
