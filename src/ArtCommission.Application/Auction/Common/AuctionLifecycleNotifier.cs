using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Notifications.Common;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Application.Auction.Common;

/// <summary>Gửi thông báo lifecycle cho seller và watcher sau khi transaction nghiệp vụ commit.</summary>
public sealed class AuctionLifecycleNotifier : IAuctionLifecycleNotifier
{
    private readonly IApplicationDbContext _db;
    private readonly INotificationPublisher _notifications;
    private readonly ILogger<AuctionLifecycleNotifier> _logger;

    public AuctionLifecycleNotifier(
        IApplicationDbContext db,
        INotificationPublisher notifications,
        ILogger<AuctionLifecycleNotifier> logger)
    {
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task NotifyParticipantsAsync(
        Guid auctionId,
        AuctionLifecycleEvent lifecycleEvent,
        Guid? operationId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sellerId = await _db.Auctions.AsNoTracking()
                // Phiên Cancelled được soft-delete; vẫn phải thông báo kết thúc sau commit.
                .Where(a => a.Id == auctionId)
                .Select(a => (Guid?)a.SellerId)
                .FirstOrDefaultAsync(cancellationToken);
            if (!sellerId.HasValue)
            {
                return;
            }

            var watchers = await _db.AuctionWatches.AsNoTracking()
                .Where(w => w.AuctionId == auctionId && !w.IsDeleted)
                .Select(w => w.UserId)
                .ToListAsync(cancellationToken);
            var (title, body) = lifecycleEvent switch
            {
                AuctionLifecycleEvent.Started => ("Phiên đấu giá bắt đầu", "Phiên đấu giá bạn theo dõi đã bắt đầu."),
                AuctionLifecycleEvent.Ended => ("Phiên đấu giá đã kết thúc", "Phiên đấu giá bạn theo dõi đã kết thúc."),
                _ => ("Phiên đấu giá được gia hạn", "Có lượt đặt giá hợp lệ trong 3 phút cuối; thời gian kết thúc được cộng thêm 3 phút.")
            };
            var phase = lifecycleEvent.ToString();

            foreach (var userId in watchers.Append(sellerId.Value).Distinct().Where(id => id != Guid.Empty))
            {
                var operationPart = operationId.HasValue ? $":{operationId.Value}" : string.Empty;
                var dedupKey = $"Auction{phase}:{auctionId}{operationPart}:{userId}";
                try
                {
                    await _notifications.PublishAsync(
                        userId,
                        NotificationType.AuctionLifecycle,
                        title,
                        body,
                        "Auction",
                        auctionId,
                        NotificationChannel.InApp,
                        dedupKey,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Không gửi được thông báo {Phase} cho auctionId={AuctionId}, userId={UserId}.",
                        phase, auctionId, userId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Không đọc được người nhận thông báo {Phase} cho auctionId={AuctionId}.",
                lifecycleEvent, auctionId);
        }
    }
}
