using System.Text.Encodings.Web;
using System.Text.Json;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Notifications.Common;
using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Application.Auction.Risk;

/// <summary>Result of one detector run, consumed by the sweep job log and the rescan endpoint.</summary>
public sealed record RiskEvaluationOutcome(
    bool Skipped,
    decimal Score,
    AuctionRiskLevel Level,
    int SignalCount,
    Guid? AssessmentId,
    bool AlertRaised);

/// <summary>
/// Auction fraud detection entry point.
///
/// Two call sites:
///   - <c>hotPathOnly: true</c> from the bid command - only rules that need data already in memory,
///     so the bid response never waits on device/history queries.
///   - <c>hotPathOnly: false</c> from the sweep job and the admin rescan - the full rule set.
///
/// The engine only writes evidence and a verdict. It never cancels an auction, never penalises a
/// user; a human resolves the sheet.
/// </summary>
public interface IRiskEngine
{
    Task<RiskEvaluationOutcome> EvaluateAsync(
        Guid auctionId,
        bool hotPathOnly,
        CancellationToken cancellationToken = default);
}

public sealed class AuctionRiskEngine : IRiskEngine
{
    private const string DetectorVersion = "rules-v2";
    private const int MaxBidsConsidered = 200;
    private const int MaxEvidenceLength = 1000;

    // Unicode is kept readable in the column: support must be able to read the evidence straight
    // from SQL, and escaped \uXXXX makes that impossible.
    private static readonly JsonSerializerOptions EvidenceJson = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IApplicationDbContext _db;
    private readonly INotificationPublisher _notifications;
    private readonly IIdentityService _identity;
    private readonly IEnumerable<IRiskRule> _rules;
    private readonly AuctionRiskScorer _scorer;
    private readonly ILogger<AuctionRiskEngine> _logger;

    public AuctionRiskEngine(
        IApplicationDbContext db,
        INotificationPublisher notifications,
        IIdentityService identity,
        IEnumerable<IRiskRule> rules,
        AuctionRiskScorer scorer,
        ILogger<AuctionRiskEngine> logger)
    {
        _db = db;
        _notifications = notifications;
        _identity = identity;
        _rules = rules;
        _scorer = scorer;
        _logger = logger;
    }

    public async Task<RiskEvaluationOutcome> EvaluateAsync(
        Guid auctionId,
        bool hotPathOnly,
        CancellationToken cancellationToken = default)
    {
        var settings = await RiskSettings.LoadAsync(_db, cancellationToken);
        if (!settings.Enabled)
        {
            return new RiskEvaluationOutcome(true, 0m, AuctionRiskLevel.Low, 0, null, false);
        }

        var auction = await _db.Auctions
            .Include(a => a.Artwork)
            .FirstOrDefaultAsync(a => a.Id == auctionId && !a.IsDeleted, cancellationToken);

        if (auction is null)
        {
            _logger.LogWarning("Risk scan skipped: auction {AuctionId} not found", auctionId);
            return new RiskEvaluationOutcome(true, 0m, AuctionRiskLevel.Low, 0, null, false);
        }

        var context = await BuildContextAsync(auction, settings, hotPathOnly, cancellationToken);

        var applicable = hotPathOnly
            ? _rules.Where(r => r.IsHotPathSafe)
            : _rules;

        var drafts = applicable.SelectMany(rule => rule.Evaluate(context, settings)).ToList();
        var scored = _scorer.Score(drafts, settings);
        var now = DateTimeOffset.UtcNow;

        var addedSignals = await PersistSignalsAsync(auctionId, drafts, settings, now, cancellationToken);

        var live = await _db.AuctionRiskAssessments
            .FirstOrDefaultAsync(
                a => a.AuctionId == auctionId
                     && !a.IsDeleted
                     && (a.Status == AuctionRiskAssessmentStatus.Open
                         || a.Status == AuctionRiskAssessmentStatus.UnderReview),
                cancellationToken);

        var previousScore = live?.Score ?? 0m;
        var previousLevel = live?.RiskLevel ?? AuctionRiskLevel.Low;

        // A clean re-scan must not erase a moderator's open sheet, and must not create noise rows.
        if (live is null && drafts.Count == 0)
        {
            auction.RiskScore = 0m;
            auction.RiskLevel = AuctionRiskLevel.Low;
            auction.LastRiskScanAt = now;
            await _db.SaveChangesAsync(cancellationToken);
            return new RiskEvaluationOutcome(false, 0m, AuctionRiskLevel.Low, 0, null, false);
        }

        var isNewAssessment = live is null;
        if (live is null)
        {
            live = new AuctionRiskAssessment { AuctionId = auctionId, DetectedAt = now };
            _db.AuctionRiskAssessments.Add(live);
        }

        // Evidence only accumulates: a later sweep must not launder a flagged auction by re-scoring
        // it lower (older bids falling out of the 200-row window is not a reason to clear the sheet).
        // The sheet keeps the highest score it ever reached until a moderator resolves it.
        var keepPreviousScore = !isNewAssessment && scored.Score < live.Score;
        if (keepPreviousScore)
        {
            live.SignalCount = Math.Max(live.SignalCount, drafts.Count);
            live.DetectedAt = now;
            live.ModelVersion = DetectorVersion;

            auction.RiskScore = live.Score;
            auction.RiskLevel = live.RiskLevel;
            auction.LastRiskScanAt = now;

            await _db.SaveChangesAsync(cancellationToken);
            return new RiskEvaluationOutcome(false, live.Score, live.RiskLevel, live.SignalCount, live.Id, false);
        }

        live.Score = scored.Score;
        live.RiskLevel = scored.Level;
        live.SignalCount = drafts.Count;
        live.Summary = Truncate(scored.Summary, MaxEvidenceLength);
        live.DetectedAt = now;
        live.ModelVersion = DetectorVersion;

        auction.RiskScore = scored.Score;
        auction.RiskLevel = scored.Level;
        auction.LastRiskScanAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        // Alert on first crossing, on escalation, or on a material score jump - not on every sweep.
        var shouldAlert = scored.Level >= AuctionRiskLevel.High
                          && (isNewAssessment
                              || scored.Level > previousLevel
                              || scored.Score >= previousScore + 10m);

        var alertRaised = false;
        if (shouldAlert)
        {
            alertRaised = await RaiseAlertsAsync(auction, live, scored, drafts.Count, cancellationToken);
        }

        _logger.LogInformation(
            "Risk scan {Scope} auction {AuctionId}: score {Score} ({Level}), {Signals} signals, {Added} new, alert={Alert}",
            hotPathOnly ? "hot" : "full",
            auctionId,
            scored.Score,
            scored.Level,
            drafts.Count,
            addedSignals,
            alertRaised);

        return new RiskEvaluationOutcome(false, scored.Score, scored.Level, drafts.Count, live.Id, alertRaised);
    }

    private async Task<RiskContext> BuildContextAsync(
        Domain.Entities.Auction.Auction auction,
        RiskSettings settings,
        bool hotPathOnly,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var recentDescending = await _db.Bids.AsNoTracking()
            .Where(b => b.AuctionId == auction.Id && !b.IsDeleted)
            .OrderByDescending(b => b.PlacedAt)
            .Take(MaxBidsConsidered)
            .ToListAsync(cancellationToken);

        recentDescending.Reverse();
        var bids = recentDescending;

        var bidderIds = bids.Select(b => b.BidderId).Distinct().ToList();

        // Hot path keeps to the bid list only: device/history lookups belong to the sweep.
        if (hotPathOnly || bidderIds.Count == 0)
        {
            return new RiskContext
            {
                Auction = auction,
                Bids = bids,
                BidderDevices = new Dictionary<Guid, DeviceFingerprint>(),
                SellerDevices = DeviceFingerprint.Empty,
                BidderHistory = new Dictionary<Guid, BidderHistory>(),
                BidderCreatedAt = new Dictionary<Guid, DateTimeOffset>(),
                Now = now
            };
        }

        var deviceUserIds = bidderIds.Append(auction.SellerId).Distinct().ToList();
        var since = now - settings.DeviceLookback;

        var tokens = await _db.RefreshTokens.AsNoTracking()
            .Where(t => deviceUserIds.Contains(t.UserId) && t.CreatedAt >= since)
            .Select(t => new { t.UserId, t.CreatedByIp, t.UserAgent })
            .ToListAsync(cancellationToken);

        var devices = new Dictionary<Guid, DeviceFingerprint>();
        foreach (var group in tokens.GroupBy(t => t.UserId))
        {
            devices[group.Key] = new DeviceFingerprint(
                group.Select(t => t.CreatedByIp)
                    .Where(ip => !string.IsNullOrWhiteSpace(ip))
                    .Select(ip => ip!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase),
                group.Select(t => t.UserAgent)
                    .Where(ua => !string.IsNullOrWhiteSpace(ua))
                    .Select(ua => ua!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase));
        }

        // "Won" = the bidder ended up the winner of a finished session, whatever happened after.
        var wonStatuses = new[]
        {
            AuctionStatus.Settled,
            AuctionStatus.AwaitingPhysical,
            AuctionStatus.InTransit,
            AuctionStatus.Completed,
            AuctionStatus.Disputed,
            AuctionStatus.Expired
        };

        var winRows = await _db.Auctions.AsNoTracking()
            .Where(a => a.WinnerId != null
                        && bidderIds.Contains(a.WinnerId.Value)
                        && wonStatuses.Contains(a.Status))
            .Select(a => new { WinnerId = a.WinnerId!.Value, a.Status })
            .ToListAsync(cancellationToken);

        var history = winRows
            .GroupBy(r => r.WinnerId)
            .ToDictionary(
                g => g.Key,
                g => new BidderHistory(
                    WonCount: g.Count(),
                    ExpiredUnpaidCount: g.Count(r => r.Status == AuctionStatus.Expired)));

        var createdAt = await _db.Users.AsNoTracking()
            .Where(u => bidderIds.Contains(u.Id))
            .Select(u => new { u.Id, u.CreatedAt })
            .ToDictionaryAsync(u => u.Id, u => u.CreatedAt, cancellationToken);

        return new RiskContext
        {
            Auction = auction,
            Bids = bids,
            BidderDevices = devices,
            SellerDevices = devices.GetValueOrDefault(auction.SellerId, DeviceFingerprint.Empty),
            BidderHistory = history,
            BidderCreatedAt = createdAt,
            Now = now
        };
    }

    /// <summary>
    /// Stores only signals that were not already recorded for this auction, so a five-minute sweep
    /// does not multiply identical evidence rows.
    /// </summary>
    private async Task<int> PersistSignalsAsync(
        Guid auctionId,
        IReadOnlyList<RiskSignalDraft> drafts,
        RiskSettings settings,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (drafts.Count == 0)
        {
            return 0;
        }

        var codes = drafts.Select(d => d.Code).Distinct().ToList();
        var existing = await _db.AuctionRiskSignals.AsNoTracking()
            .Where(s => s.AuctionId == auctionId && codes.Contains(s.SignalCode) && s.DetectorVersion == DetectorVersion)
            .Select(s => new { s.SignalCode, s.BidId, s.UserId })
            .ToListAsync(cancellationToken);

        var seen = existing
            .Select(e => (e.SignalCode, e.BidId, e.UserId))
            .ToHashSet();

        var added = 0;

        foreach (var draft in drafts)
        {
            if (!seen.Add((draft.Code, draft.BidId, draft.UserId)))
            {
                continue;
            }

            _db.AuctionRiskSignals.Add(new AuctionRiskSignal
            {
                AuctionId = auctionId,
                BidId = draft.BidId,
                UserId = draft.UserId,
                SignalCode = draft.Code,
                Weight = settings.WeightOf(draft.Code),
                EvidenceJson = JsonSerializer.Serialize(
                    new { signal = draft.Code.ToString(), evidence = Truncate(draft.Evidence, MaxEvidenceLength) },
                    EvidenceJson),
                DetectorVersion = DetectorVersion,
                DetectedAt = now,
                CreatedAt = now
            });
            added++;
        }

        return added;
    }

    private async Task<bool> RaiseAlertsAsync(
        Domain.Entities.Auction.Auction auction,
        AuctionRiskAssessment assessment,
        RiskScoreResult scored,
        int signalCount,
        CancellationToken cancellationToken)
    {
        var title = auction.Artwork?.Title ?? "phiên đấu giá";
        var levelLabel = RiskLabels.LevelLabel(scored.Level);
        var dedupSuffix = $"{assessment.Id}:{scored.Level}";

        var operators = (await _identity.GetUserIdsInRoleAsync(UserRoleNames.Administrator, cancellationToken))
            .Concat(await _identity.GetUserIdsInRoleAsync(UserRoleNames.Moderator, cancellationToken))
            .Distinct()
            .ToList();

        foreach (var operatorId in operators)
        {
            await _notifications.PublishAsync(
                operatorId,
                NotificationType.AuctionFraudAlert,
                "Phiên đấu giá cần kiểm tra",
                $"Phiên \"{title}\" bị gắn cờ rủi ro {scored.Score:0.#}/100 ({levelLabel}) với {signalCount} dấu hiệu cần xem.",
                refType: "Auction",
                refId: auction.Id,
                dedupKey: $"AuctionFraudAlert:{auction.Id}:{dedupSuffix}",
                cancellationToken: cancellationToken);
        }

        // The seller gets a neutral notice: no score, no signal names, no accusation.
        await _notifications.PublishAsync(
            auction.SellerId,
            NotificationType.AuctionRiskFlagged,
            "Phiên đấu giá đang được xem xét",
            $"Phiên \"{title}\" đang được bộ phận kiểm duyệt xem xét thêm. Bạn không cần làm gì lúc này.",
            refType: "Auction",
            refId: auction.Id,
            dedupKey: $"AuctionRiskFlagged:{auction.Id}:{dedupSuffix}",
            cancellationToken: cancellationToken);

        return true;
    }

    private static string Truncate(string value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
