using ArtCommission.Application.Auction.Risk;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.API.BackgroundWorkers;

/// <summary>
/// Background sweep behind AI Bid Fraud Detection, scheduled every five minutes by Hangfire.
///
/// Picks auctions that are running or just closed and were not scanned in the last ten minutes,
/// then runs the full rule set for each. The hot path inside the bid command only covers cheap
/// rules; this job is where device/history evidence is computed.
///
/// Idempotent by design: re-running it on the same auction only adds evidence rows that are new,
/// and re-scoring an already-open sheet updates it in place.
/// </summary>
public sealed class AuctionRiskSweepJob
{
    private static readonly TimeSpan RescanInterval = TimeSpan.FromMinutes(10);

    private readonly IApplicationDbContext _db;
    private readonly IRiskEngine _riskEngine;
    private readonly ILogger<AuctionRiskSweepJob> _logger;

    public AuctionRiskSweepJob(
        IApplicationDbContext db,
        IRiskEngine riskEngine,
        ILogger<AuctionRiskSweepJob> logger)
    {
        _db = db;
        _riskEngine = riskEngine;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var settings = await RiskSettings.LoadAsync(_db, cancellationToken);
        if (!settings.Enabled)
        {
            _logger.LogInformation("Auction risk sweep skipped: detection disabled in PlatformConfig.");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var staleBefore = now - RescanInterval;

        var candidates = await _db.Auctions.AsNoTracking()
            .Where(a => !a.IsDeleted
                        && (a.Status == AuctionStatus.Active || a.Status == AuctionStatus.Ended)
                        && (a.LastRiskScanAt == null || a.LastRiskScanAt < staleBefore))
            .OrderBy(a => a.LastRiskScanAt == null ? DateTimeOffset.MinValue : a.LastRiskScanAt.Value)
            .Take(Math.Clamp(settings.SweepBatchSize, 1, 500))
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return;
        }

        var scanned = 0;
        var alerted = 0;

        foreach (var auctionId in candidates)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var outcome = await _riskEngine.EvaluateAsync(auctionId, hotPathOnly: false, cancellationToken);

                if (!outcome.Skipped)
                {
                    scanned++;
                }

                if (outcome.AlertRaised)
                {
                    alerted++;
                }
            }
            catch (Exception ex)
            {
                // One broken auction must not abort the batch; the next run retries it because
                // LastRiskScanAt was not persisted.
                _logger.LogError(ex, "Auction risk sweep failed for auction {AuctionId}.", auctionId);
            }
        }

        _logger.LogInformation(
            "Auction risk sweep completed: {Scanned}/{Candidates} scanned, {Alerted} alerts raised.",
            scanned,
            candidates.Count,
            alerted);
    }
}
