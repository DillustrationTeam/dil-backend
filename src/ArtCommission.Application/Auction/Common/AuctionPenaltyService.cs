using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Notifications.Common;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Application.Auction.Common;

/// <summary>
/// Seller penalty ladder for confirmed auction delivery disputes.
///
/// Two payout situations share one ladder:
///   - escrow still held (buyer got the money back)      -> first case enters at step 1
///   - escrow already released to the seller             -> first case enters at step 3
///
/// Steps 3 and 4 add a surcharge on the platform fee of the next sold sessions,
/// steps 2 and 4 suspend the right to create auction sessions, step 5 is permanent.
/// </summary>
public interface IAuctionPenaltyService
{
    /// <summary>Extra percentage points charged on top of the platform fee for this seller, 0 when none.</summary>
    Task<decimal> GetActiveSurchargeAsync(Guid sellerId, CancellationToken cancellationToken = default);

    /// <summary>Creates the penalty entry for a dispute that was confirmed in favour of the buyer.</summary>
    Task<AuctionPenalty?> ApplyConfirmedDisputeAsync(
        Domain.Entities.Auction.Auction auction,
        AuctionDelivery delivery,
        bool escrowReleased,
        Guid? adminUserId,
        string? reason,
        CancellationToken cancellationToken = default);

    /// <summary>Counts one sold session against every active surcharge penalty of the seller.</summary>
    Task ConsumeSessionAsync(Guid sellerId, CancellationToken cancellationToken = default);

    /// <summary>Tells whether the seller may create a new auction session right now.</summary>
    Task<(bool Blocked, string? Reason)> GetSellBlockAsync(Guid sellerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuctionPenalty>> GetPenaltiesAsync(Guid sellerId, CancellationToken cancellationToken = default);
}

public class AuctionPenaltyService : IAuctionPenaltyService
{
    private const int MaxStep = 5;
    private const int EscrowReleasedJump = 2;

    private const decimal DefaultSurchargePercent = 20m;
    private const decimal DefaultWindowSessions = 5m;
    private const decimal DefaultSuspensionDays = 7m;
    private const int WarningOnlySuspensionDays = 1;

    private readonly IApplicationDbContext _db;
    private readonly IWalletService _walletService;
    private readonly INotificationPublisher _notifications;
    private readonly ILogger<AuctionPenaltyService> _logger;

    public AuctionPenaltyService(
        IApplicationDbContext db,
        IWalletService walletService,
        INotificationPublisher notifications,
        ILogger<AuctionPenaltyService> logger)
    {
        _db = db;
        _walletService = walletService;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<decimal> GetActiveSurchargeAsync(Guid sellerId, CancellationToken cancellationToken = default)
    {
        var surcharge = await _db.AuctionPenalties
            .AsNoTracking()
            .Where(p => p.SellerId == sellerId
                        && !p.IsDeleted
                        && p.Status == PenaltyStatus.Active
                        && p.SurchargePercent != null
                        && p.SurchargePercent > 0m)
            .OrderByDescending(p => p.SurchargePercent)
            .Select(p => p.SurchargePercent)
            .FirstOrDefaultAsync(cancellationToken);

        return surcharge ?? 0m;
    }

    public async Task<AuctionPenalty?> ApplyConfirmedDisputeAsync(
        Domain.Entities.Auction.Auction auction,
        AuctionDelivery delivery,
        bool escrowReleased,
        Guid? adminUserId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        // Idempotent: a resolved dispute must never escalate twice, even when the admin
        // presses the decision button again after a failed request.
        var already = await _db.AuctionPenalties
            .FirstOrDefaultAsync(p => p.SourceDeliveryId == delivery.Id && !p.IsDeleted, cancellationToken);

        if (already is not null)
        {
            _logger.LogInformation(
                "Delivery {DeliveryId} already produced penalty {PenaltyId} — skip.",
                delivery.Id, already.Id);
            return already;
        }

        // Bậc mới tính từ BẬC CAO NHẤT đã có, không phải từ số lượng án phạt. Trước đây dùng
        // Count() nên khi có án phạt bị thu hồi (hoặc dữ liệu nhập tay), bậc mới có thể thấp
        // hơn bậc seller đang chịu — lịch sử phạt đi lùi và mức phạt hiển thị sai.
        var highestStep = await _db.AuctionPenalties
            .Where(p => p.SellerId == auction.SellerId
                        && !p.IsDeleted
                        && p.Status != PenaltyStatus.Revoked)
            .Select(p => (int?)p.StrikeLevel)
            .MaxAsync(cancellationToken) ?? 0;

        var step = Math.Min(MaxStep,
            Math.Max(1, highestStep + 1 + (escrowReleased ? EscrowReleasedJump : 0)));

        var surchargePercent = await _walletService.GetDecimalConfigAsync(
            PlatformConfigKeys.AuctionPenaltySurchargePercent, DefaultSurchargePercent, cancellationToken);
        var windowSessions = (int)await _walletService.GetDecimalConfigAsync(
            PlatformConfigKeys.AuctionPenaltyWindowSessions, DefaultWindowSessions, cancellationToken);
        var suspensionDays = (int)await _walletService.GetDecimalConfigAsync(
            PlatformConfigKeys.AuctionPenaltySuspensionDays, DefaultSuspensionDays, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var penalty = new AuctionPenalty
        {
            SellerId = auction.SellerId,
            StrikeLevel = step,
            EscrowReleasedAtStrike = escrowReleased,
            SourceAuctionId = auction.Id,
            SourceDeliveryId = delivery.Id,
            SourceDisputeResolvedByAdminId = adminUserId,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            Status = PenaltyStatus.Active,
        };

        switch (step)
        {
            case 1:
                // Warning only: the buyer already got the money back.
                break;

            case 2:
                penalty.BanUntil = now.AddDays(WarningOnlySuspensionDays);
                break;

            case 3:
                penalty.SurchargePercent = surchargePercent;
                penalty.SessionsTotal = windowSessions;
                break;

            case 4:
                penalty.SurchargePercent = surchargePercent * 2m;
                penalty.SessionsTotal = windowSessions;
                penalty.BanUntil = now.AddDays(suspensionDays);
                break;

            default:
                // Step 5: permanent, BanUntil stays null and StrikeLevel == 5 is the marker.
                break;
        }

        _db.AuctionPenalties.Add(penalty);
        await _db.SaveChangesAsync(cancellationToken);

        await NotifySellerAsync(penalty, auction, cancellationToken);

        _logger.LogWarning(
            "Seller {SellerId} reached penalty step {Step} after delivery {DeliveryId} (escrow released: {Released}).",
            auction.SellerId, step, delivery.Id, escrowReleased);

        return penalty;
    }

    public async Task ConsumeSessionAsync(Guid sellerId, CancellationToken cancellationToken = default)
    {
        var active = await _db.AuctionPenalties
            .Where(p => p.SellerId == sellerId
                        && !p.IsDeleted
                        && p.Status == PenaltyStatus.Active
                        && p.SessionsTotal > 0
                        && p.SessionsUsed < p.SessionsTotal)
            .ToListAsync(cancellationToken);

        if (active.Count == 0)
        {
            return;
        }

        foreach (var penalty in active)
        {
            penalty.SessionsUsed += 1;
            penalty.UpdatedAt = DateTimeOffset.UtcNow;

            if (penalty.SessionsUsed >= penalty.SessionsTotal)
            {
                penalty.Status = PenaltyStatus.Consumed;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<(bool Blocked, string? Reason)> GetSellBlockAsync(
        Guid sellerId, CancellationToken cancellationToken = default)
    {
        var penalties = await _db.AuctionPenalties
            .AsNoTracking()
            .Where(p => p.SellerId == sellerId && !p.IsDeleted && p.Status == PenaltyStatus.Active)
            .ToListAsync(cancellationToken);

        if (penalties.Any(p => p.StrikeLevel >= MaxStep))
        {
            return (true, "Tài khoản không thể tạo phiên đấu giá mới.");
        }

        var now = DateTimeOffset.UtcNow;
        var ban = penalties
            .Where(p => p.BanUntil.HasValue && p.BanUntil.Value > now)
            .OrderByDescending(p => p.BanUntil)
            .FirstOrDefault();

        if (ban is not null)
        {
            var until = ban.BanUntil!.Value.ToOffset(TimeSpan.FromHours(7));
            return (true, $"Chưa thể tạo phiên mới đến {until:dd/MM/yyyy HH:mm}. Các phiên đang chạy vẫn tiếp tục.");
        }

        return (false, null);
    }

    public async Task<IReadOnlyList<AuctionPenalty>> GetPenaltiesAsync(
        Guid sellerId, CancellationToken cancellationToken = default)
        => await _db.AuctionPenalties
            .AsNoTracking()
            .Where(p => p.SellerId == sellerId && !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

    private async Task NotifySellerAsync(
        AuctionPenalty penalty, Domain.Entities.Auction.Auction auction, CancellationToken cancellationToken)
    {
        var code = auction.Id.ToString()[..8].ToUpperInvariant();
        var (title, body) = penalty.StrikeLevel switch
        {
            1 => ("Đã ghi nhận một khiếu nại giao tranh vật lý",
                $"Phiên #{code} — người mua phản ánh tranh chưa được giao hoặc bị hư hỏng và Ban quản trị đã xác nhận. "
                + "Đây là ghi nhận lần 1: nhắc nhở, chưa ảnh hưởng tới phí bán."),
            2 => ("Đã ghi nhận khiếu nại lần 2 — tạm dừng đăng bán",
                $"Phiên #{code} — Ban quản trị đã xác nhận khiếu nại. Bạn tạm dừng tạo phiên mới 1 ngày, "
                + "các phiên đang chạy vẫn tiếp tục bình thường."),
            3 => ("Đã ghi nhận khiếu nại lần 3 — phí bán tăng",
                $"Phiên #{code} — tiền phiên này đã được chuyển cho bạn trước khi phát sinh khiếu nại. "
                + "Phí bán các phiên tiếp theo là 30% trong 5 phiên."),
            4 => ("Đã ghi nhận khiếu nại lần 4 — phí bán 50% và tạm dừng đăng bán",
                $"Phiên #{code} — phí bán các phiên tiếp theo là 50% trong 5 phiên, "
                + "đồng thời tạm dừng tạo phiên mới trong 7 ngày."),
            _ => ("Tài khoản không thể tạo phiên đấu giá mới",
                $"Phiên #{code} — đã đạt mức xử lý cuối cùng. Bạn vẫn có thể mua, đặt giá và nhận Commission."),
        };

        try
        {
            await _notifications.PublishAsync(
                userId: penalty.SellerId,
                notificationType: NotificationType.AuctionLifecycle,
                title: title,
                body: body,
                refType: "Auction",
                refId: auction.Id,
                channel: NotificationChannel.InApp,
                dedupKey: $"SellerPenalty:{penalty.Id}",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cannot publish penalty notification for seller {SellerId}", penalty.SellerId);
        }
    }
}
