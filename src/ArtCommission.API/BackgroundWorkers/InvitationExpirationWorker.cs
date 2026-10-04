using ArtCommission.Application.Event.Commands;
using MediatR;
using Microsoft.Extensions.Options;

namespace ArtCommission.API.BackgroundWorkers;

/// <summary>
/// BackgroundService định kỳ quét và đánh dấu các lời mời Ban giám khảo (Invitation)
/// đang ở trạng thái Pending sang Expired nếu đã quá thời hạn phản hồi hoặc sự kiện đã kết thúc.
/// </summary>
public class InvitationExpirationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<InvitationExpirationSettings> _settings;
    private readonly ILogger<InvitationExpirationWorker> _logger;

    public InvitationExpirationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<InvitationExpirationSettings> settings,
        ILogger<InvitationExpirationWorker> logger)
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
            _logger.LogInformation("Job quét lời mời hết hạn (InvitationExpiration) đang tắt theo cấu hình.");
            return;
        }

        _logger.LogInformation(
            "Job quét lời mời hết hạn khởi động: chu kỳ {Interval} phút, thời hạn tối đa {LifetimeDays} ngày",
            options.IntervalMinutes, options.LifetimeDays);

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
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var command = new ExpireInvitationsCommand(
                MaxLifetime: TimeSpan.FromDays(Math.Max(1, options.LifetimeDays)),
                ExpireWhenEventEnded: options.ExpireWhenEventEnded,
                BatchSize: Math.Max(1, options.BatchSize));

            var (expiredCount, errors) = await mediator.Send(command, cancellationToken);

            if (expiredCount > 0)
            {
                _logger.LogInformation(
                    "Job quét lời mời hết hạn: đã chuyển {Count} lời mời sang trạng thái Expired.",
                    expiredCount);
            }

            if (errors.Length > 0)
            {
                _logger.LogWarning(
                    "Job quét lời mời hết hạn gặp cảnh báo: {Errors}",
                    string.Join("; ", errors));
            }
        }
        catch (OperationCanceledException)
        {
            // App đang tắt — thoát êm
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xảy ra trong lượt chạy của Job quét lời mời hết hạn.");
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
