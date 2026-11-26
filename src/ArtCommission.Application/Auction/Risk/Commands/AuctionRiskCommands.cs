using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Risk.Commands;

// =====================================================================
// 1. Re-run the detector for one auction (Administrator only)
// =====================================================================
public sealed record RescanAuctionRiskCommand(Guid AuctionId)
    : IRequest<(bool Success, decimal Score, string RiskLevel, int SignalCount, string[] Errors)>;

public sealed class RescanAuctionRiskCommandHandler(IRiskEngine riskEngine)
    : IRequestHandler<RescanAuctionRiskCommand, (bool Success, decimal Score, string RiskLevel, int SignalCount, string[] Errors)>
{
    public async Task<(bool, decimal, string, int, string[])> Handle(
        RescanAuctionRiskCommand request,
        CancellationToken cancellationToken)
    {
        var outcome = await riskEngine.EvaluateAsync(request.AuctionId, hotPathOnly: false, cancellationToken);

        if (outcome.Skipped)
        {
            return (false, 0m, AuctionRiskLevel.Low.ToString(), 0,
                ["Không quét được phiên này: phiên không tồn tại hoặc bộ phát hiện đang tắt."]);
        }

        return (true, outcome.Score, outcome.Level.ToString(), outcome.SignalCount, []);
    }
}

// =====================================================================
// 2. Resolve a sheet: dismiss or confirm (Administrator / Moderator)
// =====================================================================
public sealed record ReviewAuctionRiskCommand(Guid AuctionId, Guid ReviewerId, string Status, string? Note)
    : IRequest<(bool Success, AuctionRiskAssessmentDto? Data, string[] Errors)>;

public sealed class ReviewAuctionRiskCommandHandler(IApplicationDbContext db)
    : IRequestHandler<ReviewAuctionRiskCommand, (bool Success, AuctionRiskAssessmentDto? Data, string[] Errors)>
{
    private const int MinNoteLength = 10;

    public async Task<(bool, AuctionRiskAssessmentDto?, string[])> Handle(
        ReviewAuctionRiskCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ReviewerId == Guid.Empty)
        {
            return (false, null, ["Không xác định được người kiểm duyệt."]);
        }

        if (!Enum.TryParse<AuctionRiskAssessmentStatus>(request.Status, ignoreCase: true, out var target)
            || target is AuctionRiskAssessmentStatus.Open or AuctionRiskAssessmentStatus.UnderReview)
        {
            return (false, null, ["Trạng thái xử lý phải là Dismissed hoặc Confirmed."]);
        }

        var note = request.Note?.Trim();
        if (target == AuctionRiskAssessmentStatus.Confirmed
            && (note is null || note.Length < MinNoteLength))
        {
            return (false, null, [$"Khi xác nhận gian lận phải ghi chú ít nhất {MinNoteLength} ký tự."]);
        }

        var assessment = await db.AuctionRiskAssessments
            .FirstOrDefaultAsync(
                a => a.AuctionId == request.AuctionId
                     && !a.IsDeleted
                     && (a.Status == AuctionRiskAssessmentStatus.Open
                         || a.Status == AuctionRiskAssessmentStatus.UnderReview),
                cancellationToken);

        if (assessment is null)
        {
            return (false, null, ["Phiên này không có phiếu rủi ro nào đang mở."]);
        }

        var now = DateTimeOffset.UtcNow;
        assessment.Status = target;
        assessment.ReviewedByUserId = request.ReviewerId;
        assessment.ReviewedAt = now;
        assessment.DecisionNote = note;
        assessment.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);

        var auction = await db.Auctions.AsNoTracking()
            .Include(a => a.Artwork)
            .FirstOrDefaultAsync(a => a.Id == request.AuctionId, cancellationToken);

        var signals = await db.AuctionRiskSignals.AsNoTracking()
            .Where(s => s.AuctionId == request.AuctionId && !s.IsDeleted)
            .OrderByDescending(s => s.DetectedAt)
            .Select(s => new RiskSignalDto(
                s.Id,
                s.SignalCode.ToString(),
                AuctionRiskScorer.LabelOf(s.SignalCode),
                s.Weight,
                s.EvidenceJson,
                s.BidId,
                s.UserId,
                s.DetectedAt))
            .ToListAsync(cancellationToken);

        return (true, new AuctionRiskAssessmentDto(
            assessment.Id,
            request.AuctionId,
            auction?.Artwork?.Title ?? "Phiên đấu giá",
            auction?.Artwork?.ThumbnailUrl ?? auction?.Artwork?.WatermarkedUrl,
            auction?.CurrentPrice ?? 0m,
            assessment.Score,
            assessment.RiskLevel.ToString(),
            RiskLabels.LevelLabel(assessment.RiskLevel),
            assessment.Status.ToString(),
            assessment.SignalCount,
            assessment.Summary,
            assessment.DetectedAt,
            auction?.LastRiskScanAt,
            assessment.ReviewedByUserId,
            assessment.ReviewedAt,
            assessment.DecisionNote,
            signals), []);
    }
}
