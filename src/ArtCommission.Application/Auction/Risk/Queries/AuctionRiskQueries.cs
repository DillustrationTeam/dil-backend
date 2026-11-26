using System.Text;
using System.Text.Json;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Risk.Queries;

// =====================================================================
// 1. Full risk sheet of one auction (moderation drawer)
// =====================================================================
public sealed record GetAuctionRiskQuery(Guid AuctionId)
    : IRequest<(bool Success, AuctionRiskAssessmentDto? Data, string[] Errors)>;

public sealed class GetAuctionRiskQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAuctionRiskQuery, (bool Success, AuctionRiskAssessmentDto? Data, string[] Errors)>
{
    public async Task<(bool, AuctionRiskAssessmentDto?, string[])> Handle(
        GetAuctionRiskQuery request,
        CancellationToken cancellationToken)
    {
        var auction = await db.Auctions.AsNoTracking()
            .Include(a => a.Artwork)
            .FirstOrDefaultAsync(a => a.Id == request.AuctionId && !a.IsDeleted, cancellationToken);

        if (auction is null)
        {
            return (false, null, ["Không tìm thấy phiên đấu giá."]);
        }

        var assessment = await db.AuctionRiskAssessments.AsNoTracking()
            .Where(a => a.AuctionId == request.AuctionId && !a.IsDeleted)
            .OrderByDescending(a => a.DetectedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var signals = await db.AuctionRiskSignals.AsNoTracking()
            .Where(s => s.AuctionId == request.AuctionId && !s.IsDeleted && (assessment == null || s.DetectorVersion == assessment.ModelVersion))
            .OrderByDescending(s => s.DetectedAt)
            .Select(s => new { s.Id, s.SignalCode, s.Weight, s.EvidenceJson, s.BidId, s.UserId, s.DetectedAt })
            .ToListAsync(cancellationToken);

        var signalDtos = signals.Select(s => new RiskSignalDto(
            s.Id,
            s.SignalCode.ToString(),
            AuctionRiskScorer.LabelOf(s.SignalCode),
            s.Weight,
            ExtractEvidence(s.EvidenceJson),
            s.BidId,
            s.UserId,
            s.DetectedAt)).ToList();

        if (assessment is null)
        {
            // A never-scanned auction is a valid read: return an empty sheet the UI can render.
            return (true, new AuctionRiskAssessmentDto(
                Guid.Empty,
                auction.Id,
                auction.Artwork?.Title ?? "Phiên đấu giá",
                auction.Artwork?.ThumbnailUrl ?? auction.Artwork?.WatermarkedUrl,
                auction.CurrentPrice,
                0m,
                AuctionRiskLevel.Low.ToString(),
                RiskLabels.LevelLabel(AuctionRiskLevel.Low),
                AuctionRiskAssessmentStatus.Open.ToString(),
                signalDtos.Count,
                "Chưa quét rủi ro cho phiên này.",
                DateTimeOffset.UtcNow,
                auction.LastRiskScanAt,
                null,
                null,
                null,
                signalDtos), []);
        }

        return (true, new AuctionRiskAssessmentDto(
            assessment.Id,
            auction.Id,
            auction.Artwork?.Title ?? "Phiên đấu giá",
            auction.Artwork?.ThumbnailUrl ?? auction.Artwork?.WatermarkedUrl,
            auction.CurrentPrice,
            assessment.Score,
            assessment.RiskLevel.ToString(),
            RiskLabels.LevelLabel(assessment.RiskLevel),
            assessment.Status.ToString(),
            assessment.SignalCount,
            assessment.Summary,
            assessment.DetectedAt,
            auction.LastRiskScanAt,
            assessment.ReviewedByUserId,
            assessment.ReviewedAt,
            assessment.DecisionNote,
            signalDtos), []);
    }

    /// <summary>Evidence is stored as JSON ({signal, evidence}); the sheet only needs the sentence.</summary>
    private static string ExtractEvidence(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("evidence", out var evidence)
                ? evidence.GetString() ?? string.Empty
                : json;
        }
        catch (JsonException)
        {
            return json;
        }
    }
}

// =====================================================================
// 2. Queue of live risk sheets (cursor keyset by score, then id)
// =====================================================================
public sealed record GetRiskAlertsQuery(
    string? Level = null,
    string? Status = null,
    string? Cursor = null,
    int Limit = 20)
    : IRequest<(bool Success, IReadOnlyList<RiskAlertListItemDto>? Data, object? Meta, string[] Errors)>;

public sealed class GetRiskAlertsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetRiskAlertsQuery, (bool Success, IReadOnlyList<RiskAlertListItemDto>? Data, object? Meta, string[] Errors)>
{
    private const int MaxLimit = 100;

    public async Task<(bool, IReadOnlyList<RiskAlertListItemDto>?, object?, string[])> Handle(
        GetRiskAlertsQuery request,
        CancellationToken cancellationToken)
    {
        var limit = request.Limit <= 0 ? 20 : Math.Min(request.Limit, MaxLimit);

        var query = db.AuctionRiskAssessments.AsNoTracking()
            .Where(a => !a.IsDeleted);

        if (TryParseStatus(request.Status, out var status))
        {
            query = query.Where(a => a.Status == status);
        }
        else
        {
            // Default queue shows what still needs a human.
            query = query.Where(a => a.Status == AuctionRiskAssessmentStatus.Open
                                     || a.Status == AuctionRiskAssessmentStatus.UnderReview);
        }

        if (TryParseLevel(request.Level, out var level))
        {
            query = query.Where(a => a.RiskLevel == level);
        }

        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            if (!RiskAlertCursor.TryDecode(request.Cursor, out var cursorScore, out var cursorId))
            {
                return (false, null, null, ["Cursor không hợp lệ."]);
            }

            query = query.Where(a => a.Score < cursorScore
                                     || (a.Score == cursorScore && a.Id.CompareTo(cursorId) > 0));
        }

        var rows = await query
            .OrderByDescending(a => a.Score)
            .ThenBy(a => a.Id)
            .Take(limit + 1)
            .Select(a => new
            {
                a.Id,
                a.AuctionId,
                a.Score,
                a.RiskLevel,
                a.Status,
                a.SignalCount,
                a.Summary,
                a.DetectedAt,
                Title = a.Auction!.Artwork!.Title,
                Thumbnail = a.Auction!.Artwork!.ThumbnailUrl ?? a.Auction!.Artwork!.WatermarkedUrl,
                a.Auction!.CurrentPrice,
                a.Auction!.BidCount
            })
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > limit;
        var page = hasMore ? rows.Take(limit).ToList() : rows;

        var auctionIds = page.Select(r => r.AuctionId).ToList();
        var lastBidAt = await db.Bids.AsNoTracking()
            .Where(b => auctionIds.Contains(b.AuctionId) && !b.IsDeleted)
            .GroupBy(b => b.AuctionId)
            .Select(g => new { AuctionId = g.Key, Last = g.Max(b => b.PlacedAt) })
            .ToDictionaryAsync(x => x.AuctionId, x => x.Last, cancellationToken);

        var items = page.Select(r => new RiskAlertListItemDto(
            r.Id,
            r.AuctionId,
            r.Title ?? "Phiên đấu giá",
            r.Thumbnail,
            r.CurrentPrice,
            r.BidCount,
            r.Score,
            r.RiskLevel.ToString(),
            RiskLabels.LevelLabel(r.RiskLevel),
            r.Status.ToString(),
            r.SignalCount,
            r.Summary,
            r.DetectedAt,
            lastBidAt.GetValueOrDefault(r.AuctionId))).ToList();

        var nextCursor = hasMore && page.Count > 0
            ? RiskAlertCursor.Encode(page[^1].Score, page[^1].Id)
            : null;

        return (true, items, new { nextCursor, hasMore } as object, []);
    }

    private static bool TryParseStatus(string? raw, out AuctionRiskAssessmentStatus status)
    {
        status = default;
        return !string.IsNullOrWhiteSpace(raw)
               && !string.Equals(raw, "All", StringComparison.OrdinalIgnoreCase)
               && Enum.TryParse(raw, ignoreCase: true, out status);
    }

    private static bool TryParseLevel(string? raw, out AuctionRiskLevel level)
    {
        level = default;
        return !string.IsNullOrWhiteSpace(raw)
               && !string.Equals(raw, "All", StringComparison.OrdinalIgnoreCase)
               && Enum.TryParse(raw, ignoreCase: true, out level);
    }
}

/// <summary>Keyset cursor for the score-ordered queue: base64("score|id").</summary>
public static class RiskAlertCursor
{
    public static string Encode(decimal score, Guid id)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{score.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{id}"));

    public static bool TryDecode(string cursor, out decimal score, out Guid id)
    {
        score = 0m;
        id = Guid.Empty;

        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('|');
            return parts.Length == 2
                   && decimal.TryParse(parts[0], System.Globalization.NumberStyles.Number,
                       System.Globalization.CultureInfo.InvariantCulture, out score)
                   && Guid.TryParse(parts[1], out id);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

// =====================================================================
// 3. Seller-facing flags: neutral, no score, no evidence
// =====================================================================
public sealed record GetMyAuctionRiskFlagsQuery(Guid SellerId)
    : IRequest<(bool Success, IReadOnlyList<MyAuctionRiskFlagDto>? Data, string[] Errors)>;

public sealed class GetMyAuctionRiskFlagsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetMyAuctionRiskFlagsQuery, (bool Success, IReadOnlyList<MyAuctionRiskFlagDto>? Data, string[] Errors)>
{
    public async Task<(bool, IReadOnlyList<MyAuctionRiskFlagDto>?, string[])> Handle(
        GetMyAuctionRiskFlagsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.SellerId == Guid.Empty)
        {
            return (false, null, ["Không xác định được người dùng."]);
        }

        var rows = await db.AuctionRiskAssessments.AsNoTracking()
            .Where(a => !a.IsDeleted
                        && a.Auction!.SellerId == request.SellerId
                        && (a.Status == AuctionRiskAssessmentStatus.Open
                            || a.Status == AuctionRiskAssessmentStatus.UnderReview))
            .OrderByDescending(a => a.DetectedAt)
            .Select(a => new
            {
                a.AuctionId,
                Title = a.Auction!.Artwork!.Title,
                a.Status,
                a.DetectedAt
            })
            .ToListAsync(cancellationToken);

        // StatusLabel is a C# switch, so it must run after materialisation, not inside the query.
        var items = rows
            .Select(r => new MyAuctionRiskFlagDto(
                r.AuctionId,
                r.Title ?? "Phiên đấu giá",
                RiskLabels.StatusLabel(r.Status),
                r.DetectedAt))
            .ToList();

        return (true, items, []);
    }
}
