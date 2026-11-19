using ArtCommission.Application.Auction.Commands.Delivery;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ArtCommission.API.BackgroundWorkers;

/// <summary>Cấu hình job huỷ đơn giao tranh vật lý khi người bán không gửi hàng.</summary>
public sealed class ShipmentOverdueSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>Chu kỳ quét, tính bằng phút.</summary>
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>Chờ bao lâu trước lượt chạy đầu tiên (giây) — tránh dồn việc lúc khởi động.</summary>
    public int InitialDelaySeconds { get; set; } = 120;

    /// <summary>Số ngày tối đa người bán được giữ đơn mà không cập nhật vận đơn.</summary>
    public int DeadlineDays { get; set; } = 7;

    /// <summary>Số đơn xử lý mỗi lượt.</summary>
    public int BatchSize { get; set; } = 50;
}

/// <summary>
/// Quét các đơn giao tranh vật lý đang "chờ người bán gửi hàng" quá hạn và huỷ chúng.
///
/// VÌ SAO CẦN: trước đây chỉ có job tự xác nhận sau khi ĐÃ gửi hàng và job huỷ phiên khi
/// người mua không chọn hình thức. Người bán im lặng mãi thì đơn nằm vĩnh viễn ở
/// AwaitingShipment: tiền người mua bị giữ ở sàn, không ai đóng được đơn.
///
/// Mốc tính hạn: thời điểm đơn giao nhận được tạo (lúc người mua gửi địa chỉ) + DeadlineDays.
/// </summary>
public sealed class ShipmentOverdueWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<ShipmentOverdueSettings> _settings;
    private readonly ILogger<ShipmentOverdueWorker> _logger;

    public ShipmentOverdueWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<ShipmentOverdueSettings> settings,
        ILogger<ShipmentOverdueWorker> logger)
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
            _logger.LogInformation("Shipment expiry worker is disabled by configuration.");
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
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var deadline = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, options.DeadlineDays));

            // Lọc cả trạng thái phiên ngay trong query để bản ghi "chết" không chiếm batch.
            var overdue = await db.Set<ArtCommission.Domain.Entities.Auction.AuctionDelivery>()
                .AsNoTracking()
                .Where(d => !d.IsDeleted
                            && d.Status == DeliveryStatus.AwaitingShipment
                            && d.CreatedAt <= deadline)
                .Where(d => db.Auctions.Any(a => a.Id == d.AuctionId
                                                 && !a.IsDeleted
                                                 && a.Status == AuctionStatus.AwaitingPhysical
                                                 && a.RequiresPhysicalDelivery == true
                                                 && a.WinnerId != null))
                .OrderBy(d => d.CreatedAt)
                .Take(Math.Max(1, options.BatchSize))
                .Select(d => new { d.AuctionId })
                .ToListAsync(cancellationToken);

            if (overdue.Count == 0)
            {
                return;
            }

            var cancelled = 0;
            var failed = 0;

            foreach (var row in overdue)
            {
                try
                {
                    var (success, errors) = await mediator.Send(
                        new ExpireShipmentCommand(row.AuctionId), cancellationToken);

                    if (success)
                    {
                        cancelled++;
                    }
                    else
                    {
                        failed++;
                        _logger.LogWarning(
                            "Không huỷ được đơn quá hạn giao hàng của phiên {AuctionId}: {Errors}",
                            row.AuctionId, string.Join("; ", errors));
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError(ex,
                        "Lỗi khi huỷ đơn quá hạn giao hàng của phiên {AuctionId}; bỏ qua để xử lý đơn còn lại.",
                        row.AuctionId);
                }
            }

            if (cancelled > 0 || failed > 0)
            {
                _logger.LogInformation(
                    "Job huỷ đơn quá hạn gửi hàng: đã huỷ {Cancelled}, lỗi {Failed} (hạn {Days} ngày).",
                    cancelled, failed, options.DeadlineDays);
            }
        }
        catch (OperationCanceledException)
        {
            // App đang tắt
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xảy ra trong lượt chạy của job huỷ đơn quá hạn gửi hàng.");
        }
    }
}
