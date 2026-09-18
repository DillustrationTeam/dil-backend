using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.Common;
using Microsoft.Extensions.Options;

namespace ArtCommission.API.BackgroundWorkers;

/// <summary>
/// Job nền đối soát các đơn nạp tiền còn Pending.
///
/// Vì sao cần: webhook có thể không tới (dev local không mở tunnel, cổng retry hết lượt,
/// mạng lỗi). Không có job này thì tiền đã trừ ở phía khách mà ví trên sàn vẫn Pending.
///
/// Dùng BackgroundService có sẵn của .NET 8 thay vì Hangfire: không cần storage riêng,
/// không thêm bảng vào DB. Khi cần dashboard theo dõi job thì mới đổi sang Hangfire.
/// </summary>
public class PaymentReconciliationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<PaymentReconciliationSettings> _settings;
    private readonly ILogger<PaymentReconciliationWorker> _logger;

    public PaymentReconciliationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<PaymentReconciliationSettings> settings,
        ILogger<PaymentReconciliationWorker> logger)
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
            _logger.LogInformation("Job đối soát thanh toán đang tắt theo cấu hình.");
            return;
        }

        _logger.LogInformation(
            "Job đối soát thanh toán khởi động: chu kỳ {Interval} phút, đối soát đơn cũ hơn {OlderThan} phút",
            options.IntervalMinutes, options.OlderThanMinutes);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(options.InitialDelaySeconds), stoppingToken);
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
            // Service nghiệp vụ là Scoped, worker là Singleton => phải tạo scope mới mỗi lượt
            using var scope = _scopeFactory.CreateScope();
            var reconciliation = scope.ServiceProvider.GetRequiredService<IPaymentReconciliationService>();

            var summary = await reconciliation.ReconcilePendingOrdersAsync(
                TimeSpan.FromMinutes(Math.Max(1, _settings.Value.OlderThanMinutes)),
                Math.Max(1, _settings.Value.BatchSize),
                cancellationToken);

            if (summary.Scanned > 0)
            {
                _logger.LogInformation(
                    "Job đối soát xong: quét {Scanned}, cộng tiền {Credited}, hết hạn {Expired}, thất bại {Failed}, lỗi {Errors}",
                    summary.Scanned, summary.Credited, summary.Expired, summary.Failed, summary.Errors);
            }
        }
        catch (OperationCanceledException)
        {
            // App đang tắt — thoát êm
        }
        catch (Exception ex)
        {
            // Không để 1 lượt lỗi giết cả job
            _logger.LogError(ex, "Job đối soát thanh toán gặp lỗi ở lượt chạy này.");
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
