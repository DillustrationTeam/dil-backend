using ArtCommission.Application.Auction.Commands.Delivery;
using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ArtCommission.API.BackgroundWorkers;

/// <summary>
/// Tự động hoàn tất đơn giao tranh vật lý sau 48 giờ kể từ khi bưu phẩm được gửi.
/// Nếu người mua không bấm "Đã nhận hàng", hệ thống coi như đã nhận thành công, giải ngân
/// ký quỹ cho người bán và đóng phiên — tránh tiền bị treo vô hạn vì người mua im lặng.
/// </summary>
public class DeliveryAutoConfirmSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>Chu kỳ quét (phút).</summary>
    public int IntervalMinutes { get; set; } = 15;

    /// <summary>Chờ bao lâu trước lượt chạy đầu tiên (giây) để DB/app ổn định.</summary>
    public int InitialDelaySeconds { get; set; } = 45;

    /// <summary>Số bản ghi xử lý mỗi lượt.</summary>
    public int BatchSize { get; set; } = 50;
}

public class DeliveryAutoConfirmWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<DeliveryAutoConfirmSettings> _settings;
    private readonly ILogger<DeliveryAutoConfirmWorker> _logger;

    public DeliveryAutoConfirmWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<DeliveryAutoConfirmSettings> settings,
        ILogger<DeliveryAutoConfirmWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _settings.Value;
        if (!options.Enabled)
        {
            _logger.LogInformation("Job tự động xác nhận giao hàng đang tắt theo cấu hình.");
            return;
        }

        _logger.LogInformation(
            "Job tự động xác nhận giao hàng khởi động: chu kỳ {Interval} phút, ngưỡng {Hours} giờ sau khi gửi.",
            options.IntervalMinutes, AuctionDeliveryRules.AutoConfirmHours);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, options.InitialDelaySeconds)), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, options.IntervalMinutes)));

        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var options = _settings.Value;
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var threshold = DateTimeOffset.UtcNow.AddHours(-AuctionDeliveryRules.AutoConfirmHours);

            var dueDeliveries = await db.Set<ArtCommission.Domain.Entities.Auction.AuctionDelivery>()
                .AsNoTracking()
                .Where(d => !d.IsDeleted
                            && d.Status == DeliveryStatus.InTransit
                            && d.ShippedAt != null
                            && d.ShippedAt <= threshold)
                .OrderBy(d => d.ShippedAt)
                .Take(Math.Max(1, options.BatchSize))
                .Select(d => new { d.AuctionId })
                .ToListAsync(cancellationToken);

            if (dueDeliveries.Count == 0)
            {
                return;
            }

            var confirmed = 0;
            foreach (var row in dueDeliveries)
            {
                var auction = await db.Auctions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == row.AuctionId && !a.IsDeleted, cancellationToken);

                if (auction?.WinnerId is null)
                {
                    continue;
                }

                var (success, _, errors) = await mediator.Send(
                    new ConfirmPhysicalDeliveryCommand(auction.WinnerId.Value, auction.Id),
                    cancellationToken);

                if (success)
                {
                    confirmed++;
                }
                else
                {
                    _logger.LogWarning(
                        "Tự động xác nhận phiên {AuctionId} thất bại: {Errors}",
                        auction.Id, string.Join("; ", errors));
                }
            }

            if (confirmed > 0)
            {
                _logger.LogInformation(
                    "Job tự động xác nhận giao hàng: đã hoàn tất {Count} đơn quá {Hours} giờ.",
                    confirmed, AuctionDeliveryRules.AutoConfirmHours);
            }
        }
        catch (OperationCanceledException)
        {
            // App đang tắt
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xảy ra trong lượt chạy của job tự động xác nhận giao hàng.");
        }
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
