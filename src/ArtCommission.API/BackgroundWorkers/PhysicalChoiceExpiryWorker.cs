using ArtCommission.Application.Auction.Commands.Delivery;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ArtCommission.API.BackgroundWorkers;

/// <summary>
/// Closes the 48-hour physical-choice window by itself.
///
/// A session that carries a physical print waits for the winner to choose between
/// "send me the print" and "digital copy only". If the winner says nothing, nobody can
/// act: the print is not requested, the master file is not downloadable because the
/// escrow is still held, and the seller is never paid — the money just hangs.
///
/// After the deadline the platform therefore treats silence as "digital copy only",
/// releases the escrow to the seller and closes the session. The buyer keeps the same
/// rights as an explicit digital choice.
/// </summary>
public class PhysicalChoiceExpirySettings
{
    public bool Enabled { get; set; } = true;

    public int IntervalMinutes { get; set; } = 15;

    public int InitialDelaySeconds { get; set; } = 60;

    public int BatchSize { get; set; } = 50;
}

public class PhysicalChoiceExpiryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<PhysicalChoiceExpirySettings> _settings;
    private readonly ILogger<PhysicalChoiceExpiryWorker> _logger;

    public PhysicalChoiceExpiryWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<PhysicalChoiceExpirySettings> settings,
        ILogger<PhysicalChoiceExpiryWorker> logger)
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
            _logger.LogInformation("Physical-choice expiry job is disabled by configuration.");
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, options.InitialDelaySeconds)), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _logger.LogInformation(
            "Physical-choice expiry job started: interval {Interval} min, batch {Batch}.",
            options.IntervalMinutes, options.BatchSize);

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

            var now = DateTimeOffset.UtcNow;

            var expired = await db.Auctions
                .AsNoTracking()
                .Where(a => !a.IsDeleted
                            && a.Status == AuctionStatus.AwaitingPhysical
                            && a.HasPhysicalCopy
                            && a.RequiresPhysicalDelivery == null      // no explicit choice yet
                            && a.WinnerId != null
                            && a.PhysicalChoiceDeadline != null
                            && a.PhysicalChoiceDeadline < now)
                .OrderBy(a => a.PhysicalChoiceDeadline)
                .Take(Math.Max(1, options.BatchSize))
                .Select(a => new { a.Id, WinnerId = a.WinnerId!.Value })
                .ToListAsync(cancellationToken);

            if (expired.Count == 0)
            {
                return;
            }

            var completed = 0;
            foreach (var row in expired)
            {
                var (success, errors) = await mediator.Send(
                    new ExpirePhysicalChoiceCommand(row.Id),
                    cancellationToken);

                if (success)
                {
                    completed++;
                    _logger.LogInformation(
                        "Physical choice expired for auction {AuctionId}: refunded the buyer and cancelled the session.",
                        row.Id);
                }
                else
                {
                    _logger.LogWarning(
                        "Cannot auto-close physical choice of auction {AuctionId}: {Errors}",
                        row.Id, string.Join("; ", errors));
                }
            }

            if (completed > 0)
            {
                _logger.LogInformation(
                    "Physical-choice expiry job cancelled {Count} session(s) and refunded the buyers after the 48-hour window.",
                    completed);
            }
        }
        catch (OperationCanceledException)
        {
            // Application is shutting down.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Physical-choice expiry job failed.");
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
