using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.API.BackgroundWorkers;

/// <summary>
/// Periodically transitions published Upcoming events to Open once SubmissionStartAt arrives.
/// Draft events are intentionally never auto-opened.
/// </summary>
public class EventLifecycleWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EventLifecycleWorker> _logger;

    public EventLifecycleWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<EventLifecycleWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        do
        {
            await TransitionDueEventsAsync(stoppingToken);
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private async Task TransitionDueEventsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var now = DateTimeOffset.UtcNow;

            var dueEvents = await db.PlatformEvents
                .Where(e => !e.IsDeleted
                    && e.Status == EventStatus.Upcoming
                    && e.SubmissionStartAt <= now)
                .ToListAsync(cancellationToken);

            if (dueEvents.Count == 0)
            {
                return;
            }

            foreach (var ev in dueEvents)
            {
                ev.Status = EventStatus.Open;
                ev.UpdatedAt = now;
            }

            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Transitioned {Count} Upcoming event(s) to Open.", dueEvents.Count);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to transition Upcoming events to Open.");
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

