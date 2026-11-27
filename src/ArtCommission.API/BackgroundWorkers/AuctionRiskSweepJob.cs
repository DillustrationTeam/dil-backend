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

    /// <summary>
    /// Tuổi tối đa của một lượt "đang xem". Quá hạn thì phiếu tự nhả về Open để người khác nhận,
    /// tránh việc một tab đã đóng giữ phiếu vĩnh viễn.
    /// </summary>
    private static readonly TimeSpan ClaimLifetime = TimeSpan.FromMinutes(30);

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
        var now = DateTimeOffset.UtcNow;
        var staleBefore = now - RescanInterval;

        // Nhả phiếu bị bỏ quên TRƯỚC khi kiểm tra công tắc: tắt bộ phát hiện không có nghĩa là phiếu
        // đang bị một tab đã đóng giữ mãi. (Trước đây khối này nằm sau `if (!Enabled) return`.)
        // Nhả phiếu bị bỏ quên: UnderReview quá 30 phút mà không có quyết định thì trả về Open để
        // kiểm duyệt viên khác nhận, tránh phiếu bị "khoá" vĩnh viễn bởi một tab đã đóng.
        var staleClaims = await _db.AuctionRiskAssessments
            .Where(a => !a.IsDeleted
                        && a.Status == AuctionRiskAssessmentStatus.UnderReview
                        && a.ClaimedAt != null
                        && a.ClaimedAt < now - ClaimLifetime)
            .ToListAsync(cancellationToken);

        foreach (var sheet in staleClaims)
        {
            sheet.Status = AuctionRiskAssessmentStatus.Open;
            sheet.ClaimedByUserId = null;
            sheet.ClaimedAt = null;
            sheet.UpdatedAt = now;
        }

        if (staleClaims.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Released {Count} stale risk claim(s).", staleClaims.Count);
        }

        var settings = await RiskSettings.LoadAsync(_db, cancellationToken);
        if (!settings.Enabled)
        {
            _logger.LogInformation("Auction risk sweep skipped: detection disabled in PlatformConfig.");
            return;
        }

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
