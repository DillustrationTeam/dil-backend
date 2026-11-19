using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ArtCommission.API.BackgroundWorkers;

/// <summary>Cấu hình job nhắc "phiên sắp kết thúc".</summary>
public sealed class AuctionEndingSoonSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>Chu kỳ quét (phút). Để 1 phút để nhắc đúng cửa sổ.</summary>
    public int IntervalMinutes { get; set; } = 1;

    /// <summary>Chờ trước lượt chạy đầu tiên (giây).</summary>
    public int InitialDelaySeconds { get; set; } = 60;

    /// <summary>Nhắc trước khi kết thúc bao nhiêu phút.</summary>
    public int ThresholdMinutes { get; set; } = 15;

    public int BatchSize { get; set; } = 200;
}

/// <summary>
/// Nhắc người theo dõi (và người đang dẫn đầu) rằng phiên sắp đóng.
///
/// VÌ SAO CẦN: NotificationType.AuctionEndingSoon đã có sẵn trong enum và UI có hiển thị loại
/// này, nhưng KHÔNG worker nào phát nó — chỗ duy nhất dùng là nhánh "winner quá hạn thanh toán"
/// (đã chết vì PaymentDeadline luôn null). Kết quả: người theo dõi không bao giờ được nhắc và
/// mất lượt đặt giá phút chót.
///
/// Chống trùng: dùng dedupKey cố định `AuctionEndingSoon:{auctionId}` cho mỗi người, nên dù job
/// chạy mỗi phút trong suốt cửa sổ 15 phút thì mỗi người chỉ nhận đúng 1 thông báo.
/// </summary>
public sealed class AuctionEndingSoonWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<AuctionEndingSoonSettings> _settings;
    private readonly ILogger<AuctionEndingSoonWorker> _logger;

    public AuctionEndingSoonWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<AuctionEndingSoonSettings> settings,
        ILogger<AuctionEndingSoonWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _settings.CurrentValue;
        if (!options.Enabled)
        {
            _logger.LogInformation("Auction ending-soon worker is disabled by configuration.");
            return;
        }

        if (options.InitialDelaySeconds > 0)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(options.InitialDelaySeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, options.IntervalMinutes)));
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var options = _settings.CurrentValue;
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var notifications = scope.ServiceProvider
                .GetRequiredService<ArtCommission.Application.Notifications.Common.INotificationPublisher>();

            var now = DateTimeOffset.UtcNow;
            var threshold = now.AddMinutes(Math.Max(1, options.ThresholdMinutes));

            // Chỉ phiên đang chạy và sắp đóng trong cửa sổ N phút.
            var due = await db.Auctions
                .AsNoTracking()
                .Where(a => !a.IsDeleted
                            && a.Status == AuctionStatus.Active
                            && a.EndAt > now
                            && a.EndAt <= threshold)
                .OrderBy(a => a.EndAt)
                .Take(Math.Max(1, options.BatchSize))
                .Select(a => new { a.Id, a.EndAt, a.CurrentPrice, a.WinnerId })
                .ToListAsync(cancellationToken);

            if (due.Count == 0)
            {
                return;
            }

            var sent = 0;

            foreach (var auction in due)
            {
                // Người theo dõi + người đang dẫn đầu (nếu có) — họ là người cần hành động.
                var watchers = await db.AuctionWatches
                    .AsNoTracking()
                    .Where(w => w.AuctionId == auction.Id && !w.IsDeleted)
                    .Select(w => w.UserId)
                    .ToListAsync(cancellationToken);

                if (auction.WinnerId.HasValue)
                {
                    watchers.Add(auction.WinnerId.Value);
                }

                var recipients = watchers.Distinct().Where(id => id != Guid.Empty).ToList();
                var minutesLeft = Math.Max(1, (int)Math.Ceiling((auction.EndAt - now).TotalMinutes));

                foreach (var userId in recipients)
                {
                    try
                    {
                        await notifications.PublishAsync(
                            userId,
                            NotificationType.AuctionEndingSoon,
                            "Phiên đấu giá sắp kết thúc",
                            $"Phiên #{auction.Id.ToString()[..8].ToUpperInvariant()} sẽ đóng sau khoảng {minutesLeft} phút. "
                            + $"Giá đang dẫn đầu {auction.CurrentPrice:N0} ₫ — đặt giá ngay nếu bạn muốn giữ lượt.",
                            "Auction",
                            auction.Id,
                            NotificationChannel.InApp,
                            $"AuctionEndingSoon:{auction.Id}:{userId}",
                            cancellationToken);

                        sent++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Không gửi được nhắc sắp kết thúc cho phiên {AuctionId}, user {UserId}.",
                            auction.Id, userId);
                    }
                }
            }

            if (sent > 0)
            {
                _logger.LogInformation(
                    "Nhắc sắp kết thúc: {Sent} thông báo cho {Auctions} phiên trong cửa sổ {Minutes} phút.",
                    sent, due.Count, options.ThresholdMinutes);
            }
        }
        catch (OperationCanceledException)
        {
            // App đang tắt
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi trong lượt chạy của job nhắc phiên sắp kết thúc.");
        }
    }
}
